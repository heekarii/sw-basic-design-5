using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Scriptable Objects/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public GameObject ModelPrefab;
    public RuntimeAnimatorController AnimatorController;
    public string AttackAnimation;
    public float baseAttackPower;
    public float BatteryUsage;
    public int Bullets;
    public float range;
    public GameObject HitEffectPrefab;
}
