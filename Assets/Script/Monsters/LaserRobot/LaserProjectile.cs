using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [SerializeField] private float _speed = 25f;
    [SerializeField] private float _lifeTime = 5f;
    [SerializeField] private float _damage = 10f;

    private Vector3 _dir;
    private Player _target;   // LaserRobot에서 넘겨주는 Player
    private bool _hit;
    
    // 🔹 누가 쏜 탄인지(루트 Transform) 저장
    private Transform _ownerRoot;

    public void Init(Vector3 dir, Player target, Transform ownerRoot)
    {
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        _target = target;
        _ownerRoot = ownerRoot;

        // 수명 타이머
        Destroy(gameObject, _lifeTime);
    }

    void Update()
    {
        if (_hit) return;
        transform.position += _dir * _speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hit) return;

        // 🔹 자기 발사자(로봇/총구)와의 충돌은 무시
        if (_ownerRoot != null && other.transform.root == _ownerRoot)
            return;

        // Player 본체든 자식 콜라이더든 다 잡기
        Player hitPlayer = other.GetComponentInParent<Player>();
        if (hitPlayer != null && (_target == null || hitPlayer == _target))
        {
            hitPlayer.TakeDamage(_damage);
            _hit = true;
            Destroy(gameObject);
            return;
        }

        // 장애물/지형과 부딪히면 소멸 (Trigger 제외)
        if (!other.isTrigger)
        {
            _hit = true;
            Destroy(gameObject);
        }
    }
}