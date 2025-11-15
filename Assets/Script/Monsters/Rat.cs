using UnityEngine;
using UnityEngine.AI;

public class Rat : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 15f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 30f;
    [SerializeField] private float _aggravationRange = 12.75f;
    [SerializeField] private float _attackRange = 1.05f;
    [SerializeField] private float _explosionRadius = 2.0f;
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 2;

    [SerializeField] private Player _player;
    [SerializeField] private ParticleSystem _explosionEffect;
    [SerializeField] private AudioSource _explosionAudio;
    private NavMeshAgent _agent;
    
    private Transform _tr;
    private Transform _playerTr;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;
        
        _tr = transform;                 // 🔹 자기 Transform 캐시
        if (_player != null)
            _playerTr = _player.transform;  // 🔹 플레이어 Transform 캐시

        if (_agent == null)
        {
            Debug.LogError("[Rat] NavMeshAgent가 없습니다.");
            enabled = false; return;
        }
        if (_player == null)
        {
            Debug.LogError("[Rat] Player를 찾지 못했습니다.");
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
            Debug.LogError("[Rat] 시작 위치 근처에 NavMesh가 없습니다. Bake/레이어/높이 확인 필요.");
            enabled = false; return;
        }
        if ((snapped - transform.position).sqrMagnitude > 0.0001f)
        {
            _agent.Warp(snapped);
            //Debug.Log($"[Rat] NavMesh에 워프: {snapped}");
        }
        //Debug.Log("[Rat] Start OK: OnNavMesh=" + _agent.isOnNavMesh);
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
               // Debug.LogWarning("[Rat] NavMesh 이탈 감지 → 재워프");
            }
            else
            {
               // Debug.LogError("[Rat] 재워프 실패: 주변에 NavMesh 없음");
                return;
            }
        }

        // --- ✅ NavMesh 기반 거리 판정 ---
        float navDist = _agent.remainingDistance;
        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // ---------------------------------

    // ✅ 공격 조건: 실제 거리 기반 + 정지 상태 확인
    if (worldDist <= _attackRange
        && (!_agent.hasPath || _agent.remainingDistance <= _attackRange + 0.1f)
        && HasLineOfSight())  
        {
            _agent.isStopped = true;
            AttackPlayer();
            return;
        }


        // ✅ 추적 조건
        if (worldDist <= _aggravationRange && HasLineOfSight()) 
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
               // Debug.LogWarning($"[Rat] Player 주변에 NavMesh 없음! 원본 위치: {targetPos}");
            }
        }
        else
        {
            _agent.isStopped = true;
        }

      //  Debug.Log($"[Rat] remainingDist={navDist:F2}, worldDist={worldDist:F2}, pathStatus={_agent.pathStatus}, hasPath={_agent.hasPath}");

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
    
    private bool HasLineOfSight()
    {
        if (_playerTr == null)
            return false;

        // 쥐 눈 위치 / 플레이어 몸 정도 높이
        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        // 장애물 체크 (트리거는 무시)
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신의 콜라이더 먼저 맞았을 때 처리
            if (hit.collider.transform.IsChildOf(_tr))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(_tr))
                        continue;

                    return h.collider.GetComponentInParent<Player>() != null;
                }

                // 자기 자신 말고 아무도 안 맞았으면 시야 있음으로 간주
                return true;
            }

            // 첫번째로 맞은 게 플레이어인지 여부
            return hit.collider.GetComponentInParent<Player>() != null;
        }

        // 아무것도 안 맞으면 중간에 막는 게 없는 것 → 시야 있음
        return true;
    }
    
    private void PlayExplosion()
    {
        // 🔹 이펙트 실행
        if (_explosionEffect != null)
        {
            _explosionEffect.transform.SetParent(null); // 부모 떼기
            _explosionEffect.Play();

            float effectDuration =
                _explosionEffect.main.duration +
                _explosionEffect.main.startLifetime.constantMax;

            Destroy(_explosionEffect.gameObject, effectDuration + 0.1f);
        }

        // 🔹 사운드 실행
        if (_explosionAudio != null && _explosionAudio.clip != null)
        {
            _explosionAudio.transform.SetParent(null); // 부모 떼기
            _explosionAudio.Play();

            Destroy(_explosionAudio.gameObject, _explosionAudio.clip.length + 0.1f);
        }
    }

    private void AttackPlayer()
    {
        _agent.isStopped = true;
        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (dist <= _explosionRadius)
        {
            _player?.TakeDamage(_damage);
            Debug.Log($"Rat attacked player for {_damage} damage!");
        }
        PlayExplosion();
        Destroy(gameObject);
    }

    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
        Debug.Log($"Rat took {dmg} damage, current HP: {_curHp}");
    }

    private void Die()
    {
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("Rat has died.");
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
