using UnityEngine;

public class WeaponManager
{
    public WeaponData[] WeaponList;

    public WeaponManager(WeaponData[] list)
    {
        WeaponList = list;
    }

    public WeaponData GetWeapon(int level)
    {
        return WeaponList[Mathf.Clamp(level, 0, WeaponList.Length - 1)];
    }
}

