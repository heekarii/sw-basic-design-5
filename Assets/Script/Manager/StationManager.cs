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
    [SerializeField] private Image _runStation;
    [SerializeField] private Image _exitStation;
    [SerializeField] private Image _upgradeHealth;
    [SerializeField] private Image _upgradeWeapon;
    [SerializeField] private Image _upgradeMove;
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
        _transitionManager = FindObjectOfType<TransitionManager>();
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

        _transitionManager.RegisterStationManager(this);
        
        // 이미지에 클릭 스크립트 붙이기
        AddClick(_runStation, OnRunStationClick);
        AddClick(_exitStation, OnExitStationClick);
        AddClick(_upgradeHealth, OnUpgradeHealth);
        AddClick(_upgradeWeapon, OnUpgradeWeapon);
        AddClick(_upgradeMove, OnUpgradeMoveSpeed);
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
        _transitionManager.ExitRepairStation();
    }

    private void OnUpgradeHealth()
    {
        Debug.Log("▶ 체력 강화 선택");
        _transitionManager.StartMiniGame("JumpMGame");
        if (_selectUpgradeImage != null) _selectUpgradeImage.gameObject.SetActive(false);
        UpgradeIdx = 1;
    }

    private void OnUpgradeWeapon()
    {
        Debug.Log("▶ 무기 강화 선택");
        _transitionManager.StartMiniGame("MCardGame");
        UpgradeIdx = 2;
    }

    private void OnUpgradeMoveSpeed()
    {
        Debug.Log("▶ 이동속도 강화 선택");
        _transitionManager.StartMiniGame("MMazeGame");
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

            if (UpgradeIdx == 1)
            {
                // 체력 강화: 레벨 +1 / 증가량 안내
                if (_level != null)
                    _level.text = $"{_enterStationStatus.CurrentHealthLevel + 1}";

                if (_amount != null)
                    _amount.text = (_enterStationStatus.CurrentHealthLevel == 1) ? "+200" : "+300";
            }
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
        _transitionManager.ExitRepairStation();
    }
    
    private void OnClickFailureImage()
    {
        _transitionManager.ExitRepairStation();
    }
}