using System.Numerics;
using UnityEngine;
using UnityEngine.AI;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

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
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    [SerializeField] private float _moveSpeed = 1.3f;
    [SerializeField] private Player _player;
    [SerializeField] private Transform _eyeMuzzle;
    [SerializeField] private GameObject _laserProjectilePrefab;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 3;
    
    [Header("Burst Settings")]
    [SerializeField] private int _burstCount = 2;
    [SerializeField] private float _betweenShotDelay = 0.3f;
    
    
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
            Debug.LogError("[LaserRobot] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[LaserRobot] Player를 찾지 못했습니다.");
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
            Debug.LogError("[LaserRobot] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[LaserRobot] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[LaserRobot] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
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
                // Debug.LogWarning("[LaserRobot] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
                // Debug.LogError("[LaserRobot] 재워프 실패: 주변에 NavMesh 없음");
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

        //  Debug.Log($"[LaserRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

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

    private void FireLaser(Transform muzzle, Vector3 targetPoint)
    {
        if (!muzzle || !_laserProjectilePrefab) return;

        Vector3 dir = muzzle.forward;

        var go = Instantiate(_laserProjectilePrefab, muzzle.position, muzzle.rotation);

        // 투사체 초기화
        if (go.TryGetComponent(out LaserProjectile proj))
            proj.Init(dir, _player);

        // 🔊 발사 시점에 투사체에 달린 AudioSource 재생
        if (go.TryGetComponent(out AudioSource audio))
        {
            // PlayOnAwake 꺼둔 상태라면 여기서 수동으로 재생
            audio.Stop();
            audio.Play();
            // 또는 특정 클립만 쏘고 싶으면:
            // audio.PlayOneShot(audio.clip);
        }
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

    
    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _agent.isStopped = true;
        
        LookAtPlayer();
        
        for (int i = 0; i < _burstCount; i++)
        {
            LookAtPlayer();
            FireLaser(_eyeMuzzle, Vector3.zero);
            if (i < _burstCount - 1 && i + 1 != _burstCount)   
                yield return new WaitForSeconds(_betweenShotDelay);
        }
        
        _agent.isStopped = false;
        _isAttacking = false;
        _isCoolingDown = true;
        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }


    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
        Debug.Log($"LaserRobot took {dmg} damage, current HP: {_curHp}");
    }

    private void Die()
    {
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("LaserRobot has died.");
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
