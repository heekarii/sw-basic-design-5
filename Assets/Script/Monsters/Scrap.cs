using UnityEngine;
using UnityEngine.Serialization;

public class Scrap : MonoBehaviour
{
    [SerializeField] private int _amount;
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
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (_isPicked) return;
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
