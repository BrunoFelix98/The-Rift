using UnityEngine;

[CreateAssetMenu(fileName = "NewNebula", menuName = "Game Data/Nebula")]
public class NebulaSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string nebulaName;
    public Color nebulaColor;
    public float density; // Could influence visibility, navigation, etc.
    public Vector3 size;  // Dimensions or scale of the nebula
    public string description;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }

    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}