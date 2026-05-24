using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RevisedAsteroidData", menuName = "Universe/Revised Asteroid Data", order = 9)]
public class RevisedAsteroidData : ScriptableObject
{
    public string asteroidName;
    public List<RevisedResourceData> resources = new();
}