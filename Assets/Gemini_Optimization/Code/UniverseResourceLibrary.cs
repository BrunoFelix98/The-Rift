using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public class ResourceDataJSON
{
    public int resourceID;
    public string resourceName;
    public string resourceDescription;
    public int minQuantity;
    public int maxQuantity;
    public float resourceWeight;
    public List<string> appliesTo;
    public List<string> environment;
    public List<string> allowedFactions;
    public string rarity;
}

[Serializable]
public class ResourceCollection
{
    public List<ResourceDataJSON> resources;
}

[Serializable]
public class FactionDataJSON_Short
{
    public int factionId;
    public string factionName;
}

[Serializable]
public class FactionCollection_Short
{
    public List<FactionDataJSON_Short> factions;
}

public class UniverseResourceLibrary : MonoBehaviour
{
    public static UniverseResourceLibrary Instance { get; private set; }
    public string resourcesJsonPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/ResourcesJSON.json";
    public string factionsJsonPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/FactionsJSON.json";

    private ResourceCollection library;
    private Dictionary<int, string> factionNames = new Dictionary<int, string>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadLibrary();
        LoadFactions();
    }

    public void LoadLibrary()
    {
        string path = Path.Combine(Application.dataPath, "../", resourcesJsonPath);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            library = JsonUtility.FromJson<ResourceCollection>(json);
            Debug.Log($"[ResourceLibrary] Loaded {library?.resources?.Count ?? 0} resources.");
        }
    }

    public void LoadFactions()
    {
        string path = Path.Combine(Application.dataPath, "../", factionsJsonPath);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            var collection = JsonUtility.FromJson<FactionCollection_Short>(json);
            factionNames.Clear();
            if (collection?.factions != null)
            {
                foreach (var f in collection.factions) factionNames[f.factionId] = f.factionName;
            }
        }
    }

    public List<CelestialEnvironment> GetPreferredEnvironments(int factionId)
    {
        if (library == null) LoadLibrary();
        if (factionNames.Count == 0) LoadFactions();

        string factionName = factionNames.ContainsKey(factionId) ? factionNames[factionId] : "Unknown";
        HashSet<CelestialEnvironment> preferred = new HashSet<CelestialEnvironment>();

        if (library?.resources == null) return new List<CelestialEnvironment>();

        foreach (var res in library.resources)
        {
            if (res.allowedFactions != null && res.allowedFactions.Contains(factionName))
            {
                foreach (var envStr in res.environment)
                {
                    if (Enum.TryParse(envStr, true, out CelestialEnvironment env))
                    {
                        preferred.Add(env);
                    }
                }
            }
        }
        return preferred.ToList();
    }

    public List<ResourceDataJSON> GetResourcesFor(CelestialType type, CelestialEnvironment env, int factionId)
    {
        if (library == null) LoadLibrary();
        if (factionNames.Count == 0) LoadFactions();

        string typeStr = type.ToString().ToLower(); // "planet", "moon", etc
        string envStr = env.ToString(); // "ROCKY", etc
        string factionName = factionNames.ContainsKey(factionId) ? factionNames[factionId] : "Unknown";

        List<ResourceDataJSON> results = new();
        if (library?.resources == null) return results;

        foreach (var res in library.resources)
        {
            // Case-insensitive type match
            bool typeMatch = res.appliesTo != null && res.appliesTo.Any(s => s.Equals(typeStr, StringComparison.OrdinalIgnoreCase));
            
            // Environment match - handles "ROCKY" vs "Rocky" etc
            bool envMatch = res.environment != null && res.environment.Any(s => s.Equals(envStr, StringComparison.OrdinalIgnoreCase) || s.Equals("UNIVERSAL", StringComparison.OrdinalIgnoreCase));
            
            // Faction match
            bool factionMatch = res.allowedFactions != null && (res.allowedFactions.Contains("Universal") || res.allowedFactions.Contains(factionName));

            if (typeMatch && envMatch && factionMatch)
            {
                results.Add(res);
            }
        }

        if (results.Count == 0)
        {
            // Debug if no resources found for a planet/moon
            if (type == CelestialType.PLANET || type == CelestialType.MOON || type == CelestialType.ASTEROID)
            {
                // Debug.LogWarning($"[ResourceLibrary] No resources found for {type} in {env} territory of {factionName}");
            }
        }

        return results;
    }
}
