using UnityEngine;

[CreateAssetMenu(fileName = "NewStar", menuName = "Game Data/Star")]
public class StarSO : ScriptableObject
{
    public string starName;
    public GameObject prefabReference;
    public OrbitParams orbitParams;
}