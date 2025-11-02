using UnityEngine;

public class GroundScroller : MonoBehaviour
{
    public Transform groundA;
    public Transform groundB;
    public float speed = 5f;

    float width;

    void Start()
    {
        width = groundA.GetComponent<SpriteRenderer>().bounds.size.x;
    }

    void Update()
    {
        Move(groundA);
        Move(groundB);
        RepositionIfNeeded();
    }

    void Move(Transform t)
    {
        t.position += Vector3.left * speed * Time.deltaTime;
    }

    void RepositionIfNeeded()
    {
        if (groundA.position.x <= -width) groundA.position += Vector3.right * width * 2f;
        if (groundB.position.x <= -width) groundB.position += Vector3.right * width * 2f;
    }
}
