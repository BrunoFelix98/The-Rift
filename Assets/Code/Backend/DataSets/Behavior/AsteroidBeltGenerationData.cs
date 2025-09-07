using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AsteroidBeltGenerationData : MonoBehaviour
{
    public string name;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
    public List<AsteroidGenerationData> asteroids;
}