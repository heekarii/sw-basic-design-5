using UnityEngine;

public class JumpGameManager : MonoBehaviour
{
    public static JumpGameManager Instance { get; private set; }

    [Header("Rule")]
    public float gameTime = 30f;   // 제한 시간(초)
    public int successCount = 0;   // 통과 수(옵션)
    public int weight = 2;         // 회복 가중치

    float timeLeft;
    bool playing = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() => StartGame();

    void Update()
    {
        if (!playing) return;

        timeLeft -= Time.deltaTime;

        // ⏱️ 타이머 끝 → "성공"으로 종료
        if (timeLeft <= 0f)
            EndGame(isSuccess: true);
    }

    public void StartGame()
    {
        successCount = 0;
        timeLeft = gameTime;
        playing = true;
        Time.timeScale = 1f; // 혹시 멈춰 있었다면 재개
        Debug.Log("게임 시작!");
    }

    // 🚫 플레이어가 장애물에 부딪힘 → "실패"로 종료
    public void OnPlayerHitObstacle()
    {
        EndGame(isSuccess: false);
    }

    // ✅ 장애물 통과 카운트(원하면 유지)
    public void OnObstaclePassed()
    {
        successCount++;
        // Debug.Log($"성공 +1 (합계 {successCount})");
    }

    public int CalculatorRecovery(int success) => success * weight;

    // 🔚 종료 처리 (성공/실패 구분)
    public void EndGame(bool isSuccess)
    {
        if (!playing) return;   // 중복 종료 방지
        playing = false;

        if (isSuccess)
        {
            int recovery = CalculatorRecovery(successCount);
            Debug.Log($"[성공 종료] 성공:{successCount}, 회복량:{recovery}");
            SendPlayer_HP(recovery);      // ← 성공일 때만 회복 전달
        }
        else
        {
            Debug.Log($"FAIL");
        }

        Time.timeScale = 0f;              // 게임 멈춤
    }

    // 메인 게임으로 회복 전달 훅
    public void SendPlayer_HP(int recovery)
    {
        Debug.Log($"플레이어 HP에 +{recovery} 전달");
        // TODO: 실제 Player 참조해서 HP += recovery;
    }

    // 필요하면 외부에서 상태 확인용
    public bool IsPlaying => playing;
    public float TimeLeft => timeLeft;
}
