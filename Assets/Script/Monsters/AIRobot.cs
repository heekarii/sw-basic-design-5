using UnityEngine;

public class AIRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 100.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 30.0f;
    [SerializeField] private float _damageInterval = 1.0f;
    [SerializeField] private float _attackCooldown = 5.0f;
    [SerializeField] private float _attackingTime = 10.0f;
    [SerializeField] private float _attackRange = 15.1f;
    [SerializeField] private GameObject _lightningPrefab;
    [SerializeField] private float _strikeSize = 2f;
    [SerializeField] private GameObject _redLightning;
    [SerializeField] private GameObject _blueLightning;
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 10;
    [SerializeField] private Player _player;

    private Collider _playerCol;
    private Transform _tr;
    private Transform _playerTr;

    private bool _isAttacking = false;
    private bool _isCoolingDown = false;

    private void Awake()
    {
        _tr = transform;
    }

    private void Start()
    {
        if (_player == null)
            _player = FindObjectOfType<Player>();

        if (_player == null)
        {
            Debug.LogError("[AIRobot] Player를 찾지 못했습니다.");
            enabled = false;
            return;
        }

        _playerTr = _player.transform;
        _playerCol = _player.GetComponentInChildren<Collider>();
        _curHp = _maxHp;
    }

    private void Update()
    {
        if (_playerTr == null)
            return;

        // 사망 체크
        if (_curHp <= 0f)
        {
            Die();
            return;
        }

        // 이미 공격 중이거나 쿨타임이면 가만히 있음
        if (_isAttacking || _isCoolingDown)
            return;

        float dist = Vector3.Distance(_tr.position, _playerTr.position);

        // 공격 범위 + 시야 확보되면 공격 시작
        if (dist <= _attackRange && HasLineOfSight())
        {
            AttackPlayer();
        }
    }

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        Debug.Log("[AIRobot] start attack casting!");

        float elapsed = 0f;

        while (elapsed < _attackingTime)
        {
            if (_playerTr == null)
                break;

            // 1) 공격 범위 안의 랜덤한 지점 계산
            Vector3 strikePos = GetRandomStrikePosition();

            // 2) 번개 이펙트 생성 (선택 사항)
            if (_lightningPrefab != null)
            {
                GameObject vfx = Instantiate(
                    _lightningPrefab,
                    strikePos,
                    Quaternion.identity
                );

                // 2x2 사이즈로 맞추기 (XZ 기준)
                Vector3 s = vfx.transform.localScale;
                vfx.transform.localScale = new Vector3(_strikeSize, s.y, _strikeSize);
                
                Destroy(vfx, 0.5f);
            }
            
            if (IsPlayerInStrikeArea(strikePos))
            {
                _player.TakeDamage(_damage);
                Debug.Log($"[AIRobot] lightning hit player for {_damage} dmg!");
            }

            // 다음 타격까지 대기
            yield return new WaitForSeconds(_damageInterval);
            elapsed += _damageInterval;
        }

        // 종료 → 쿨다운
        _isAttacking = false;
        _isCoolingDown = true;

        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }
    
    // 로봇 위치를 중심으로 반지름 _attackRange인 원 안의 랜덤 지점
    private Vector3 GetRandomStrikePosition()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.value) * _attackRange; // 균일 분포

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 pos = _tr.position + offset;

        // 바닥 높이 맞추기 (지금은 로봇과 같은 y로 둠)
        pos.y = _tr.position.y;
        return pos;
    }

    private bool IsPlayerInStrikeArea(Vector3 strikePos)
    {
        if (_playerCol == null)
            _playerCol = _player.GetComponentInChildren<Collider>();

        if (_playerCol == null)
            return false;

        Bounds b = _playerCol.bounds;

        float half = _strikeSize * 0.5f;

        // 번개 영역 (XZ 평면)
        float minX = strikePos.x - half;
        float maxX = strikePos.x + half;

        float minZ = strikePos.z - half;
        float maxZ = strikePos.z + half;

        // 플레이어 콜라이더 경계 박스가 번개 영역과 겹치는지 체크
        bool overlapX = b.max.x >= minX && b.min.x <= maxX;
        bool overlapZ = b.max.z >= minZ && b.min.z <= maxZ;

        return overlapX && overlapZ;
    }


    
    private bool HasLineOfSight()
    {
        if (_playerTr == null)
            return false;

        // 약간 위에서 쏘게 해서 바닥에 안 찍히도록
        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // Ray가 닿은 오브젝트에 Player가 달려있으면 시야 확보
            return hit.collider.GetComponentInParent<Player>() != null;
        }

        // 아무 것도 안 맞으면(공중에 떠있다든가) 시야가 막힌 건 아니라고 보고 true
        return true;
    }

    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        Debug.Log($"[AIRobot] took {dmg} damage, current HP: {_curHp}");

        if (_curHp <= 0f)
            Die();
    }

    private void Die()
    {
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("[AIRobot] has died.");
    }

    public void DropScrap(int amount)
    {
        if (!_scrapData) return;

        GameObject scrap = Instantiate(
            _scrapData.ScrapPrefab,
            _tr.position,
            Quaternion.identity);

        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AIRobot] 스크랩 {amount} 드랍");
    }
}
