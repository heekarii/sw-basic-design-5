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
    }

    private void InitStatus()
    {
        _curScrap = 0;
        _curBattery = 0;
    }

    // TODO : 탐사 관련 매니저 구현 후 ShowTargetUI 함수 구현
    public void ShowTargetUI()
    {
        
    }

    private void UpdateStatusUI()
    {
        _batteryFillbar.fillAmount = _curBattery / 100f;
        _batteryText.text = $"{_curBattery:F2}%";
    }
    
    private void Update()
    {
        _playerStatus = Player.GetStatus();
        _curBattery = _playerStatus.BatteryRemaining;
        UpdateStatusUI();
        
    }
    
    public void AddScrap(int amount)
    {
        _curScrap += amount;
        Debug.Log($"[GameManager] 스크랩 {_curScrap} 보유 중");
    }
}
