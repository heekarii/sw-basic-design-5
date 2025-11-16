using UnityEngine;
using TMPro;

public class MJumpGameManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text timerText; // ← 타이머 UI 연결용

    [Header("Rule")]
    public float gameTime = 30f;

    float timeLeft;
    bool playing = false;

    void Start() => StartGame();

    void Update()
    {
        if (!playing) return;

        timeLeft -= Time.deltaTime;
        UpdateTimerUI();

        if (timeLeft <= 0f)
            EndGame(true);
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

        GameManager.Instance.ApplyHealthMiniGame(isSuccess);
        Debug.Log(isSuccess ? "게임 성공!" : "게임 실패!");
        TransitionManager.Instance.EndMiniGame("JumpMGame");
        TransitionManager.Instance.CurStationManager.ShowEndingPage(isSuccess);
    }
    

    public bool IsPlaying => playing;
    public float TimeLeft => timeLeft;
}