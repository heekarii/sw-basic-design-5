using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class PunchRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 50.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 15.0f;
    [SerializeField] private float _attackCastingTime = 2.0f;
    [SerializeField] private float _attackCooldown = 1.0f;
    [SerializeField] private float _aggravationRange = 6.9f;   // 인식 범위
    [SerializeField] private float _attackRange = 3.0f;        // 공격 범위
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _lookAtTurnSpeed = 8.0f;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 3;
    [SerializeField] private AudioSource _attackAudio;
    [SerializeField] private Player _player;
    
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 3.0f;
    
    
    private bool _isDead = false;
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    
    private NavMeshAgent _agent;
    private Animator _animator;
    private Rigidbody _rb;
    private Transform _tr;
    private Transform _playerTr;

    // 거의 멈췄다고 보는 기준
    private const float STOP_VEL_SQR = 0.05f;

    private void Awake()
    {
        _tr       = transform;
        _agent    = GetComponent<NavMeshAgent>();
        _rb       = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();

        if (_player == null)
            _player = FindObjectOfType<Player>();

        if (_player != null)
            _playerTr = _player.transform;
    }

    private void Start()
    {
        if (_agent == null || _player == null)
        {
            Debug.LogError("[PunchRobot] NavMeshAgent 또는 Player가 없습니다. 스크립트 비활성화.");
            enabled = false;
            return;
        }

        // Rigidbody가 있다면 NavMeshAgent와 충돌하지 않게 설정
        if (_rb != null)
        {
            _rb.isKinematic = true;       // 이동은 NavMeshAgent가 담당
            _rb.useGravity  = false;
            _rb.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;   // 옆으로 넘어지는 것 방지
        }

        // NavMeshAgent 기본 세팅
        _agent.speed            = _moveSpeed;
        _agent.acceleration     = 20f;
        _agent.angularSpeed     = 720f;
        _agent.stoppingDistance = _attackRange;
        _agent.updatePosition   = true;
        _agent.updateRotation   = false;  // 회전은 우리가 직접 제어
        _agent.autoBraking      = true;

        // HP UI 세팅
        _curHp = _maxHp;
        if (_hpFillImage != null)
        {
            _hpFillImage.type       = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        UpdateHpUI();

        // 시작 위치 NavMesh에 스냅
        if (!TrySnapToNavMesh(_tr.position, out var snapped))
        {
            Debug.LogError("[PunchRobot] 시작 위치 근처에 NavMesh가 없습니다.");
            enabled = false;
            return;
        }

        if ((_tr.position - snapped).sqrMagnitude > 0.0001f)
            _agent.Warp(snapped);
    }

    private void Update()
    {
        if (_playerTr == null || _agent == null)
            return;
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

        // NavMesh 이탈 시 복구
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(_tr.position, out var snapped))
                _agent.Warp(snapped);
            else
                return;
        }

        float dist = Vector3.Distance(_tr.position, _playerTr.position);

        // 인식 범위 안에서는 항상 플레이어 쪽 바라보도록 시도
        if (dist <= _aggravationRange)
            FacePlayer();

        // 공격 중이면 이동 정지
        if (_isAttacking)
        {
            _agent.isStopped = true;
            _animator?.SetBool("isWalking", false);
            return;
        }

        float attackStartRange = _attackRange * 1.1f;   // 살짝 여유

        bool canSee = HasLineOfSight();

        // ───────── 공격 진입 ─────────
        if (!_isCoolingDown &&
            dist <= attackStartRange &&
            canSee &&
            _agent.velocity.sqrMagnitude < STOP_VEL_SQR)
        {
            _agent.isStopped = true;
            _animator?.SetBool("isWalking", false);
            AttackPlayer();
            return;
        }

        // ───────── 추적 로직 ─────────
        if (!_isCoolingDown &&
            dist > attackStartRange &&
            dist <= _aggravationRange &&
            canSee)
        {
            _agent.isStopped = false;

            // 플레이어 위치 근처의 NavMesh 지점 찾기
            if (NavMesh.SamplePosition(_playerTr.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                // 목적지가 충분히 달라졌을 때만 SetDestination 호출
                if (!_agent.hasPath ||
                    (_agent.destination - hit.position).sqrMagnitude > 0.1f)
                {
                    _agent.SetDestination(hit.position);
                }
            }

            _animator?.SetBool("isWalking", true);

            // 이동 방향을 기준으로 회전 (좀 더 자연스럽게)
            Vector3 vel = _agent.desiredVelocity;
            if (vel.sqrMagnitude > 0.01f)
            {
                FaceDirection(vel);
            }
            else
            {
                FacePlayer();
            }
        }
        else
        {
            // 추적 조건이 안 되면 정지
            _agent.isStopped = true;
            if (_agent.hasPath)
                _agent.ResetPath();

            _animator?.SetBool("isWalking", false);

            if (dist <= _aggravationRange)
                FacePlayer();
        }
    }

    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
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
        if (_playerTr == null) return false;

        Vector3 origin = _tr.position + Vector3.up * 0.8f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir  = target - origin;
        float   dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 콜라이더면 RaycastAll로 다시 검사
            if (hit.collider.transform.IsChildOf(_tr))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(_tr)) continue;
                    return h.collider.GetComponentInParent<Player>() != null;
                }
                return true;
            }

            return hit.collider.GetComponentInParent<Player>() != null;
        }

        // 아무것도 안 맞으면 가려진 게 없는 것으로 간주
        return true;
    }

    // 특정 방향을 바라보게 회전
    private void FaceDirection(Vector3 dirWorld)
    {
        dirWorld.y = 0f;
        if (dirWorld.sqrMagnitude < 0.0001f) return;

        dirWorld.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(dirWorld);
        _tr.rotation = Quaternion.Slerp(
            _tr.rotation,
            targetRot,
            Time.deltaTime * _lookAtTurnSpeed
        );
    }

    // 플레이어를 향해 회전
    private void FacePlayer()
    {
        if (_playerTr == null) return;
        Vector3 dir = _playerTr.position - _tr.position;
        FaceDirection(dir);
    }

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;

        _isAttacking   = true;
        _agent.isStopped = true;

        _animator?.SetBool("isWalking", false);
        _animator?.SetTrigger("isAttacking");

        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        Debug.Log("[PunchRobot] Start AttackCasting");

        // 캐스팅 전반부
        yield return new WaitForSeconds(_attackCastingTime * 0.7f);

        _attackAudio?.Play();

        // 캐스팅 후반부
        yield return new WaitForSeconds(_attackCastingTime * 0.3f);

        if (_playerTr == null)
        {
            _isAttacking    = false;
            _agent.isStopped = false;
            yield break;
        }

        float dist     = Vector3.Distance(_tr.position, _playerTr.position);
        float hitRange = _attackRange * 1.2f;

        if (dist > hitRange || !HasLineOfSight())
        {
            Debug.Log($"[PunchRobot] Attack missed (dist={dist:F2})");
            _isAttacking    = false;
            _agent.isStopped = false;
            yield break;
        }

        // 실제 타격
        _player.TakeDamage(_damage);
        Debug.Log($"[PunchRobot] Hit player for {_damage} damage!");

        _isAttacking   = false;
        _isCoolingDown = true;

        // 쿨다운 동안은 제자리
        _agent.isStopped = true;
        _animator?.SetBool("isWalking", false);

        yield return new WaitForSeconds(_attackCooldown);

        _isCoolingDown = false;
        _agent.isStopped = false;
    }
    
    // public void CancelAttack()
    // {
    //     Debug.Log("[PunchRobot] Attack canceled");
    //     _isAttacking    = false;
    //     _agent.isStopped = false;
    // }
    
    public void TakeDamage(float dmg)
    {
        _curHp = Mathf.Max(0f, _curHp - dmg);
        UpdateHpUI();

        Debug.Log($"[PunchRobot] took {dmg} damage, current HP: {_curHp}");

        if (_curHp <= 0f)
            Die();
    }

    // 체력바 채우기 갱신
    private void UpdateHpUI()
    {
        if (_hpFillImage == null) return;

        float ratio = (_maxHp > 0f) ? _curHp / _maxHp : 0f;
        _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
    }


    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        
        StopAllCoroutines();
        _isAttacking   = false;
        _isCoolingDown = false;
        
        if (_agent != null)
        {
            _agent.isStopped      = true;
            _agent.velocity       = Vector3.zero;
            _agent.ResetPath();
            _agent.updateRotation = false;
        }
        
        if (_animator != null)
        {
            _animator.ResetTrigger("isAttacking");
            _animator.SetBool("isWalking", false);
            _animator.SetTrigger("isDie");
        }

        if (_attackAudio != null && _attackAudio.isPlaying)
            _attackAudio.Stop();
        
        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);
        
        StartCoroutine(DieRoutine());
    }

    
    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);               
        Destroy(gameObject);
    }
    
    
    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, _tr.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[PunchRobot] 스크랩 {amount} 드랍");
    }
    
    // 몬스터를 중심으로 인식 범위(_aggravationRange)를 흰 원으로 시각화
    private void DrawAggroRadiusGizmo()
    {
        if (_aggravationRange <= 0f) return;

        Gizmos.color = Color.white;

        Vector3 center = transform.position;
        center.y += 0.05f;

        float radius   = _aggravationRange;
        int   segments = 48;
        float step     = 360f / segments;

        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i * Mathf.Deg2Rad;
            float x     = Mathf.Cos(angle) * radius;
            float z     = Mathf.Sin(angle) * radius;

            Vector3 next = center + new Vector3(x, 0f, z);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
