using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAsteroidBelt", menuName = "Game Data/Asteroid Belt")]
public class AsteroidBeltSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string beltName;
    public List<AsteroidSO> asteroids = new List<AsteroidSO>();
    public CelestialEnvironment type;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }

    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
