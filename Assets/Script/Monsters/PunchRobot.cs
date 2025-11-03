using UnityEngine;
using UnityEngine.AI;

public class PunchRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 50f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackCastingTime = 0.5f;
    [SerializeField] private float _attackCooldown = 1.0f;
    [SerializeField] private float _aggravationRange = 5.5f;
    [SerializeField] private float _attackRange = 1f;
    [SerializeField] private float _moveSpeed = 1f;

    [SerializeField] private Player _player;
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
            Debug.LogError("[PunchRobot] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[PunchRobot] Player를 찾지 못했습니다.");
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
            Debug.LogError("[PunchRobot] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[PunchRobot] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[PunchRobot] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
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
               // Debug.LogWarning("[PunchRobot] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
               // Debug.LogError("[PunchRobot] 재워프 실패: 주변에 NavMesh 없음");
                return;
            }
        }

        // --- ✅ NavMesh 기반 거리 판정 ---
        float navDist = _agent.remainingDistance;
        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // ---------------------------------

    // ✅ 공격 조건: 실제 거리 기반 + 정지 상태 확인
        if (worldDist <= _attackRange && _agent.velocity.sqrMagnitude < 0.1f)
        {
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }


        // ✅ 추적 조건
        if (worldDist <= _aggravationRange)
        {
            _agent.isStopped = false;

            Vector3 targetPos = _player.transform.position;

            // 플레이어를 NavMesh 위로 투영
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
            else
            {
               // Debug.LogWarning($"[PunchRobotRat] Player 주변에 NavMesh 없음! 원본 위치: {targetPos}");
            }
        }
        else
        {
            _agent.isStopped = true;
        }

      //  Debug.Log($"[PunchRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

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

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _agent.isStopped = true;
        
        Debug.Log("[PunchRobot] Start AttackCasting");
        yield return new WaitForSeconds(_attackCastingTime);

        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (_player == null || dist > _attackRange * 1.05f) 
        {
            CancelAttack();
            yield break;
        }
        
        _player.TakeDamage(_damage);
        Debug.Log($"PunchRobot attacked player for {_damage} damage!");

        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;
        Debug.Log($"PunchRobot Start Cooldown");
        yield return new WaitForSeconds(_attackCooldown);

        Debug.Log($"PunchRobot End Cooldown");
        _isCoolingDown = false;

    }
    
    public void CancelAttack()
    {
        Debug.Log($"PunchRobot Fail Attack");
        _isAttacking = false;
        _agent.isStopped = false;
    }
    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
        Debug.Log($"PunchRobot took {dmg} damage, current HP: {_curHp}");
    }

    private void Die()
    {
        Destroy(gameObject);
        Debug.Log("PunchRobot has died.");
    }
}
