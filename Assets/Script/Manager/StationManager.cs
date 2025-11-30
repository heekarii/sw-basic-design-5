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

    /// <summary> 
    /// RepairShop 들어올 때 한 번 저장해두는 스냅샷(= 들어오기 직전 상태) 
    /// </summary>
    private PlayerStatus _enterStationStatus;


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 1) Awake: 오브젝트 초기화만 — 상태 가져오지 않음(타이밍 문제 해결) */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void Awake()
    {
        _transitionManager = TransitionManager.Instance;
        _gameManager = GameManager.Instance;

        _transitionManager?.RegisterStationManager(this);

        InitButtonEvents();
        InitUIState();
    }

    private void InitButtonEvents()
    {
        _runStation?.onClick.AddListener(OnRunStationClick);
        _exitStation?.onClick.AddListener(OnExitStationClick);

        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 2) OnEnable: 최신 PlayerStatus를 항상 여기서 가져옴 (핵심 수정) */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void OnEnable()
    {
        // 최신 상태를 반드시 가져옴
        _enterStationStatus = FetchLatestPlayerStatus();

        if (_enterStationStatus == null)
        {
            Debug.LogWarning("[StationManager] OnEnable: PlayerStatus를 찾지 못함");
            return;
        }

        PopulateOptionTextsFromStatus(_enterStationStatus);
        Debug.Log("[StationManager] 최신 PlayerStatus로 옵션 텍스트 갱신 완료");

        if (_repairSource != null)
            Debug.Log($"[StationManager] Repair 진입 출처: {_repairSource.gameObject.name}");
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 3) Upgrade 결과 처리 */
    /* ──────────────────────────────────────────────────────────────────────── */
    public void ShowEndingPage(bool isSuccess)
    {
        if (!isSuccess)
        {
            _failureImage?.gameObject.SetActive(true);
            return;
        }

        _successImage?.gameObject.SetActive(true);

        // Player 찾기
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

        // 업그레이드 후 최신 상태 받아옴
        PlayerStatus newStatus = player.GetStatus();

        if (_level != null)
        {
            if      (UpgradeIdx == 1) _level.text = $"{newStatus.CurrentHealthLevel}";
            else if (UpgradeIdx == 2) _level.text = $"{newStatus.CurrentWeaponLevel}";
            else if (UpgradeIdx == 3) _level.text = $"{newStatus.CurrentSpeedLevel}";
        }

        if (_amount != null)
        {
            if      (UpgradeIdx == 1) _amount.text = $"+{(int)(newStatus.MaxHealth - _enterStationStatus.MaxHealth)}";
            else if (UpgradeIdx == 2) _amount.text = $"+{(int)(newStatus.AttackPower - _enterStationStatus.AttackPower)}";
            else if (UpgradeIdx == 3) _amount.text = $"+{(newStatus.MoveSpeed - _enterStationStatus.MoveSpeed):F0}";
        }
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 4) PlayerStatus 가져오기 (완전히 안정화됨) */
    /* ──────────────────────────────────────────────────────────────────────── */
    private PlayerStatus FetchLatestPlayerStatus()
    {
        // 1순위: GameManager가 마지막 프레임에 계산해둔 스냅샷
        if (_gameManager != null)
        {
            PlayerStatus s = _gameManager.GetLatestStatus();
            if (s != null) return s;
        }

        // 2순위: Player 객체에서 직접 가져오기
        Player player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            return player.GetStatus();
        }

        return null;
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 5) 옵션 텍스트 적용 로직 (수정 없음, 안전화만) */
    /* ──────────────────────────────────────────────────────────────────────── */

    private void PopulateOptionTextsFromStatus(PlayerStatus status)
    {
        if (status == null) return;

        if (_curHealth != null)
            _curHealth.text = $"Lv. {status.CurrentHealthLevel}";

        if (_curWeapon != null)
            _curWeapon.text = $"Lv. {status.CurrentWeaponLevel}";

        if (_curMoveSpeed != null)
            _curMoveSpeed.text = $"Lv. {status.CurrentSpeedLevel}";

        if (_scrapForHealth != null)
        {
            _scrapForHealth.text = status.CurrentHealthLevel switch
            {
                1 => "20",
                2 => "30",
                3 => "50",
                _ => "-"
            };
        }

        if (_scrapForWeapon != null)
        {
            _scrapForWeapon.text = status.CurrentWeaponLevel switch
            {
                1 => "10",
                2 => "30",
                3 => "50",
                _ => "-"
            };
        }

        if (_scrapForMoveSpeed != null)
        {
            _scrapForMoveSpeed.text = status.CurrentSpeedLevel switch
            {
                1 => "20",
                2 => "40",
                _ => "-"
            };
        }
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 6) 유틸리티 */
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

    private void AddClick(Image img, Action callback)
    {
        if (img == null) return;
        var clickable = img.GetComponent<ClickableImage>() ?? img.gameObject.AddComponent<ClickableImage>();
        clickable.onClick = callback;
    }

    public void SetRepairSource(Repair source)
    {
        _repairSource = source;
    }


    /* ──────────────────────────────────────────────────────────────────────── */
    /* 7) 버튼 콜백 */
    /* ──────────────────────────────────────────────────────────────────────── */
    private void OnRunStationClick()
    {
        _gameManager.DecreaseBattery(2f);

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

    private void OnUpgradeHealth()
    {
        UpgradeIdx = 1;
        TryStartMiniGame(_enterStationStatus.CurrentHealthLevel switch { 1 => 20, 2 => 30, 3 => 50, _ => 0 });
    }

    private void OnUpgradeWeapon()
    {
        UpgradeIdx = 2;
        TryStartMiniGame(_enterStationStatus.CurrentWeaponLevel switch { 1 => 10, 2 => 30, 3 => 50, _ => 0 });
    }

    private void OnUpgradeMoveSpeed()
    {
        UpgradeIdx = 3;
        TryStartMiniGame(_enterStationStatus.CurrentSpeedLevel switch { 1 => 20, 2 => 40, _ => 0 });
    }

    private void TryStartMiniGame(int neededScrap)
    {
        if (GameManager.Instance.GetScrapAmount >= neededScrap)
        {
            GameManager.Instance.DecreaseScrap(neededScrap);
            _transitionManager.StartMiniGame("MCardGame");
            _selectUpgradeImage?.gameObject.SetActive(false);
        }
    }
}
