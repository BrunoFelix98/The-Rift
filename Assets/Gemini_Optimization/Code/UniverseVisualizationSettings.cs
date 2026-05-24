using UnityEngine;

[System.Serializable]
public class UniverseVisualizationSettings
{
    [Header("Global Multipliers")]
    public float orbitRadiusMultiplier = 1.0f;
    public float systemRadiusMultiplier = 1.0f;
    public float orbitSpeedMultiplier = 1.0f;
    public float modelScaleMultiplier = 1.0f;

    [Header("Model Base Scales")]
    public float starScale = 7500f;   
    public float planetScale = 2250f; 
    public float moonScale = 1250f;   
    public float asteroidScale = 80f; 
    public float stationScale = 200f; 
    public float gateScale = 300f;
    public float nebulaScale = 200000f;
    public float systemRadius = 100000f;
    public float starViewDistance = 500000f;

    [Header("Center Markers")]
    public bool showCenterMarkers = true;
    public float universeCenterScale = 1000f;
    public float galaxyCenterScale = 500f;
}
