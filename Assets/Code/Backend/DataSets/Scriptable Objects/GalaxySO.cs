using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGalaxy", menuName = "Game Data/Galaxy")]
public class GalaxySO : ScriptableObject
{
    public string galaxyName;
    public List<SystemSO> systems = new List<SystemSO>();
    public GameObject prefabReference;
    public OrbitParams orbitParams;
}
