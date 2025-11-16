using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TransitionManager : Singleton<TransitionManager>
{
    protected override void Awake()
    {
        base.Awake();
    }
    
    // ▣ Map → Repair 이동
    public void EnterRepairStation()
    {
        // 1) Repair_main 로드
        SceneManager.LoadScene("Repair_main", LoadSceneMode.Additive);
        Cursor.visible = true;

        // 2) Map_SCENE 비활성화
        SetSceneActive("Map_SCENE", false);
    }

    // ▣ Repair → 미니게임
    public void StartMiniGame(string gameSceneName)
    {
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Additive);
    }

    // ▣ 미니게임 완료 → Repair 복귀
    public void EndMiniGame(string gameSceneName)
    {
        SceneManager.UnloadSceneAsync(gameSceneName);        
        // Repair_main은 그대로 있음
    }

    // ▣ Repair 종료 → Map 복귀
    public void ExitRepairStation()
    {
        SceneManager.UnloadSceneAsync("Repair_main");
        SetSceneActive("Map_SCENE", true);
    }

    // 씬 활성/비활성화 도우미
    private void SetSceneActive(string sceneName, bool active)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return;

        foreach (GameObject obj in scene.GetRootGameObjects())
        {
            obj.SetActive(active);
        }
    }
}
