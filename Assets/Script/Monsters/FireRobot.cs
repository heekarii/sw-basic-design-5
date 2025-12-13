using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class FireRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 80.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _attackCooldown = 3.0f;
    [SerializeField] private float _aggravationRange = 15.0f;
    [SerializeField] private float _attackRange = 6.0f;
    [SerializeField] private float _moveSpeed = 4.0f;
    [SerializeField] private AudioSource _damagedSound;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    [SerializeField] private Player _player;
    [SerializeField] private int _scrapAmount = 8;
    
    [Header("Fire")]
    [SerializeField] private Transform _muzzle;      // 중앙 머즐(불 기준)
    [SerializeField] private float _damage = 15.0f;
    [SerializeField] private float _damageInterval = 1.0f;
    [SerializeField] private float _attackingTime = 3.0f;
    [SerializeField] private float _halfWidth = 3.0f;   
    [SerializeField] private float _length = 3.6f;      // 전방 길이
    [SerializeField] private float _height = 3.0f;      // 높이
    [SerializeField] private ParticleSystem[] _fireVFX;
    [SerializeField] private AudioSource[] _fireSfx;

    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    private Transform _camTr;                      // 카메라 Transform
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 2f;
    [SerializeField] private ParticleSystem _DeathEffect;
    [SerializeField] private AudioSource _DeathAudio;
    
    private bool _isDead = false;
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;

        // HP Image 기본 설정 강제 (실수 방지용)
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();
        
        if (_agent == null)
        {
            Debug.LogError("[FireRobot] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[FireRobot] Player를 찾지 못했습니다.");
            enabled = false; return;
        }

        // 기본 파라미터
        _agent.speed = _moveSpeed;
        _agent.stoppingDistance = _attackRange;
        _agent.updateRotation = true;
        _agent.autoBraking = true;

        // 시작 위치가 NavMesh 위가 아니면 가장 가까운 NavMesh 위치로 워프
        if (!TrySnapToNavMesh(transform.position, out var snapped))
        {
            Debug.LogError("[FireRobot] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[FireRobot] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[FireRobot] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
    }

    void Update()
    {
        if (_player == null || _agent == null) return;
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
        
        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(transform.position, out var snapped))
            {
                _agent.Warp(snapped);
               // Debug.LogWarning("[FireRobot] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
               // Debug.LogError("[FireRobot] 재워프 실패: 주변에 NavMesh 없음");
                return;
            }
        }

        // --- ✅ NavMesh 기반 거리 판정 ---
        float navDist = _agent.remainingDistance;
        if (_agent == null)
            Debug.LogError("what");
        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // ---------------------------------

        // 인식범위 밖의 플레이어가 아니라면 계속 쳐다보게
        if (worldDist <= _aggravationRange)   
            LookAtPlayer();
        
    // ✅ 공격 조건: 실제 거리 기반 + 정지 상태 확인
        if (worldDist <= _attackRange && HasLineOfSight() && _agent.velocity.sqrMagnitude < 0.1f)
        {
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }


        // ✅ 추적 조건
        if (worldDist <= _aggravationRange && HasLineOfSight())
        {
            _agent.isStopped = false;
            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
        else
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

      //  Debug.Log($"[FireRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

        if (_curHp <= 0f) Die();
    }

    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
    }

    private bool TrySnapToNavMesh(Vector3 origin, out Vector3 snapped)
    {
        // 높이 오차/피벗 문제를 감안해 반경을 충분히 준다
        if (NavMesh.SamplePosition(origin, out var hit, 2.0f, NavMesh.AllAreas))
        {
            snapped = hit.position;
            return true;
        }
        snapped = origin;
        return false;
    }
    
    private bool HasLineOfSight()
    {
        if (_player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = _player.transform.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        // 첫 번째로 맞은 것이 플레이어면 "시야 있음"
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신 콜라이더는 무시
            if (hit.collider.transform.IsChildOf(transform))
            {
                // 자기 자신을 맞았으면 그 다음 것을 보기 위해 RaycastAll 사용
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue; // 내 콜라이더 스킵
                    // 첫 번째 유효한 히트가 플레이어면 LOS 있음
                    if (h.collider.GetComponentInParent<Player>() != null) return true;
                    // 아니면 가려짐
                    return false;
                }
                return true; // 유효 히트가 없으면 가려진 게 없음
            }

            // 첫 히트가 플레이어면 시야 OK
            if (hit.collider.GetComponentInParent<Player>() != null) return true;

            // 그 외(벽/지형/기타)가 먼저 맞으면 가려짐
            return false;
        }

        // 아무것도 안 맞았으면 가려진 게 없는 것으로 간주
        return true;
    }
    
    private void LookAtPlayer()
    {
        if (_player == null || !HasLineOfSight()) return;

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
    
    private void FxOn()
    {
        if (_fireVFX != null)
        {
            foreach (var ps in _fireVFX)
            {
                if (ps == null) continue;
                if (!ps.gameObject.activeSelf) ps.gameObject.SetActive(true);
                ps.Clear(true);
                ps.Play(true);
            }
        }

        // 🔊 불 사운드 재생
        if (_fireSfx != null)
        {
            foreach (var sfx in _fireSfx)
            {
                if (sfx == null) continue;
                if (!sfx.isPlaying)
                    sfx.Play();
            }
        }
    }

    private void FxOff()
    {
        if (_fireVFX != null)
        {
            foreach (var ps in _fireVFX)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // 🔇 불 사운드 정지
        if (_fireSfx != null)
        {
            foreach (var sfx in _fireSfx)
            {
                if (sfx == null) continue;
                if (sfx.isPlaying)
                    sfx.Stop();
            }
        }
    }



    // ✅ Scene 뷰에서 공격 판정 박스를 시각화
    private void OnDrawGizmosSelected()
    {
        if (_muzzle == null)
            _muzzle = transform; // 혹시 에디터에서 안 넣었을 때 기본값

        // 박스의 중심, 절반 크기, 회전 계산
        GetAOEBox(out Vector3 center, out Vector3 half, out Quaternion rot);

        // 색상 (공격 중엔 빨간색, 아닐 땐 파란색)
        Gizmos.color = _isAttacking ? new Color(1f, 0.3f, 0f, 0.35f) : new Color(0f, 0.5f, 1f, 0.25f);

        // 회전된 박스 적용
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

        // 반투명 와이어 큐브
        Gizmos.DrawWireCube(Vector3.zero, half * 2f);

        // 원래 매트릭스로 복구
        Gizmos.matrix = prevMatrix;
    }

    
    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown || !HasLineOfSight() || _isDead) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        // 이동 정지(관성 제거)
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();

        float elapsed = 0f;
        float tickTimer = 0f;
        FxOn();
        
        while (elapsed < _attackingTime + 0.1f) 
        {
            // 플레이어가 사거리/시야 내에 있는지 계속 확인
            if (_player == null || !HasLineOfSight()) break;

            float dist = Vector3.Distance(transform.position, _player.transform.position);
            if (dist <= _attackRange * 1.05f && HasLineOfSight())
            {
                // 1초마다 틱 처리
                tickTimer += Time.deltaTime;
                if (tickTimer >= _damageInterval)
                {
                    tickTimer = 0f;

                    GetAOEBox(out Vector3 boxCenter, out Vector3 boxHalf, out Quaternion boxRot);
                    Collider[] hits = Physics.OverlapBox(boxCenter, boxHalf, boxRot, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var c in hits)
                    {
                        var p = c.GetComponentInParent<Player>();
                        if (p != null) p.TakeDamage(_damage);
                    }

                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        FxOff();
        
        // 쿨다운
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;
        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }

    // 원통 AOE의 월드 좌표 캡슐 끝점 계산
    private void GetAOEBox(out Vector3 center, out Vector3 half, out Quaternion rot)
    {
        Transform t = _muzzle != null ? _muzzle : transform;

        // 방향(불이 나가는 방향)
        rot = Quaternion.LookRotation(t.forward, Vector3.up);

        // 박스 크기 (좌우, 높이, 길이)
        half = new Vector3(_halfWidth, _height * 0.5f, _length * 0.5f);

        // 중심: 머즐 위치 + 전방으로 절반 길이만큼 (불 끝까지 커버)
        center = t.position + t.forward * half.z;
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
        _damagedSound.Play();
        Debug.Log($"FireRobot took {dmg} damage, current HP: {_curHp}");
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
        if (_isDead) return;
        _isDead = true;

        // 1) 진행 중인 모든 코루틴(불 공격 루틴 포함) 정지
        StopAllCoroutines();
        _isAttacking   = false;
        _isCoolingDown = false;

        // 2) 불 VFX / SFX 끄기
        FxOff();   // 파티클 + 불 사운드 전부 정지

        // 3) NavMeshAgent 완전히 멈추기
        if (_agent != null)
        {
            _agent.isStopped      = true;
            _agent.velocity       = Vector3.zero;
            _agent.ResetPath();
            _agent.updateRotation = false;
        }

        // 4) 콜라이더 비활성화 (시체에 부딪히는 거 방지)
        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
            selfCol.enabled = false;

        // 5) HP바 끄기
        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        // 6) 죽음 이펙트 / 사운드 재생
        PlayDeath();

        // 7) 약간 딜레이 후 스크랩 드랍 + 본체 삭제
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

}




