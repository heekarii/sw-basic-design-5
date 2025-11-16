using UnityEngine;
using UnityEngine.AI;

public class PunchRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 50.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 15.0f;
    [SerializeField] private float _attackCastingTime = 2.0f;
    [SerializeField] private float _attackCooldown = 1.0f;
    [SerializeField] private float _aggravationRange = 6.9f;
    [SerializeField] private float _attackRange = 2.0f;
    [SerializeField] private float _moveSpeed = 2.0f;
    [SerializeField] private float _lookAtTurnSpeed = 8.0f;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 3;
    [SerializeField] private AudioSource _attackAudio;
    [SerializeField] private Player _player;
    
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;
    private Animator _animator;

    void Start()
    {
        _agent   = GetComponent<NavMeshAgent>();
        _player  = FindObjectOfType<Player>();
        _animator = GetComponentInChildren<Animator>();
        _curHp   = _maxHp;

        if (_agent == null)
        {
            Debug.LogError("[PunchRobot] NavMeshAgent가 없습니다.");
            enabled = false; 
            return;
        }
        if (_player == null)
        {
            Debug.LogError("[PunchRobot] Player를 찾지 못했습니다.");
            enabled = false; 
            return;
        }

        // 기본 파라미터
        _agent.speed           = _moveSpeed;
        _agent.stoppingDistance = _attackRange;
        _agent.updateRotation  = true;
        _agent.autoBraking     = true;

        // 시작 위치 NavMesh에 스냅
        if (!TrySnapToNavMesh(transform.position, out var snapped))
        {
            Debug.LogError("[PunchRobot] 시작 위치 근처에 NavMesh가 없습니다.");
            enabled = false; 
            return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
            _agent.Warp(snapped);
    }

    void Update()
    {
        if (_player == null || _agent == null) return;

        // 공격 중이면 이동/추적 로직 패스
        if (_isAttacking)
        {
            _agent.isStopped = true;
            return;
        }

        // NavMesh 이탈 방지
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(transform.position, out var snapped))
                _agent.Warp(snapped);
            else
                return;
        }

        // --- 거리 판정 ---
        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // ------------------

        // 인식범위 안이면 계속 플레이어 바라보기
        if (worldDist <= _aggravationRange)
            LookAtPlayer();

        // 공격 시작 거리 (조금 여유)
        float attackStartRange = _attackRange * 1.2f;

        // ✅ 공격 조건: 쿨다운 아님 + 사거리 안 + 정지 상태
        if (!_isCoolingDown &&
            worldDist <= attackStartRange &&
            HasLineOfSight() &&
            _agent.velocity.sqrMagnitude < 0.1f)
        {
            _animator.SetBool("isWalking", false);
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }

        // ✅ 추적 조건: 쿨다운이 아닐 때만
        if (!_isCoolingDown &&
            worldDist <= _aggravationRange &&
            HasLineOfSight())
        {
            _agent.isStopped = false;
            _animator.SetBool("isWalking", true);

            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
        else
        {
            // 쿨다운 중이거나 범위 밖이면 멈춤
            _agent.isStopped = true;
            _animator.SetBool("isWalking", false);
            _agent.ResetPath();
        }

        if (_curHp <= 0f) Die();
    }

    // NavMesh 근처 위치 찾는 헬퍼
    private bool TrySnapToNavMesh(Vector3 origin, out Vector3 snapped)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
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

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 target = _player.transform.position + Vector3.up * 1.0f;

        Vector3 dir  = target - origin;
        float   dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 콜라이더면 RaycastAll로 다시 검사
            if (hit.collider.transform.IsChildOf(transform))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    if (h.collider.GetComponentInParent<Player>() != null) return true;
                    return false;
                }
                return true;
            }

            if (hit.collider.GetComponentInParent<Player>() != null) return true;
            return false;
        }

        // 아무것도 안 맞으면 가려진 게 없는 것으로 간주
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

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;

        _isAttacking = true;
        _agent.isStopped = true;

        _animator.SetTrigger("isAttacking");
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        Debug.Log("[PunchRobot] Start AttackCasting");

        // 캐스팅 전반부
        yield return new WaitForSeconds(_attackCastingTime * 0.7f);

        if (_attackAudio != null)
            _attackAudio.Play();

        // 캐스팅 후반부
        yield return new WaitForSeconds(_attackCastingTime * 0.3f);

        if (_player == null)
        {
            _isAttacking = false;
            _agent.isStopped = false;
            yield break;
        }

        // 히트 판정
        float dist     = Vector3.Distance(transform.position, _player.transform.position);
        float hitRange = _attackRange * 1.2f;  // 공격 시작 범위와 동일 계수

        if (dist > hitRange || !HasLineOfSight())
        {
            // ▶ 회피 성공: 쿨다운 없이 바로 추적 재개
            Debug.Log($"[PunchRobot] Attack missed (dist={dist:F2})");
            _isAttacking  = false;
            _agent.isStopped = false;
            yield break;
        }

        // ▶ 실제 타격
        _player.TakeDamage(_damage);
        Debug.Log($"[PunchRobot] Hit player for {_damage} damage!");

        _isAttacking   = false;
        _isCoolingDown = true;

        // 쿨다운 동안은 제자리
        _agent.isStopped = true;
        _animator.SetBool("isWalking", false);

        yield return new WaitForSeconds(_attackCooldown);

        _isCoolingDown = false;
    }
    
    public void CancelAttack()
    {
        Debug.Log("[PunchRobot] Attack canceled");
        _isAttacking   = false;
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
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("PunchRobot has died.");
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
