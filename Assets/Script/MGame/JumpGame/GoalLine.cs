using UnityEngine;

public class GoalLine : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Obstacle"))
        {
            GameManager.Instance.OnObstaclePassed(); 
            // Debug.Log("[GoalLine] 장애물 통과! 성공 +1");
        }
    }
}
