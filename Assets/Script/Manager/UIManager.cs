using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인게임 HUD UI 전담 매니저.
/// GameManager에서 상태(PlayerStatus, Scrap 등)를 받아서 UI만 갱신.
/// </summary>
public class UIManager : MonoBehaviour
{
    private GameManager _gameManager;

    [Header("UI - Battery")]
    [SerializeField] private Image _batteryFillbar;
    [SerializeField] private TextMeshProUGUI _batteryText;

    [Header("UI - Health")]
    [SerializeField] private TextMeshProUGUI _healthLevel;
    [SerializeField] private TextMeshProUGUI _maxHPText;
    [SerializeField] private TextMeshProUGUI _curHealthText;

    [Header("UI - Attack")]
    [SerializeField] private TextMeshProUGUI _attackLevel;
    [SerializeField] private TextMeshProUGUI _curAttackText;
    [SerializeField] private TextMeshProUGUI _curBulletText;
    [SerializeField] private Image _meleeImage;

    [Header("UI - Move")]
    [SerializeField] private TextMeshProUGUI _moveLevel;
    [SerializeField] private TextMeshProUGUI _curSpeedText;
    [SerializeField] private TextMeshProUGUI _curBoostText;

    [Header("UI - Resource")]
    [SerializeField] private TextMeshProUGUI _curScrapText;

    [SerializeField] private Image _keyImage;

    private void Awake()
    {
        _gameManager = GameManager.Instance;
        _gameManager.RegisterUIManager(this);
    }

    private void Start()
    {
        InitUIVisibility();
    }

    private void InitUIVisibility()
    {
        if (_meleeImage != null)
            _meleeImage.gameObject.SetActive(false);

        if (_curBulletText != null)
            _curBulletText.gameObject.SetActive(true);
        if (_keyImage != null)
            _keyImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// GameManager가 매 프레임 또는 필요할 때 호출해서
    /// 최신 PlayerStatus + Scrap 정보로 UI를 갱신.
    /// </summary>
    public void Refresh(PlayerStatus status, int curScrap)
    {
        if (status == null) return;

        UpdateBatteryUI(status.BatteryRemaining);
        UpdateHealthUI(status);
        UpdateAttackUI(status);
        UpdateMoveUI(status);
        UpdateResourceUI(curScrap);
    }

    public void GetKey()
    {
        _keyImage.gameObject.SetActive(true);
    }

    private void UpdateBatteryUI(float battery)
    {
        if (_batteryFillbar != null)
            _batteryFillbar.fillAmount = Mathf.Clamp01(battery / 100f);

        if (_batteryText != null)
            _batteryText.text = $"{battery:F2}%";
    }

    private void UpdateHealthUI(PlayerStatus s)
    {
        _healthLevel?.SetText(s.CurrentHealthLevel.ToString());
        _maxHPText?.SetText(s.MaxHealth.ToString("F0"));
        _curHealthText?.SetText(s.CurrentHealth.ToString("F0"));
    }

    private void UpdateAttackUI(PlayerStatus s)
    {
        _attackLevel?.SetText(s.CurrentWeaponLevel.ToString());
        _curAttackText?.SetText(s.AttackPower.ToString("F0"));

        bool isMelee = s.CurrentWeaponLevel <= 4;

        if (isMelee)
        {
            if (_curBulletText != null)
                _curBulletText.gameObject.SetActive(false);

            if (_meleeImage != null)
                _meleeImage.gameObject.SetActive(true);
        }
        else
        {
            if (_meleeImage != null)
                _meleeImage.gameObject.SetActive(false);

            if (_curBulletText != null)
            {
                _curBulletText.gameObject.SetActive(true);
                _curBulletText.text = s.BulletCount.ToString();
            }
        }
    }

    private void UpdateMoveUI(PlayerStatus s)
    {
        _moveLevel?.SetText(s.CurrentSpeedLevel.ToString());
        _curSpeedText?.SetText(s.MoveSpeed.ToString("F2"));
        _curBoostText?.SetText(s.SpeedWithBoost.ToString("F2"));
    }

    private void UpdateResourceUI(int scrap)
    {
        _curScrapText?.SetText(scrap.ToString());
    }
}
