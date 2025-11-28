using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class LaserRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 30.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 10.0f;
    [SerializeField] private float _attackCastingTime = 0f;
    [SerializeField] private float _attackCooldown = 2.5f;
    [SerializeField] private float _aggravationRange = 15.9f;
    [SerializeField] private float _attackRange = 12.9f;
    [SerializeField] private float _lookAtTurnSpeed = 8f;
    [SerializeField] private float _moveSpeed = 5.0f;

    [Header("Refs")]
    [SerializeField] private Player _player;
    [SerializeField] private Transform _eyeMuzzle;
    [SerializeField] private GameObject _laserProjectilePrefab;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 3;

    [Header("Burst Settings")]
    [SerializeField] private int _burstCount = 2;
    [SerializeField] private float _betweenShotDelay = 0.3f;

    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 2f;
    [SerializeField] private ParticleSystem _DeathEffect;
    [SerializeField] private AudioSource _DeathAudio;
    
    
    private bool _isDead = false;
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;
    private Transform _tr;
    private Transform _playerTr;
    

    private void Awake()
    {
        _tr = transform;
    }

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_player == null)
            _player = FindObjectOfType<Player>();

        if (_agent == null)
        {
            Debug.LogError("[LaserRobot] NavMeshAgent가 없습니다.");
            enabled = false;
            return;
        }

        if (_player == null)
        {
            Debug.LogError("[LaserRobot] Player를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        _playerTr = _player.transform;
        _curHp = _maxHp;

        // NavMesh 기본 설정
        _agent.speed           = _moveSpeed;
        _agent.stoppingDistance = _attackRange;
        _agent.updateRotation  = false;   // 회전은 우리가 직접 제어
        _agent.autoBraking     = true;

        // HP Image 설정
        if (_hpFillImage != null)
        {
            _hpFillImage.type       = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼→오 줄어듦
        }
        UpdateHpUI();

        // 시작 위치 NavMesh 보정 (살짝 위로 띄우기)
        if (!TrySnapToNavMesh(_tr.position, out var snapped))
        {
            Debug.LogError("[LaserRobot] 시작 위치 근처에 NavMesh가 없습니다.");
            enabled = false;
            return;
        }

        snapped.y += 0.05f;
        if ((_tr.position - snapped).sqrMagnitude > 0.0001f)
            _agent.Warp(snapped);
    }

    private void Update()
    {
        if (_playerTr == null || _agent == null)
            return;

        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(_tr.position, out var snapped))
            {
                snapped.y += 0.05f;
                _agent.Warp(snapped);
            }
            else
                return;
        }

        // 사망 체크
        if (_curHp <= 0f)
        {
            Die();
            return;
        }

        float worldDist = Vector3.Distance(_tr.position, _playerTr.position);
        bool hasLOS = HasLineOfSight();

        // 인식 범위 안이면 항상 플레이어 쪽으로 부드럽게 회전
        if (worldDist <= _aggravationRange)
            LookAtPlayer();

        // 이미 공격 중 / 쿨다운 중이면 이동/공격 로직 건너뜀
        if (_isAttacking || _isCoolingDown)
            return;

        // 공격 진입 조건: 사거리 내 + 시야 확보 + 거의 정지 상태
        if (worldDist <= _attackRange &&
            hasLOS &&
            _agent.velocity.sqrMagnitude < 0.01f)
        {
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }

        // 추적 조건: 인식 범위 내 + 시야 확보
        if (worldDist <= _aggravationRange && hasLOS)
        {
            _agent.isStopped = false;

            Vector3 targetPos = _playerTr.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                if (!_agent.hasPath ||
                    (_agent.destination - hit.position).sqrMagnitude > 0.25f)
                {
                    _agent.SetDestination(hit.position);
                }
            }
        }
        else
        {
            _agent.isStopped = true;
            if (_agent.hasPath)
                _agent.ResetPath();
        }
    }

    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
    }

    // ---------------------------------------------
    //  NavMesh / 이동 관련
    // ---------------------------------------------
    private bool TrySnapToNavMesh(Vector3 origin, out Vector3 snapped)
    {
        if (NavMesh.SamplePosition(origin, out var hit, 2.0f, NavMesh.AllAreas))
        {
            snapped = hit.position;
            return true;
        }
        snapped = origin;
        return false;
    }

    // 항상 수평으로 플레이어 바라보기 (LOS와 별개로 회전만 담당)
    private void LookAtPlayer()
    {
        if (_playerTr == null) return;

        Vector3 dir = _playerTr.position - transform.position;
        dir.y = 0.0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * _lookAtTurnSpeed
        );
    }

    // ---------------------------------------------
    //  공격 관련
    // ---------------------------------------------
    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _agent.isStopped = true;

        // 캐스팅 타임이 있으면 잠깐 멈췄다가 발사
        if (_attackCastingTime > 0f)
            yield return new WaitForSeconds(_attackCastingTime);

        for (int i = 0; i < _burstCount; i++)
        {
            if (_playerTr == null)
                break;

            // 공격 도중에도 플레이어 쪽으로 정렬
            LookAtPlayer();

            // 실제 발사
            FireLaser(_eyeMuzzle);

            // 마지막 발사 전까지만 딜레이
            if (i < _burstCount - 1 && _betweenShotDelay > 0f)
                yield return new WaitForSeconds(_betweenShotDelay);
        }

        _agent.isStopped = false;
        _isAttacking = false;
        _isCoolingDown = true;

        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }

    private void FireLaser(Transform muzzle)
    {
        if (muzzle == null || _laserProjectilePrefab == null || _playerTr == null) return;

        // 머즐 위치에서 플레이어를 향하는 방향으로 항상 발사
        Vector3 targetPos = _playerTr.position + Vector3.up * 1.0f;
        Vector3 dir = (targetPos - muzzle.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        GameObject go = Instantiate(_laserProjectilePrefab, muzzle.position, rot);

        // 투사체 초기화
        if (go.TryGetComponent(out LaserProjectile proj))
            proj.Init(dir, _player, transform);

        // 발사 시점에 투사체에 달린 오디오 재생
        if (go.TryGetComponent(out AudioSource audio))
        {
            audio.Stop();
            audio.Play();
        }
    }

    private bool HasLineOfSight()
    {
        if (_playerTr == null) return false;

        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir /= dist;

        // 자기 콜라이더 스킵 + 가장 가까운 유효 히트만 사용
        RaycastHit[] hits = Physics.RaycastAll(
            origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(_tr))
                continue; // 내 몸은 무시

            return h.collider.GetComponentInParent<Player>() != null;
        }

        // 자기 몸 말고 아무것도 안 맞았으면 막힌 게 없는 것으로 처리
        return true;
    }

    // ---------------------------------------------
    //  대미지 / 사망 / 스크랩
    // ---------------------------------------------
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        UpdateHpUI();
        Debug.Log($"[LaserRobot] took {dmg} damage, current HP: {_curHp}");
        if (_curHp <= 0f)
            Die();
    }

    private void UpdateHpUI()
    {
        if (_hpFillImage == null) return;

        float ratio = (_maxHp > 0f) ? _curHp / _maxHp : 0f;
        _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    private void PlayDeath()
    {
        // 🔹 이펙트 실행
        if (_DeathEffect != null)
        {
            _DeathEffect.transform.SetParent(null); // 부모 떼기
            _DeathEffect.Play();

            float effectDuration =
                _DeathEffect.main.duration +
                _DeathEffect.main.startLifetime.constantMax;

            Destroy(_DeathEffect.gameObject, effectDuration + 0.1f);
        }

        // 🔹 사운드 실행
        if (_DeathAudio != null && _DeathAudio.clip != null)
        {
            _DeathAudio.transform.SetParent(null); // 부모 떼기
            _DeathAudio.Play();

            Destroy(_DeathAudio.gameObject, _DeathAudio.clip.length + 0.1f);
        }
    }
    
    private void Die()
    {
        if (_isDead) return;    // 여러 번 실행되는 것 방지
        _isDead = true;
        PlayDeath();

        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        StartCoroutine(DieRoutine());
    }
    
    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);               
        Destroy(gameObject);                   // 삭제
    }
    
    public void DropScrap(int amount)
    {
        if (!_scrapData) return;

        GameObject scrap = Instantiate(
            _scrapData.ScrapPrefab,
            _tr.position,
            Quaternion.identity);

        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[LaserRobot] 스크랩 {amount} 드랍");
    }

    // 몬스터를 중심으로 인식 범위(_aggravationRange)를 흰 원으로 시각화
    private void DrawAggroRadiusGizmo()
    {
        if (_aggravationRange <= 0f) return;

        Gizmos.color = Color.white;

        Vector3 center = transform.position;
        center.y += 0.05f;

        float radius = _aggravationRange;
        int segments = 48;
        float step = 360f / segments;

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
}
