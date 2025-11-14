using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LaserDrone : MonoBehaviour, IEnemy
{
    [Header("섬광 로봇 기본 설정")]
    [SerializeField] private float _detectDistance = 13.7f;  // 시야 감지 거리
    [SerializeField] private float _attackDistance = 8.7f;   // 공격 거리
    [SerializeField] private float _hoverHeight = 4.2f;      // 공중 높이
    [SerializeField] private float _moveSpeed = 6f;          // 이동 속도
    [SerializeField] private int _maxHealth = 50;            // 체력
    [SerializeField] private int _currentHealth;
    [SerializeField] private float _attackCooldown = 10f;    // 재공격 시간
    [SerializeField] private int _dropScrap = 5;             // 처치 시 스크랩 수
    [SerializeField] private GameObject _flashEffectPrefab;  // 공격시 밝은 불빛 이펙트
    [SerializeField] private int _scrapAmount = 3;            // 드랍 스크랩 양
    
    [Header("참조 오브젝트")]
    [SerializeField] private Transform _player;              // ZERON
    [SerializeField] private Image _flashOverlay;            // 섬광 피격용 UI (Canvas Image)
    [SerializeField] private ScrapData _scrapData;          // 스크랩 데이터
    

    private bool _isActive = false;
    private bool _isAttacking = false;
    private float _lastAttackTime = -999f;

    private void Start()
    {
        _currentHealth = _maxHealth;

        if (_player == null)
            _player = GameObject.FindWithTag("Player")?.transform;
    }

    private void Update()
    {
        if (_player == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        // ✅ 시야 및 거리 감지 (HasLineOfSight 적용)
        if (distance <= _detectDistance && HasLineOfSight())
            _isActive = true;

        if (!_isActive) return;

        // ✅ 공격 / 이동 판단
        if (distance > _attackDistance && !_isAttacking)
        {
            MoveTowardTarget();
        }
        else if (distance <= _attackDistance && !_isAttacking && HasLineOfSight())
        {
            TryAttack();
        }
    }

    // ============================================================
    //  이동 (공중 4.2m 높이 유지)
    // ============================================================
    private void MoveTowardTarget()
    {
        Vector3 targetPos = _player.position;
        targetPos.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, _moveSpeed * Time.deltaTime);
        transform.LookAt(_player);
    }

    // ============================================================
    //  공격
    // ============================================================
    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < _attackCooldown) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;

        // 공격 모션 (밝은 불빛 발사)
        if (_flashEffectPrefab != null)
        {
            GameObject effect = Instantiate(_flashEffectPrefab, transform.position + transform.forward * 1.5f, Quaternion.identity);
            Destroy(effect, 1.0f);
        }

        // 피해 즉시 적용 (내구도 영향 X, 섬광 효과만)
        if (_flashOverlay != null)
            StartCoroutine(ApplyFlashEffect());
        else
            Debug.LogWarning("[AirRobot] FlashOverlay 연결되지 않음");

        yield return new WaitForSeconds(1.0f);
        _isAttacking = false;
    }

    // ============================================================
    //  섬광 효과 (3초간 시야 차단)
    // ============================================================
    private IEnumerator ApplyFlashEffect()
    {
        _flashOverlay.gameObject.SetActive(true);
        _flashOverlay.color = new Color(1f, 1f, 0.7f, 0.9f); // 밝은 노란색
        yield return new WaitForSeconds(3f);
        _flashOverlay.gameObject.SetActive(false);
    }

    // ============================================================
    //  시야 감지 (Line of Sight)
    // ============================================================
    private bool HasLineOfSight()
    {
        if (_player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = _player.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신 콜라이더는 무시
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

            // 첫 번째 히트가 플레이어면 시야 있음
            if (hit.collider.GetComponentInParent<Player>() != null) return true;
            return false; // 다른 오브젝트에 가려짐
        }

        return true; // 아무것도 안 맞았으면 개방된 시야
    }

    // ============================================================
    //  피해 / 사망 처리
    // ============================================================
    public void TakeDamage(float damage)
    {
        _currentHealth -= Mathf.RoundToInt(damage);
        if (_currentHealth <= 0)
        {
            DropScrap(_scrapAmount);
            Destroy(gameObject);
        }
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
