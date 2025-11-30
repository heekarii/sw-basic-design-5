using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Serialization;

public class StationManager : MonoBehaviour
{
    private TransitionManager _transitionManager;
    private GameManager _gameManager;
    private Repair _repairSource;

    [Header("Station Manager images")] 
    [SerializeField] private Image _bgoff;
    [SerializeField] private Image _bgon;
    [SerializeField] private Image _selectRunImage;
    [SerializeField] private Image _selectUpgradeImage;
    [SerializeField] private Image _successImage;
    [SerializeField] private Image _failureImage;

    [Header("Repair Main Station Buttons")] 
    [SerializeField] private Button _runStation;
    [SerializeField] private Button _exitStation;
    [SerializeField] private Button _upgradeHealth;
    [SerializeField] private Button _upgradeWeapon;
    [SerializeField] private Button _upgradeMove;
    [SerializeField] private TextMeshProUGUI _level;
    [SerializeField] private TextMeshProUGUI _amount;

    [Header("Options Texts")] 
    [SerializeField] private TextMeshProUGUI _curHealth;
    [SerializeField] private TextMeshProUGUI _scrapForHealth;
    [SerializeField] private TextMeshProUGUI _curWeapon;
    [SerializeField] private TextMeshProUGUI _scrapForWeapon;
    [SerializeField] private TextMeshProUGUI _curMoveSpeed;
    [SerializeField] private TextMeshProUGUI _scrapForMoveSpeed;

    [Header("Station Info")]
    [SerializeField] private int UpgradeIdx;
    
    private PlayerStatus _enterStationStatus;
    
    private void Awake()
    {
        _transitionManager = TransitionManager.Instance;
        _gameManager = GameManager.Instance;

        _transitionManager?.RegisterStationManager(this);

        InitButtonEvents();
        InitUIState();
    }

    private void OnEnable()
    {
        _enterStationStatus = FetchLatestPlayerStatus();

        if (_enterStationStatus == null)
        {
            Debug.LogWarning("[StationManager] OnEnable: PlayerStatus 가져오기 실패");
            return;
        }

        PopulateOptionTextsFromStatus(_enterStationStatus);
        Debug.Log("[StationManager] 최신 PlayerStatus로 UI 갱신 완료");

        if (_repairSource != null)
            Debug.Log($"[StationManager] Repair 진입 출처: {_repairSource.gameObject.name}");
    }
    
    private void InitButtonEvents()
    {
        _runStation?.onClick.AddListener(OnRunStationClick);
        _exitStation?.onClick.AddListener(OnExitStationClick);

        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);

