using UnityEngine;

public class RevisedNebulaData : ScriptableObject
{
    public string nebulaName;
    public Color nebulaColor;
    public float density; // Could influence visibility, navigation, etc.
    public Vector3 size;  // Dimensions or scale of the nebula
    public string description;
    public OrbitParams orbitParams;
}