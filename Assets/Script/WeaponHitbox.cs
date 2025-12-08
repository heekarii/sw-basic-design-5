using UnityEngine;
using System.Collections.Generic;

public class WeaponHitbox : MonoBehaviour
{
    private float _damage;
    [SerializeField]private bool _active = false;
    private HashSet<IEnemy> _hitEnemies = new HashSet<IEnemy>();

    // [SerializeField]private ParticleSystem _hitEffect;
    [Header("Hit Effect")]
    [SerializeField] private ParticleSystem _hitEffectPrefab; // 프리팹 넣는 공간 (Inspector에 넣음)
    
    // private void Awake()
    // {
    //     // 자식 중에서 ParticleSystem 자동 검색
    //     _hitEffect = GetComponentInChildren<ParticleSystem>(true);
    //
    //     if (_hitEffect == null)
    //         Debug.LogWarning("[WeaponHitbox] No ParticleSystem found in children!");
    // }
    
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

        // 파티클이 끝나면 자동 삭제
        Destroy(effect.gameObject, effect.main.duration);
    }

    
    // private void PlayHitEffect(Collider target)
    // {
    //     if (_hitEffect == null) return;
    //
    //     // 이펙트를 적 위치에서 재생
    //     _hitEffect.transform.position = target.ClosestPoint(transform.position);
    //
    //     _hitEffect.Play();   // 파티클 재생
    // }
}