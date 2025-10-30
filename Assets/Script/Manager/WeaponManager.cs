using UnityEngine;

public class WeaponManager : Singleton<WeaponManager>
{
    [SerializeField] private WeaponData[] weaponList;
    private int _curWeaponIndex;
    private Player _player;

    protected override void Awake()
    {
        base.Awake();
        _player = FindObjectOfType<Player>();
    }

    // 무기 장착
    public void EquipWeapon(int index)
    {
        if (_player == null || index < 0 || index >= weaponList.Length)
            return;
        
        _player.InitWeapon(weaponList[index]);
        _curWeaponIndex = index;
        Debug.Log($"[WeaponManager] {weaponList[index].WeaponName} 장착 완료");
    }

    // 무기 강화
    public void UpgradeWeapon(float powerDelta)
    {
        if (_player == null)
            return;

        if (_curWeaponIndex is >= 0 and < 3)
        {
            _curWeaponIndex++;
            _player.InitWeapon(weaponList[_curWeaponIndex]);
        }
        
        Debug.Log($"[WeaponManager] 공격력 +{powerDelta} 강화됨");
    }
}
