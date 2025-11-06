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
    
    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _agent.isStopped = true;

        // 1) 캐스팅 시작 시점의 "발사 방향" 스냅샷(트래킹 금지)
        Vector3 lockedDir = (_eyeMuzzle != null) ? _eyeMuzzle.forward : transform.forward;

        // 선택: 캐스팅 동안 몸을 그 방향으로 부드럽게 정렬하고 싶을 때
        // (상하 회전은 빼고 수평만 맞춤)
        Quaternion startRot  = transform.rotation;
        Vector3 flatDir      = new Vector3(lockedDir.x, 0f, lockedDir.z);
        Quaternion targetRot = flatDir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(flatDir)
            : transform.rotation;

        float elapsed = 0f;
        while (elapsed < _attackCastingTime)
        {
            elapsed += Time.deltaTime;

            // ※ “그 방향만 보기”를 원하면 아래 줄 유지,
            //    "가만히 서있게" 하고 싶으면 아래 줄을 주석처리하면 됨.
            transform.rotation = Quaternion.Slerp(
                startRot, targetRot,
                Mathf.Clamp01(elapsed / _attackCastingTime)
            );

            yield return null;
        }

        // 2) 따–당(버스트) 발사: 한 눈에서 여러 발, 고정 방향으로 직진
        for (int i = 0; i < _burstCount; i++)
        {
            FireLaser(_eyeMuzzle, Vector3.zero);
            if (i < _burstCount - 1)
                yield return new WaitForSeconds(_betweenShotDelay);
        }

        // CoolDowning
        _isAttacking   = false;
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
