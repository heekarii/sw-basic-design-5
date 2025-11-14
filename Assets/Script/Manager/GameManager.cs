using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Status")] 
    [SerializeField] private int _curScrap;
    [SerializeField] private float _curBattery;
    public Player Player;
    public int WeaponType; // 0 : Short, 1 : long
    
    // [Header("UI")]
    
    
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

    public void AddScrap(int amount)
    {
        _curScrap += amount;
        Debug.Log($"[GameManager] 스크랩 {_curScrap} 보유 중");
    }
    
}
