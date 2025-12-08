using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

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
    [SerializeField] private Image _notEnoughScrapImage;

    [Header("Repair Main Station Buttons")] 
    [SerializeField] private Button _runStation;
    [SerializeField] private Button _exitStation;
    [SerializeField] private Button _exitButton;
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

        InitButtonEvents();
        InitUIState();
    }

    private void Start()
    {
        _transitionManager?.RegisterStationManager(this);
    }

    private void OnEnable()
    {
        _enterStationStatus = _gameManager.StatusManager.CurrentStatus;

        if (_enterStationStatus == null)
        {
            Debug.LogWarning("[StationManager] PlayerStatus 가져오기 실패");
            return;
        }

        PopulateOptionTextsFromStatus(_enterStationStatus);

        if (_repairSource != null)
            Debug.Log($"[StationManager] Repair 진입 출처: {_repairSource.gameObject.name}");
    }

    private void InitButtonEvents()
    {
        _runStation?.onClick.AddListener(OnRunStationClick);
        _exitStation?.onClick.AddListener(OnExitStationClick);
        _exitButton?.onClick.AddListener(OnExitStationClick);

        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);

        _upgradeHealth?.onClick.AddListener(OnUpgradeHealth);
        _upgradeWeapon?.onClick.AddListener(OnUpgradeWeapon);
        _upgradeMove?.onClick.AddListener(OnUpgradeMoveSpeed);
    }

    private void OnUpgradeHealth()
    {
        UpgradeIdx = 1;
        TryStartMiniGame(CalculateNeededScrap(1));
    }

    private void OnUpgradeWeapon()
    {
        UpgradeIdx = 2;
        TryStartMiniGame(CalculateNeededScrap(2));
    }

    private void OnUpgradeMoveSpeed()
    {
        UpgradeIdx = 3;
        TryStartMiniGame(CalculateNeededScrap(3));
    }

    private int CalculateNeededScrap(int upgradeType)
    {
        var s = _enterStationStatus;
        if (s == null) return 0;

        return upgradeType switch
        {
            1 => s.CurrentHealthLevel switch { 1 => 20, 2 => 30, 3 => 50, _ => 0 },
            2 => s.CurrentWeaponLevel switch { 1 => 20, 2 => 40, 3 => 70, _ => 0 },
            3 => s.CurrentSpeedLevel  switch { 1 => 20, 2 => 40, _ => 0 },
            _ => 0
        };
    }

    private void TryStartMiniGame(int neededScrap)
    {
        int scrapNow = _gameManager.Resources.Scrap;

        if (scrapNow >= neededScrap)
        {
            _gameManager.Resources.DecreaseScrap(neededScrap);

            _selectUpgradeImage?.gameObject.SetActive(false);
            _transitionManager.StartMiniGame("MCardGame");
        }
        else
        {
            StartCoroutine(ShowNotEnoughScrap());
        }
    }

    private IEnumerator ShowNotEnoughScrap()
    {
        _notEnoughScrapImage?.gameObject.SetActive(true);
        yield return new WaitForSeconds(2f);
        _notEnoughScrapImage?.gameObject.SetActive(false);
    }

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

        if      (UpgradeIdx == 1) player.ApplyHealthUpgrade();
        else if (UpgradeIdx == 2) player.ApplyWeaponUpgrade();
        else if (UpgradeIdx == 3) player.ApplySpeedUpgrade();

        PlayerStatus newStatus = player.GetStatus();
        if (newStatus == null) return;

        if (_level != null)
        {
            _level.text = UpgradeIdx switch
            {
                1 => $"{newStatus.CurrentHealthLevel}",
                2 => $"{newStatus.CurrentWeaponLevel}",
                3 => $"{newStatus.CurrentSpeedLevel}",
                _ => "-"
            };
        }

        if (_amount != null)
        {
            _amount.text = UpgradeIdx switch
            {
                1 => $"+{(int)(newStatus.MaxHealth - _enterStationStatus.MaxHealth)}",
                2 => $"+{(int)(newStatus.AttackPower - _enterStationStatus.AttackPower)}",
                3 => $"+{(newStatus.SpeedWithBoost - _enterStationStatus.SpeedWithBoost):F0}",
                _ => ""
            };
        }
    }

    private void PopulateOptionTextsFromStatus(PlayerStatus status)
    {
        if (status == null) return;

        _curHealth.text = $"Lv. {status.CurrentHealthLevel}";
        _curWeapon.text = $"Lv. {status.CurrentWeaponLevel}";
        _curMoveSpeed.text = $"Lv. {status.CurrentSpeedLevel}";

        _scrapForHealth.text  = status.CurrentHealthLevel switch  { 1 => "20", 2 => "30", 3 => "50", _ => "-" };
        _scrapForWeapon.text  = status.CurrentWeaponLevel switch  { 1 => "20", 2 => "40", 3 => "70", _ => "-" };
        _scrapForMoveSpeed.text = status.CurrentSpeedLevel switch { 1 => "20", 2 => "40", _ => "-" };
    }

    private void InitUIState()
    {
        _bgoff?.gameObject.SetActive(true);
        _bgon?.gameObject.SetActive(false);
        _notEnoughScrapImage?.gameObject.SetActive(false);
        _selectRunImage?.gameObject.SetActive(true);
        _selectUpgradeImage?.gameObject.SetActive(false);
        _successImage?.gameObject.SetActive(false);
        _failureImage?.gameObject.SetActive(false);
    }

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

    private void OnRunStationClick()
    {
        _gameManager.Resources.DecreaseBattery(2f);

        _repairSource?.SetEnter(true);
        _bgoff?.gameObject.SetActive(false);
        _bgon?.gameObject.SetActive(true);
        _selectRunImage?.gameObject.SetActive(false);
        _selectUpgradeImage?.gameObject.SetActive(true);
    }

    private void OnExitStationClick()
    {
        _transitionManager.ExitRepairStation(_repairSource);
    }
    private void OnClickSuccessImage() => _transitionManager.ExitRepairStation(null);
    private void OnClickFailureImage() => _transitionManager.ExitRepairStation(null);
}
