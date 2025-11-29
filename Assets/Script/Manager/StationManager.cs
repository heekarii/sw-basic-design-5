using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class StationManager : MonoBehaviour
{
    private TransitionManager _transitionManager;
    private GameManager _gameManager;

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
        
        // 버튼에 클릭 리스너 연결
        if (_runStation != null) _runStation.onClick.AddListener(OnRunStationClick);
        if (_exitStation != null) _exitStation.onClick.AddListener(OnExitStationClick);
        if (_upgradeHealth != null) _upgradeHealth.onClick.AddListener(OnUpgradeHealth);
        if (_upgradeWeapon != null) _upgradeWeapon.onClick.AddListener(OnUpgradeWeapon);
        if (_upgradeMove != null) _upgradeMove.onClick.AddListener(OnUpgradeMoveSpeed);

        // 이미지(성공/실패)는 ClickableImage로 클릭 이벤트 연결
        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);

        InitUIState();
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
        if (_bgoff != null) _bgoff.gameObject.SetActive(false);
        if (_bgon != null) _bgon.gameObject.SetActive(true);
        if (_selectRunImage != null) _selectRunImage.gameObject.SetActive(false);
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(true);
    }

    private void OnExitStationClick()
    {
        Debug.Log("▶ 정비소 종료");
        if (_transitionManager != null)
            _transitionManager.ExitRepairStation();
        else
            Debug.LogWarning("[StationManager] ExitRepairStation 호출 실패: TransitionManager가 없습니다.");
    }

    private void OnUpgradeHealth()
    {
        Debug.Log("▶ 체력 강화 선택");
        if (_transitionManager != null)
            _transitionManager.StartMiniGame("MCardGame");
        else
            Debug.LogWarning("[StationManager] StartMiniGame 호출 실패: TransitionManager가 없습니다.");
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(false);
        UpgradeIdx = 1;
    }

    private void OnUpgradeWeapon()
    {
        Debug.Log("▶ 무기 강화 선택");
        if (_transitionManager != null)
            _transitionManager.StartMiniGame("MCardGame");
        else
            Debug.LogWarning("[StationManager] StartMiniGame 호출 실패: TransitionManager가 없습니다.");
        UpgradeIdx = 2;
    }

    private void OnUpgradeMoveSpeed()
    {
        Debug.Log("▶ 이동속도 강화 선택");
        if (_transitionManager != null)
            _transitionManager.StartMiniGame("MCardGame");
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

            // (구) _enterStationStatus 기반 텍스트 업데이트는 위 newStatus 업데이트로 대체됨
            // if (UpgradeIdx == 1)
            // {
            //     // 체력 강화: 레벨 +1 / 증가량 안내
            //     if (_level != null)
            //         _level.text = $"{_enterStationStatus.CurrentHealthLevel + 1}";
            //
            //     if (_amount != null)
            //         _amount.text = (_enterStationStatus.CurrentHealthLevel == 1) ? "+200" : "+300";
            // }
            // TODO: 나중에 업그레이드 시스템 구현 시 주석 해제 및 Player/PlayerStatus 연동
            // else if (UpgradeIdx == 2)
            // {
            //     _player.UpgradeWeaponLevel();
            //     _level.text = $"{_enterStationStatus.CurrentWeaponLevel}";
            //     _amount.text = $"+{_enterStationStatus.WeaponLevelUpAmount}";
            // }
            // else if (UpgradeIdx == 3)
            // {
            //     _player.UpgradeSpeedLevel();
            //     _level.text = $"{_enterStationStatus.CurrentSpeedLevel}";
            //     _amount.text = $"+{_enterStationStatus.SpeedLevelUpAmount:F2}";
            // }
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
}