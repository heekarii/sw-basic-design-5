using UnityEngine;
using UnityEngine.AI;

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
               // Debug.LogWarning($"[LaserRobot] Player 주변에 NavMesh 없음! 원본 위치: {targetPos}");
            }
        }
        else
        {
            _agent.isStopped = true;
        }

      //  Debug.Log($"[LaserRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

        if (_curHp <= 0f) Die();
        
        if (!_isAttacking) 
            LookAtPlayerSmooth();
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
        if (go.TryGetComponent(out LaserProjectile proj))
            proj.Init(dir, _player);
    }

    private void LookAtPlayerSmooth()
    {
        if (_player == null) return;

        // 플레이어와의 방향 계산 (수평 회전만)
        Vector3 dir = _player.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        // 부드럽게 회전
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            _lookAtTurnSpeed * Time.deltaTime
        );
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

        // NavMeshAgent의 회전 제어 끄기 (공격 중엔 우리가 직접 조종)
        _agent.updateRotation = false;

        // 1️⃣ 공격 시작 시점의 '고정 방향' 스냅샷
        Vector3 lockedDir = (_player != null)
            ? (_player.transform.position - transform.position)
            : transform.forward;
        lockedDir.y = 0f;
        lockedDir.Normalize();

        // 몸을 스냅샷 방향으로 즉시 정렬
        if (lockedDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lockedDir);

        // 2️⃣ 따당 2발 고정 방향으로 발사
        for (int i = 0; i < _burstCount; i++)
        {
            FireLaser(_eyeMuzzle, Vector3.zero);
            if (i < _burstCount - 1 && i + 1 != _burstCount)   
                yield return new WaitForSeconds(_betweenShotDelay);
        }

        // 3️⃣ 공격 종료 → 다시 회전/추적 복구
        _agent.updateRotation = true; // NavMeshAgent 회전 복원
        _agent.isStopped = false;     // 이동 재개
        _isAttacking = false;
        _isCoolingDown = true;

        // 쿨다운 대기
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
        Destroy(gameObject);
        Debug.Log("LaserRobot has died.");
    }
}
