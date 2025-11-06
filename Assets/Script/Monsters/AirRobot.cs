using UnityEngine;
using UnityEngine.Serialization;

public class AirRobot : MonoBehaviour, IEnemy
{
    [FormerlySerializedAs("detectDistance")]
    [Header("Settings")]
    [SerializeField] private float _detectDistance = 7.5f;
    [SerializeField] private float _attackRange = 5f;
    [SerializeField] private int _maxHealth = 40;
    [FormerlySerializedAs("windEffectPrefab")] [SerializeField] private GameObject _windEffectPrefab;
    [SerializeField] private Transform windOrigin;

    private Transform _zeron;
    private bool _isActive = false;
    private float _currentHealth;
    
    private void Start()
    {
        _zeron = GameObject.FindWithTag("Player").transform;
        _currentHealth = _maxHealth;

        if (windOrigin == null)
        {
            Transform found = transform.Find("WindOrigin");
            if (found != null)
            {
                windOrigin = found;
                Debug.Log("[AirRobot] WindOrigin 자동 할당 완료");
            }
            else
            {
                Debug.LogWarning("[AirRobot] WindOrigin 오브젝트를 찾지 못했습니다. 기본 위치로 대체합니다.");
                // 자동 생성 (없을 경우)
                GameObject originObj = new GameObject("Gman5_0Thruster");
                originObj.transform.SetParent(transform);
                originObj.transform.localPosition = new Vector3(0, 1.0f, 1.0f); // 로봇 앞쪽으로 배치
                windOrigin = originObj.transform;
            }
        }
        // 감속 범위용 SphereCollider 자동 설정
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = _attackRange;
    }

    private void Update()
    {
        if (!_isActive)
        {
            float distance = Vector3.Distance(transform.position, _zeron.position);
            if (distance <= _detectDistance)
            {
                _isActive = true;
                Debug.Log("[AirRobot] 활성화됨");
            }
            else
            {
                if (_isActive)
                    Debug.Log("[AirRobot] 비활성화");
                _isActive = false;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_isActive) return;

        if (other.CompareTag("Player"))
        {
            // 플레이어 이동속도 20% 감소 유지
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                player.ApplyWindSlow(true);
            }

            // 바람 이펙트 유지용 (선택)
            if (_windEffectPrefab && windOrigin && !IsInvoking(nameof(PlayWindEffect)))
                InvokeRepeating(nameof(PlayWindEffect), 0f, 1.0f);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player player = other.GetComponent<Player>();
            if (player != null)
            {
                player.ApplyWindSlow(false); // 즉시 속도 복원
            }

            CancelInvoke(nameof(PlayWindEffect));
        }
    }

    private void PlayWindEffect()
    {
        if (_windEffectPrefab && windOrigin)
        {
            Instantiate(_windEffectPrefab, windOrigin.position, windOrigin.rotation);
        }
    }

    public void TakeDamage(float dmg)
    {
        _currentHealth -= dmg;
        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        Destroy(gameObject);
        Debug.Log("[AirRobot] Died");
    }
}
