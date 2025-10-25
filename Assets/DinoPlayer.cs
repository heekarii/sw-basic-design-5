using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class DinoPlayer : MonoBehaviour
{
    public float jumpForce = 12f;
    public LayerMask groundMask; // Ground가 포함된 레이어 체크

    Rigidbody2D rb;
    bool isGrounded;

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Update()
    {
        CheckGround();
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void CheckGround()
    {
        var origin = (Vector2)transform.position + Vector2.down * 0.6f;
        var hit = Physics2D.Raycast(origin, Vector2.down, 0.1f, groundMask);
        isGrounded = hit.collider != null;
    }
}
