using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;

public class AirRobot : MonoBehaviour, IEnemy
{
    [FormerlySerializedAs("detectDistance")]
    [Header("Settings")]
    [SerializeField] private float _maxHealth = 40;
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _detectDistance = 19.4f; // 활성화 거리
    [SerializeField] private float _windLength = 19.4f;       // 바람 길이 (앞으로 뻗는 거리)
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
    private Transform _playerTr;
    private Transform _tr;
    
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    
    [Header("Death")]
    [SerializeField] private float _deathTime = 2f;
    [SerializeField] private ParticleSystem _DeathEffect;
    [SerializeField] private AudioSource _DeathAudio;
    private bool _isDead = false;
    
    private void Start()
    { 
        _zeron = GameObject.FindWithTag("Player")?.transform;
        _player = FindObjectOfType<Player>();
        _currentHealth = _maxHealth;
        _tr = transform;
        _playerTr = _zeron;
        
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();   // 데미지 받을 때마다 HP바 갱신
        
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
        if (_isDead)
            return;
        if (_zeron == null || _player == null) return;
        bool hasLOS = HasLineOfSight();
        
        float distance = Vector3.Distance(transform.position, _zeron.position);

        // 🔹 감지 범위 진입 시 활성화
        if (!_isActive && distance <= _detectDistance)
        {
            _isActive = true;
            Debug.Log("[AirRobot] 활성화됨");
        }

        // 🔹 감지 범위 내라면 계속 바람 판정
        if (_isActive && hasLOS) 
        {
            CheckWindHit();
        }

        // 🔹 감지 범위 이탈 시 비활성화 처리 + 즉시 해제
        if (_isActive && distance > _detectDistance || !hasLOS) 
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

        UpdateHpBarFacing();
    }
    
    private void UpdateHpBarFacing()
    {
        if (_hpCanvas == null) return;

        Transform target = _playerTr;  // 플레이어를 바라보게

        if (target == null) return;

        // HP바 위치에서 플레이어 방향
        Vector3 dir = target.position - _hpCanvas.position;
        dir.y = 0f; // 위아래 기울어지는 거 싫으면 y 고정

        if (dir.sqrMagnitude < 0.0001f) return;

        _hpCanvas.rotation = Quaternion.LookRotation(dir);
    }
    
    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
    }


    private bool HasLineOfSight()
    {
        if (_playerTr == null)
            return false;

        Vector3 origin = _tr.position + Vector3.up * 1.2f;
        Vector3 target = _playerTr.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f)
            return true;

        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신의 콜라이더 먼저 맞았을 때 처리
            if (hit.collider.transform.IsChildOf(_tr))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(_tr))
                        continue;

                    return h.collider.GetComponentInParent<Player>() != null;
                }

                return true;
            }

            return hit.collider.GetComponentInParent<Player>() != null;
        }

        // 아무것도 안 맞으면 시야 확보된 것으로 처리
        return true;
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
                _attackAudio.Play();
                _activeWindFX = Instantiate(_windEffectPrefab, _windOrigin.position, _windOrigin.rotation);
                //_activeWindFX.transform.localPosition += -Vector3.forward * 3f;
                //_activeWindFX.transform.localRotation = _windEffectPrefab.transform.localRotation;
            }
        }
        else
        {
            if (_activeWindFX)
            {
                Debug.Log("[AirRobot] WindEffect 해제 (플레이어 이탈)");
                _attackAudio.Stop();
                Destroy(_activeWindFX);
                _activeWindFX = null;
            }
        }
    }

    public void TakeDamage(float dmg)
    {
        _currentHealth -= dmg;
        UpdateHpUI();
        if (_currentHealth <= 0)
            Die();
    }

    private void UpdateHpUI()
    {
        if (_hpFillImage == null) return;

        float ratio = (_maxHealth > 0f) ? _currentHealth / _maxHealth : 0f;
        _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
    }
    
    private void PlayDeath()
    {
        // 🔹 이펙트 실행
        if (_DeathEffect != null)
        {
            _DeathEffect.transform.SetParent(null); // 부모 떼기
            _DeathEffect.Play();

            float effectDuration =
                _DeathEffect.main.duration +
                _DeathEffect.main.startLifetime.constantMax;

            Destroy(_DeathEffect.gameObject, effectDuration + 0.1f);
        }

        // 🔹 사운드 실행
        if (_DeathAudio != null && _DeathAudio.clip != null)
        {
            _DeathAudio.transform.SetParent(null); // 부모 떼기
            _DeathAudio.Play();

            Destroy(_DeathAudio.gameObject, _DeathAudio.clip.length + 0.1f);
        }
    }
    
    private void Die()
    {
        if (_isDead) return;    // 여러 번 실행되는 것 방지
        _isDead = true;

        // 1) 바람/슬로우 상태 정리
        _isActive = false;

        if (_player != null)
            _player.ApplyWindSlow(false);  // 슬로우 효과 해제

        // 2) 바람 이펙트 / 사운드 정지
        if (_activeWindFX != null)
        {
            Destroy(_activeWindFX);
            _activeWindFX = null;
            Debug.Log("[AirRobot] WindEffect 해제 (사망)");
        }

        if (_attackAudio != null && _attackAudio.isPlaying)
            _attackAudio.Stop();

        // 3) 콜라이더 비활성화 (원하는 경우)
        Collider selfCol = GetComponent<Collider>();
        if (selfCol != null)
            selfCol.enabled = false;

        // 4) HP바 끄기
        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        // 5) 죽음 이펙트 / 사운드 재생
        PlayDeath();

        // 6) 딜레이 후 스크랩 드랍 + 삭제
        StartCoroutine(DieRoutine());
    }

    
    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);               
        Destroy(gameObject);
    }

    public void DropScrap(int amount)
    {
        if (!_scrapData) return;
        
        GameObject scrap = Instantiate(_scrapData.ScrapPrefab, transform.position, Quaternion.identity);
        Scrap scrapComponent = scrap.AddComponent<Scrap>();
        scrapComponent.InitScrap(amount);
        Debug.Log($"[AirRobot] 스크랩 {amount} 드랍");
    }
    
    // 몬스터를 중심으로 인식 범위(_aggravationRange)를 흰 원으로 시각화
    private void DrawAggroRadiusGizmo()
    {
        // 반경이 0 이하면 그릴 필요 없음
        if (_detectDistance <= 0f) return;

        Gizmos.color = Color.white;

        // 원의 중심: 몬스터 위치, 살짝 위로 띄워서 바닥에 안 묻히게
        Vector3 center = transform.position;
        center.y += 0.05f;

        float radius = _detectDistance;
        int segments = 48;
        float step = 360f / segments;

        // 시작점: 중심 기준 X축 방향으로 radius 떨어진 곳
        Vector3 prev = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = step * i * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            Vector3 next = center + new Vector3(x, 0f, z);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
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
