using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Player : MonoBehaviour
{
    [Header("Player Status")]
    [SerializeField] private float _attackPower = 10f;   // 공격력
    [SerializeField] private float _moveSpeed = 1f;      // 이동 속도
    [SerializeField] private float _maxHealth = 100f;    // 최대 체력
    [SerializeField] private float _currentHealth;       // 현재 체력
    [SerializeField] private bool _isGrounded = true;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private  float _curBattery = 100;
    [SerializeField] private List<int> _batteryReductionAmount;
    [SerializeField] private List<float> _batteryReductionTerm;
    [FormerlySerializedAs("_curStatus")] [SerializeField] private int _curPlayerStatus;
    [SerializeField] private int _curHealthLevel = 1;
    [SerializeField] private int _curSpeedLevel = 1;
    [SerializeField] private bool _isShifting = false;
    
    [SerializeField] private float[] _speedPerLevel =
    {
        1.3f,
        1.5f,
        1.7f
    };
    
    
    [Header("Combat")] 
    [SerializeField] private LayerMask _attackRaycastMask;
    [SerializeField] private float _attackRaycastDist;
    
    [Header("Camera")]
    [SerializeField] private Transform _camera;
    [SerializeField] private float _mouseSensitivity = 2f;
    private float _cameraPitch = 0f;
    
    [SerializeField]
    private WeaponData _currentWeaponData;
    private GameObject _currentWeaponModel;
    
    private Rigidbody _rb;
    private Animator _animator;
    private WeaponManager _wm;

    private Transform _weaponSocket;
    
    private Vector3 _moveDirection;
    

    public float AttackPower => _attackPower;
    public float MoveSpeed => _moveSpeed;
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _weaponSocket = transform.Find("WeaponSocket");
        _currentHealth = _maxHealth;
        _curPlayerStatus = 0;
        Cursor.visible = false;
        StartCoroutine(BatteryReduction());
        Cursor.visible = false;
    }

    private void Start()
    {
        _wm = WeaponManager.Instance;
        _wm.EquipWeapon(0);
        _attackRaycastDist = _currentWeaponData.range;
        
    }
    
    private void FixedUpdate()
    {
        Move();

    }
    private void Update()
    {
        HandleInput();
        HandleCamera();
        
        if (Input.GetMouseButtonDown(0))
        {
            Camera cam = _camera.GetComponent<Camera>();
            Ray ray  = cam.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            
            if (Physics.Raycast(ray, out RaycastHit hit, _attackRaycastDist, _attackRaycastMask))
            {
                //Debug.Log($"[Player] 공격 목표: {hit.collider.name} @ {hit.point}");
                Attack(hit.point); // y=0 강제 필요하면 new Vector3(hit.point.x, 0f, hit.point.z)
            }
            else
            {
                //Debug.Log("[Player] 조준 지점에 충돌 없음");
            }
        }
    }

    private void HandleCamera()
    {
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        _cameraPitch -= mouseY;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -45f, 45f);

        _camera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
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
            Vector3 targetPos;
            if (_isShifting)
            {
                targetPos = _rb.position + _moveDirection * 
                    (_moveSpeed* _speedPerLevel[_curSpeedLevel - 1] * Time.fixedDeltaTime);
            }
            else
            {
                targetPos = _rb.position + _moveDirection * (_moveSpeed * Time.fixedDeltaTime);
            }
            _rb.MovePosition(Vector3.Lerp(_rb.position, targetPos, 0.8f));
            _animator?.SetBool("isMoving", true);
        }
        else
        {
            _animator?.SetBool("isMoving", false);
        }
    }
    
    private void Jump()
    {
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _isGrounded = false;
        _animator?.SetTrigger("jump");
    }
    
    private void OnCollisionEnter(Collision collision)
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


    /// <summary>
    /// 
    /// </summary>
    /// <param name="targetPosition"></param>
    void Attack(Vector3 targetPosition)
    {
        if (_currentWeaponData == null)
        {
            Debug.LogWarning("[Player] 무기가 설정되지 않았습니다.");
            return;
        }

        
        _animator?.SetTrigger("attack");

        string weaponName = _currentWeaponData.WeaponName;


        if (weaponName.Contains("Close"))
        {
            float range = _currentWeaponData.range;
            float angle = 90f;
            
            Collider[] hits = Physics.OverlapSphere(transform.position, range);
            Vector3 forward = transform.forward;

            foreach (Collider col in hits)
            {
                IEnemy enemy = col.GetComponent<IEnemy>();
                if (enemy != null)
                {
                    Vector3 direction = (col.transform.position - transform.position).normalized;

                    if (Vector3.Angle(forward, direction) < angle)
                    {
                        enemy.TakeDamage(_attackPower);

                        Vector3 hitPos = col.ClosestPoint(transform.position);
                        // TODO:
                        //Instantiate(_currentWeaponData.HitEffectPrefab, hitPos, Quaternion.identity);
                    }

                    Debug.Log($"hit {col.name}");
                }
            }
        }
        else
        {
            // Ray ray = new Ray(transform.position + Vector3.up, (targetPosition - transform.position).normalized);
            // if (Physics.Raycast(ray, out RaycastHit hit, _currentWeaponData.range))
            // {
            //     IEnemy enemy = hit.collider.GetComponent<IEnemy>();
            //     if (enemy != null)
            //     {
            //         enemy.TakeDamage(_attackPower);
            //
            //         // 공격 이펙트 생성
            //         if (_currentWeaponData.HitEffectPrefab != null)
            //         {
            //             Instantiate(_currentWeaponData.HitEffectPrefab, hit.point, Quaternion.identity);
            //         }
            //     }
            // }
            Debug.Log("No collider in range");
        }

        _curBattery -= _currentWeaponData.BatteryUsage;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="additionalSpeed"></param>
    public void SetSpeedStatus(float additionalSpeed)
    {
        _moveSpeed += additionalSpeed;
        Debug.Log($"[Player] 이동속도 증가 → {_moveSpeed}");
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
        _currentWeaponModel = Instantiate(weaponData.ModelPrefab, _weaponSocket);
        _currentWeaponModel.transform.localPosition = Vector3.zero;
        _currentWeaponModel.transform.localRotation = Quaternion.identity;

        // 공격력, 모션, 사거리 등 세팅
        _attackPower = weaponData.baseAttackPower;
        _currentWeaponData = weaponData;

        // 애니메이션 클립 전환
        if (_animator != null && !string.IsNullOrEmpty(weaponData.AttackAnimation))
        {
            _animator.runtimeAnimatorController = weaponData.AnimatorController;
        }

        Debug.Log($"[Player] 무기 초기화 완료: {weaponData.WeaponName}, 공격력: {_attackPower}");
    }

    IEnumerator BatteryReduction()
    {
        GameManager gm = GameManager.Instance;
        while (true)
        {
            yield return new WaitForSeconds(_batteryReductionTerm[_curPlayerStatus]);
            _curBattery -= _batteryReductionAmount[_curPlayerStatus];
            gm.SetGameScore();
        }
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


