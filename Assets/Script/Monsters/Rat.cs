using UnityEngine;
using UnityEngine.AI;

public class Rat : MonoBehaviour, IEnemy
{
    [SerializeField] private NavMeshAgent _navMeshAgent;
    [Header("Moster Status")]
    [SerializeField] private float _curHp;
    [SerializeField] private float _maxHp = 15f;
    [SerializeField] private Player _player;
    [SerializeField] private float _damage = 30f;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _aggravationRange = 7.5f;
    
    void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _player = FindObjectOfType<Player>();
        _curHp = _maxHp;
    }
    
    void Update()
    {
        MoveTowardsPlayer();
        if (_curHp <= 0)
        {
            Die();
        }
    }
    
    private void MoveTowardsPlayer()
    {
        if (_player == null) return;

        // --- 콜라이더 기반 거리 계산 ---
        Collider ratCol = GetComponent<Collider>();
        Collider playerCol = _player.GetComponent<Collider>();

        float distanceToPlayer;
        if (ratCol != null && playerCol != null)
        {
            Vector3 ratPoint = ratCol.ClosestPoint(playerCol.bounds.center);
            Vector3 playerPoint = playerCol.ClosestPoint(ratCol.bounds.center);
            distanceToPlayer = Vector3.Distance(ratPoint, playerPoint);
        }
        else
        {
            // 콜라이더가 없으면 기존 방식 fallback
            Vector3 ratPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 playerPos = new Vector3(_player.transform.position.x, 0, _player.transform.position.z);
            distanceToPlayer = Vector3.Distance(ratPos, playerPos);
        }
        // --------------------------------

        if (distanceToPlayer <= 0.9f)
        {
            AttackPlayer();
            return;
        }

        if (distanceToPlayer <= _aggravationRange)
        {
            Vector3 direction = (_player.transform.position - transform.position);
            direction.y = 0;
            direction.Normalize();
            // ✅ 회전: 플레이어 방향으로 부드럽게 Y축만 회전
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,       // 현재 회전
                    targetRot,                // 목표 회전
                    Time.deltaTime * 10f      // 회전 속도 (값 높일수록 더 빠름)
                );
            }
            transform.position += direction * _moveSpeed * Time.deltaTime;
        }
    }

    
    private void AttackPlayer()
    {
        if (_player != null)
        {
            _player.TakeDamage(_damage);
            Debug.Log($"Rat attacked player for {_damage} damage.");
            Destroy(gameObject);
        }
    }
    
    private void Die()
    {
        Destroy(gameObject);
        Debug.Log("Rat has died.");
    }
    public void TakeDamage(float damage)
    {
        _curHp -= damage;
        Debug.Log($"Rat took {damage} damage, current HP: {_curHp}");
    }
}

