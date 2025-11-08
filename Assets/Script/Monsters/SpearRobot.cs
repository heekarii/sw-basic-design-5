using UnityEngine;
using UnityEngine.AI;

public class SpearRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 150.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 50.0f;
    [SerializeField] private float _Batterydamage = 1.0f;
    [SerializeField] private float _attackCastingTime = 0.5f;
    [SerializeField] private float _attackCooldown = 3.0f;
    [SerializeField] private float _attackingTime = 1.0f;
    [SerializeField] private float _stunTime = 1.0f;
    [SerializeField] private float _aggravationRange = 12.25f;
    [SerializeField] private float _attackRange = 5.25f;
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절

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
            Debug.LogError("[SpearRobot] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[SpearRobot] Player를 찾지 못했습니다.");
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
            Debug.LogError("[SpearRobot] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[SpearRobot] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[SpearRobot] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
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
               // Debug.LogWarning("[SpearRobot] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
               // Debug.LogError("[SpearRobot] 재워프 실패: 주변에 NavMesh 없음");
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
               // Debug.LogWarning($"[SpearRobot] Player 주변에 NavMesh 없음! 원본 위치: {targetPos}");
            }
        }
        else
        {
            _agent.isStopped = true;
        }

      //  Debug.Log($"[SpearRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

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
    

    private void LookAtPlayer()
    {
        if (_player == null) return;

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
        
        Debug.Log($"[SpearRobot] Start AttackCasting");
        yield return new WaitForSeconds(_attackCastingTime);

        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (_player != null || dist < _attackRange * 1.05f)
        {
            _player.TakeDamage(_damage);
            _player.ConsumeBatteryPercentOfCurrent(_Batterydamage);
            _player.Stun(_stunTime);
        }
        
        // Debug.Log($"SpearRobot attacked player for {_damage} damage!");
        
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;
        // Debug.Log($"SpearRobot Start Cooldown");
        yield return new WaitForSeconds(_attackCooldown);

        // Debug.Log($"SpearRobot End Cooldown");
        _isCoolingDown = false;

    }
    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
        Debug.Log($"SpearRobot took {dmg} damage, current HP: {_curHp}");
    }

    private void Die()
    {
        Destroy(gameObject);
        Debug.Log("SpearRobot has died.");
    }
}
