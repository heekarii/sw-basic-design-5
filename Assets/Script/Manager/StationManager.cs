using UnityEngine;
using UnityEngine.UI;

public class StationManager : MonoBehaviour
{
    private TransitionManager _transitionManager;
    [Header("Station Manager images")] 
    [SerializeField] private Image _bgoff;
    [SerializeField] private Image _bgon;
    [SerializeField] private Image _selectRunImage;
    [SerializeField] private Image _selectUpgradeImage;
    
    [Header("Repair Main Station Buttons")] 
    [SerializeField] private Image _runStation;
    [SerializeField] private Image _exitStation;
    [SerializeField] private Image _upgradeHealth;
    [SerializeField] private Image _upgradeWeapon;
    [SerializeField] private Image _upgradeMove;

    private void Start()
    {
        _transitionManager = FindObjectOfType<TransitionManager>();
        // 이미지에 클릭 스크립트 붙이기
        AddClick(_runStation, OnRunStationClick);
        AddClick(_exitStation, OnExitStationClick);
        AddClick(_upgradeHealth, OnUpgradeHealth);
        AddClick(_upgradeWeapon, OnUpgradeWeapon);
        AddClick(_upgradeMove, OnUpgradeMoveSpeed);
    }

    private void AddClick(Image img, System.Action callback)
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
    }

    private void OnUpgradeWeapon()
    {
        Debug.Log("▶ 무기 강화 선택");
        _transitionManager.StartMiniGame("MCardGame");
    }

    private void OnUpgradeMoveSpeed()
    {
        Debug.Log("▶ 이동속도 강화 선택");
        _transitionManager.StartMiniGame("MMazeGame");
    }
}