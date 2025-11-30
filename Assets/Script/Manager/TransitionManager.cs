using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TransitionManager : Singleton<TransitionManager>
{
    private Player _player;
    public StationManager CurStationManager;
    private Repair _lastRepairSource;
    
    protected override void Awake()
    {
        base.Awake();
#if !UNITY_EDITOR
        // TransitionManager는 씬 전환 중에도 유지되어야 하므로 파괴되지 않도록 설정
        DontDestroyOnLoad(gameObject);
#else
        // 에디터에서 디버깅 시에도 유지
        DontDestroyOnLoad(gameObject);
#endif
#if UNITY_2023_2_OR_NEWER
        _player = UnityEngine.Object.FindFirstObjectByType<Player>();
#else
        _player = FindObjectOfType<Player>();
#endif
    }

    #region SceneLoad
    private void LoadSceneWithLoading(string targetScene, LoadSceneMode mode)
    {
        PlayerPrefs.SetString("LOAD_SCENE_NAME", targetScene);
        PlayerPrefs.SetInt("LOAD_SCENE_MODE", (int)mode);
        
        SceneManager.LoadScene("LoadingScene", LoadSceneMode.Additive);
    }

    #endregion

    #region Lobby Management

    public void StartGame(int weaponType)
    {
        SetSceneActive("MainUIScene", false);
        LoadSceneWithLoading("Map_SCENE", LoadSceneMode.Single);
        
        GameManager.Instance.SetWeaponType(weaponType);
        
    }

    #endregion

    #region GameEnd Management

    public void UnloadGameScenes()
    {
        SceneManager.UnloadSceneAsync("Map_SCENE");
        LoadSceneWithLoading("Escape Scene", LoadSceneMode.Additive);
    }

    #endregion
    
    #region Repair Station Management
    
    public void RegisterStationManager(StationManager manager)
    {
        CurStationManager = manager;
        // StationManager 등록 시 마지막으로 EnterRepairStation을 호출한 오브젝트 정보를 전달
        if (_lastRepairSource != null && CurStationManager != null)
        {
            CurStationManager.SetRepairSource(_lastRepairSource);
        }
    }

    // 호출한 Repair 컴포넌트를 전달받아 추적합니다.
    public void EnterRepairStation(Repair source)
    {
        _lastRepairSource = source;
        
        LoadSceneWithLoading("RepairShopUIscene", LoadSceneMode.Additive);
        Cursor.visible = true;


        if (CurStationManager != null && _lastRepairSource != null)
        {
            CurStationManager.SetRepairSource(_lastRepairSource);
        }

        if (_player != null)
            _player.EnterStationaryState();
        else
            Debug.LogWarning("[TransitionManager] EnterRepairStation: Player가 할당되지 않았습니다.");
        
    }

    // ▣ Repair → 미니게임
    public void StartMiniGame(string gameSceneName)
    {
        SetSceneActive("RepairShopUIscene", false);
        LoadSceneWithLoading(gameSceneName, LoadSceneMode.Additive);
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

    #endregion
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
