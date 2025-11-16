using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class StationManager : MonoBehaviour
{
    private TransitionManager _transitionManager;
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
    
    private PlayerStatus _playerStatus;

    private void Awake()
    {
        _transitionManager = FindObjectOfType<TransitionManager>();

        _playerStatus = GameManager.Instance.SendStatus;
        _transitionManager.RegisterStationManager(this);
        
        // 이미지에 클릭 스크립트 붙이기
        AddClick(_runStation, OnRunStationClick);
        AddClick(_exitStation, OnExitStationClick);
        AddClick(_upgradeHealth, OnUpgradeHealth);
        AddClick(_upgradeWeapon, OnUpgradeWeapon);
        AddClick(_upgradeMove, OnUpgradeMoveSpeed);
        AddClick(_successImage, OnClickSuccessImage);
        AddClick(_failureImage, OnClickFailureImage);
    }

    private void AddClick(Image img, Action callback)
    {
        var clickable = img.gameObject.AddComponent<ClickableImage>();
        clickable.onClick = callback;
    }

    private void OnRunStationClick()
    {
        Debug.Log("▶ 정비 기능 실행");
        _bgoff.gameObject.SetActive(false);
        _bgon.gameObject.SetActive(true);
        _selectRunImage.gameObject.SetActive(false);
        _selectUpgradeImage.gameObject.SetActive(true);

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
        _selectUpgradeImage.gameObject.SetActive(false);
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

    public void ShowEndingPage(bool isSuccess)
    {
        if (isSuccess)
        {
            _successImage.gameObject.SetActive(true);
            if (UpgradeIdx == 1)
            {
                _level.text = $"{_playerStatus.CurrentHealthLevel + 1}";
                _amount.text = (_playerStatus.CurrentHealthLevel == 1) ? "+200" : "+300";
            }
            // TODO: 나중에 업그레이드 시스템 구현 시 주석 해제
            // else if (UpgradeIdx == 2)
            // {
            //     _playerStatus.UpgradeWeaponLevel();
            //     _level.text = $"{_playerStatus.CurrentWeaponLevel}";
            //     _amount.text = $"+{_playerStatus.WeaponLevelUpAmount}";
            // }
            // else if (UpgradeIdx == 3)
            // {
            //     _playerStatus.UpgradeSpeedLevel();
            //     _level.text = $"{_playerStatus.CurrentSpeedLevel}";
            //     _amount.text = $"+{_playerStatus.SpeedLevelUpAmount:F2}";
            // }
            
        }
        else
        {
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