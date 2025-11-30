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
        // 로딩씬에서 참고할 값 저장
        PlayerPrefs.SetString("LOAD_SCENE_NAME", targetScene);
        PlayerPrefs.SetInt("LOAD_SCENE_MODE", (int)mode);

        // 로딩 화면을 추가로 띄움(오버레이)
        // NOTE: Single로 로드하면 현재 씬이 언로드되어 GameManager/Player 등이 파괴되어 NullReference가 발생할 수 있습니다.
        //       따라서 LoadingScene은 Additive로 띄워 기존 씬을 유지한 상태에서 로딩 UX만 보여주도록 합니다.
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
        // 호출 출처(Repair 컴포넌트)를 저장
        _lastRepairSource = source;

        // 1) 로딩 씬을 Additive로 불러서 로딩 UX를 보여주고 이후에 목적 씬을 로드하게 합니다.
        //    이렇게 하면 현재 Map 씬이 즉시 언로드되지 않으므로 GameManager/Player가 파괴되어 발생하는 NRE를 방지합니다.
        LoadSceneWithLoading("RepairShopUIscene", LoadSceneMode.Additive);
        Cursor.visible = true;

        // 만약 StationManager가 이미 등록되어 있으면 즉시 출처 전달
        if (CurStationManager != null && _lastRepairSource != null)
        {
            CurStationManager.SetRepairSource(_lastRepairSource);
        }

        // Map_SCENE은 로딩이 끝나고 목적 씬이 활성화된 뒤 비활성화하는 것이 안전하지만,
        // 게임플레이상 즉시 비활성화가 필요하면 EnterStationaryState를 먼저 호출합니다.
        if (_player != null)
            _player.EnterStationaryState();
        else
            Debug.LogWarning("[TransitionManager] EnterRepairStation: Player가 할당되지 않았습니다.");

        // SetSceneActive("Map_SCENE", false); // 언로드/비활성화는 Loading->Repair 로드 완료 후 처리하는 로직(로더)에 맡기는 것이 안정적입니다.
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
