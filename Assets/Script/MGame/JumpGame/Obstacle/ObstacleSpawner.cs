using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public GameObject obstaclePrefab;
    public float minInterval = 1.2f;
    public float maxInterval = 2.5f;
    public float spawnX = 9f;
    public float spawnY = -2f;
    public float moveSpeed = 6f;

    float timer;

    void Start() => Schedule();

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Spawn();
            Schedule();
        }
    }

    void Spawn()
    {
        var go = Instantiate(obstaclePrefab, new Vector3(spawnX, spawnY, 0), Quaternion.identity);
        var mover = go.AddComponent<ObstacleMover>();
        mover.speed = moveSpeed;
    }

    void Schedule() => timer = Random.Range(minInterval, maxInterval);
}
