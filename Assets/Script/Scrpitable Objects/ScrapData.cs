using UnityEngine;

[CreateAssetMenu(fileName = "ScrapData", menuName = "Scriptable Objects/ScrapData")]
public class ScrapData : ScriptableObject
{
    public int Amount;
    public GameObject ScrapPrefab;
}
