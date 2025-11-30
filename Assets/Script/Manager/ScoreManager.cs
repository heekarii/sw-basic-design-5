using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Unity.VisualScripting;

public class ScoreManager : MonoBehaviour
{
    [SerializeField] private Image _batteryFillbar;
    [SerializeField] private TextMeshProUGUI _batteryText;

    [SerializeField] private TextMeshProUGUI _durabilityLevel;
    [SerializeField] private TextMeshProUGUI _weaponLevel;
    [SerializeField] private TextMeshProUGUI _boostLevel;
    
    private GameManager _gameManager;
    private PlayerStatus _playerStatus;

    void Awake()
    {
    }

    void Start()
    {
        _gameManager = GameManager.Instance;
#if UNITY_2023_2_OR_NEWER
        Player player = UnityEngine.Object.FindFirstObjectByType<Player>();
#else
        Player player = FindObjectOfType<Player>();
#endif
        player.StopBatteryReduction();
        _playerStatus = player.GetStatus();
        
        _batteryFillbar.fillAmount = 0f;

        _durabilityLevel.text = _playerStatus.CurrentHealthLevel.ToString();
        _weaponLevel.text = _playerStatus.CurrentWeaponLevel.ToString();
        _boostLevel.text = _playerStatus.CurrentSpeedLevel.ToString();
        StartCoroutine(UpdateBatteryUI());
        
    }
    
    private IEnumerator UpdateBatteryUI()
    {
        // UI가 초기화되도록 1프레임 기다린다 (Awake에서 바로 돌리면 문제 생김)
        yield return null;

        float targetPercent = _playerStatus.BatteryRemaining; // 0~100
        float target = targetPercent / 100f;                  // 0~1 정규화

        float current = 0f;

        // animation duration
        float duration = 2.4f;   // 1.2초 동안 부드럽게 올라가는 식
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            current = Mathf.Lerp(0, target, time / duration);

            _batteryFillbar.fillAmount = current;
            _batteryText.text = $"{current * 100:F2}%";

            yield return null;
        }

        // 마지막 값 보정
        _batteryFillbar.fillAmount = target;
        _batteryText.text = $"{targetPercent:F2}%";
    }
    
    
}
