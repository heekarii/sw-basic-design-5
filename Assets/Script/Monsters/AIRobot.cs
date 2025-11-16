using UnityEngine;

public class AIRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 100.0f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _damage = 30.0f;
    [SerializeField] private float _damageInterval = 0.5f;   // 번개 떨어지는 간격
    [SerializeField] private float _attackCooldown = 5.0f;   // 공격 한 사이클 끝난 후 쿨다운
    [SerializeField] private float _attackingTime = 10.0f;   // 공격 유지 시간
    [SerializeField] private float _aggravationRange = 20.1f; // 인식 범위
    [SerializeField] private float _attackRange = 15.1f;      // 공격 범위
    [SerializeField] private float _strikeSize = 4.0f;

    [Header("VFX & SFX")]
    [SerializeField] private GameObject _lightningPrefab;    // 떨어지는 번개
    [SerializeField] private GameObject _redFx;              // vfx_Lightning_red (몸에 붙은 경고 이펙트)
    [SerializeField] private GameObject _blueFx;             // vfx_Lightning_blue (공격 중 이펙트)
    [SerializeField] private AudioSource _attackStartSource; // 공격 시작 사운드 (별도 AudioSource)
    
    [Header("Drop")]
    [SerializeField] private ScrapData _scrapData;
    [SerializeField] private int _scrapAmount = 10;

    [Header("Ref")]
    [SerializeField] private Player _player;
    
    private float _attackRangeSqr;
    private float _aggravationRangeSqr;

    private AudioSource _blueAudio;
    private Collider _playerCol;
    private Transform _tr;
    private Transform _playerTr;

    private bool _isAttacking = false;
    private bool _isCoolingDown = false;


    //===== Unity LifeCycle =====
    private void Awake()
    {
        _tr = transform;
    }

    private void Start()
    {
        _curHp = _maxHp;
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

        _attackRangeSqr      = _attackRange * _attackRange;
        _aggravationRangeSqr = _aggravationRange * _aggravationRange;

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

        // 거리/시야 계산
        bool inDetect = IsPlayerInDetectRangeAndVisible();   // 인식 범위 + 시야
        bool inAttack = IsPlayerInAttackRangeAndVisible();   // 공격 범위 + 시야

        // 인식 범위 바깥이고, 공격/쿨다운도 아니면 이펙트 전부 OFF
        if (!inDetect && !_isAttacking && !_isCoolingDown)
        {
            SetRed(false);
            SetBlue(false);
            return;
        }

        // 공격/쿨다운이 아니고, 공격 범위 안이면 공격 시작
        if (!_isAttacking && !_isCoolingDown && inAttack)
        {
            AttackPlayer();
        }

        // ===== 이펙트 제어 =====
        if (_isAttacking)
        {
            if (inAttack)
            {
                // 공격 중 + 공격 범위 안 → 파랑 ON, 빨강 OFF
                SetRed(false);
                SetBlue(true);
            }
            else if (inDetect)
            {
                // 공격 중인데 공격 범위 밖 (하지만 인식 범위 안) → 빨강 ON, 파랑 OFF
                SetBlue(false);
                SetRed(true);
            }
            else
            {
                // 완전 범위 밖 → 모두 OFF
                SetRed(false);
                SetBlue(false);
            }
        }
        else if (_isCoolingDown)
        {
            // 쿨다운 중이면 인식 범위 안에서 빨강, 아니면 OFF
            if (inDetect)
            {
                SetBlue(false);
                SetRed(true);
            }
            else
            {
                SetRed(false);
                SetBlue(false);
            }
        }
        else
        {
            // 대기 상태: 인식 범위 안이면 빨강, 아니면 OFF
            if (inDetect)
            {
                SetBlue(false);
                SetRed(true);
            }
            else
            {
                SetRed(false);
                SetBlue(false);
            }
        }
    }


    //===== Attack Logic =====

    private void AttackPlayer()
    {
        if (_isAttacking || _isCoolingDown) return;
        StartCoroutine(AttackRoutine());
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        // 공격 시작 사운드 1회 재생
        if (_attackStartSource != null)
            _attackStartSource.Play();
        
        float elapsed      = 0f;
        float tickDuration = _damageInterval;

        while (elapsed < _attackingTime)
        {
            if (_playerTr == null)
                break;

            // 다음 공격까지 대기 (→ 1초,2초,3초,... 타이밍 유지)
            yield return new WaitForSeconds(_damageInterval);
            
            // 대기 후에도 여전히 "공격 범위 + 시야" 안인지 확인
            if (!IsPlayerInAttackRangeAndVisible())
                break;

            // 1) 번개 떨어질 위치 계산 + 실제 번개 프리팹 생성
            Vector3 strikePos = GetRandomStrikePosition();

            if (_lightningPrefab != null)
            {
                GameObject bolt = Instantiate(_lightningPrefab, strikePos, Quaternion.identity);
                Vector3 s = bolt.transform.localScale;
                bolt.transform.localScale = new Vector3(_strikeSize, s.y, _strikeSize);
                Destroy(bolt, _damageInterval);
            }

            // 2) 파란 이펙트 오디오 재생
            if (_blueAudio != null && _blueAudio.clip != null)
            {
                _blueAudio.PlayOneShot(_blueAudio.clip);
            }

            // 3) 이 시점에 데미지 판정
            if (IsPlayerInStrikeArea(strikePos))
            {
                _player.TakeDamage(_damage);
                Debug.Log($"[AIRobot] lightning hit player for {_damage} dmg!");
            }
            
            elapsed += tickDuration;
        }

        // 공격 종료
        _isAttacking = false;

        // 아직 "인식 범위 + 시야" 안이라면 쿨다운 진입
        if (IsPlayerInDetectRangeAndVisible())
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


    //===== Helper: Range / Position / Hit Check =====

    // 인식 범위(_aggravationRange) + 시야 체크
    private bool IsPlayerInDetectRangeAndVisible()
    {
        if (_playerTr == null) return false;

        Vector3 a = _tr.position;
        Vector3 b = _playerTr.position;
        a.y = 0f;
        b.y = 0f;

        if ((a - b).sqrMagnitude > _aggravationRangeSqr)
            return false;

        return HasLineOfSight();
    }

    // 공격 범위(_attackRange) + 시야 체크
    private bool IsPlayerInAttackRangeAndVisible()
    {
        if (_playerTr == null) return false;

        Vector3 a = _tr.position;
        Vector3 b = _playerTr.position;
        a.y = 0f;
        b.y = 0f;

        if ((a - b).sqrMagnitude > _attackRangeSqr)
            return false;

        return HasLineOfSight();
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

        Bounds b   = _playerCol.bounds;
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

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            dir,
            dist,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return true; // 아무것도 안 맞으면 막힌 건 아니라고 봄

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


    //===== FX Toggle =====
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


    //===== HP & Die & Drop =====
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
