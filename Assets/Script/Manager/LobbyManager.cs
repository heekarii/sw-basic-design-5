using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("UI Elements")] 
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private Button _tutorialButton;
    [SerializeField] private Button _tutorialExitButton;
    [SerializeField] private Button _rankingButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Image _selectPanel;
    
    [SerializeField] private Image _tutorialImage;

    [Header("Game Settings")] 
    [SerializeField] private int _weaponType = 0;

    [SerializeField] private Button _meleeButton;
    [SerializeField] private Button _rangedButton;
    
    private TransitionManager _transitionManager;
    
    private void Start()
    {
        _startButton.onClick.AddListener(OnStartButtonClicked);
        _tutorialButton.onClick.AddListener(OnTutorialButtonClicked);
        _rankingButton.onClick.AddListener(OnRankingButtonClicked);
        _settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        
        _meleeButton.onClick.AddListener(OnClickMeleeButton);
        _rangedButton.onClick.AddListener(OnClickRangedButton);

        _transitionManager = TransitionManager.Instance;
        
        _playerNameText.text = "Player1"; // Example player name
    }
    
    private void OnStartButtonClicked()
    {
        _selectPanel.gameObject.SetActive(true);
    }

    private void OnClickMeleeButton()
    {
        _weaponType = 0;
        Debug.Log(_weaponType);
        _transitionManager.StartGame(_weaponType);
    }

    private void OnClickRangedButton()
    {
        _weaponType = 1;
        Debug.Log(_weaponType);
        _transitionManager.StartGame(_weaponType);
    }
    
    private void OnTutorialButtonClicked()
    {
        Debug.Log("Tutorial Button Clicked - Load Tutorial Scene");
        _tutorialImage.gameObject.SetActive(true);
        _tutorialExitButton.onClick.AddListener(() =>
            {
                _tutorialImage.gameObject.SetActive(false);
            }
        );

    }
    private void OnRankingButtonClicked()
    {
        Debug.Log("Ranking Button Clicked - Show Rankings");
        // Show rankings UI or load rankings scene
        // SceneManager.LoadScene("RankingScene");
    }

    private void OnSettingsButtonClicked()
    {
        
    }
    
}
