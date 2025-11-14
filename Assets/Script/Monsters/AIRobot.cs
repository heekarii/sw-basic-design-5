using UnityEngine;

public class AIRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 100.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 30.0f;
    [SerializeField] private float _damageInterval = 1.0f;   // 번개 떨어지는 간격
    [SerializeField] private float _attackCooldown = 5.0f;   // 공격 한 사이클 끝난 후 쿨다운
    [SerializeField] private float _attackingTime = 10.0f;   // 공격 유지 시간
    [SerializeField] private float _attackRange = 15.1f;
    [SerializeField] private float _strikeSize = 2f;
    [SerializeField] private GameObject _lightningPrefab;    // 떨어지는 번개
    [SerializeField] private GameObject _redFx;              // vfx_Lightning_red (몸에 붙은 경고 이펙트)
    [SerializeField] private GameObject _blueFx;             // vfx_Lightning_blue (공격 중 이펙트)
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 10;
    [SerializeField] private Player _player;

    private AudioSource _blueAudio;
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

        _playerTr  = _player.transform;
        _playerCol = _player.GetComponentInChildren<Collider>();
        _curHp     = _maxHp;

        // 파티클 초기 상태 꺼두기
        if (_redFx != null)  _redFx.SetActive(false);
        if (_blueFx != null)
        {
            _blueFx.SetActive(false);
            _blueAudio = _blueFx.GetComponentInChildren<AudioSource>();
        }
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

        float dist   = GetFlatDistanceToPlayer();
        bool inRange = dist <= _attackRange;
        bool hasLOS  = HasLineOfSight();

        // 사거리 밖이거나 시야 없으면 → 이펙트 모두 OFF, 새로운 공격도 시작 X
        if (!inRange || !hasLOS)
        {
            SetRed(false);
            SetBlue(false);
            return;
        }

        // 여기부터는: 사거리 안 + 시야 OK

        // 공격/쿨다운 상태가 아니면 공격 시작
        if (!_isAttacking && !_isCoolingDown)
        {
            AttackPlayer();
        }

        // 상태 플래그에 따라 이펙트 제어
        if (_isAttacking)
        {
            // 공격 중 → 파랑 ON, 빨강 OFF
            SetRed(false);
            SetBlue(true);
        }
        else if (_isCoolingDown)
        {
            // 쿨다운 중 → 빨강 ON, 파랑 OFF
            SetBlue(false);
            SetRed(true);
        }
        else
        {
            // 둘 다 아니면 → 모두 OFF
            SetRed(false);
            SetBlue(false);
        }
    }

    // 수평 거리(XZ)만 사용해서 공격 범위 판단
    private float GetFlatDistanceToPlayer()
    {
        if (_playerTr == null) return float.MaxValue;

        Vector3 a = _tr.position;
        Vector3 b = _playerTr.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        float elapsed = 0f;

        // 번개는 damageInterval 주기로 떨어짐
        float tickDuration = _damageInterval;

        while (elapsed < _attackingTime)
        {
            if (_playerTr == null)
                break;

            float dist   = GetFlatDistanceToPlayer();
            bool inRange = dist <= _attackRange;
            bool hasLOS  = HasLineOfSight();

            // 공격 도중에라도 범위/시야 끊기면 종료
            if (!inRange || !hasLOS)
            {
                break;
            }

            // 1) 번개 떨어질 위치 계산 + 실제 번개 프리팹 생성
            Vector3 strikePos = GetRandomStrikePosition();

            if (_lightningPrefab != null)
            {
                GameObject bolt = Instantiate(_lightningPrefab, strikePos, Quaternion.identity);
                Vector3 s = bolt.transform.localScale;
                bolt.transform.localScale = new Vector3(_strikeSize, s.y, _strikeSize);
                // damageInterval 동안만 보이게 (조금 여유 줄려면 *0.9f 정도로)
                Destroy(bolt, _damageInterval);
            }

            // 2) 파란 이펙트 오디오 재생 (이때 이미 Update에서 파란 FX는 ON 상태)
            if (_blueAudio != null)
            {
                _blueAudio.Stop();
                _blueAudio.Play();
            }

            // 3) 이 시점에 데미지 판정
            if (IsPlayerInStrikeArea(strikePos))
            {
                _player.TakeDamage(_damage);
                Debug.Log($"[AIRobot] lightning hit player for {_damage} dmg!");
            }

            // 4) 다음 번개까지 damageInterval 만큼 대기
            yield return new WaitForSeconds(_damageInterval);
            elapsed += tickDuration;
        }

        // 공격 종료
        _isAttacking = false;

        // 아직 사거리 안 + 시야 OK라면 쿨다운 진입
        if (GetFlatDistanceToPlayer() <= _attackRange && HasLineOfSight())
        {
            _isCoolingDown = true;
            yield return new WaitForSeconds(_attackCooldown);
            _isCoolingDown = false;
        }
        else
        {
            // 범위 밖으로 나가 있으면 그냥 이펙트 다 끄고 끝
            SetRed(false);
            SetBlue(false);
        }
    }

    // 로봇 위치를 중심으로 반지름 _attackRange인 원 안의 랜덤 지점 (판정용)
    private Vector3 GetRandomStrikePosition()
    {
        float angle  = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.value) * _attackRange;

        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        Vector3 pos    = _tr.position + offset;
        pos.y = _tr.position.y;
        return pos;
    }

    private bool IsPlayerInStrikeArea(Vector3 strikePos)
    {
        if (_playerCol == null)
            _playerCol = _player.GetComponentInChildren<Collider>();

        if (_playerCol == null)
            return false;

        Bounds b  = _playerCol.bounds;
        float half = _strikeSize * 0.5f;

        float minX = strikePos.x - half;
        float maxX = strikePos.x + half;
        float minZ = strikePos.z - half;
        float maxZ = strikePos.z + half;

        bool overlapX = b.max.x >= minX && b.min.x <= maxX;
        bool overlapZ = b.max.z >= minZ && b.min.z <= maxZ;

        return overlapX && overlapZ;
    }

    private bool HasLineOfSight()
    {
        if (_playerTr == null)
            return false;

        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir  = target - origin;
        float   dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        // 모든 히트 가져오기
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            dir,
            dist,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return true; // 아무것도 안 맞으면 막힌 건 아니라고 봄

        // 가까운 순으로 정렬
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var h in hits)
        {
            // 내 몸(자기 자신/자식)은 무시
            if (h.collider.transform.IsChildOf(_tr))
                continue;

            // 처음으로 만난 "자기 아닌" 오브젝트가 Player면 시야 OK
            return h.collider.GetComponentInParent<Player>() != null;
        }

        // 자기 콜라이더만 맞고 끝난 경우
        return true;
    }

    // 파티클 토글 헬퍼
    private void SetRed(bool on)
    {
        if (_redFx == null) return;
        if (_redFx.activeSelf == on) return;
        _redFx.SetActive(on);
    }

    private void SetBlue(bool on)
    {
        if (_blueFx == null) return;
        if (_blueFx.activeSelf == on) return;
        _blueFx.SetActive(on);
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
        SetRed(false);
        SetBlue(false);

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
