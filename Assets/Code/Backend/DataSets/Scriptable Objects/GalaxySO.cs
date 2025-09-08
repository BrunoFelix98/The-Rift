using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGalaxy", menuName = "Game Data/Galaxy")]
public class GalaxySO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string galaxyName;
    public List<SystemSO> systems = new List<SystemSO>();
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }

    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
