using UnityEngine;

[CreateAssetMenu(fileName = "NewStar", menuName = "Game Data/Star")]
public class StarSO : ScriptableObject, IOrbitable, IHasPrefab
{
    public string starName;
    public OrbitParams orbitParams;

    public GameObject prefabReference { get; set; }

    public OrbitParams GetOrbitParams()
    {
        return orbitParams;
    }
    public void InitializeOrbit(OrbitParams orbitParams) => this.orbitParams = orbitParams;
}