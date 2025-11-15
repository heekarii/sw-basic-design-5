using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Serialization;

public class AirRobot : MonoBehaviour, IEnemy
{
    [FormerlySerializedAs("detectDistance")]
    [Header("Settings")]
    [SerializeField] private float _maxHealth = 40;
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _detectDistance = 15.5f; // 활성화 거리
    [SerializeField] private float _windLength = 15.5f;       // 바람 길이 (앞으로 뻗는 거리)
    [SerializeField] private float _windRadius = 1.5f;       // 바람 반지름 (원통형 범위)
    [FormerlySerializedAs("windEffectPrefab")] 
    [SerializeField] private GameObject _windEffectPrefab;
    [SerializeField] private Transform _windOrigin;
    [SerializeField] private AudioSource _attackAudio;
    [SerializeField] private int _scrapAmount = 2;

    [SerializeField] private Transform _zeron;
    [SerializeField] private Player _player;
    [SerializeField] bool _isActive = false;
    [SerializeField] private GameObject _activeWindFX;
    [SerializeField] private ScrapData _scrapData;

    private void Start()
    { 
        _zeron = GameObject.FindWithTag("Player")?.transform;
        _player = FindObjectOfType<Player>();
        _currentHealth = _maxHealth;

        // ✅ WindOrigin 자동 할당
        if (_windOrigin == null)
        {
            Transform found = transform.Find("air_robot+collider/air_robot/Gman5_0Thruster/Object_65");
            if (found != null)
            {
                _windOrigin = found;
                Debug.Log("[AirRobot] WindOrigin 자동 할당 완료");
            }
            else
            {
                Debug.LogWarning("[AirRobot] WindOrigin 오브젝트를 찾지 못했습니다. 기본 위치로 대체합니다.");
                GameObject originObj = new GameObject("WindOrigin");
                originObj.transform.SetParent(transform);
                originObj.transform.localPosition = new Vector3(0, 1.0f, 1.0f); // 로봇 앞쪽
                _windOrigin = originObj.transform;
            }
        }
    }

    private void Update()
    {
        if (_zeron == null || _player == null) return;

        float distance = Vector3.Distance(transform.position, _zeron.position);

        // 🔹 감지 범위 진입 시 활성화
        if (!_isActive && distance <= _detectDistance)
        {
            _isActive = true;
            Debug.Log("[AirRobot] 활성화됨");
        }

        // 🔹 감지 범위 내라면 계속 바람 판정
        if (_isActive)
        {
            CheckWindHit();
        }

        // 🔹 감지 범위 이탈 시 비활성화 처리 + 즉시 해제
        if (_isActive && distance > _detectDistance)
        {
            _isActive = false;
            Debug.Log("[AirRobot] 비활성화됨");
            _player.ApplyWindSlow(false);

            if (_activeWindFX)
            {
                Destroy(_activeWindFX);
                _activeWindFX = null;
                Debug.Log("[AirRobot] WindEffect 강제 해제 (범위 이탈)");
            }
        }
    }


    private void CheckWindHit()
    {
        if (_player == null || _windOrigin == null) return;

        Vector3 origin = _windOrigin.position;
        // ✅ 로봇 전체가 바라보는 방향을 기준으로 함
        Vector3 dir = transform.forward.normalized;  

        Vector3 start = origin - dir * (_windRadius);
        Vector3 end = origin + dir * _windLength;
        Debug.DrawRay(_windOrigin.position, dir * _windLength, Color.red);

        
        Collider[] hits = Physics.OverlapCapsule(start, 
            end, _windRadius);
        bool playerInWind = false;
        if (hits.Length == 0)
        {
            playerInWind = false;
            Debug.Log("[AirRobot] 바람 범위 내에 아무도 없음");
        }

        foreach (var col in hits)
        {
            if (col.CompareTag("Player"))
            {
                Vector3 toPlayer = (col.transform.position - origin).normalized;
                float dot = Vector3.Dot(dir, toPlayer);
                if (dot > 0.95f) // 정면 ±45도
                {
                    Debug.Log("[AirRobot] 플레이어가 바람 범위 내에 있음");
                    playerInWind = true;
                    break;
                }
            }
        }

        _player.ApplyWindSlow(playerInWind);
        
        Debug.Log($"[AirRobot] playerInWind={playerInWind}");
        if (playerInWind)
        {
            if (_windEffectPrefab && _windOrigin && _activeWindFX == null)
            {
                _activeWindFX = Instantiate(_windEffectPrefab, _windOrigin.position, _windOrigin.rotation);
                
                _activeWindFX.transform.localPosition += -Vector3.forward * 3f;
                _activeWindFX.transform.localRotation = _windEffectPrefab.transform.localRotation;
            }
        }
        else
        {
            if (_activeWindFX)
            {
                Debug.Log("[AirRobot] WindEffect 해제 (플레이어 이탈)");
                Destroy(_activeWindFX);
                _activeWindFX = null;
            }
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
        DropScrap(_scrapAmount);
        Destroy(gameObject);
        Debug.Log("[AirRobot] 파괴됨");
    }

    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, transform.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AirRobot] 스크랩 {amount} 드랍");
    }
    
    /// <summary>
    /// Scene에서 바람 범위 시각화 (디버그용)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (_windOrigin == null) return;
        Gizmos.color = Color.cyan;
        Vector3 origin = _windOrigin.position;
        // ✅ 로봇 전체가 바라보는 방향을 기준으로 함
        Vector3 dir = transform.forward.normalized;  

        Vector3 start = origin - dir * (_windRadius);
        Vector3 end = origin + dir * _windLength;
        Gizmos.DrawWireSphere(start, _windRadius);
        Gizmos.DrawWireSphere(end, _windRadius);
        Gizmos.DrawLine(start, end);
    }
}
