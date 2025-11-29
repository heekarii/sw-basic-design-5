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
    [FormerlySerializedAs("_ScrapForHealth")] [SerializeField] private TextMeshProUGUI _scrapForHealth;
    [SerializeField] private TextMeshProUGUI _curWeapon;
    [FormerlySerializedAs("_ScrapForWeapon")] [SerializeField] private TextMeshProUGUI _scrapForWeapon;
    [SerializeField] private TextMeshProUGUI _curMoveSpeed;
    [FormerlySerializedAs("_ScrapForMoveSpeed")] [SerializeField] private TextMeshProUGUI _scrapForMoveSpeed;

    [Header("Station Info")]
    [SerializeField] private int UpgradeIdx;

    /// <summary>
    /// StationManager가 활성화되기 직전 시점의 PlayerStatus 스냅샷.
    /// Awake에서 한 번만 캡처하고, 이후에는 이 값을 기준으로 UI를 계산한다.
    /// </summary>
    private PlayerStatus _enterStationStatus;

    private void Awake()
    {
        #if UNITY_2023_2_OR_NEWER
        _transitionManager = UnityEngine.Object.FindFirstObjectByType<TransitionManager>();
        #else
        _transitionManager = FindObjectOfType<TransitionManager>();
        #endif
        _gameManager = GameManager.Instance;

        // StationManager가 열리기 직전(== 이 씬/오브젝트가 활성화되기 직전)에
        // GameManager가 마지막으로 계산해 둔 PlayerStatus를 한 번만 캡처
        if (_gameManager != null)
        {
            _enterStationStatus = _gameManager.GetLatestStatus();
            if (_enterStationStatus == null)
            {
                Debug.LogWarning("[StationManager] 초기 PlayerStatus가 아직 세팅되지 않았습니다.");
            }
        }

        if (_transitionManager != null)
        {
            _transitionManager.RegisterStationManager(this);
        }
        else
        {
            Debug.LogWarning("[StationManager] TransitionManager를 찾을 수 없습니다. 등록을 건너뜁니다.");
        }
        
        if (_runStation != null) _runStation.onClick.AddListener(OnRunStationClick);
        if (_exitStation != null) _exitStation.onClick.AddListener(OnExitStationClick);

        // 이미지(성공/실패)는 ClickableImage로 클릭 이벤트 연결
        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);

        InitUIState();

        // Awake 시점에 플레이어 최신 상태를 조회해 옵션 텍스트를 초기화
        var status = FetchCurrentPlayerStatus();
        if (status != null)
        {
            PopulateOptionTextsFromStatus(status);
            
            if (_upgradeHealth != null && status.CurrentHealthLevel <= 3) _upgradeHealth.onClick.AddListener(OnUpgradeHealth);
            if (_upgradeWeapon != null && status.CurrentWeaponLevel <= 3) _upgradeWeapon.onClick.AddListener(OnUpgradeWeapon);
            if (_upgradeMove != null && status.CurrentSpeedLevel <= 2) _upgradeMove.onClick.AddListener(OnUpgradeMoveSpeed);
        }
    }

    private void AddClick(Image img, Action callback)
    {
        if (img == null)
        {
            Debug.LogWarning("[StationManager] 클릭 이미지를 찾을 수 없습니다.");
            return;
        }
        var clickable = img.gameObject.GetComponent<ClickableImage>();
        if (clickable == null)
            clickable = img.gameObject.AddComponent<ClickableImage>();
        clickable.onClick = callback;
    }

    private void InitUIState()
    {
        if (_bgoff != null) _bgoff.gameObject.SetActive(true);
        if (_bgon != null) _bgon.gameObject.SetActive(false);
        if (_selectRunImage != null) _selectRunImage.gameObject.SetActive(true);
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(false);
        if (_successImage != null) _successImage.gameObject.SetActive(false);
        if (_failureImage != null) _failureImage.gameObject.SetActive(false);
    }

    private void OnRunStationClick()
    {
        Debug.Log("▶ 정비 기능 실행");
        GameManager.Instance.DecreaseBattery(2.0f);
        _repairSource.SetEnter(true);
        if (_bgoff != null) _bgoff.gameObject.SetActive(false);
        if (_bgon != null) _bgon.gameObject.SetActive(true);
        if (_selectRunImage != null) _selectRunImage.gameObject.SetActive(false);
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(true);
    }

    private void OnExitStationClick()
    {
        Debug.Log("▶ 정비소 종료");
        if (_transitionManager != null)
        {
            _transitionManager.ExitRepairStation();
        }
        else
            Debug.LogWarning("[StationManager] ExitRepairStation 호출 실패: TransitionManager가 없습니다.");
    }

    private void OnUpgradeHealth()
    {
        PlayerStatus status = FetchCurrentPlayerStatus();
        
        int neededScrap = 0;
        
        switch (status.CurrentHealthLevel)
        {
            case 1: neededScrap = 20; break;
            case 2: neededScrap = 30; break;
            case 3: neededScrap = 50; break;
            default: Debug.LogWarning("[StationManager] 체력 강화: 잘못된 현재 레벨"); break;
        }
        
        Debug.Log("▶ 체력 강화 선택");
        if (_transitionManager != null && GameManager.Instance.GetScrapAmount >= neededScrap)
        {
            switch (status.CurrentHealthLevel)
            {
                case 1: GameManager.Instance.DecreaseScrap(20); break;
                case 2: GameManager.Instance.DecreaseScrap(30); break;
                case 3: GameManager.Instance.DecreaseScrap(50); break;
                default: Debug.LogWarning("[StationManager] 체력 강화: 잘못된 현재 레벨"); break;
            }
            _transitionManager.StartMiniGame("MCardGame");
        }
        else
            Debug.LogWarning("[StationManager] StartMiniGame 호출 실패: TransitionManager가 없습니다.");
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(false);
        UpgradeIdx = 1;
    }

    private void OnUpgradeWeapon()
    {
        PlayerStatus status = FetchCurrentPlayerStatus();
        
        int neededScrap = 0;

        switch (status.CurrentWeaponLevel)
        {
            case 1: neededScrap = 10; break;
            case 2: neededScrap = 30; break;
            case 3: neededScrap = 50; break;
            default: Debug.LogWarning("[StationManager] 무기 강화: 잘못된 현재 레벨"); break;
        }
        
        Debug.Log("▶ 무기 강화 선택");
        if (_transitionManager != null && GameManager.Instance.GetScrapAmount >= neededScrap)
        {
            switch (status.CurrentWeaponLevel)
            {
                case 1: GameManager.Instance.DecreaseScrap(10); break;
                case 2: GameManager.Instance.DecreaseScrap(30); break;
                case 3: GameManager.Instance.DecreaseScrap(50); break;
                default: Debug.LogWarning("[StationManager] 무기 강화: 잘못된 현재 레벨"); break;
            }
            _transitionManager.StartMiniGame("MCardGame");
        }
        else
            Debug.LogWarning("[StationManager] StartMiniGame 호출 실패: TransitionManager가 없습니다.");
        UpgradeIdx = 2;
    }

    private void OnUpgradeMoveSpeed()
    {
        PlayerStatus status = FetchCurrentPlayerStatus();
        
        int neededScrap = 0;
        
        switch (status.CurrentSpeedLevel)
        {
            case 1: neededScrap = 20; break;
            case 2: neededScrap = 40; break;
            default: Debug.LogWarning("[StationManager] 이동속도 강화: 잘못된 현재 레벨"); break;
        }
        
        Debug.Log("▶ 이동속도 강화 선택");
        if (_transitionManager != null && GameManager.Instance.GetScrapAmount >= neededScrap)
        {
            switch (status.CurrentSpeedLevel)
            {
                case 1: GameManager.Instance.DecreaseScrap(20); break;
                case 2: GameManager.Instance.DecreaseScrap(40); break;
                default: Debug.LogWarning("[StationManager] 이동속도 강화: 잘못된 현재 레벨"); break;
            }
            _transitionManager.StartMiniGame("MCardGame");
        }
        else
            Debug.LogWarning("[StationManager] StartMiniGame 호출 실패: TransitionManager가 없습니다.");
        UpgradeIdx = 3;
    }

    /// <summary>
    /// 미니게임 종료 후 성공/실패 결과 페이지 출력
    /// TransitionManager에서 호출
    /// </summary>
    public void ShowEndingPage(bool isSuccess)
    {
        if (isSuccess)
        {
            if (_successImage != null)
                _successImage.gameObject.SetActive(true);

            // Station 입장 직전 스냅샷이 준비되지 않았다면 더 이상 진행하지 않음
            if (_enterStationStatus == null)
            {
                Debug.LogWarning("[StationManager] 입장 시 PlayerStatus 스냅샷이 없습니다.");
                return;
            }

            // 실제로 플레이어의 스탯을 강화하도록 Player API 호출
            Player player;
             #if UNITY_2023_2_OR_NEWER
             player = UnityEngine.Object.FindFirstObjectByType<Player>();
             #else
             player = FindObjectOfType<Player>();
             #endif
             if (player == null)
             {
                 Debug.LogWarning("[StationManager] Player를 찾을 수 없어 업그레이드를 적용하지 못했습니다.");
             }
             else
             {
                 if (UpgradeIdx == 1)
                 {
                     player.ApplyHealthUpgrade();
                 }
                 else if (UpgradeIdx == 2)
                 {
                     player.ApplyWeaponUpgrade();
                 }
                 else if (UpgradeIdx == 3)
                 {
                     player.ApplySpeedUpgrade();
                 }

                // 업그레이드 적용 직후 최신 상태를 받아 UI에 반영
                var newStatus = player.GetStatus();
                if (newStatus != null)
                {
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
                            _amount.text = $"+{(newStatus.MoveSpeed - _enterStationStatus.MoveSpeed):F2}";
                    }
                }
             }
        }
        else
        {
            if (_failureImage != null)
                _failureImage.gameObject.SetActive(true);
        }
    }
    
    private void OnClickSuccessImage()
    {
        if (_transitionManager != null)
            _transitionManager.ExitRepairStation();
        else
            Debug.LogWarning("[StationManager] ExitRepairStation 호출 실패: TransitionManager가 없습니다.");
    }
    
    private void OnClickFailureImage()
    {
        if (_transitionManager != null)
            _transitionManager.ExitRepairStation();
        else
            Debug.LogWarning("[StationManager] ExitRepairStation 호출 실패: TransitionManager가 없습니다.");
    }

    // 플레이어 또는 GameManager에서 최신 PlayerStatus를 안전하게 가져옵니다.
    // null 허용; 호출자는 null 체크 필요합니다.
    private PlayerStatus FetchCurrentPlayerStatus()
    {
        // 우선 GameManager에 캐시된 최신 스냅샷이 있으면 사용
        if (_gameManager != null)
        {
            var latest = _gameManager.GetLatestStatus();
            if (latest != null)
                return latest;
        }

        // GameManager가 없거나 최신 상태가 비어있으면 Player 인스턴스에서 직접 조회
        Player player;
#if UNITY_2023_2_OR_NEWER
        player = UnityEngine.Object.FindFirstObjectByType<Player>();
#else
        player = FindObjectOfType<Player>();
#endif
        if (player != null)
        {
            try
            {
                return player.GetStatus();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StationManager] 플레이어 상태 조회 중 예외: {ex.Message}");
                return null;
            }
        }

        Debug.LogWarning("[StationManager] FetchCurrentPlayerStatus: Player 또는 GameManager에서 상태를 찾을 수 없습니다.");
        return null;
    }

    // PlayerStatus를 받아 옵션 텍스트(현재 수치)를 채웁니다. 비용 계산은 별도 로직으로 분리할 계획입니다.
    private void PopulateOptionTextsFromStatus(PlayerStatus status)
    {
        if (status == null) return;

        if (_curHealth != null)
            _curHealth.text = $"Lv. {status.CurrentHealthLevel}";

        if (_curWeapon != null)
            _curWeapon.text = $"Lv. {status.CurrentWeaponLevel}";

        if (_curMoveSpeed != null)
            _curMoveSpeed.text = $"Lv. {status.CurrentSpeedLevel}";

        // 스크랩 비용 텍스트는 아직 계산 로직이 정해지지 않았으므로 빈값 또는 대시 표기
        if (_scrapForHealth != null)
        {
            switch (status.CurrentHealthLevel)
            {
                case 1: _scrapForHealth.text = "20"; break;
                case 2: _scrapForHealth.text = "30"; break;
                case 3: _scrapForHealth.text = "50"; break;
                default: _scrapForHealth.text = "-"; break;
            }
        }

        if (_scrapForWeapon != null)
        {
            switch (status.CurrentWeaponLevel)
            {
                case 1: _scrapForWeapon.text = "10"; break;
                case 2: _scrapForWeapon.text = "30"; break;
                case 3: _scrapForWeapon.text = "50"; break;
                default: _scrapForWeapon.text = "-"; break;
            }
        }
        if (_scrapForMoveSpeed != null) 
        {
            switch (status.CurrentSpeedLevel)
            {
                case 1: _scrapForMoveSpeed.text = "20"; break;
                case 2: _scrapForMoveSpeed.text = "40"; break;
                default: _scrapForMoveSpeed.text = "-"; break;
            }
        }
    }

    // Station UI가 활성화될 때(예: RepairShop 씬이 활성화될 때) 최신 플레이어 상태로 옵션 텍스트를 갱신합니다.
    private void OnEnable()
    {
        var status = FetchCurrentPlayerStatus();
        if (status != null)
        {
            PopulateOptionTextsFromStatus(status);
            Debug.Log("[StationManager] 옵션 텍스트를 최신 플레이어 상태로 갱신했습니다.");
        }
        else
        {
            Debug.Log("[StationManager] OnEnable: PlayerStatus를 가져오지 못했습니다.");
        }

        if (_repairSource != null)
        {
            Debug.Log($"[StationManager] Repair 진입 출처(컴포넌트): {_repairSource.gameObject.name}");
        }
        else
        {
            Debug.Log("[StationManager] Repair 진입 출처가 설정되지 않음");
        }
    }

    // TransitionManager가 어떤 Repair 컴포넌트에서 진입했는지 전달할 때 호출
    public void SetRepairSource(Repair source)
    {
        _repairSource = source;
        Debug.Log($"[StationManager] Repair 진입 출처 설정: {_repairSource?.gameObject.name ?? "(null)"}");
    }
}