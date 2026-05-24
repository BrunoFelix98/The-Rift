using UnityEngine;

[CreateAssetMenu(fileName = "RevisedGalaxyData", menuName = "Universe/Revised Galaxy Data", order = 1)]
public class RevisedGalaxyData : ScriptableObject
{
    public string galaxyName;
    public int dustParticles;

    [HideInInspector] public int remainingDust; // runtime value, not serialized
}