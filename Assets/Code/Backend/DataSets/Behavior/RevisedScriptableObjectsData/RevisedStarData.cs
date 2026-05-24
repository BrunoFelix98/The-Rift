using UnityEngine;

[CreateAssetMenu(fileName = "RevisedStarData", menuName = "Universe/Revised Star Data", order = 5)]
public class RevisedStarData : ScriptableObject
{
    public string starName;
    public float mass;
    public Color color;
    public Vector3 position; // Baked or generated
}