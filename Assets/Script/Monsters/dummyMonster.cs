using UnityEngine;

public class dummyMonster : MonoBehaviour, IEnemy
{
    [SerializeField] private float _curHp;
    
    void Start()
    {
        _curHp = 100;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(float damage)
    {
        _curHp -= damage;
    }
    
    public void DropScrap(int amount)
    {
        
    }
}
