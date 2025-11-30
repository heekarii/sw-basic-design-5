using UnityEngine;

public class JumpPlayer : MonoBehaviour
{
    [Header("점프 설정")]
    public float jumpForce = 17f;
    public LayerMask groundMask;

    [SerializeField] private AudioSource _jumpAudio;
    [SerializeField] private AudioSource _walkAudio;
    [SerializeField] private AudioSource _collisionAudio;

    private MJumpGameManager _jm;
    private Rigidbody2D rb;
    private bool isGrounded = false;

    void Start()
    {
        _jm = FindObjectOfType<MJumpGameManager>();
        rb = GetComponent<Rigidbody2D>();

        // 시작 시 모든 소리를 정리
        _collisionAudio.Stop();
        _jumpAudio.Stop();
        _walkAudio.Stop();

        // 걷는 소리는 루프 추천 (Inspector에서 Loop 체크)
        // _walkAudio.loop = true; // 필요하면 직접 켜기
    }

    void Update()
    {
        if (_jm == null) return;

        // --- 점프 ---
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;

            _walkAudio.Stop();    // 점프 순간 걷기 소리 제거
            _jumpAudio.Play();    // 점프 소리 재생
        }
        

        CheckGround();

        // --- 걷기 상태 판단 ---
        bool isWalkingNow = isGrounded;

        // 걷기 "시작" 순간
        if (isWalkingNow)
        {
            _jumpAudio.Stop();
            if (!_walkAudio.isPlaying)
                _walkAudio.Play();
        }

        // 걷기 "종료" 순간 (공중 / 게임 끝 / 착지 X)
        if (!isWalkingNow)
        {
            if (_walkAudio.isPlaying)
                _walkAudio.Stop();
        }
    }

    // --- 바닥 체크 ---
    void CheckGround()
    {
        Vector2 origin = new Vector2(transform.position.x, transform.position.y - 0.7f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f, groundMask);

        isGrounded = (hit.collider != null);

        Debug.DrawRay(origin, Vector2.down * 0.2f, isGrounded ? Color.green : Color.red);
    }

    // --- 장애물 충돌 ---
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Obstacle"))
        {
            _collisionAudio.Play();
            // GameManager에 충돌 알림
            _jm.OnPlayerHitObstacle();
        }
    }
}
