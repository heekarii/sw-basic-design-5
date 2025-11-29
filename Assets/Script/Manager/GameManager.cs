using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Status")] 
    [SerializeField] private int _curScrap;
    [SerializeField] private float _curBattery;

    private PlayerStatus _playerStatus;  // 항상 마지막으로 계산된 스냅샷
    private bool _initialized;

    [Header("UI - Battery")]
    [SerializeField] private Image _batteryFillbar;
    [SerializeField] private TextMeshProUGUI _batteryText;

    [Header("UI - Health")]
    [SerializeField] private TextMeshProUGUI _healthLevel;
    [SerializeField] private TextMeshProUGUI _maxHPText;
    [SerializeField] private TextMeshProUGUI _curHealthText;
    
    [Header("UI - Attack")]
    [SerializeField] private TextMeshProUGUI _attackLevel;
    [SerializeField] private TextMeshProUGUI _curAttackText;
    [SerializeField] private TextMeshProUGUI _curBulletText;
    [SerializeField] private Image _meleeImage;
    
    [Header("UI - Move")]
    [SerializeField] private TextMeshProUGUI _moveLevel;
    [SerializeField] private TextMeshProUGUI _curSpeedText;
    [SerializeField] private TextMeshProUGUI _curBoostText;
    
    [Header("UI - Resource")]
    [SerializeField] private TextMeshProUGUI _curScrapText;
    
    [Header("References")]
    public Player Player;          // 씬에 존재하는 플레이어 참조
    public int WeaponType;        // 0 : 근거리, 1 : 원거리 (플레이어 초기 무기 선택용)
    
    protected override void Awake()
    {
        base.Awake();
        InitStatus();
    }

    private void Start()
    {
        CachePlayerIfNeeded();
        InitUIVisibility();
        _initialized = true;

        // 처음 한 번 상태/ UI 동기화
        SafeUpdateStatusAndUI();
    }

    private void Update()
    {
        // Player가 아직 null이면 한 번 더 시도 (씬 전환 후 등)
        if (Player == null)
        {
            CachePlayerIfNeeded();
        }

        SafeUpdateStatusAndUI();
    }

    #region Initialization

    private void InitStatus()
    {
        _curScrap = 0;
        _curBattery = 100f;
        _playerStatus = null;
    }

    private void CachePlayerIfNeeded()
    {
        if (Player != null) return;

        Player = FindAnyObjectByType<Player>();
        if (Player == null)
        {
            Debug.LogWarning("[GameManager] Player를 씬에서 찾지 못했습니다.");
        }
    }

    private void InitUIVisibility()
    {
        if (_meleeImage != null)
            _meleeImage.gameObject.SetActive(false);

        if (_curBulletText != null)
            _curBulletText.gameObject.SetActive(true);
    }

    #endregion

    #region Status & UI Update

    /// <summary>
    /// Player가 null일 수 있는 상황을 고려해 안전하게 상태/ UI를 갱신.
    /// </summary>
    private void SafeUpdateStatusAndUI()
    {
        if (!_initialized) return;
        if (Player == null) return;

        UpdateStatusFromPlayer();
        UpdateUI();
    }

    /// <summary>
    /// Player로부터 PlayerStatus 스냅샷을 받아와 내부 상태를 갱신.
    /// </summary>
    private void UpdateStatusFromPlayer()
    {
        _playerStatus = Player.GetStatus();
        _curBattery = _playerStatus.BatteryRemaining;
    }

    /// <summary>
    /// 현재 상태(_playerStatus, _curBattery, _curScrap 등)를 기반으로 UI를 갱신.
    /// </summary>
    private void UpdateUI()
    {
        if (_playerStatus == null) return;

        UpdateBatteryUI();
        UpdateHealthUI();
        UpdateAttackUI();
        UpdateMoveUI();
        UpdateResourceUI();
    }

    private void UpdateBatteryUI()
    {
        if (_batteryFillbar != null)
            _batteryFillbar.fillAmount = Mathf.Clamp01(_curBattery / 100f);

        if (_batteryText != null)
            _batteryText.text = $"{_curBattery:F2}%";
    }

    private void UpdateHealthUI()
    {
        if (_healthLevel != null)
            _healthLevel.text = _playerStatus.CurrentHealthLevel.ToString();

        if (_maxHPText != null)
            _maxHPText.text = _playerStatus.MaxHealth.ToString();

        if (_curHealthText != null)
            _curHealthText.text = _playerStatus.CurrentHealth.ToString();
    }

    private void UpdateAttackUI()
    {
        if (_attackLevel != null)
            _attackLevel.text = _playerStatus.CurrentWeaponLevel.ToString();

        if (_curAttackText != null)
            _curAttackText.text = _playerStatus.AttackPower.ToString();

        bool isMelee = _playerStatus.CurrentWeaponLevel <= 4; // 기존 로직 유지

        if (isMelee)
        {
            if (_curBulletText != null)
                _curBulletText.gameObject.SetActive(false);

            if (_meleeImage != null)
                _meleeImage.gameObject.SetActive(true);
        }
        else
        {
            if (_meleeImage != null)
                _meleeImage.gameObject.SetActive(false);

            if (_curBulletText != null)
            {
                _curBulletText.gameObject.SetActive(true);
                _curBulletText.text = _playerStatus.BulletCount.ToString();
            }
        }
    }

    private void UpdateMoveUI()
    {
        if (_moveLevel != null)
            _moveLevel.text = _playerStatus.CurrentSpeedLevel.ToString();

        if (_curSpeedText != null)
            _curSpeedText.text = _playerStatus.MoveSpeed.ToString("F2");

        if (_curBoostText != null)
            _curBoostText.text = _playerStatus.SpeedWithBoost.ToString("F2");
    }

    private void UpdateResourceUI()
    {
        if (_curScrapText != null)
            _curScrapText.text = _curScrap.ToString();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 다른 시스템에서 플레이어 상태 스냅샷을 조회할 수 있도록 제공.
    /// 항상 가장 마지막으로 계산된 상태를 반환한다.
    /// </summary>
    public PlayerStatus SendStatus => _playerStatus;

    /// <summary>
    /// StationManager 등에서 "지금 이 시점의 스냅샷"이 필요할 때 호출.
    /// 내부적으로 PlayerStatus를 한 번 더 계산해 두는 것이 필요하면 여기에서 처리.
    /// 현재는 Update()에서 매 프레임 갱신하므로, 단순히 캐시된 값을 반환한다.
    /// </summary>
    public PlayerStatus GetLatestStatus()
    {
        return _playerStatus;
    }

    /// <summary>
    /// 스크랩 자원을 증가시키고, 디버그 로그를 남깁니다.
    /// </summary>
    public void AddScrap(int amount)
    {
        if (amount == 0) return;

        _curScrap += amount;
        if (_curScrap < 0) _curScrap = 0;

        Debug.Log($"[GameManager] 스크랩 {_curScrap} 보유 중");
    }

    /// <summary>
    /// 체력 미니게임 결과를 적용합니다.
    /// </summary>
    public void ApplyHealthMiniGame(bool isSuccess)
    {
        if (!isSuccess) return;
        if (Player == null) return;

        // Player에 추가한 공개 업그레이드 API 사용
        Player.ApplyHealthUpgrade();
        
        // 즉시 상태/ UI를 최신화
        UpdateStatusFromPlayer();
        UpdateUI();
    }

    public void DecreaseBattery(float amount)
    {
        _curBattery -= amount;
    }
    
    public void DecreaseScrap(int amount)
    {
        _curScrap -= amount;
    }
    
    public int GetScrapAmount => _curScrap;
    
    #endregion
}
