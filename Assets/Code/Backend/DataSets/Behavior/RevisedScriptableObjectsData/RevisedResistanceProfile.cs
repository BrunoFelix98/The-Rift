using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class RevisedResistanceProfile
{
    [Range(0f, 100f)] public float EM;
    [Range(0f, 100f)] public float Thermal;
    [Range(0f, 100f)] public float Kinetic;
    [Range(0f, 100f)] public float Explosive;
}