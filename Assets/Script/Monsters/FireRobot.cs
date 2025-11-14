using UnityEngine;
using UnityEngine.AI;

public class FireRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 150.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _attackCooldown = 3.0f;
    [SerializeField] private float _aggravationRange = 9.1f;
    [SerializeField] private float _attackRange = 3.6f;
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    [SerializeField] private Player _player;
    
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

    
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;

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
        lockedDir.y = 0f;
        lockedDir.Normalize();
        
        // 몸을 스냅샷 방향으로 즉시 정렬
        if (lockedDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lockedDir);
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
        if (_isAttacking || _isCoolingDown || !HasLineOfSight()) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
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
    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
        Debug.Log($"FireRobot took {dmg} damage, current HP: {_curHp}");
    }

    private void Die()
    {
        Destroy(gameObject);
        Debug.Log("FireRobot has died.");
    }
    
    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, transform.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AirRobot] 스크랩 {amount} 드랍");
    }
}




