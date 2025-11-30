using UnityEngine;

public class EndingBuilding : MonoBehaviour
{
    [SerializeField] private bool _isActivated = false;
    [SerializeField] private GameObject _key;
    private int _index = 0;
    private GameManager _gm;
    private Vector3 _keyPos = new Vector3(0, 0, 0);

    private void Start()
    {
        _gm = GameManager.Instance;
    }
    
    private void OnCollisionEnter(Collision col)
    {
        if (_isActivated)
        {
            if (col.gameObject.CompareTag("Player") && _gm.HasKey == true)
            {
                Debug.Log("탈출 성공 !");
            }
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
