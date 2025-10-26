using UnityEngine;

public class JumpPlayer : MonoBehaviour
{
    [Header("점프 설정")]
    public float jumpForce = 12f;
    public LayerMask groundMask;

    private Rigidbody2D rb;
    private bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 점프 입력 처리
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isGrounded = false;  // 점프 직후 공중 상태로 변경
        }

        CheckGround();
    }

    void CheckGround()
    {
        // 플레이어 아래쪽 중앙에서 살짝 더 아래로 레이 쏘기
        Vector2 origin = new Vector2(transform.position.x, transform.position.y - 0.7f);

        // 거리 0.2f 정도로 충분히 여유 있게
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 0.2f, groundMask);

        // 충돌 감지 시만 접지 판정
        if (hit.collider != null)
            isGrounded = true;
        else
            isGrounded = false;

        // Scene 뷰에서 디버그용 Ray 표시
        Debug.DrawRay(origin, Vector2.down * 0.2f, isGrounded ? Color.green : Color.red);
    }
}