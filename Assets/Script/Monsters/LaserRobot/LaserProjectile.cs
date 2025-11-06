using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 2.5f;   // 6124
    [SerializeField] private float _lifeTime = 5f;
    [SerializeField] private float _damage = 10f;   // 6123

    private Vector3 _dir;        // 발사 방향 (정규화)
    private Player _target;      // ← 태그 대신 '플레이어 참조'를 직접 갖고 다님
    private bool _hit;

    // LaserRobot에서 발사 직후 호출: 방향 + 목표 전달
    public void Init(Vector3 dir, Player target)
    {
        _dir = dir.normalized;
        _target = target;
        Destroy(gameObject, _lifeTime); // 수명 종료
    }

    void Update()
    {
        if (_hit) return;
        transform.position += _dir * _speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hit) return;
        
        if (_target != null && other.gameObject == _target.gameObject)
        {
            _target.TakeDamage(_damage);
            _hit = true;
            Destroy(gameObject);
            return;
        }

        // 장애물/지형에 부딪혀도 제거하고 싶으면:
        if (!other.isTrigger)
        {
            _hit = true;
            Destroy(gameObject);
        }
    }
}