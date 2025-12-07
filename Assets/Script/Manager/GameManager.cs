using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResourceManager
{
    public int Scrap { get; private set; }
    public float Battery { get; private set; }

    public ResourceManager()
    {
        Scrap = 0;
        Battery = 100f;
    }

    public void AddScrap(int amount)
    {
        Scrap = Mathf.Max(0, Scrap + amount);
    }

    public void DecreaseScrap(int amount)
    {
        Scrap = Mathf.Max(0, Scrap - amount);
    }

    public void DecreaseBattery(float amount)
    {
        Battery = Mathf.Max(0, Battery - amount);
    }
}

public class PlayerStatusManager
{
    public PlayerStatus CurrentStatus { get; private set; }

    public void UpdateStatus(Player player)
    {
        if (player == null) return;
        CurrentStatus = player.GetStatus();
    }
}

public class GameManager : Singleton<GameManager>
{
    [Header("Game Flags")]
    [SerializeField] private bool _hasKey = false;

    [Header("References")]
    public Player Player;         
    public int WeaponType;         

    [Header("Start Buildings")]
    public List<GameObject> Buildings;
    public List<GameObject> BuildingOutlines;
    private int _buildingToActivate = 0;

    [Header("Weapon Data")]
    [SerializeField] private WeaponData[] weaponList;

    private UIManager _uiManager;
    public ResourceManager Resources { get; private set; }
    public PlayerStatusManager StatusManager { get; private set; }
    public WeaponManager WeaponDB { get; private set; }

    private bool _initialized = false;

    protected override void Awake()
    {
        // 중복 인스턴스가 존재하면 데이터만 전달하고 씬 인스턴스는 제거
        if (Instance != null && Instance != this)
        {
            // 씬 GM → Persistent GM에게 데이터만 전달
            Instance.AbsorbSceneDataFrom(this);

            // 씬에 있는 중복 인스턴스는 파괴해서 Persistent Singleton이 유지되도록 함
            Destroy(gameObject);
            return;
        }
        else
        {
            // 내가 싱글톤인 경우에만 Awake 초기화
            base.Awake(); // DontDestroyOnLoad 적용
            Resources = new ResourceManager();
            StatusManager = new PlayerStatusManager();
            WeaponDB = new WeaponManager(weaponList);
        }
    }



    private void Start()
    {
        if (SceneManager.GetActiveScene().name=="Map_SCENE")
            ActivateBuildingOnStart();
        CachePlayerIfNeeded();
        _initialized = true;

        if (_uiManager != null && StatusManager.CurrentStatus != null)
            _uiManager.Refresh(StatusManager.CurrentStatus, Resources.Scrap);
    }

    private void Update()
    {
        if (!_initialized) return;

        if (Player == null)
            CachePlayerIfNeeded();

        StatusManager.UpdateStatus(Player);

        if (_uiManager != null && StatusManager.CurrentStatus != null)
            _uiManager.Refresh(StatusManager.CurrentStatus, Resources.Scrap);
    }

    private void CachePlayerIfNeeded()
    {
        if (Player != null) return;

        Player = FindAnyObjectByType<Player>();
        if (Player == null)
            Debug.LogWarning("[GameManager] Player not found in scene.");
    }

    private void ActivateBuildingOnStart()
    {
        if (Buildings == null || Buildings.Count == 0) return;
        if (BuildingOutlines == null || BuildingOutlines.Count == 0) return;

        _buildingToActivate = Random.Range(0, Buildings.Count);

        EndingBuilding buildingComponent = Buildings[_buildingToActivate].GetComponent<EndingBuilding>();
        if (buildingComponent != null)
            buildingComponent.SetActivate(true, _buildingToActivate);

        BuildingOutlines[_buildingToActivate].SetActive(true);
    }

    // -------------------- PUBLIC API --------------------

    public void RegisterUIManager(UIManager uiManager)
    {
        _uiManager = uiManager;
    }

    public PlayerStatus GetLatestStatus() => StatusManager.CurrentStatus;

    public bool HasKey => _hasKey;
    public void SetHasKey(bool v) => _hasKey = v;

    public void AddScrap(int amount) => Resources.AddScrap(amount);
    public void DecreaseScrap(int amount) => Resources.DecreaseScrap(amount);
    public int ScrapAmount => Resources.Scrap;

    public void DecreaseBattery(float amount) => Resources.DecreaseBattery(amount);

    public void SetWeaponType(int type) => WeaponType = type;

    public void AbsorbSceneDataFrom(GameManager sceneGM)
    {
        this.Buildings = sceneGM.Buildings;
        this.BuildingOutlines = sceneGM.BuildingOutlines;
    }
}
