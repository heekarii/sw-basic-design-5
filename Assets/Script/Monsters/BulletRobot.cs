using UnityEngine;
using UnityEngine.AI;

public class BulletRobot : MonoBehaviour, IEnemy
{
    [Header("Monster Status")]
    [SerializeField] private float _maxHp = 80f;
    [SerializeField] private float _curHp;
    [SerializeField] private float _attackCooldown = 5.0f;
    [SerializeField] private float _aggravationRange = 15.1f;
    [SerializeField] private float _attackRange = 12.1f;      // 사거리(= 원뿔 길이와 같게 맞춰도 OK)
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private float _lookAtTurnSpeed = 8f;
    [SerializeField] private Player _player;

    [Header("Bolt Setting")]
    [SerializeField] private Transform _muzzleVisual;
    [SerializeField] private Transform _muzzleDetect;
    [SerializeField] private float _coneLength = 12.1f;   // 원뿔 길이
    [SerializeField] private float _coneRadius = 5.0f;    // 밑면 반지름
    [SerializeField] private float _tickDamage = 1.2f;
    [SerializeField] private float _damageInterval = 0.1f;
    [SerializeField] private float _attackingTime = 5.0f;
    [SerializeField] private float _boltSpeed = 20f;
    [SerializeField] private GameObject _boltPrefab;
    [SerializeField] private int _boltsPerSecond = 24;       // 초당 생성 개수

    // coneAngleDeg는 코드에서 계산해서 씀 (인스펙터에 안 보이게 private만)
    private float _coneAngleDeg = 0.0f;

