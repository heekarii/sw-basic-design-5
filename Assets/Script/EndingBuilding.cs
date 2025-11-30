using UnityEngine;
using UnityEngine.UI;

public class EndingBuilding : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private bool _isActivated = false;
    [SerializeField] private GameObject _key;

    [Header("UI Elements")] 
    [SerializeField] private Image _checkImage;

    [SerializeField] private Button _escapeButton;
    [SerializeField] private Button _backButton;
    
    private int _index = 0;
    private Player _player;
    private GameManager _gm;
    private Vector3 _keyPos = new Vector3(0, 0, 0);

    private void Start()
    {
        _gm = GameManager.Instance;
        _player = FindAnyObjectByType<Player>();
        _escapeButton?.onClick.AddListener(OnEscapeButton);
        _backButton?.onClick.AddListener(OnBackButton);
    }
    
    private void OnCollisionEnter(Collision col)
    {
        if (_isActivated && col.gameObject.CompareTag("Player"))
        {
            _player.EnterStationaryState();
            Cursor.visible = true;
            _checkImage.gameObject.SetActive(true);
        }
    }

    private void OnEscapeButton()
    {
        if (_gm.HasKey && _isActivated)
        {
            Debug.Log("Escape Key");
            TransitionManager.Instance.UnloadGameScenes();
        }
    }
    
    private void OnBackButton()
    {
        if (_isActivated)
        {
            _checkImage.gameObject.SetActive(false);
            _player.ExitStationaryState();
            Debug.Log("Player Exit Stationary State");
            Cursor.visible = false;
        }
    }
    
    public void SetActivate(bool state, int index)
    {
        _isActivated = state;
        _index = index;
        SetkeyPos();
        if (_isActivated)
        {
            Instantiate(_key, _keyPos, _key.transform.rotation);
        }
    }

    private void SetkeyPos()
    {
        switch (_index)
        {
            case 0: _keyPos = new Vector3(-39.25f, 34.395f, 165.72f); break;
            case 1: _keyPos = new Vector3(-19.3f, 34.395f, 120.67f); break;
            case 2: _keyPos = new Vector3(-3.8f, 34.395f, 2.3f); break;
            default: break;
        }
    }
}
