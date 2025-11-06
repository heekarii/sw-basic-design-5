using UnityEngine;
using UnityEngine.AI;

public class LaserRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 30.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 10.0f;
    [SerializeField] private float _attackCastingTime = 0.5f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private float _aggravationRange = 6.5f;
    [SerializeField] private float _attackRange = 5.5f;
    [SerializeField] private float _moveSpeed = 1.3f;
    [SerializeField] private Player _player;
    [SerializeField] private Transform _eyeMuzzle;
    [SerializeField] private GameObject _laserProjectilePrefab;
    
    [Header("Burst Settings")]
    [SerializeField] private int _burstCount = 2;              // 따당=2발
    [SerializeField] private float _betweenShotDelay = 1f;     // 1초 간격
    
    
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

        Vector3 dir = (targetPoint - muzzle.position).normalized;

        var go = Instantiate(_laserProjectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
        if (go.TryGetComponent(out LaserProjectile proj))
            proj.Init(dir, _player);
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

        // 1) 캐스팅 시작 시점의 '조준 지점' 스냅샷 (트래킹 금지)
        Vector3 targetPoint = (_player != null) ? _player.transform.position
            : transform.position + transform.forward * _attackRange;

        // 2) 캐스팅 동안은 스냅샷 지점만 향하게 부드럽게 회전
        float elapsed = 0f;
        while (elapsed < _attackCastingTime)
        {
            elapsed += Time.deltaTime;

            Vector3 dir = targetPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
            yield return null;
        }

        // 3) 따–당: 한 눈에서 2발, 1초 간격으로 발사
        for (int i = 0; i < _burstCount; i++)
        {
            FireLaser(_eyeMuzzle, targetPoint);

            // 마지막 발 전까지만 간격 대기
            if (i < _burstCount - 1)
                yield return new WaitForSeconds(_betweenShotDelay);
        }

        // 4) 쿨다운 (쿨다운 동안 이동 가능)
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;
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
