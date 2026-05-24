using System.Collections.Generic;
using UnityEngine;

public class CelestialAssetRegistry : MonoBehaviour
{
    public static CelestialAssetRegistry Instance { get; private set; }

    [Header("Asset Templates (Grouped by Type)")]
    public List<StarSO> starTemplates = new List<StarSO>();
    public List<PlanetSO> planetTemplates = new List<PlanetSO>();
    public List<MoonSO> moonTemplates = new List<MoonSO>();
    public List<StationSO> stationTemplates = new List<StationSO>();
    public List<AsteroidSO> asteroidTemplates = new List<AsteroidSO>();
    public List<GateSO> gateTemplates = new List<GateSO>();
    public List<NebulaSO> nebulaTemplates = new List<NebulaSO>();

    private Dictionary<string, ScriptableObject> templateMap = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeTemplates();
    }

    private void InitializeTemplates()
    {
        templateMap.Clear();
        foreach (var so in starTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in planetTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in moonTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in stationTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in asteroidTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in gateTemplates) if (so != null) templateMap[so.name] = so;
        foreach (var so in nebulaTemplates) if (so != null) templateMap[so.name] = so;
    }

    public ScriptableObject GetTemplate(string templateName)
    {
        return templateMap.GetValueOrDefault(templateName);
    }
    
    public T GetRandomTemplate<T>(List<T> templatesList) where T : ScriptableObject
    {
        if (templatesList == null || templatesList.Count == 0) return null;
        return templatesList[Random.Range(0, templatesList.Count)];
    }
}
