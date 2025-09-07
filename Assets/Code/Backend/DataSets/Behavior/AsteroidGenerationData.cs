using UnityEngine;

[System.Serializable]
public class AsteroidGenerationData : MonoBehaviour
{
    public string name;
    public CelestialEnvironment type;
    public OrbitParams orbitParams;
}