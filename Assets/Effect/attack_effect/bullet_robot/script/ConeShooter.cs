using UnityEngine;

public class ConeShooter : MonoBehaviour
{
    [Header("References")]
    public Transform muzzle;     // 총구 Transform
    public Bullet bulletPrefab;  // 방금 만든 bolt 프리팹
    public ParticleSystem muzzleFlash; // 선택 (머즐 플래시 이펙트)

    [Header("Settings")]
    [Range(0f, 179f)] public float arcDeg = 40f; // 퍼지는 각도
    public int bulletCount = 30;  // 한 번에 발사할 탄 수
    public float speed = 0f;    // 탄 속도

    void Start()
    {
        Debug.Log("[ConeShooter] Alive on " + gameObject.name);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("[ConeShooter] Fire() called");
            Fire();
        }
    }

    public void Fire()
    {
        if (muzzleFlash) muzzleFlash.Play();

        Quaternion baseRot = Quaternion.LookRotation(muzzle.forward, muzzle.up);

        for (int i = 0; i < bulletCount; i++)
        {
            float yaw = Random.Range(-arcDeg, arcDeg);
            float pitch = Random.Range(-arcDeg, arcDeg);

            Quaternion rot = baseRot
                           * Quaternion.AngleAxis(yaw, Vector3.up)
                           * Quaternion.AngleAxis(pitch, Vector3.right);

            Vector3 spawnPos = muzzle.position + muzzle.forward * 0.2f; // ★ 총구 앞에서 생성
            Bullet b = Instantiate(bulletPrefab, spawnPos, rot);
            Vector3 dir = rot * Vector3.forward;
            b.Launch(dir * speed);
        }
    }
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 25), "ConeShooter: Alive");
    }

}

