using UnityEngine;
using UnityEngine.AI;

public class PunchRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 50.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 10.0f;
    [SerializeField] private float _attackCastingTime = 2.0f;
    [SerializeField] private float _attackCooldown = 1.0f;
    [SerializeField] private float _aggravationRange = 6.9f;
    [SerializeField] private float _attackRange = 2.0f;
    [SerializeField] private float _moveSpeed = 2.0f;
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
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;
        _animator = GetComponentInChildren<Animator>();

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
        }
    }

    void Update()
    {
        if (_player == null || _agent == null) return;

        if (_isAttacking)
        {
            _agent.isStopped = true;
            return;
        }

        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(transform.position, out var snapped))
            {
                _agent.Warp(snapped);
            }
            else
            {
                return;
            }
        }

        // --- 거리 판정 ---
        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // ------------------

        // 인식범위 안이면 계속 쳐다보기
        if (!_isAttacking && worldDist <= _aggravationRange)
            LookAtPlayer();

        // ✅ 공격 조건: 실제 거리 기반 + 정지 상태 + 쿨타임 아님
        //    ➜ 공격 시작 거리와 히트 거리 계수를 "같은 기준"으로 맞춘다.
        float attackStartRange = _attackRange * 1.2f;        // ★ 여기 계수 조절 가능 (1.1~1.3 사이 감으로)
        
        if (!_isCoolingDown &&                         // 쿨타임 중엔 공격 X
            worldDist <= attackStartRange &&           // ★ 공격 시작 가능한 최대 거리
            HasLineOfSight() && 
            _agent.velocity.sqrMagnitude < 0.1f)
        {
            _animator.SetBool("isWalking", false);
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }

        // ✅ 추적 조건
        if (worldDist <= _aggravationRange && HasLineOfSight())
        {
            _agent.isStopped = false;
            _animator.SetBool("isWalking", true);
            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
        else
        {
            _agent.isStopped = true;
            _animator.SetBool("isWalking", false);
            _agent.ResetPath();
        }

        if (_curHp <= 0f) Die();
    }


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
    
    private bool HasLineOfSight()
    {
        if (_player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 0.8f;
        Vector3 target = _player.transform.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
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

        return true;
    }
    
    private void LookAtPlayer()
    {
        if (_player == null || !HasLineOfSight()) return;

        Vector3 lockedDir = _player.transform.position - transform.position;
        lockedDir.y = 0f;
        lockedDir.Normalize();
        
        if (lockedDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lockedDir);
    }

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        _isAttacking = true;                        // ★ 여기서 바로 설정
        _agent.isStopped = true;
        _animator.SetTrigger("isAttacking");
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        Debug.Log($"[PunchRobot] Start AttackCasting");

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

        // ✅ 히트 거리도 "공격 시작 거리"와 같은 기준으로 체크
        float dist = Vector3.Distance(transform.position, _player.transform.position);
        float hitRange = _attackRange * 1.2f;       // ★ 위의 attackStartRange와 같은 값 사용

        if (dist > hitRange || !HasLineOfSight())   // ★ 이 거리 밖이면 → 회피 성공
        {
            Debug.Log($"[PunchRobot] Attack missed (dist={dist:F2})");
            _isAttacking = false;
            _agent.isStopped = false;               // 다시 이동 가능하게
            yield break;                            // 쿨다운 없음 (플레이어 회피 성공)
        }
        
        // ✅ 여기까지 왔으면 실제 히트
        _player.TakeDamage(_damage);
        Debug.Log($"PunchRobot attacked player for {_damage} damage!");

        _isAttacking = false;
        _isCoolingDown = true;                      // ★ 맞췄을 때만 쿨다운
        _agent.isStopped = false;

        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }
    
    public void CancelAttack()
    {
        Debug.Log($"PunchRobot Fail Attack (Cancel)");
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
