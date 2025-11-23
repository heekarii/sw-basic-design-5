using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;      // HP바 Image용

public class ChainsawRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 200.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 70.0f;
    [SerializeField] private float _attackCastingTime = 0.5f;
    [SerializeField] private float _attackCooldown = 3.0f;
    [SerializeField] private float _attackingTime = 1.0f;
    [SerializeField] private float _aggravationRange = 12.25f;
    [SerializeField] private float _attackRange = 4.25f;
    [SerializeField] private float _moveSpeed = 4.0f;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    [SerializeField] private Animator _anim;   // 인스펙터 비워두면 Start에서 찾아줌
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 15;
    
    [SerializeField] private Player _player;

    [Header("Effects")] 
    [SerializeField] private AudioClip _sawSound;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _hitSound;
    
    // ================== HP BAR UI ==================
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    private Transform _camTr;                      // 카메라 Transform
    // =================================================
    
    private AudioSource _sawAudioSource;
    private AudioSource _attackAudioSource;
    private AudioSource _hitAudioSource;
    
// Animator Parameters (Animator 창에 동일한 이름으로 만들어야 함)
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving"); // bool
    private static readonly int HashAttack   = Animator.StringToHash("Attack");   // trigger

    
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;
        
        // HP Image 기본 설정 강제 (실수 방지용)
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();
        
        // 사운드 소스 설정
        _sawAudioSource = gameObject.AddComponent<AudioSource>();
        _sawAudioSource.clip = _sawSound;
        _sawAudioSource.loop = true;
        _sawAudioSource.playOnAwake = false;
        _sawAudioSource.spatialBlend = 1.0f; // 3D
        _sawAudioSource.volume = 0.5f;
        
        _attackAudioSource = gameObject.AddComponent<AudioSource>();
        _attackAudioSource.clip = _attackSound;
        _attackAudioSource.loop = false;
        _attackAudioSource.playOnAwake = false;
        _attackAudioSource.spatialBlend = 1.0f; // 3D
        
        _hitAudioSource = gameObject.AddComponent<AudioSource>();
        _hitAudioSource.clip = _hitSound;
        _hitAudioSource.loop = false;
        _hitAudioSource.playOnAwake = false;
        _hitAudioSource.spatialBlend = 1.0f; // 3D

        //애니메이션 효과 적용을 위한 애니메이터 찾기
        if (_anim == null)
            _anim = GetComponentInChildren<Animator>(true); // 자식에 붙어도 탐색
        if (_anim != null)
            _anim.applyRootMotion = false;                  // 이동은 NavMeshAgent가 담당
        
        
        if (_agent == null)
        {
            Debug.LogError("[ChainsawRobot] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[ChainsawRobot] Player를 찾지 못했습니다.");
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
            Debug.LogError("[ChainsawRobot] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[ChainsawRobot] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[ChainsawRobot] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
    }

    void Update()
    {
        if (_player == null || _agent == null) return;

        // 이동 애니메이션 on/off (공격 중에는 false)
        if (_anim != null)
        {
            bool moving = !_isAttacking && !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.04f;
            _anim.SetBool(HashIsMoving, moving);
        }
        
        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (TrySnapToNavMesh(transform.position, out var snapped))
            {
                _agent.Warp(snapped);
               // Debug.LogWarning("[ChainsawRobot] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
               // Debug.LogError("[ChainsawRobot] 재워프 실패: 주변에 NavMesh 없음");
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
            if (!_sawAudioSource.isPlaying)
                _sawAudioSource.Play();
            _agent.isStopped = false;
            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
        else
        {
            if (_sawAudioSource.isPlaying)
                _sawAudioSource.Stop();
            _agent.isStopped = true;
            _agent.ResetPath();
            if (_anim != null) _anim.SetBool(HashIsMoving, false);
        }

      //  Debug.Log($"[ChainsawRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

        if (_curHp <= 0f) Die();
    }
    
    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
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
        // Debug.Log("[ChainsawRobot] AttackPlayer() called");
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        if (_anim != null)
        {
            _anim.SetBool(HashIsMoving, false); // 걷기 OFF
            _anim.SetTrigger(HashAttack);
        }

        
        // 이동 정지(관성 제거)
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();

        // 모션 전환
        if (_anim != null)
        {
            _anim.SetBool(HashIsMoving, false); // 걷기 OFF
            _anim.SetTrigger(HashAttack);       // 공격 트리거
            _anim.CrossFade("Attack", 0.05f, 0, 0f); // Attack = 상태 이름 정확히

        }

        Debug.Log("ChainsawRobot start attack casting!");
        // 공격 사운드 대기 시간
        yield return new WaitForSeconds(0.5f);
        // 공격 사운드 재생
        if (_attackAudioSource != null && _attackSound != null)
            _attackAudioSource.Play();
        yield return new WaitForSeconds(_attackCastingTime);

        // 유효성 재확인 후 대미지
        if (_player != null)
        {
            float dist = Vector3.Distance(transform.position, _player.transform.position);
            if (dist <= _attackRange * 1.05f && HasLineOfSight())
            {
                _player.TakeDamage(_damage);
                yield return new WaitForSeconds(0.2f);
                if (_hitAudioSource != null && _hitSound != null) 
                    _hitAudioSource.Play();
            }
        }

        // 종료 → 쿨다운
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;

        yield return new WaitForSeconds(_attackCooldown - 1.0f);
        _isCoolingDown = false;
    }

    
    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        UpdateHpUI();
        if (_curHp <= 0f) Die();
        Debug.Log($"ChainsawRobot took {dmg} damage, current HP: {_curHp}");
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
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("ChainsawRobot has died.");
    }
    
    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, transform.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AirRobot] 스크랩 {amount} 드랍");
    }
    
    // 몬스터를 중심으로 인식 범위(_aggravationRange)를 흰 원으로 시각화
    private void DrawAggroRadiusGizmo()
    {
        // 반경이 0 이하면 그릴 필요 없음
        if (_aggravationRange <= 0f) return;

        Gizmos.color = Color.white;

        // 원의 중심: 몬스터 위치, 살짝 위로 띄워서 바닥에 안 묻히게
        Vector3 center = transform.position;
        center.y += 0.05f;

        float radius = _aggravationRange;
        int segments = 48;
        float step = 360f / segments;

        // 시작점: 중심 기준 X축 방향으로 radius 떨어진 곳
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 next = center + new Vector3(x, 0f, z);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

}
