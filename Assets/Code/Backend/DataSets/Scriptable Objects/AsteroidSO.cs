using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAsteroid", menuName = "Game Data/Asteroid")]
public class AsteroidSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string asteroidName;
    public List<ResourceSO> resources = new List<ResourceSO>();
    public CelestialEnvironment type;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }

    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
