using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAsteroid", menuName = "Game Data/Asteroid")]
public class AsteroidSO : ScriptableObject
{
    public string asteroidName;
    public List<ResourceSO> resources = new List<ResourceSO>();
    public CelestialType type;
}
