using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class BulletRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _attackCooldown = 5.0f;
    [SerializeField] private float _aggravationRange = 15.1f;
    [SerializeField] private float _attackRange = 12.1f;      // 사거리(= 원뿔 길이와 같게 맞춰도 OK)
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private ParticleSystem _damagedEffect;
    [SerializeField] private AudioSource _damagedSound;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private float _lookAtTurnSpeed = 8f;
    [SerializeField] private Player _player;
    [SerializeField] private Animator _anim;
    [SerializeField] private int _scrapAmount = 7;

    [Header("Bolt Setting")]
    [SerializeField] private Transform _muzzleVisual;
    [SerializeField] private Transform _muzzleDetect;
    [SerializeField] private float _coneLength = 12.1f;   // 원뿔 길이
    [SerializeField] private float _coneRadius = 5.0f;    // 밑면 반지름
    [SerializeField] private float _tickDamage = 1.2f;
    [SerializeField] private float _damageInterval = 0.1f;
    [SerializeField] private float _attackingTime = 5.0f;
    [SerializeField] private float _boltSpeed = 20f;
    [SerializeField] private GameObject _boltPrefab;
    [SerializeField] private int _boltsPerSecond = 24;       // 초당 생성 개수
    [SerializeField] private AudioSource _attackAudio;
    [SerializeField] private GameObject _shotLeftFx;
    [SerializeField] private GameObject _shotRightFx;
    
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    private Transform _camTr;                      // 카메라 Transform
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 2.0f;
    [SerializeField] private ParticleSystem _DeathEffect;
    [SerializeField] private AudioSource _DeathAudio;
    
    
    // ===== 내부 캐시 =====
    private Collider _playerCol;    // 플레이어 콜라이더
    private Transform _playerTr;
    private Transform _tr;
    private NavMeshAgent _agent;

    private float _coneAngleDeg = 0.0f;

    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private bool _isDead = false;

    // 이동 판정용 상수
    private const float STOP_VEL_SQR = 0.1f;

    private void Awake()
    {
        _tr = transform;
    }

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();

        if (_player == null)
            _player = FindObjectOfType<Player>();
        
        if (_agent == null || _player == null)
        {
            enabled = false;
            Debug.LogWarning("[BulletRobot] NavMeshAgent 또는 Player가 없습니다. 스크립트를 비활성화합니다.");
            return;
        }

        _playerTr = _player.transform;
        _playerCol = _player.GetComponentInChildren<Collider>();

        if (_playerCol == null)
            Debug.LogWarning("[BulletRobot] Player에 Collider가 없습니다.");

        if (_anim == null)
            _anim = GetComponentInChildren<Animator>();

        _curHp = _maxHp;

        // HP Image 기본 설정 강제 (실수 방지용)
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();
        
        // NavMesh 기본 세팅
        _agent.speed = _moveSpeed;
        _agent.stoppingDistance = _attackRange;
        _agent.updateRotation = true;
        _agent.autoBraking = true;

        // 밑면 반지름 / 길이로 원뿔 각도 계산
        _coneAngleDeg = Mathf.Atan(_coneRadius / _coneLength) * Mathf.Rad2Deg;

        // 시작 위치 NavMesh 보정
        if (!_agent.isOnNavMesh &&
            NavMesh.SamplePosition(_tr.position, out var hit, 2f, NavMesh.AllAreas))
        {
            _agent.Warp(hit.position);
        }
        SetShotFx(false);
    }

    private void Update()
    {
        if (_agent == null || _playerTr == null)
            return;
        if (_isDead)
        {
            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _agent.ResetPath();
                _agent.updateRotation = false;
            }
            return;
        }
        
        // 애니메이션 Speed 파라미터 갱신
        if (_anim != null)
            _anim.SetFloat("Speed", _agent.velocity.magnitude);

        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(_tr.position, out var hit, 2f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                return;
        }

        // 사망 체크
        if (_curHp <= 0f)
        {
            Die();
            return;
        }

        // 기본 거리 / 시야 체크
        float worldDist = Vector3.Distance(_tr.position, _playerTr.position);
        bool hasLOS = HasLineOfSight();

        // 인식 범위 안에서는 플레이어 바라보기
        if (worldDist <= _aggravationRange && hasLOS) 
            LookAtPlayer();

        // 공격 진입 조건
        if (!_isAttacking &&
            !_isCoolingDown &&
            worldDist <= _attackRange &&
            hasLOS &&
            _agent.velocity.sqrMagnitude < STOP_VEL_SQR)
        {
            _agent.isStopped = true;
            StartCoroutine(AttackRoutine());
            return;
        }

        // 추적 로직
        if (!_isAttacking && worldDist <= _aggravationRange && hasLOS)
        {
            _agent.isStopped = false;

            Vector3 targetPos = _playerTr.position;

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                // 목적지가 많이 다르면 갱신
                if (!_agent.hasPath ||
                    (navHit.position - _agent.destination).sqrMagnitude > 0.25f)
                {
                    _agent.SetDestination(navHit.position);
                }
            }
        }
        else if (!_isAttacking)
        {
            // 추적 중이 아니면 정지
            _agent.isStopped = true;
            if (_agent.hasPath)
                _agent.ResetPath();
        }
    }
    
    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
    }

    private void SetShotFx(bool on)
    {
        if (_shotLeftFx  != null) _shotLeftFx.SetActive(on);
        if (_shotRightFx != null) _shotRightFx.SetActive(on);
    }

    // 항상 수평으로 플레이어 바라보기
    private void LookAtPlayer()
    {
        if (_player == null || !HasLineOfSight() || _isDead) return;

        Vector3 lockedDir = (_player != null)
            ? (_player.transform.position - transform.position)
            : transform.forward;
        lockedDir.y = 0.0f;
        lockedDir.Normalize();
        
        // 몸을 스냅샷 방향으로 즉시 정렬
        if (lockedDir.sqrMagnitude > 0.001f)
        {
            float rotSpeed = _lookAtTurnSpeed;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lockedDir),
                Time.deltaTime * rotSpeed
            );
        }
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        // 이동 정지
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();

        float elapsed = 0f;
        float tickTimer = 0f;
        float spawnTimer = 0f;
        float spawnInterval = (_boltsPerSecond > 0) ? (1f / _boltsPerSecond) : 999f;

        Transform tDetect = _muzzleDetect != null ? _muzzleDetect : _tr;
        Transform tVisual = _muzzleVisual != null ? _muzzleVisual : tDetect;
    
        if (_attackAudio != null && !_attackAudio.isPlaying)
            _attackAudio.Play();

        // 🔹 공격 시작할 때 이펙트 ON
        SetShotFx(true);

        while (elapsed < _attackingTime)
        {
            if (_playerTr == null || !HasLineOfSight() || _isDead) 
                break;

            // 공격 중에도 플레이어 바라보기
            LookAtPlayer();

            // 볼트 시각 효과 스폰
            spawnTimer += Time.deltaTime;
            while (spawnTimer >= spawnInterval)
            {
                spawnTimer -= spawnInterval;
                SpawnVisualBolt(tVisual);
            }

            // 데미지 틱
            tickTimer += Time.deltaTime;
            if (tickTimer >= _damageInterval)
            {
                tickTimer = 0f;
                ConeDamageTick(tDetect);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 🔹 공격 끝났으면 이펙트 OFF
        SetShotFx(false);

        // ★ 공격 종료 시 사운드 정지
        if (_attackAudio != null && _attackAudio.isPlaying)
            _attackAudio.Stop();
    
        // 쿨다운
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;

        yield return new WaitForSeconds(_attackCooldown);

        _isCoolingDown = false;
    }


    // ===== 시각용 볼트 스폰 =====
    private void SpawnVisualBolt(Transform muzzle)
    {
        if (_boltPrefab == null || muzzle == null)
            return;

        // 원뿔 내부에서 랜덤 방향 생성
        Vector3 dir = RandomDirectionInCone(muzzle.forward, _coneAngleDeg, muzzle);

        GameObject go = Instantiate(
            _boltPrefab,
            muzzle.position,
            Quaternion.LookRotation(dir)
        );

        // 리지드바디로 직선 이동
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = dir * _boltSpeed;
#else
            rb.velocity = dir * _boltSpeed;
#endif
        }

        // Raycast로 앞으로 장애물 확인 후 생존 시간 계산
        float maxDistance = _coneLength;
        float lifeTime;

        if (Physics.Raycast(
                muzzle.position,
                dir,
                out RaycastHit hit,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            lifeTime = hit.distance / _boltSpeed;
        }
        else
        {
            lifeTime = maxDistance / _boltSpeed;
        }

        lifeTime += 0.02f;    // 여유 조금

        Destroy(go, lifeTime);
    }

    // ===== 판정 틱(거리 1당 5% 데미지 감소, 콜라이더가 원뿔에 "조금이라도" 걸리면 히트) =====
    private void ConeDamageTick(Transform t)
    {
        if (_player == null || t == null)
            return;

        // 콜라이더가 없으면 센터 포인트만 검사
        if (_playerCol == null)
        {
            Vector3 center = _playerTr.position;
            if (!IsPointInsideCone(center, t, _coneAngleDeg, _coneLength))
                return;

            Vector3 flat = center - t.position;
            flat.y = 0f;
            float distFlat = flat.magnitude;

            float falloff = Mathf.Max(0f, 1f - 0.05f * distFlat);
            float dmg = _tickDamage * falloff;
            _player.TakeDamage(dmg);
            return;
        }

        // 콜라이더의 여러 샘플 포인트 중 하나라도 원뿔 안에 들어오면 히트
        Bounds b = _playerCol.bounds;
        Vector3 c = b.center;
        Vector3 e = b.extents;

        Vector3[] samples =
        {
            c,
            c + new Vector3( e.x, 0f, 0f),
            c + new Vector3(-e.x, 0f, 0f),
            c + new Vector3(0f, 0f,  e.z),
            c + new Vector3(0f, 0f, -e.z),
            c + new Vector3(0f,  e.y, 0f),
            c + new Vector3(0f, -e.y, 0f),
        };

        bool anyInside = false;
        for (int i = 0; i < samples.Length; i++)
        {
            if (IsPointInsideCone(samples[i], t, _coneAngleDeg, _coneLength))
            {
                anyInside = true;
                break;
            }
        }

        if (!anyInside)
            return;

        // falloff는 머즐 기준 XZ 평면에서 가장 가까운 지점 기준
        Vector3 closest = _playerCol.ClosestPoint(t.position);
        Vector3 flatFromMuzzle = closest - t.position;
        flatFromMuzzle.y = 0f;
        float distFlat2 = flatFromMuzzle.magnitude;

        float falloff2 = Mathf.Max(0f, 1f - 0.05f * distFlat2);
        float dmg2 = _tickDamage * falloff2;
        _player.TakeDamage(dmg2);
    }

    private Vector3 RandomDirectionInCone(Vector3 forward, float coneAngleDeg, Transform basis)
    {
        float yaw = Random.Range(-coneAngleDeg, coneAngleDeg);
        float pitch = Random.Range(-coneAngleDeg, coneAngleDeg);

        Quaternion rotYaw = Quaternion.AngleAxis(yaw, basis.up);
        Vector3 yRot = rotYaw * forward;

        Quaternion rotPitch = Quaternion.AngleAxis(
            pitch,
            Vector3.Cross(basis.up, yRot).normalized
        );

        return (rotPitch * yRot).normalized;
    }

    private bool IsPointInsideCone(Vector3 point, Transform t, float angleDeg, float length)
    {
        if (t == null || length <= 0f)
            return false;

        Vector3 local = t.InverseTransformPoint(point);

        float z = local.z;
        if (z <= 0f || z > length)
            return false;

        float maxRadius = _coneRadius * (z / length);
        float radialSqr = local.x * local.x + local.y * local.y;

        return radialSqr <= maxRadius * maxRadius;
    }

    // ===== 유틸 =====
    private bool HasLineOfSight()
    {
        if (_playerTr == null)
            return false;

        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신의 콜라이더 먼저 맞았을 때 처리
            if (hit.collider.transform.IsChildOf(_tr))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(_tr))
                        continue;

                    return h.collider.GetComponentInParent<Player>() != null;
                }

                return true;
            }

            return hit.collider.GetComponentInParent<Player>() != null;
        }

        // 아무것도 안 맞으면 시야 확보된 것으로 처리
        return true;
    }

    // 체력바 채우기 갱신
    private void UpdateHpUI()
    {
        if (_hpFillImage == null) return;

        float ratio = (_maxHp > 0f) ? _curHp / _maxHp : 0f;
        _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
    }
    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        UpdateHpUI();
        if (_curHp <= 0f)
        {
            Die();
            return;
        }
        _damagedEffect.Play();
        _damagedSound.Play();
    }
    
    // private void PlayDeath()
    // {
    //     // 🔹 이펙트 실행
    //     if (_DeathEffect != null)
    //     {
    //         _DeathEffect.transform.SetParent(null); // 부모 떼기
    //         _DeathEffect.Play();
    //
    //         float effectDuration =
    //             _DeathEffect.main.duration +
    //             _DeathEffect.main.startLifetime.constantMax;
    //
    //         Destroy(_DeathEffect.gameObject, effectDuration + 0.1f);
    //     }
    //
    //     // 🔹 사운드 실행
    //     if (_DeathAudio != null && _DeathAudio.clip != null)
    //     {
    //         _DeathAudio.transform.SetParent(null); // 부모 떼기
    //         _DeathAudio.Play();
    //
    //         Destroy(_DeathAudio.gameObject, _DeathAudio.clip.length + 0.1f);
    //     }
    // }
    
    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // 1) 진행 중인 모든 코루틴(총알 난사 공격 포함) 정지
        StopAllCoroutines();
        _isAttacking   = false;
        _isCoolingDown = false;

        // 2) 공격 이펙트 / 사운드 정리
        SetShotFx(false);                     // 총구 이펙트 끄기

        if (_attackAudio != null && _attackAudio.isPlaying)
            _attackAudio.Stop();              // 공격 사운드 정지

        // 3) NavMeshAgent 완전히 멈추기
        if (_agent != null)
        {
            _agent.isStopped      = true;
            _agent.velocity       = Vector3.zero;
            _agent.ResetPath();
            _agent.updateRotation = false;
        }

        // 4) 콜라이더 비활성화 (원하면 꺼두는 게 깔끔함)
        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
            selfCol.enabled = false;

        // 5) 애니메이션 속도 0으로 (걷기 멈춘 모션 유지)
        if (_anim != null)
        {
            _anim.SetFloat("Speed", 0f);
            _anim.SetTrigger("isDie");
        }

        // 6) HP바 끄기
        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        // 7) 죽음 이펙트 / 사운드 재생
        // PlayDeath();

        // 8) 약간 딜레이 후 스크랩 드랍 + 삭제
        StartCoroutine(DieRoutine());
    }


    
    private IEnumerator DieRoutine()
    {
        _DeathAudio.Play();
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);               
        Destroy(gameObject);                   // 삭제
    }
    
    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, transform.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AirRobot] 스크랩 {amount} 드랍");
    }
    
    // 몬스터를 중심으로 인식 범위(_aggravationRange)를 흰 원으로 시각화
    private void DrawAggroRadiusGizmo()
    {
        // 반경이 0 이하면 그릴 필요 없음
        if (_aggravationRange <= 0f) return;

        Gizmos.color = Color.white;

        // 원의 중심: 몬스터 위치, 살짝 위로 띄워서 바닥에 안 묻히게
        Vector3 center = transform.position;
        center.y += 0.05f;

        float radius = _aggravationRange;
        int segments = 48;
        float step = 360f / segments;

        // 시작점: 중심 기준 X축 방향으로 radius 떨어진 곳
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 next = center + new Vector3(x, 0f, z);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }


    // Scene 뷰에서 원뿔 시각화
    private void OnDrawGizmosSelected()
    {
        Transform t = _muzzleDetect != null ? _muzzleDetect : transform;

        float ang = (_coneLength > 0f)
            ? Mathf.Atan(_coneRadius / _coneLength) * Mathf.Rad2Deg
            : 0f;
        float len = _coneLength;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(t.position, t.position + t.forward * len);

        int rings = 4;
        for (int i = 1; i <= rings; i++)
        {
            float z = len * i / rings;
            float radius = Mathf.Tan(ang * Mathf.Deg2Rad) * z;
            DrawCircle(
                t.position + t.forward * z,
                t.up,
                t.forward,
                radius,
                Color.red
            );
        }
    }

    private void DrawCircle(
        Vector3 center,
        Vector3 up,
        Vector3 forward,
        float radius,
        Color color,
        int segments = 28)
    {
        Gizmos.color = color;

        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.right;

        Vector3 prev = center + right * radius;
        float step = 360f / segments;

        for (int i = 1; i <= segments; i++)
        {
            Quaternion q = Quaternion.AngleAxis(step * i, forward);
            Vector3 next = center + (q * right) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
