using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Player : MonoBehaviour
{
    [Header("Player Status")]
    [SerializeField] private float _attackPower = 10f;   // 공격력
    [SerializeField] private float _moveSpeed = 5f;      // 이동 속도
    [SerializeField] private float _maxHealth = 500f;    // 최대 체력
    [SerializeField] private float _currentHealth;       // 현재 체력
    [SerializeField] private bool _isGrounded = true;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private  float _curBattery = 100;
    [SerializeField] private int _curBullets;
    [SerializeField] private bool _isStunned = false;   
    
    [SerializeField] private float _curSpeed = 0f;
    [SerializeField] private bool _isSlowed = false;
    
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
    [SerializeField] private bool _isShifting = false;
    
    [FormerlySerializedAs("_speedPerLevel")] [SerializeField] private float[] _speedWithBoostPerLevel =
    {
        7f,
        8f,
        9f
    };
    
    [Header("Combat")] 
    [SerializeField] private LayerMask _attackRaycastMask;
    [SerializeField] private float _attackRaycastDist;

    [FormerlySerializedAs("_attackSpeedRate")] [SerializeField] private float[] _coolDownTime =
    {
        0.7f,
        0.5f
    };

    [SerializeField] private float _castingTime = 0.7f;
    [SerializeField] private bool _isMeleeCasting = false;
    private float _lastAttackTime = 0f;
    private bool _isReloading = false;
    
    [Header("Camera")]
    [SerializeField] private Transform _camera;
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private Transform _cameraPitchTarget;
    private float _cameraPitch = 0f;
    
    [Header("Weapon")]
    [SerializeField]
    private WeaponData _currentWeaponData;
    private GameObject _currentWeaponModel;
    
    private Rigidbody _rb;
    private Animator _animator;
    private WeaponManager _wm;
    private GameManager _gm;
    
    [SerializeField]
    private Transform _weaponSocket;

    [Header("Effects")] 
    [SerializeField] private AudioClip _stunSound;
    private AudioSource _stunAudioSource;
    
    
    private Vector3 _moveDirection;
    

    public float AttackPower => _attackPower;
    public float MoveSpeed => _moveSpeed;
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        if (_weaponSocket == null)
            _weaponSocket = transform.Find("WeaponSocket");
        _currentHealth = _maxHealth;
        _curPlayerStatus = 0;
        _stunAudioSource = gameObject.AddComponent<AudioSource>();
        _stunAudioSource.clip = _stunSound;
        _stunAudioSource.loop = false;
        _stunAudioSource.playOnAwake = false;
        
        Cursor.visible = false;
    }

    private void Start()
    {
        _gm = GameManager.Instance;
        _wm = WeaponManager.Instance;
        if (_gm.WeaponType == 0)
        {
            _wm.EquipWeapon(0); // 근접 무기 장착
        }
        else
        {
            _wm.EquipWeapon(4); // 원거리 무기 장착
        }
        
        StartCoroutine(BatteryReduction());
        
    }
    
    private void FixedUpdate()
    {
        //스턴중이면 움직임과 카메라가 멈추도록
        if (_isStunned) return;
        Move();
    }
    private void Update()
    {
        //스턴중이면 움직임과 카메라가 멈추도록
        if (_isStunned) return;
        HandleInput();
        HandleCamera();
        
        if (Input.GetMouseButton(0) && !_isReloading)
        {
            if (Time.time - _lastAttackTime >= _coolDownTime[_gm.WeaponType])
            {
                _lastAttackTime = Time.time;
                Camera cam = _camera.GetComponent<Camera>();
                Ray ray  = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                Debug.DrawRay(ray.origin, ray.direction * _attackRaycastDist, Color.red, 1f);

                if (Physics.Raycast(ray, out RaycastHit hit, _attackRaycastDist, _attackRaycastMask))
                {
                    //Debug.Log($"[Player] 공격 목표: {hit.collider.name} @ {hit.point}");
                    Attack(hit); // y=0 강제 필요하면 new Vector3(hit.point.x, 0f, hit.point.z)
                }
                else
                {
                    Attack(hit, false);
                }
            }
            
        }
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && _curBullets < _currentWeaponData.Bullets)
        {
            Reload();
        }
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
    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            _isShifting = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            _isShifting = false;
        }
        
        _moveDirection = (transform.forward * v + transform.right * h).normalized;

        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            Jump();
    }

    /// <summary>
    /// 
    /// </summary>
    private void Move()
    {
        if (_moveDirection.sqrMagnitude > 0f)
        {
            float baseSpeed;

            // 1) 기본 속도: 걷기 or 달리기
            if (_isShifting)
                baseSpeed = _speedWithBoostPerLevel[_curSpeedLevel - 1];
            else
                baseSpeed = _moveSpeed;

            // 2) 감속 적용: 20% 감소
            if (_isSlowed)
                baseSpeed *= 0.8f;

            _curSpeed = baseSpeed;

            // 최종 이동
            Vector3 targetPos = _rb.position + _moveDirection * (_curSpeed * Time.fixedDeltaTime);
            Vector3 nextPos = Vector3.Lerp(_rb.position, targetPos, 0.8f);
            nextPos.y = _rb.position.y;
            _rb.MovePosition(nextPos);

            // 애니메이션
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
        else
        {
            _animator.SetBool("isWalking", false);
            _animator.SetBool("isRunning", false);
        }
    }

    
    private void Jump()
    {
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _isGrounded = false;
        //_animator?.SetTrigger("jump");
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

    private void Reload()
    {
        if (!_isReloading)
            StartCoroutine(ReloadCoroutine());
    }
    IEnumerator ReloadCoroutine()
    {
        _isReloading = true;
        yield return new WaitForSeconds(2f); 
        _curBullets = _currentWeaponData.Bullets;
        _isReloading = false;
        Debug.Log("[Player] 재장전 완료");
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="hit"></param>
    /// <param name="isHit"></param>
    void Attack(RaycastHit hit, bool isHit = true)
    {
        if (_currentWeaponData == null)
        {
            Debug.LogWarning("[Player] 무기가 설정되지 않았습니다.");
            return;
        }

        string weaponName = _currentWeaponData.WeaponName;


        if (weaponName.Contains("Close") && !_isMeleeCasting)
        {

            if (_wm.GetWeaponLevel() == 0)
            {
                _animator.SetTrigger("isPunching");
                Debug.Log("punch");
            }
            else
            {
                _animator.SetTrigger("isSwing");
            }

            _isMeleeCasting = true;
            StartCoroutine(MeleeAttackCoroutine());


        }
        else if(weaponName.Contains("Long"))
        {
            if (isHit)
            {
                IEnemy enemy = hit.collider.GetComponentInParent<IEnemy>();
                if (enemy != null)
                {
                    float distance = Vector3.Distance(transform.position, hit.point);
                    float multiplier = Mathf.Max(0f, 1f - (distance * 0.05f));
                    float damage = _attackPower * multiplier;
                    enemy.TakeDamage(damage); // 거리 비례 데미지 감소
                    Debug.Log($"[Player] {hit.collider.name}에게 {damage} 데미지 입힘 (거리 보정 계수: {multiplier})");

                    if (_currentWeaponData.HitEffectPrefab != null)
                    {
                        Instantiate(_currentWeaponData.HitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    }
                    Debug.Log($"hit {hit.collider.name}");
                }
                
            }

            _curBullets--;
            if (_curBullets <= 0)
            {
                Reload();
            }
        }

        _curBattery -= _currentWeaponData.BatteryUsage;
    }

    private IEnumerator MeleeAttackCoroutine()
    {
        yield return new WaitForSeconds(_castingTime);
        float range = _currentWeaponData.range;
        float halfAngle = 45f;

        // 공격 중심 = 카메라 위치
        Vector3 center = _camera.position;

        // 공격 방향 = 카메라 forward
        Vector3 forward = _camera.forward;

        // 주변 적 스캔
        HashSet<IEnemy> hitEnemies = new HashSet<IEnemy>();
        Collider[] hits = Physics.OverlapSphere(center, range, _attackRaycastMask);
            
        foreach (Collider target in hits)
        {
            IEnemy enemy = target.GetComponentInParent<IEnemy>();
            if (enemy == null) continue;

            // "카메라 위치 → 적" 방향
            Vector3 dir = (target.transform.position - center).normalized;

            // 각도 판정
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="durabilityLevel"></param>
    public void SetHealth(float durabilityLevel)
    {
        _maxHealth = durabilityLevel;
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="damage"></param>
    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        Debug.Log($"[Player] 피격됨: {damage}, 남은 체력: {_currentHealth}");
    }
    private Coroutine _stunCo;

    public void Stun(float seconds)
    {
        if (seconds <= 0f) return;
        if (_stunCo != null) StopCoroutine(_stunCo);
        Debug.Log($"[Player] isStunned");
        _stunCo = StartCoroutine(StunRoutine(seconds));
    }

    private IEnumerator StunRoutine(float seconds)
    {
        _isStunned = true;
        _rb.linearVelocity = Vector3.zero;   // 관성 즉시 제거
        _moveDirection = Vector3.zero; // 입력 방향 초기화
        
        _stunAudioSource.Play();
     
        Debug.Log($"[Player] isStunned");
        _animator.SetBool("isStunning", true);
        _animator.SetBool("isWalking", false);
        _animator.SetBool("isRunning", false);
        yield return new WaitForSeconds(seconds);
        _animator.SetBool("isStunning", false);
        _animator.SetBool("isWalking", true);
        _animator.SetBool("isRunning", true);
        Debug.Log($"[Player] release Stun");
        
        _isStunned = false;            // ▶ 자동 복귀 (속도값은 그대로)
        _stunCo = null;
    }

    /// <summary>
    /// 바람에 맞을 때 이동속도 20% 감소
    /// </summary>
    public void ApplyWindSlow(bool enable)
    {
        // 최초로 슬로우 상태 진입
        if (enable && !_isSlowed)
        {
            _isSlowed = true;
            
            Debug.Log("[Player] 바람 감속 적용");
        }
        // 바람 범위를 벗어나면 원래 속도 복원
        else if (!enable && _isSlowed)
        {
            _isSlowed = false;
            
            Debug.Log("[Player] 바람 감속 해제");
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public PlayerStatus GetStatus()
    {
        return new PlayerStatus(_attackPower, _moveSpeed, _maxHealth, _currentHealth, _curBattery);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="weaponData"></param>
    public void InitWeapon(WeaponData weaponData)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("[Player] InitWeapon() 호출 시 WeaponData가 null입니다.");
            return;
        }

        // 이전 무기 모델 제거
        if (_currentWeaponModel != null)
            Destroy(_currentWeaponModel);

        // 무기 모델 생성 및 장착
        _currentWeaponModel = Instantiate(weaponData.ModelPrefab, 
            _weaponSocket.transform.Find("WeaponHolder"), 
            false);
        _currentWeaponModel.transform.localPosition = new Vector3(0, 0.25f, 1);
        _currentWeaponModel.transform.localRotation = Quaternion.identity;
        //_currentWeaponModel.transform.localScale = new Vector3(1, 1, 1);

        // 공격력, 모션, 사거리 등 세팅
        _attackPower = weaponData.baseAttackPower;
        _currentWeaponData = weaponData;
        _curBullets = weaponData.Bullets;
        _attackRaycastDist = weaponData.range;

        // 애니메이션 클립 전환
        if (_animator != null && !string.IsNullOrEmpty(weaponData.AttackAnimation))
        {
            _animator.runtimeAnimatorController = weaponData.AnimatorController;
        }
        _attackRaycastDist = _currentWeaponData.range;
        Debug.Log($"[Player] 무기 초기화 완료: {weaponData.WeaponName}, 공격력: {_attackPower}");
    }

    IEnumerator BatteryReduction()
    {
        GameManager gm = GameManager.Instance;
        while (true)
        {
            yield return new WaitForSeconds(1f);
            var reductionAmount = _curBattery * _batteryReductionAmount[_curPlayerStatus];
            if (_isShifting) reductionAmount *= 2f;
            //Debug.Log("[Player] 배터리 감소: " + reductionAmount);
            _curBattery -= reductionAmount;
        }
    }
    
    /// <summary>
    /// 현재 배터리의 percent%만큼 즉시 감소 (예: 1 -> 현재값의 1%)
    /// </summary>
    public void ConsumeBatteryPercentOfCurrent(float percent)
    {
        if (percent <= 0f) return;

        float reduction = _curBattery * (percent * 0.01f);
        _curBattery = Mathf.Max(0f, _curBattery - reduction);

        // Debug.Log($"[Player] 배터리 {reduction:F3}감소  현재 → {_curBattery:F2}");
    }
}


/// <summary>
/// 
/// </summary>
[System.Serializable]
public class PlayerStatus
{
    public readonly float AttackPower;
    public readonly float MoveSpeed;
    public readonly float MaxHealth;
    public readonly float CurrentHealth;
    public readonly float BatteryRemaining;

    public PlayerStatus(float attack, float speed, float maxHp, float curHp, float batteryRemaining)
    {
        AttackPower = attack;
        MoveSpeed = speed;
        MaxHealth = maxHp;
        CurrentHealth = curHp;
        BatteryRemaining = batteryRemaining;
    }
}


