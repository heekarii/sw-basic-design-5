using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LaserDrone : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _detectDistance = 13.7f;  // 시야 감지 거리
    [SerializeField] private float _attackDistance = 8.7f;   // 공격 거리
    [SerializeField] private float _moveSpeed = 6f;          // 이동 속도
    [SerializeField] private float _maxHealth = 50.0f;            // 체력
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _attackCooldown = 10f;    // 재공격 시간
    [SerializeField] private int _dropScrap = 5;             // 처치 시 스크랩 수
    [SerializeField] private int _scrapAmount = 5;            // 드랍 스크랩 양
    [SerializeField] private float _flashMaintainTime = 3f;
    [SerializeField] private float _lookAtTurnSpeed = 8f; // 회전 속도 조절
    
    [Header("Object")]
    [SerializeField] private Transform _player;              // ZERON
    [SerializeField] private Image _flashOverlay;            // 섬광 피격용 UI (Canvas Image)
    [SerializeField] private ScrapData _scrapData;          // 스크랩 데이터
    [SerializeField] private AudioSource _attackAudio;
    [SerializeField] private ParticleSystem _damagedEffect;
    [SerializeField] private AudioSource _damagedSound;
    
    [Header("HP Bar UI")]
    [SerializeField] private Image _hpFillImage;   // 빨간 체력바 (HPBar_Fill)
    [SerializeField] private Transform _hpCanvas;  // HpBarCanvas (World Space Canvas)
    private Transform _camTr;                      // 카메라 Transform

    [Header("Death")]
    [SerializeField] private float _deathTime = 2f;
    [SerializeField] private ParticleSystem _DeathEffect;
    [SerializeField] private AudioSource _DeathAudio;
    
    
    private bool _isDead = false;
    private bool _isActive = false;
    private bool _isAttacking = false;
    private float _lastAttackTime = -999f;

    private void Start()
    {
        _currentHealth = _maxHealth;
        
        if (_player == null)
            _player = GameObject.FindWithTag("Player")?.transform;

        if (_flashOverlay == null)
        {
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img.CompareTag("FlashOverlay"))
                {
                    _flashOverlay = img;
                    break;
                }
            }
        }
        
        // HP Image 기본 설정 강제 (실수 방지용)
        if (_hpFillImage != null)
        {
            _hpFillImage.type = Image.Type.Filled;
            _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
            _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // 왼쪽 고정, 오른쪽이 줄어듦
        }
        UpdateHpUI();
    }

    private void Update()
    {
        if (_player == null) return;

        float distance = Vector3.Distance(transform.position, _player.position);

        // ✅ 시야 및 거리 감지 (HasLineOfSight 적용)
        if (distance <= _detectDistance && HasLineOfSight())
            _isActive = true;

        if (!_isActive) return;

        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        // 인식범위 밖의 플레이어가 아니라면 계속 쳐다보게
        if (worldDist <= _detectDistance)   
            LookAtPlayer();
        
        // ✅ 공격 / 이동 판단
        if (distance > _attackDistance && !_isAttacking)
        {
            MoveTowardTarget();
        }
        else if (distance <= _attackDistance && !_isAttacking && HasLineOfSight())
        {
            TryAttack();
        }
    }
    
    private void OnDrawGizmos()
    {
        DrawAggroRadiusGizmo();
    }


    // ============================================================
    //  이동 (공중 4.2m 높이 유지)
    // ============================================================
    private void MoveTowardTarget()
    {
        Vector3 targetPos = _player.position;
        targetPos.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, _moveSpeed * Time.deltaTime);
        transform.LookAt(_player);
    }

    // ============================================================
    //  공격
    // ============================================================
    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < _attackCooldown) return;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        Player player = FindObjectOfType<Player>();
        if (_attackAudio != null && !_attackAudio.isPlaying)
            _attackAudio.Play();
        
        // 피해 즉시 적용 (내구도 영향 X, 섬광 효과만)
        if (_flashOverlay != null)
            //StartCoroutine(ApplyFlashEffect());
            player.ApplyFlash(3f);
        else
            Debug.LogWarning("[AirRobot] FlashOverlay 연결되지 않음");

        yield return new WaitForSeconds(_flashMaintainTime);
        _isAttacking = false;
    }

    // ============================================================
    //  섬광 효과 (3초간 시야 차단)
    // ============================================================
    private IEnumerator ApplyFlashEffect()
    {
        _flashOverlay.gameObject.SetActive(true);
        _flashOverlay.color = new Color(1f, 1f, 0.7f, 0.8f); // 밝은 노란색
        yield return new WaitForSeconds(3f);
        _flashOverlay.gameObject.SetActive(false);
    }

    // ============================================================
    //  시야 감지 (Line of Sight)
    // ============================================================
    private bool HasLineOfSight()
    {
        if (_player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = _player.position + Vector3.up * 1.0f;

        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;

        dir.Normalize();

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신 콜라이더는 무시
            if (hit.collider.transform.IsChildOf(transform))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    if (h.collider.GetComponentInParent<Player>() != null) return true;
                    return false;
                }
                return true;
            }

            // 첫 번째 히트가 플레이어면 시야 있음
            if (hit.collider.GetComponentInParent<Player>() != null) return true;
            return false; // 다른 오브젝트에 가려짐
        }

        return true; // 아무것도 안 맞았으면 개방된 시야
    }
    
    private void LookAtPlayer()
    {
        if (_player == null || !HasLineOfSight()) return;

        Vector3 lockedDir = (_player != null)
            ? (_player.transform.position - transform.position)
            : transform.forward;
        lockedDir.y = 0.0f;
        lockedDir.Normalize();
        
        // 몸을 스냅샷 방향으로 즉시 정렬
        if (lockedDir.sqrMagnitude > 0.001f)
        {
            float rotSpeed = _lookAtTurnSpeed;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lockedDir),
                Time.deltaTime * rotSpeed
            );
        }
    }
    
    // 체력바 채우기 갱신
    private void UpdateHpUI()
    {
        if (_hpFillImage == null) return;

        float ratio = (_maxHealth > 0f) ? _currentHealth / _maxHealth : 0f;
        _hpFillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    // ============================================================
    //  피해 / 사망 처리
    // ============================================================
    public void TakeDamage(float damage)
    {
        _currentHealth -= Mathf.RoundToInt(damage);
        UpdateHpUI();
        _damagedEffect.Play();
        _damagedSound.Play();
        if (_currentHealth <= 0)
            Die();
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
        if (_isDead) return;
        _isDead = true;
        
        
        PlayDeath();

        if (_hpCanvas != null)
            _hpCanvas.gameObject.SetActive(false);

        StartCoroutine(DieRoutine());
    }

    
    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(_deathTime);
        DropScrap(_scrapAmount);     
        Destroy(gameObject);                   // 삭제
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

}