        _upgradeHealth?.onClick.AddListener(OnUpgradeHealth);
        _upgradeWeapon?.onClick.AddListener(OnUpgradeWeapon);
        _upgradeMove?.onClick.AddListener(OnUpgradeMoveSpeed);
    }
    
    private void OnUpgradeHealth()
    {
        UpgradeIdx = 1;
        int neededScrap = CalculateNeededScrap(UpgradeIdx);
        TryStartMiniGame(neededScrap);
    }

    private void OnUpgradeWeapon()
    {
        UpgradeIdx = 2;
        int neededScrap = CalculateNeededScrap(UpgradeIdx);
        TryStartMiniGame(neededScrap);
    }

    private void OnUpgradeMoveSpeed()
    {
        UpgradeIdx = 3;
        int neededScrap = CalculateNeededScrap(UpgradeIdx);
        TryStartMiniGame(neededScrap);
    }
    
    private int CalculateNeededScrap(int upgradeType)
    {
        if (_enterStationStatus == null) return 0;

        return upgradeType switch
        {
            1 => _enterStationStatus.CurrentHealthLevel switch
            {
                1 => 20,
                2 => 30,
                3 => 50,
                _ => 0
            },

            2 => _enterStationStatus.CurrentWeaponLevel switch
            {
                1 => 10,
                2 => 30,
                3 => 50,
                _ => 0
            },

            3 => _enterStationStatus.CurrentSpeedLevel switch
            {
                1 => 20,
                2 => 40,
                _ => 0
            },

            _ => 0
        };
    }

    
    private void TryStartMiniGame(int neededScrap)
    {
        if (GameManager.Instance.GetScrapAmount >= neededScrap)
        {
            GameManager.Instance.DecreaseScrap(neededScrap);

            if (_selectUpgradeImage != null)
                _selectUpgradeImage.gameObject.SetActive(false);
            
            _transitionManager.StartMiniGame("MCardGame");
        }
        else
        {
            Debug.LogWarning("[StationManager] 스크랩 부족");
        }
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 7) 미니게임 종료 후 결과 화면 처리 */
    /* ──────────────────────────────────────────────────────────────────────── */
    public void ShowEndingPage(bool isSuccess)
    {
        if (!isSuccess)
        {
            _failureImage?.gameObject.SetActive(true);
            return;
        }

        _successImage?.gameObject.SetActive(true);

        Player player = FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogWarning("[StationManager] Player not found");
            return;
        }

        // 실제 업그레이드 적용
        if      (UpgradeIdx == 1) player.ApplyHealthUpgrade();
        else if (UpgradeIdx == 2) player.ApplyWeaponUpgrade();
        else if (UpgradeIdx == 3) player.ApplySpeedUpgrade();

        // 업그레이드 후 상태 비교
        PlayerStatus newStatus = player.GetStatus();
        if (newStatus == null) return;

        if (_level != null)
        {
            if (UpgradeIdx == 1) _level.text = $"{newStatus.CurrentHealthLevel}";
            else if (UpgradeIdx == 2) _level.text = $"{newStatus.CurrentWeaponLevel}";
            else if (UpgradeIdx == 3) _level.text = $"{newStatus.CurrentSpeedLevel}";
        }

        if (_amount != null)
        {
            if (UpgradeIdx == 1)
                _amount.text = $"+{(int)(newStatus.MaxHealth - _enterStationStatus.MaxHealth)}";
            else if (UpgradeIdx == 2)
                _amount.text = $"+{(int)(newStatus.AttackPower - _enterStationStatus.AttackPower)}";
            else if (UpgradeIdx == 3)
                _amount.text = $"+{(newStatus.MoveSpeed - _enterStationStatus.MoveSpeed):F0}";
        }
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 8) PlayerStatus 최신값 가져오기 (완전 안정화) */
    /* ──────────────────────────────────────────────────────────────────────── */
    private PlayerStatus FetchLatestPlayerStatus()
    {
        if (_gameManager != null)
        {
            var s = _gameManager.GetLatestStatus();
            if (s != null) return s;
        }

        Player p = FindAnyObjectByType<Player>();
        return p?.GetStatus();
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 9) 옵션 텍스트 세팅 */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void PopulateOptionTextsFromStatus(PlayerStatus status)
    {
        if (status == null) return;

        _curHealth.text = $"Lv. {status.CurrentHealthLevel}";
        _curWeapon.text = $"Lv. {status.CurrentWeaponLevel}";
        _curMoveSpeed.text = $"Lv. {status.CurrentSpeedLevel}";

        _scrapForHealth.text = status.CurrentHealthLevel switch { 1 => "20", 2 => "30", 3 => "50", _ => "-" };
        _scrapForWeapon.text = status.CurrentWeaponLevel switch { 1 => "10", 2 => "30", 3 => "50", _ => "-" };
        _scrapForMoveSpeed.text = status.CurrentSpeedLevel switch { 1 => "20", 2 => "40", _ => "-" };
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 10) UI 초기 상태 설정 */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void InitUIState()
    {
        _bgoff?.gameObject.SetActive(true);
        _bgon?.gameObject.SetActive(false);
        _selectRunImage?.gameObject.SetActive(true);
        _selectUpgradeImage?.gameObject.SetActive(false);
        _successImage?.gameObject.SetActive(false);
        _failureImage?.gameObject.SetActive(false);
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 11) 유틸리티 */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void AddClick(Image img, Action callback)
    {
        if (img == null) return;

        var clickable = img.GetComponent<ClickableImage>() ??
                        img.gameObject.AddComponent<ClickableImage>();

        clickable.onClick = callback;
    }

    public void SetRepairSource(Repair source)
    {
        _repairSource = source;
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 12) 버튼 콜백 */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void OnRunStationClick()
    {
        _gameManager.DecreaseBattery(2f);
        _repairSource.SetEnter(true);
        _bgoff?.gameObject.SetActive(false);
        _bgon?.gameObject.SetActive(true);
        _selectRunImage?.gameObject.SetActive(false);
        _selectUpgradeImage?.gameObject.SetActive(true);
    }

    private void OnExitStationClick()
    {
        _transitionManager.ExitRepairStation();
    }

    private void OnClickSuccessImage() => _transitionManager.ExitRepairStation();
    private void OnClickFailureImage() => _transitionManager.ExitRepairStation();
}
