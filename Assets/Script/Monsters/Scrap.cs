using UnityEngine;
using UnityEngine.Serialization;

public class Scrap : MonoBehaviour
{
    [SerializeField] private int _amount;
    [SerializeField] private float _pickupRange;
    [SerializeField] private Transform _player;
    [SerializeField] private bool _isPicked = false;
    private GameManager _gm;
    void Start()
    {
        _player = GameObject.FindWithTag("Player").transform;
        _gm = GameManager.Instance;
    }
    
    void Update()
    {
        if (_isPicked || _player == null) return;
        
        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance <= _pickupRange)
        {
            _isPicked = true;
            // 스크랩 획득 처리
            _gm.AddScrap(_amount);
            Destroy(gameObject);
        }
    }

    public void InitScrap(int amount)
    {
        _amount = amount;
    }
}
