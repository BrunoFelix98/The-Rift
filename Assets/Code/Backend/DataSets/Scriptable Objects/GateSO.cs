using UnityEngine;

[CreateAssetMenu(fileName = "NewGate", menuName = "Game Data/Gate")]
public class GateSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string gateName;

    // Reference to the connected system ScriptableObject
    public SystemSO connectedSystem;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }
    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}
