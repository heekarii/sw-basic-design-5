using UnityEngine;
using System.Collections.Generic;

public class WeaponHitbox : MonoBehaviour
{
    private float _damage;
    [SerializeField]private bool _active = false;
    private HashSet<IEnemy> _hitEnemies = new HashSet<IEnemy>();
    
    [Header("Hit Effect")]
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    
    
    public void Activate(float damage)
    {
        _damage = damage;
        _active = true;
        _hitEnemies.Clear();
    }

    public void Deactivate()
    {
        _active = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_active) return;

        IEnemy enemy = other.GetComponentInParent<IEnemy>();
        if (enemy == null) return;

        if (_hitEnemies.Contains(enemy)) return;
        
        PlayHitEffect(other);
        _hitEnemies.Add(enemy);
        enemy.TakeDamage(_damage);
        Debug.Log($"[Hitbox] {other.name} hit for {_damage}");
    }
    private void PlayHitEffect(Collider target)
    {
        if (_hitEffectPrefab == null) return;

        // 몬스터와 무기 사이의 실제 충돌 지점 계산
        Vector3 hitPos = target.ClosestPoint(transform.position);

        // 해당 위치에 이펙트 생성
        ParticleSystem effect = Instantiate(
            _hitEffectPrefab,
            hitPos, 
            Quaternion.identity
        );

        effect.Play();
        
        Destroy(effect.gameObject, effect.main.duration);
    }
}