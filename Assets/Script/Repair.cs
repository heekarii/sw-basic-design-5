using UnityEngine;
using UnityEngine.SceneManagement;

public class Repair : MonoBehaviour
{
    void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            SceneManager.LoadScene("Repair_main", LoadSceneMode.Additive);
            Scene mapScene = SceneManager.GetSceneByName("Map_SCENE");
            
            foreach (GameObject go in mapScene.GetRootGameObjects())
            {
                go.SetActive(false);
            }
        }
        
        
    }
}
