using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAsteroidBelt", menuName = "Game Data/Asteroid Belt")]
public class AsteroidBeltSO : ScriptableObject
{
    public string beltName;
    public List<AsteroidSO> asteroids = new List<AsteroidSO>();
    public CelestialType type;
}
