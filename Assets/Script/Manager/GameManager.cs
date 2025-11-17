using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Status")] 
    [SerializeField] private int _curScrap;
    [SerializeField] private float _curBattery;
    private PlayerStatus _playerStatus;

    [Header("UI Elements")]
    [SerializeField] private Image _batteryFillbar;
    [SerializeField] private TextMeshProUGUI _batteryText;

    [SerializeField] private TextMeshProUGUI _healthLevel;
    [SerializeField] private TextMeshProUGUI _maxHPText;
    [SerializeField] private TextMeshProUGUI _curHealthText;
    
    [SerializeField] private TextMeshProUGUI _attackLevel;
    [SerializeField] private TextMeshProUGUI _curAttackText;
    [SerializeField] private TextMeshProUGUI _curBulletetText;
    [SerializeField] private Image _meleeImage;
    
    [SerializeField] private TextMeshProUGUI _moveLevel;
    [SerializeField] private TextMeshProUGUI _curSpeedText;
    [SerializeField] private TextMeshProUGUI _curBoostText;
    
    [SerializeField] private TextMeshProUGUI _curScrapText;
    
    public Player Player;
    public int WeaponType; // 0 : Short, 1 : long
    
    
    protected override void Awake()
    {
        base.Awake();
        InitStatus();
    }

    private void Start()
    {
        Player = FindAnyObjectByType<Player>();
        _meleeImage.gameObject.SetActive(false);
    }

    private void InitStatus()
    {
        _curScrap = 0;
        _curBattery = 100;
    }

    // TODO : 탐사 관련 매니저 구현 후 ShowTargetUI 함수 구현
    public void ShowTargetUI()
    {
        
    }

    private void UpdateStatus()
    {
        _playerStatus = Player.GetStatus();
        _curBattery = _playerStatus.BatteryRemaining;
        _batteryFillbar.fillAmount = _curBattery / 100f;
        _batteryText.text = $"{_curBattery:F2}%";
        
        _healthLevel.text = $"{_playerStatus.CurrentHealthLevel}";
        _maxHPText.text = $"{_playerStatus.MaxHealth}";
        _curHealthText.text = $"{_playerStatus.CurrentHealth}";
        
        _attackLevel.text = $"{_playerStatus.CurrentWeaponLevel}";
        _curAttackText.text = $"{_playerStatus.AttackPower}";
        // 원거리 무기인 경우에만 잔탄 표시
        if (_playerStatus.CurrentWeaponLevel <= 4)
        {
            _curBulletetText.gameObject.SetActive(false);
            _meleeImage.gameObject.SetActive(true);
        }
        else _curBulletetText.text = $"{_playerStatus.BulletCount}";
        
        _moveLevel.text = $"{_playerStatus.CurrentSpeedLevel}";
        _curSpeedText.text = $"{_playerStatus.MoveSpeed:F2}";
        _curBoostText.text = $"{_playerStatus.SpeedWithBoost:F2}";
        
        _curScrapText.text = $"{_curScrap}";
    }

    public PlayerStatus SendStatus => _playerStatus;
    
    private void Update()
    {
        UpdateStatus();
        
    }
    
    public void AddScrap(int amount)
    {
        _curScrap += amount;
        Debug.Log($"[GameManager] 스크랩 {_curScrap} 보유 중");
    }

    public void ApplyHealthMiniGame(bool isSuccess)
    {
        if (!isSuccess) return;
        Player.UpdateHealth();
        
    }
}
