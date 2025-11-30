using UnityEngine;

public class KeyCard : MonoBehaviour
{
    private void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.CompareTag("Player"))
        {
            GameManager.Instance.SetHasKey(true);
            Destroy(this.gameObject);
        }
    }
}
