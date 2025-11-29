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
#if UNITY_2023_2_OR_NEWER
        _player = UnityEngine.Object.FindFirstObjectByType<Player>();
#else
        _player = FindObjectOfType<Player>();
#endif
    }

    public void RegisterStationManager(StationManager manager)
    {
        CurStationManager = manager;
    }
    
    // ▣ Map → Repair 이동
    public void EnterRepairStation()
    {
        // 1) Repair_main 로드
        SceneManager.LoadScene("RepairShopUIscene", LoadSceneMode.Additive);
        Cursor.visible = true;
        
        // 2) Map_SCENE 비활성화
        if (_player != null)
            _player.EnterStationaryState();
        else
            Debug.LogWarning("[TransitionManager] EnterRepairStation: Player가 할당되지 않았습니다.");
        SetSceneActive("Map_SCENE", false);
    }

    // ▣ Repair → 미니게임
    public void StartMiniGame(string gameSceneName)
    {
        SetSceneActive("RepairShopUIscene", false);
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Additive);
    }

    // ▣ 미니게임 완료 → Repair 복귀
    public void EndMiniGame(string gameSceneName, bool isSuccess)
    {
        // 언로드하고 Repair UI를 활성화
        SceneManager.UnloadSceneAsync(gameSceneName);
        SetSceneActive("RepairShopUIscene", true);

        // 결과를 현재 등록된 StationManager에 전달 (null 검사)
        if (CurStationManager != null)
        {
            CurStationManager.ShowEndingPage(isSuccess);
        }
        else
        {
            Debug.LogWarning("[TransitionManager] CurStationManager가 없습니다. 미니게임 결과를 전달할 수 없습니다.");
        }
        // Repair_main은 그대로 있음
    }

    // ▣ Repair 종료 → Map 복귀
    public void ExitRepairStation()
    {
        SceneManager.UnloadSceneAsync("RepairShopUIscene");
        Cursor.visible = false; 
        SetSceneActive("Map_SCENE", true);
        if (_player != null)
            _player.ExitStationaryState();
        else
            Debug.LogWarning("[TransitionManager] ExitRepairStation: Player가 할당되지 않았습니다.");
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
