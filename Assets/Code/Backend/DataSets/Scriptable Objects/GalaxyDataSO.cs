using UnityEngine;

[CreateAssetMenu(fileName = "NewGalaxyData", menuName = "Game Data/Galaxy Generation Data")]
public class GalaxyDataSO : ScriptableObject
{
    public int galaxyId;
    public string galaxyName;
    public int dustParticles;
    public int seed;
    public int maxSystems;
}