    private bool _isAttacking = false;
    private bool _isCoolingDown = false;
    private NavMeshAgent _agent;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_player == null) _player = FindObjectOfType<Player>();
        _curHp = _maxHp;

        if (_agent == null || _player == null)
        {
            Debug.LogError("[BulletRobot] 필수 컴포넌트 누락");
            enabled = false; 
            return;
        }

        // Agent 기본
        _agent.speed = _moveSpeed;
        _agent.stoppingDistance = _attackRange;
        _agent.updateRotation = true;
        _agent.autoBraking = true;

        // 밑면 반지름 / 길이로 원뿔 각도 계산
        _coneAngleDeg = Mathf.Atan(_coneRadius / _coneLength) * Mathf.Rad2Deg;

        // 시작 위치 NavMesh 보정
        if (!_agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            _agent.Warp(hit.position);
    }

    private void Update()
    {
        if (_player == null || _agent == null) return;

        // NavMesh 이탈 복구
        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                return;
        }

        // 사망 체크
        if (_curHp <= 0f) { Die(); return; }

        float worldDist = Vector3.Distance(transform.position, _player.transform.position);
        bool hasLOS = HasLineOfSight();

        // 👉 인식 범위 안에 있으면 항상 플레이어를 수평으로 쳐다봄
        if (worldDist <= _aggravationRange)
            LookAtPlayer();

        // 공격 진입 조건
        if (!_isAttacking && !_isCoolingDown &&
            worldDist <= _attackRange && hasLOS &&
            _agent.velocity.sqrMagnitude < 0.1f)
        {
            _agent.isStopped = true;
            StartCoroutine(AttackRoutine());
            return;
        }

        // 추적
        if (!_isAttacking && worldDist <= _aggravationRange && hasLOS)
        {
            _agent.isStopped = false;
            Vector3 targetPos = _player.transform.position;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
            {
                if (!_agent.hasPath || (navHit.position - _agent.destination).sqrMagnitude > 0.25f)
                    _agent.SetDestination(navHit.position);
            }
        }
        else if (!_isAttacking)
        {
            _agent.isStopped = true;
            if (_agent.hasPath) _agent.ResetPath();
        }
    }

    // 🔁 항상 수평으로 플레이어 바라보는 함수 (네가 원래 쓰던 역할)
    private void LookAtPlayer()
    {
        if (_player == null) return;

        Vector3 dir = _player.transform.position - transform.position;
        dir.y = 0f; // 수평만

        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            _lookAtTurnSpeed * Time.deltaTime
        );
    }

    private System.Collections.IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        // 이동 정지
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.ResetPath();

        float elapsed = 0f;
        float tickTimer = 0f;
        float spawnTimer = 0f;
        float spawnInterval = (_boltsPerSecond > 0) ? (1f / _boltsPerSecond) : 999f;

        Transform tDetect = _muzzleDetect != null ? _muzzleDetect : transform;
        Transform tVisual = _muzzleVisual != null ? _muzzleVisual : tDetect;

        while (elapsed < _attackingTime)
        {
            if (_player == null) break;

            // 👉 공격 중에도 계속 수평 회전해서 플레이어를 바라보게
            LookAtPlayer();

            // 시각용 볼트 스폰
            spawnTimer += Time.deltaTime;
            while (spawnTimer >= spawnInterval)
            {
                spawnTimer -= spawnInterval;
                SpawnVisualBolt(tVisual);
            }

            // 판정 틱
            tickTimer += Time.deltaTime;
            if (tickTimer >= _damageInterval)
            {
                tickTimer = 0f;
                ConeDamageTick(tDetect);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 쿨다운
        _isAttacking = false;
        _isCoolingDown = true;
        _agent.isStopped = false;
        yield return new WaitForSeconds(_attackCooldown);
        _isCoolingDown = false;
    }

    // ===== 시각용 볼트 스폰 =====
    private void SpawnVisualBolt(Transform muzzle)
    {
        if (_boltPrefab == null || muzzle == null) return;

        // 원뿔 내부 방향
        Vector3 dir = RandomDirectionInCone(muzzle.forward, _coneAngleDeg, muzzle);

        GameObject go = Instantiate(_boltPrefab, muzzle.position, Quaternion.LookRotation(dir));

        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            // Unity 6000 이후엔 linearVelocity, 구버전이면 velocity 쓰면 됨
            rb.linearVelocity = dir * _boltSpeed;
        }

        // 볼트가 공격 범위를 딱 도달할 정도의 시간 후 자동 삭제
        float life = (_coneLength / _boltSpeed) + 0.05f;
        Destroy(go, life);
    }

    // 원뿔 내부에서 임의 방향 벡터 생성
    private Vector3 RandomDirectionInCone(Vector3 forward, float coneAngleDeg, Transform basis)
    {
        float yaw = Random.Range(-coneAngleDeg, coneAngleDeg);
        float pitch = Random.Range(-coneAngleDeg, coneAngleDeg);

        Quaternion rotYaw = Quaternion.AngleAxis(yaw, basis.up);
        Vector3 yRot = rotYaw * forward;
        Quaternion rotPitch = Quaternion.AngleAxis(pitch, Vector3.Cross(basis.up, yRot).normalized);
        return (rotPitch * yRot).normalized;
    }

    // ===== 판정 틱(거리 1당 5% 데미지 감소) =====
    private void ConeDamageTick(Transform t)
    {
        if (_player == null) return;

        Vector3 pos = _player.transform.position;

        if (IsPointInsideCone(pos, t, _coneAngleDeg, _coneLength))
        {
            float dist = Vector3.Distance(t.position, pos);
            float falloff = Mathf.Max(0f, 1f - 0.05f * dist); // 거리 1m당 5% 감소
            float dmg = _tickDamage * falloff;

            _player.TakeDamage(dmg);
        }
    }

    private bool IsPointInsideCone(Vector3 point, Transform t, float angleDeg, float length)
    {
        Vector3 apex = t.position;
        Vector3 axis = t.forward.normalized;

        Vector3 v = point - apex;
        float z = Vector3.Dot(v, axis);
        if (z <= 0f || z > length) return false;

        float radiusAtZ = Mathf.Tan(angleDeg * Mathf.Deg2Rad) * z;
        Vector3 radial = v - axis * z;
        float r = radial.magnitude;

        return r <= radiusAtZ;
    }

    // ===== 유틸 =====
    private bool HasLineOfSight()
    {
        if (_player == null) return false;
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = _player.transform.position + Vector3.up * 1.0f;

        Vector3 dir = (target - origin);
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        dir /= dist;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                var hits = Physics.RaycastAll(origin, dir, dist, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider.transform.IsChildOf(transform)) continue;
                    return h.collider.GetComponentInParent<Player>() != null;
                }
                return true;
            }
            return hit.collider.GetComponentInParent<Player>() != null;
        }
        return true;
    }

    public void TakeDamage(float dmg)
    {
        _curHp -= dmg;
        if (_curHp <= 0f) Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    // Scene 뷰에서 원뿔 시각화
    private void OnDrawGizmosSelected()
    {
        Transform t = _muzzleDetect != null ? _muzzleDetect : transform;
        float ang = _coneAngleDeg;
        float len = _coneLength;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(t.position, t.position + t.forward * len);

        int rings = 4;
        for (int i = 1; i <= rings; i++)
        {
            float z = len * i / rings;
            float radius = Mathf.Tan(ang * Mathf.Deg2Rad) * z;
            DrawCircle(t.position + t.forward * z, t.up, t.forward, radius, Color.Lerp(Color.red, Color.red, i / (float)rings));
        }
    }

    private void DrawCircle(Vector3 center, Vector3 up, Vector3 forward, float radius, Color color, int segments = 28)
    {
        Gizmos.color = color;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;

        Vector3 prev = center + right * radius;
        float step = 360f / segments;
        for (int i = 1; i <= segments; i++)
        {
            Quaternion q = Quaternion.AngleAxis(step * i, forward);
            Vector3 next = center + (q * right) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
