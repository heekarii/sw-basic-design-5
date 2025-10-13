using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [Header("Game Status")] 
    [SerializeField] private int _curScore;
    [SerializeField] private int _curScrap;
    [SerializeField] private int _curBattery;
    [SerializeField] private bool _isTreasureFound;
    public Player Player;

    [Header("Game Setting")] 
    [SerializeField] private float _fScrapWeight;
    [SerializeField] float _fBatteryWeight;
    
    // [Header("UI")]
    
    
    private void Awake()
    {
        InitStatus();
    }

    private void InitStatus()
    {
        _curScore = 0;
        _curScrap = 0;
        _curBattery = 0;
        _isTreasureFound = false;
    }
    
    public void SetGameScore()
    {
        PlayerStatus curStat = Player.GetStatus();
        
        // _curScore = ???
        
    }

    public void ShowTargetUI()
    {
        
    }
    
}
