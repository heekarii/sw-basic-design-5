using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TransitionManager : Singleton<TransitionManager>
{
    private Player _player;
    public StationManager CurStationManager;
    protected override void Awake()
    {
        base.Awake();
        _player = FindObjectOfType<Player>();
    }

    public void RegisterStationManager(StationManager manager)
    {
        CurStationManager = manager;
    }
    
    // ▣ Map → Repair 이동
    public void EnterRepairStation()
    {
        // 1) Repair_main 로드
        SceneManager.LoadScene("Repair_main", LoadSceneMode.Additive);
        Cursor.visible = true;
        
        // 2) Map_SCENE 비활성화
        _player.EnterStationaryState();
        SetSceneActive("Map_SCENE", false);
    }

    // ▣ Repair → 미니게임
    public void StartMiniGame(string gameSceneName)
    {
        SetSceneActive("Repair_main", false);
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Additive);
    }

    // ▣ 미니게임 완료 → Repair 복귀
    public void EndMiniGame(string gameSceneName)
    {
        SceneManager.UnloadSceneAsync(gameSceneName);        
        SetSceneActive("Repair_main", true);
        // Repair_main은 그대로 있음
    }

    // ▣ Repair 종료 → Map 복귀
    public void ExitRepairStation()
    {
        SceneManager.UnloadSceneAsync("Repair_main");
        Cursor.visible = false; 
        SetSceneActive("Map_SCENE", true);
        _player.ExitStationaryState();
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
