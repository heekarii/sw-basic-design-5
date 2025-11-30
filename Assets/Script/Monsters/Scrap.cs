using UnityEngine;
using UnityEngine.Serialization;

public class Scrap : MonoBehaviour
{
    [SerializeField] private int _amount;
    [SerializeField] private Transform _player;
    [SerializeField] private bool _isPicked = false;
    private GameManager _gm;
    public bool IsPicked => _isPicked;
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

            _gm.AddScrap(_amount);

            // 사운드만 따로 띄우고
            AudioSource audio = GetComponent<AudioSource>();
            if (audio != null && audio.clip != null)
                AudioSource.PlayClipAtPoint(audio.clip, transform.position);

            Destroy(gameObject);  // 바로 파괴해도 소리는 새로 생성된 오브젝트에서 끝까지 남음
        }
    }
    public void InitScrap(int amount)
    {
        _amount = amount;
    }
}
