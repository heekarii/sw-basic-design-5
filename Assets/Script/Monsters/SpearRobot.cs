using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

public class SpearRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 200.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 50.0f;
    [SerializeField] private float _Batterydamage = 1.0f;
    [SerializeField] private float _attackCastingTime = 0.5f;
    [SerializeField] private float _attackCooldown = 3.0f;
    [SerializeField] private float _attackingTime = 1.0f;
    [SerializeField] private float _stunTime = 1.0f;
    [SerializeField] private float _aggravationRange = 15.25f;
    [SerializeField] private float _attackRange = 5.75f;
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 12;

    [Header("others")]
    [SerializeField] private Player _player;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _electricSound;
    private AudioSource _electricAudioSource;
    private AudioSource _attackAudioSource;
    
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 3.0f;
    [SerializeField] private AudioSource _deathAudio;
    
    private Animator _animator;
    private bool _isDead = false;
    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _animator = GetComponent<Animator>();
        _curHp = _maxHp;
        
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();
        
        
        _electricAudioSource = gameObject.AddComponent<AudioSource>();
        _electricAudioSource.clip = _electricSound;
        _electricAudioSource.loop = true;
        _electricAudioSource.playOnAwake = false;
        _electricAudioSource.spatialBlend = 1.0f; // 3D
        
        _attackAudioSource = gameObject.AddComponent<AudioSource>();
        _attackAudioSource.clip = _attackSound;
        _attackAudioSource.loop = false;
        _attackAudioSource.playOnAwake = false;
        _attackAudioSource.spatialBlend = 1.0f; // 3D
        
        
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
        if (worldDist <= _attackRange && HasLineOfSight() && _agent.velocity.sqrMagnitude < 0.1f)
        {
            _animator.SetBool("isWalking", false);
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }


        // ✅ 추적 조건
        if (worldDist <= _aggravationRange && HasLineOfSight())
        {
            if (!_electricAudioSource.isPlaying)
                _electricAudioSource.Play();
            _agent.isStopped = false;
                _animator.SetBool("isWalking", true);
            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }
        else
        {
            if (_electricAudioSource.isPlaying)
                _electricAudioSource.Stop();
            _agent.isStopped = true;
            _animator.SetBool("isWalking", false);
            _agent.ResetPath();
        }

      //  Debug.Log($"[SpearRobot] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

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
        lockedDir.y = 0f;
        lockedDir.Normalize();
        
        // 몸을 스냅샷 방향으로 즉시 정렬
        if (lockedDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lockedDir);
    }
    
    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown || !HasLineOfSight() || _isDead) return;
        _animator.SetTrigger("isAttacking");
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _agent.isStopped = true;
        if (_attackAudioSource != null)
            _attackAudioSource.Play();
        

        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[SpearRobot] Start AttackCasting");
        yield return new WaitForSeconds(_attackCastingTime-0.5f);
        

        // 공격 시점에 다시 조건 검사 (거리 + 시야 + 존재)
        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (_player != null && dist < _attackRange * 1.05f && HasLineOfSight()) 
        {
            _player.TakeDamage(_damage);
            _player.ConsumeBatteryPercent(_Batterydamage);
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
        UpdateHpUI();   // 데미지 받을 때마다 HP바 갱신
        if (_curHp <= 0f) Die();
        Debug.Log($"SpearRobot took {dmg} damage, current HP: {_curHp}");
    }

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

        // 1) 진행 중인 코루틴(공격 등) 전부 정지 + 상태 플래그 초기화
        StopAllCoroutines();
        _isAttacking   = false;
        _isCoolingDown = false;

        // 2) NavMeshAgent 완전히 멈추기
        if (_agent != null)
        {
            _agent.isStopped      = true;
            _agent.velocity       = Vector3.zero;
            _agent.ResetPath();
            _agent.updateRotation = false;
        }

        // 3) 이동/공격 관련 사운드 정지
        if (_electricAudioSource != null && _electricAudioSource.isPlaying)
            _electricAudioSource.Stop();

        if (_attackAudioSource != null && _attackAudioSource.isPlaying)
            _attackAudioSource.Stop();

        // 4) 애니메이션 정리 + 죽는 모션 강제
        if (_animator != null)
        {
            _animator.ResetTrigger("isAttacking");  // 공격 트리거 리셋
            _animator.SetBool("isWalking", false);  // 걷기 끄기
            _animator.SetTrigger("isDie");          // 죽음 트리거
        }

        // 5) 콜라이더 비활성화 (시체에 부딪히는 것 방지)
        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
            selfCol.enabled = false;

        // 6) HP바 끄기
        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        // 7) 일정 시간 후 스크랩 드랍 + 오브젝트 삭제
        StartCoroutine(DieRoutine());
    }

    
    private IEnumerator DieRoutine()
    {
        _deathAudio.Play();
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);               
        Destroy(gameObject);
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
