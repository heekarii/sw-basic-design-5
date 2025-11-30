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

    [SerializeField] private Image _tutorialImage;

    [Header("Game Settings")] 
    [SerializeField] private int _weaponType = 0;

    private TransitionManager _transitionManager;
    
    private void Start()
    {
        _startButton.onClick.AddListener(OnStartButtonClicked);
        _tutorialButton.onClick.AddListener(OnTutorialButtonClicked);
        _rankingButton.onClick.AddListener(OnRankingButtonClicked);
        _settingsButton.onClick.AddListener(OnSettingsButtonClicked);

        _transitionManager = TransitionManager.Instance;
        
        _playerNameText.text = "Player1"; // Example player name
    }
    
    private void OnStartButtonClicked()
    {
        Debug.Log("Start Button Clicked - Load Game Scene");
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
