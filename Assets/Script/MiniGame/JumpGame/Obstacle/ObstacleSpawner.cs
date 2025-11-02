using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    // 🔹 생성할 장애물 프리팹 (Inspector에서 연결)
    public GameObject obstaclePrefab;

    [Header("스폰 간격 설정")]
    public float minInterval = 1.0f;  // 최소 생성 간격 (초)
    public float maxInterval = 2.0f;  // 최대 생성 간격 (초)

    [Header("스폰 위치 설정")]
    public float spawnX = 15f;         // 생성되는 X좌표 (오른쪽 끝)
    public float spawnY = -2f;        // 생성되는 Y좌표 (땅 높이와 맞추기)

    [Header("장애물 이동 속도")]
    public float moveSpeed = 6f;      // 장애물 왼쪽 이동 속도

    // 다음 장애물 생성까지 남은 시간
    float timer;

    // 🔸 시작할 때 스폰 타이머 예약
    void Start() => Schedule();

    // 🔸 매 프레임마다 타이머 감소 → 0이 되면 새 장애물 생성
    void Update()
    {
        timer -= Time.deltaTime; // 매 프레임마다 1초당 1씩 감소
        if (timer <= 0f)
        {
            Spawn();   // 장애물 생성
            Schedule(); // 다음 생성 타이머 재설정
        }
    }

    // 🔸 장애물 실제 생성 함수
    void Spawn()
    {
        // 새로운 장애물 프리팹을 (spawnX, spawnY)에 생성
        var go = Instantiate(obstaclePrefab, new Vector3(spawnX, spawnY, 0), Quaternion.identity);

        // 이동 기능을 담당할 ObstacleMover 스크립트를 추가
        var mover = go.AddComponent<ObstacleMover>();

        // 속도 설정 (왼쪽으로 이동)
        mover.speed = moveSpeed;
    }

    // 🔸 다음 스폰까지의 대기 시간 랜덤 설정
    void Schedule() => timer = Random.Range(minInterval, maxInterval);
}