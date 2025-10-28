using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 🔹 전역 접근용 (다른 스크립트에서도 쉽게 접근 가능)
    public static GameManager Instance { get; private set; }

    [Header("Rule")]
    public float gameTime = 20f;  // 제한 시간(초)
    public int successCount = 0;  // 장애물 통과 횟수
    public int weight = 2;        // 회복 가중치

    float timeLeft;
    bool playing = false;

    void Awake()
    {
        // 싱글톤 (씬에 여러 개 생기지 않도록)
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartGame(); // 게임 자동 시작
    }

    void Update()
    {
        if (!playing) return;

        // 제한 시간 감소
        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            EndGame();
        }
    }

    // 🔹 게임 시작
    public void StartGame()
    {
        successCount = 0;
        timeLeft = gameTime;
        playing = true;
        Debug.Log("게임 시작!");
    }

    // 🔹 플레이어가 장애물에 부딪힘
    public void OnPlayerHitObstacle()
    {
        Debug.Log("장애물과 충돌 - 게임 종료");
        EndGame();
    }

    // 🔹 장애물 통과 시
    public void OnObstaclePassed()
    {
        successCount++;
        Debug.Log($"성공 +1 (합계 {successCount})");
    }

    // 🔹 회복량 계산
    public int CalculatorRecovery(int success)
    {
        return success * weight;
    }

    // 🔹 게임 종료 처리
    public void EndGame()
    {
        if (!playing) return;
        playing = false;

        int recovery = CalculatorRecovery(successCount);
        Debug.Log($"[종료] 성공:{successCount}, 회복량:{recovery}");
        SendPlayer_HP(recovery);

        // 게임 오브젝트들 멈추게 하기
        Time.timeScale = 0f; // 🔥 물리/이동 멈춤
    }

    // 🔹 플레이어 체력 회복용 (현재는 출력만)
    public void SendPlayer_HP(int recovery)
    {
        Debug.Log($"플레이어 HP에 +{recovery} 전달");
    }
}