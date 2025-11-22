using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Player : MonoBehaviour
{
    [Header("Player Status")]
    [SerializeField] private float _attackPower = 10f;   // 공격력
    [SerializeField] private float _moveSpeed = 3f;      // 이동 속도
    [SerializeField] private float _maxHealth = 500f;    // 최대 체력
    [SerializeField] private float _currentHealth;       // 현재 체력

    [Header("Movement")]
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private float _curSpeed = 0f;
    [SerializeField] private bool _isGrounded = true;
    [SerializeField] private bool _isShifting = false;
    [SerializeField] private bool _isSlowed = false;

    [Header("Battery & Upgrade")]
    [SerializeField] private float _curBattery = 100;
    [SerializeField] private float[] _batteryReductionAmount =
    {
        0.0002f
    };
    [SerializeField] private float[] _batteryReductionTerm =
    {
        1f
    };
    [FormerlySerializedAs("_curStatus")] [SerializeField] private int _curPlayerStatus;
    [SerializeField] private int _curHealthLevel = 1;
    [SerializeField] private int _curSpeedLevel = 1;
    [FormerlySerializedAs("_speedPerLevel")] [SerializeField]
    private float[] _speedWithBoostPerLevel =
    {
        5f,
        6f,
        7f
    };

    [Header("Combat")]
    [SerializeField] private LayerMask _attackRaycastMask;
    [SerializeField] private float _attackRaycastDist;
    [FormerlySerializedAs("_attackSpeedRate")] [SerializeField]
    private float[] _coolDownTime =
    {
        1.5f,
        0.5f
    };

    [SerializeField] private float _castingTime = 1f;
    [SerializeField] private bool _isMeleeCasting = false;
    [SerializeField] private bool _isStunned = false;

    private float _lastAttackTime = 0f;
    private bool _isReloading = false;
    private int _curBullets;

    [Header("Camera")]
    [SerializeField] private Transform _camera;
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private Transform _cameraPitchTarget;
    private float _cameraPitch = 0f;

    [Header("Weapon")]
    [SerializeField] private WeaponData _currentWeaponData;
    [SerializeField] private int _currentWeaponIdx;
    [SerializeField] private Transform _weaponSocket;

    private GameObject _currentWeaponModel;

    [Header("Effects")]
    [SerializeField] private AudioClip _stunSound;
    private AudioSource _stunAudioSource;

    // Cached Components & Managers
    private Rigidbody _rb;
    private Animator _animator;
    private WeaponManager _wm;
    private GameManager _gm;

    // Runtime state
    private Vector3 _moveDirection;
    private Coroutine _stunCo;

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        InitializeState();
        SetupStunAudio();

        Cursor.visible = false;
    }

    private void Start()
    {
        CacheManagers();
        InitWeaponByGameManager();
        StartCoroutine(BatteryReduction());
    }

    private void FixedUpdate()
    {
        if (_isStunned) return; // 스턴 중에는 이동 불가
        Move();
    }

    private void Update()
    {
        if (_isStunned) return; // 스턴 중에는 입력/카메라 불가

        HandleInput();
        HandleCamera();
        HandleAttackInput();
        HandleReloadInput();
    }

    #endregion

    #region Initialization

    private void CacheComponents()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        if (_weaponSocket == null)
            _weaponSocket = transform.Find("WeaponSocket");
    }

    private void CacheManagers()
    {
        _gm = GameManager.Instance;
        _wm = WeaponManager.Instance;
    }

    private void InitializeState()
    {
        _currentHealth = _maxHealth;
        _curPlayerStatus = 0;
    }

    private void SetupStunAudio()
    {
        _stunAudioSource = gameObject.AddComponent<AudioSource>();
        _stunAudioSource.clip = _stunSound;
        _stunAudioSource.loop = false;
        _stunAudioSource.playOnAwake = false;
    }

    /// <summary>
    /// GameManager의 무기 타입에 따라 시작 무기 장착
    /// </summary>
    private void InitWeaponByGameManager()
    {
        if (_gm == null) return;

        if (_gm.WeaponType == 0)
        {
            _currentWeaponIdx = 0;
            _wm.EquipWeapon(_currentWeaponIdx); // 근접 무기 장착
        }
        else
        {
            _currentWeaponIdx = 4;
            _wm.EquipWeapon(_currentWeaponIdx); // 원거리 무기 장착
        }
    }

    #endregion

    #region Input & Camera

    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        UpdateShiftState();
        UpdateMoveDirection(h, v);
        HandleJumpInput();
    }

    private void UpdateShiftState()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            _isShifting = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            _isShifting = false;
        }
    }

    private void UpdateMoveDirection(float horizontal, float vertical)
    {
        _moveDirection = (transform.forward * vertical + transform.right * horizontal).normalized;
    }

    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            Jump();
    }

    private void HandleCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -60f, 60f);

        _cameraPitchTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleAttackInput()
    {
        if (!Input.GetMouseButton(0) || _isReloading) return;
        if (_gm == null) return;

        if (Time.time - _lastAttackTime < _coolDownTime[_gm.WeaponType]) return;

        _lastAttackTime = Time.time;
        Camera cam = _camera.GetComponent<Camera>();
        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        Debug.DrawRay(ray.origin, ray.direction * _attackRaycastDist, Color.red, 1f);

        if (Physics.Raycast(ray, out RaycastHit hit, _attackRaycastDist, _attackRaycastMask))
        {
            Attack(hit);
        }
        else
        {
            Attack(hit, false);
        }
    }

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && _currentWeaponData != null &&
            _curBullets < _currentWeaponData.Bullets)
        {
            Reload();
        }
    }

    #endregion

    #region Movement

    private void Move()
    {
        if (_moveDirection.sqrMagnitude > 0f)
        {
            float baseSpeed = CalculateBaseSpeed();

            _curSpeed = baseSpeed;

            Vector3 targetPos = _rb.position + _moveDirection * (_curSpeed * Time.fixedDeltaTime);
            Vector3 nextPos = Vector3.Lerp(_rb.position, targetPos, 0.8f);
            nextPos.y = _rb.position.y;
            _rb.MovePosition(nextPos);

            UpdateMoveAnimation(isMoving: true);
        }
        else
        {
            UpdateMoveAnimation(isMoving: false);
        }
    }

    private float CalculateBaseSpeed()
    {
        float baseSpeed = _isShifting
            ? _speedWithBoostPerLevel[Mathf.Clamp(_curSpeedLevel - 1, 0, _speedWithBoostPerLevel.Length - 1)]
            : _moveSpeed;

        if (_isSlowed)
            baseSpeed *= 0.8f; // 감속 적용

        return baseSpeed;
    }

    private void UpdateMoveAnimation(bool isMoving)
    {
        if (!isMoving)
        {
            _animator.SetBool("isWalking", false);
            _animator.SetBool("isRunning", false);
            return;
        }

        if (_isShifting)
        {
            _animator.SetBool("isRunning", true);
            _animator.SetBool("isWalking", false);
        }
        else
        {
            _animator.SetBool("isWalking", true);
            _animator.SetBool("isRunning", false);
        }
    }

    private void Jump()
    {
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _isGrounded = false;
        // _animator?.SetTrigger("jump");
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            _isGrounded = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            _isGrounded = false;
        }
    }

    #endregion

    #region Reload

    private void Reload()
    {
        if (_isReloading) return;
        StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        _isReloading = true;
        yield return new WaitForSeconds(2f);

        if (_currentWeaponData != null)
        {
            _curBullets = _currentWeaponData.Bullets;
        }

        _isReloading = false;
        Debug.Log("[Player] 재장전 완료");
    }

    #endregion

    #region Combat

    private void Attack(RaycastHit hit, bool isHit = true)
    {
        if (_currentWeaponData == null)
        {
            Debug.LogWarning("[Player] 무기가 설정되지 않았습니다.");
            return;
        }

        string weaponName = _currentWeaponData.WeaponName;

        if (weaponName.Contains("Close") && !_isMeleeCasting)
        {
            PlayMeleeAnimation();

            _isMeleeCasting = true;
            StartCoroutine(MeleeAttackCoroutine());
        }
        else if (weaponName.Contains("Long"))
        {
            HandleRangedAttack(hit, isHit);
        }

        _curBattery -= _currentWeaponData.BatteryUsage;
    }

    private void PlayMeleeAnimation()
    {
        if (_wm != null && _wm.GetWeaponLevel() == 0)
        {
            _animator.SetTrigger("isPunching");
            Debug.Log("punch");
        }
        else
        {
            _animator.SetTrigger("isSwing");
        }
    }

    private void HandleRangedAttack(RaycastHit hit, bool isHit)
    {
        if (isHit)
        {
            IEnemy enemy = hit.collider.GetComponentInParent<IEnemy>();
            if (enemy != null)
            {
                float damage = CalculateRangedDamage(hit.point);
                enemy.TakeDamage(damage);
                Debug.Log($"[Player] {hit.collider.name}에게 {damage} 데미지 입힘");

                if (_currentWeaponData.HitEffectPrefab != null)
                {
                    Instantiate(_currentWeaponData.HitEffectPrefab, hit.point,
                        Quaternion.LookRotation(hit.normal));
                }
            }
        }

        _curBullets--;
        if (_curBullets <= 0)
        {
            Reload();
        }
    }

    private float CalculateRangedDamage(Vector3 hitPoint)
    {
        float distance = Vector3.Distance(transform.position, hitPoint);
        float multiplier = Mathf.Max(0f, 1f - distance * 0.05f);
        return _attackPower * multiplier; // 거리 비례 데미지 감소
    }

    private IEnumerator MeleeAttackCoroutine()
    {
        yield return new WaitForSeconds(_castingTime);

        float range = _currentWeaponData.range;
        float halfAngle = 45f;

        Vector3 center = _camera.position; // 공격 중심 = 카메라 위치
        Vector3 forward = _camera.forward; // 공격 방향 = 카메라 forward

        HashSet<IEnemy> hitEnemies = new HashSet<IEnemy>();
        Collider[] hits = Physics.OverlapSphere(center, range, _attackRaycastMask);

        foreach (Collider target in hits)
        {
            IEnemy enemy = target.GetComponentInParent<IEnemy>();
            if (enemy == null) continue;

            Vector3 dir = (target.transform.position - center).normalized;

            if (Vector3.Angle(forward, dir) <= halfAngle)
            {
                if (hitEnemies.Contains(enemy)) continue; // 중복 데미지 방지
                hitEnemies.Add(enemy);
                enemy.TakeDamage(_attackPower);
                Debug.Log($"근거리 hit: {target.name}");
            }
        }

        _isMeleeCasting = false;
    }

    #endregion

    #region Damage & CrowdControl

    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        Debug.Log($"[Player] 피격됨: {damage}, 남은 체력: {_currentHealth}");
    }

    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;

        if (_stunCo != null)
            StopCoroutine(_stunCo);

        _stunCo = StartCoroutine(StunRoutine(seconds));
    }

    private IEnumerator StunRoutine(float seconds)
    {
        _isStunned = true;
        _rb.linearVelocity = Vector3.zero;   // 관성 즉시 제거
        _moveDirection = Vector3.zero;       // 입력 방향 초기화

        _stunAudioSource.Play();

        Debug.Log("[Player] isStunned");
        SetStunAnimation(true);

        yield return new WaitForSeconds(seconds);

        SetStunAnimation(false);
        Debug.Log("[Player] release Stun");

        _isStunned = false;
        _stunCo = null;
    }

    private void SetStunAnimation(bool isStunned)
    {
        _animator.SetBool("isStunning", isStunned);

        // 스턴 시 이동 모션 OFF, 해제 시 기본 이동 모션 ON
        bool moving = !isStunned;
        _animator.SetBool("isWalking", moving);
        _animator.SetBool("isRunning", moving);
    }

    /// <summary>
    /// 바람에 맞을 때 이동속도 20% 감소
    /// </summary>
    public void ApplyWindSlow(bool enable)
    {
        if (enable && !_isSlowed)
        {
            _isSlowed = true;
            Debug.Log("[Player] 바람 감속 적용");
        }
        else if (!enable && _isSlowed)
        {
            _isSlowed = false;
            Debug.Log("[Player] 바람 감속 해제");
        }
    }

    #endregion

    #region Weapon Init & Status

    public void InitWeapon(WeaponData weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("[Player] InitWeapon() 호출 시 WeaponData가 null입니다.");
            return;
        }

        if (_currentWeaponModel != null)
            Destroy(_currentWeaponModel);

        Transform holder = _weaponSocket != null
            ? _weaponSocket.transform.Find("WeaponHolder")
            : null;

        _currentWeaponModel = holder != null
            ? Instantiate(weaponData.ModelPrefab, holder, false)
            : Instantiate(weaponData.ModelPrefab, _weaponSocket, false);

        _currentWeaponModel.transform.localPosition = new Vector3(0, 0.25f, 1);
        _currentWeaponModel.transform.localRotation = Quaternion.identity;

        _attackPower = weaponData.baseAttackPower;
        _currentWeaponData = weaponData;
        _curBullets = weaponData.Bullets;
        _attackRaycastDist = weaponData.range;

        if (_animator != null && !string.IsNullOrEmpty(weaponData.AttackAnimation))
        {
            _animator.runtimeAnimatorController = weaponData.AnimatorController;
        }

        Debug.Log($"[Player] 무기 초기화 완료: {weaponData.WeaponName}, 공격력: {_attackPower}");
    }

    /// <summary>
    /// 현재 플레이어 상태를 조회
    /// </summary>
    public PlayerStatus GetStatus()
    {
        int idx = _currentWeaponIdx + 1;

        if (_currentWeaponIdx >= 5)
            _currentWeaponIdx = -3;
        else
            _currentWeaponIdx = 0;

        float speedWithBoost = _speedWithBoostPerLevel[Mathf.Clamp(_curSpeedLevel - 1, 0,
            _speedWithBoostPerLevel.Length - 1)];

        return new PlayerStatus(
            _attackPower,
            _moveSpeed,
            _maxHealth,
            _currentHealth,
            _curBattery,
            _curHealthLevel,
            _curSpeedLevel,
            idx,
            _curBullets,
            speedWithBoost
        );
    }

    #endregion

    #region Battery & Upgrade

    private IEnumerator BatteryReduction()
    {
        GameManager gm = GameManager.Instance;
        while (true)
        {
            yield return new WaitForSeconds(1f);

            float reductionAmount = _curBattery * _batteryReductionAmount[_curPlayerStatus];
            if (_isShifting) reductionAmount *= 2f;

            _curBattery -= reductionAmount;
        }
    }

    /// <summary>
    /// 배터리의 percent%만큼 즉시 감소 (예: 1 -> 전체의 1%)
    /// </summary>
    public void ConsumeBatteryPercent(float percent)
    {
        if (percent <= 0f) return;

        float reduction = percent * 0.01f;
        _curBattery -= reduction;
    }

    public void UpdateHealth()
    {
        _curHealthLevel++;
        switch (_curHealthLevel)
        {
            case 2:
                _maxHealth = 700f;
                _currentHealth = Mathf.Min(_currentHealth + 200f, _maxHealth);
                break;
            case 3:
                _maxHealth = 1000f;
                _currentHealth = Mathf.Min(_currentHealth + 300f, _maxHealth);
                break;
            case 4:
                _maxHealth = 1300f;
                _currentHealth = Mathf.Min(_currentHealth + 300f, _maxHealth);
                break;
            default:
                break;
        }
    }

    #endregion
}

/// <summary>
/// 플레이어의 스냅샷 상태 데이터
/// </summary>
[Serializable]
public class PlayerStatus
{
    public readonly float AttackPower;
    public readonly float MoveSpeed;
    public readonly float MaxHealth;
    public readonly float CurrentHealth;
    public readonly float BatteryRemaining;
    public readonly int CurrentHealthLevel;
    public readonly int CurrentSpeedLevel;
    public readonly int CurrentWeaponLevel;
    public readonly int BulletCount;
    public readonly float SpeedWithBoost;

    public PlayerStatus(
        float attack,
        float speed,
        float maxHp,
        float curHp,
        float batteryRemaining,
        int curHealthLevel,
        int curSpeedLevel,
        int curWeaponLevel,
        int bulletCount = 0,
        float speedWithBoost = 0)
    {
        AttackPower = attack;
        MoveSpeed = speed;
        MaxHealth = maxHp;
        CurrentHealth = curHp;
        BatteryRemaining = batteryRemaining;
        CurrentHealthLevel = curHealthLevel;
        CurrentSpeedLevel = curSpeedLevel;
        CurrentWeaponLevel = curWeaponLevel;
        BulletCount = bulletCount;
        SpeedWithBoost = speedWithBoost;
    }
}
