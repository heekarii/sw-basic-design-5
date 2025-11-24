using UnityEngine;
using System.Collections.Generic;

public class WeaponHitbox : MonoBehaviour
{
    private float _damage;
    private bool _active = false;
    private HashSet<IEnemy> _hitEnemies = new HashSet<IEnemy>();

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

        _hitEnemies.Add(enemy);
        enemy.TakeDamage(_damage);
        Debug.Log($"[Hitbox] {other.name} hit for {_damage}");
    }
}