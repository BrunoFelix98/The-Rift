using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RevisedAsteroidBeltData", menuName = "Universe/Revised Asteroid Belt Data", order = 8)]
public class RevisedAsteroidBeltData : ScriptableObject
{
    public string beltName;
    public List<RevisedAsteroidData> asteroids = new();
}