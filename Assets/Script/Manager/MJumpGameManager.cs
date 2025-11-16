using UnityEngine;
using TMPro;

public class MJumpGameManager : MonoBehaviour
{
    public static MJumpGameManager Instance { get; private set; }

    [Header("UI")]
    public TMP_Text timerText; // ← 타이머 UI 연결용

    [Header("Rule")]
    public float gameTime = 30f;
    [SerializeField] private AudioSource _successAudio;
    [SerializeField] private AudioSource _BGAudio;

    float timeLeft;
    public bool playing = false;
    
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _successAudio.Stop();
        _BGAudio.Play();
    }

    void Start() => StartGame();

    void Update()
    {
        if (!playing) return;

        timeLeft -= Time.deltaTime;
        UpdateTimerUI();

        if (timeLeft <= 0f)
        {
            EndGame(true);
        }
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = $"Time: {timeLeft:F1}";
    }

    public void StartGame()
    {
        timeLeft = gameTime;
        playing = true;
        Time.timeScale = 1f;
        UpdateTimerUI();
        Debug.Log("게임 시작!");
    }

    public void OnPlayerHitObstacle() => EndGame(false);

    public void EndGame(bool isSuccess)
    {
        if (!playing) return;
        playing = false;
        
        _BGAudio.Stop();
        
        if (isSuccess)
        {
            Debug.Log($"SUCCES");
            _successAudio.Play();
            SendPlayer_HP();
        }
        else
        {
            Debug.Log("FAIL");
        }

        Time.timeScale = 0f;
    }

    public void SendPlayer_HP()
    {
        Debug.Log($"true 전달");
    }
}