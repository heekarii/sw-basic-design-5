using UnityEngine;

public class Rat : MonoBehaviour, IEnemy
{
    [Header("Moster Status")]
    [SerializeField] private float _curHp;
    [SerializeField] private float _maxHp = 15f;
    [SerializeField] private Player _player;
    [SerializeField] private float _damage = 30f;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _aggravationRange = 7.5f;
    
    void Start()
    {
        _player = GetComponent<Player>();
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

        float distanceToPlayer = Vector3.Distance(transform.position, _player.transform.position);
        if (distanceToPlayer <= _aggravationRange)
        {
            Vector3 direction = (_player.transform.position - transform.position).normalized;
            transform.position += direction * _moveSpeed * Time.deltaTime;
        }

        if (distanceToPlayer <= 0.7f)
        {
            AttackPlayer();
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
