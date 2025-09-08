using System;
using UnityEngine;

[Serializable]
public class GalaxyData : MonoBehaviour
{
    public string galaxyName;
    public int dustParticles;
    public GalaxySO data;

    public void Initialize(GalaxySO so)
    {
        data = so;
    }
}