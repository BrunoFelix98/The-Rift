using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class UniverseDataImporter : EditorWindow
{
    private string factionsPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/FactionsJSON.json";
    private string galaxiesPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/GalaxiesJSON.json";
    private string resourcesPath = "Assets/Code/Backend/DataSets/Behavior/JSONData/ResourcesJSON.json";
    private string outputFolder = "Assets/Code/Backend/DataSets/GameAssets";

    [MenuItem("Tools/The Rift/Import Universe Data from JSON")]
    public static void ShowWindow()
    {
        GetWindow<UniverseDataImporter>("Universe Data Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Import JSON to ScriptableObjects", EditorStyles.boldLabel);
        factionsPath = EditorGUILayout.TextField("Factions JSON", factionsPath);
        galaxiesPath = EditorGUILayout.TextField("Galaxies JSON", galaxiesPath);
        resourcesPath = EditorGUILayout.TextField("Resources JSON", resourcesPath);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("IMPORT ALL DATA"))
        {
            UniverseDataImportCore.ImportAll(factionsPath, galaxiesPath, resourcesPath, outputFolder);
        }
    }
}

public static class UniverseDataImportCore
{
    public static void ImportAll(string factionsPath, string galaxiesPath, string resourcesPath, string outputFolder)
    {
        if (!AssetDatabase.IsValidFolder(outputFolder))
        {
            string[] parts = outputFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + parts[i]))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current += "/" + parts[i];
            }
        }

        ImportFactions(factionsPath, outputFolder);
        ImportGalaxies(galaxiesPath, outputFolder);
        ImportResources(resourcesPath, outputFolder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UniverseDataImportCore] Import Complete!");
    }

    private static void ImportFactions(string factionsPath, string outputFolder)
    {
        string path = Path.Combine(Application.dataPath, "../", factionsPath);
        if (!File.Exists(path)) { Debug.LogError($"[Importer] Factions file not found at {path}"); return; }
        
        string json = File.ReadAllText(path);
        FactionCollection data = JsonUtility.FromJson<FactionCollection>(json);

        string folder = outputFolder + "/Organizations";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(outputFolder, "Organizations");
        
        string leaderFolder = outputFolder + "/Leaders";
        if (!AssetDatabase.IsValidFolder(leaderFolder)) AssetDatabase.CreateFolder(outputFolder, "Leaders");

        foreach (var f in data.factions)
        {
            string factionPath = $"{folder}/{f.factionName.Replace(" ", "_")}.asset";
            OrganizationSO factionSO = AssetDatabase.LoadAssetAtPath<OrganizationSO>(factionPath);
            if (factionSO == null)
            {
                factionSO = ScriptableObject.CreateInstance<OrganizationSO>();
                AssetDatabase.CreateAsset(factionSO, factionPath);
            }

            factionSO.organizationId = f.factionId;
            factionSO.organizationName = f.factionName;
            factionSO.type = OrganizationType.Faction;
            factionSO.homeGalaxy = f.homeGalaxy;
            factionSO.starNames = f.starNames;
            factionSO.requiresNebulas = f.requiresNebulas;
            ColorUtility.TryParseHtmlString(f.factionColor, out factionSO.organizationColor);
            factionSO.seed = f.seed;
            
            factionSO.gates.Clear();
            if (f.gates != null)
            {
                foreach (var g in f.gates)
                    factionSO.gates.Add(new GatePair { fromStar = g.from, toStar = g.to });
            }

            factionSO.memberOrganizations.Clear();
            for (int i = 0; i < f.corporations.Count; i++)
            {
                string corpName = f.corporations[i];
                string leaderName = (f.leaderNames != null && i < f.leaderNames.Count) ? f.leaderNames[i] : "Unknown Leader";
                
                string lPath = $"{leaderFolder}/{leaderName.Replace(" ", "_")}.asset";
                LivingSO lSO = AssetDatabase.LoadAssetAtPath<LivingSO>(lPath);
                if (lSO == null)
                {
                    lSO = ScriptableObject.CreateInstance<LivingSO>();
                    AssetDatabase.CreateAsset(lSO, lPath);
                }
                lSO.displayName = leaderName;

                string corpPath = $"{folder}/{corpName.Replace(" ", "_")}.asset";
                OrganizationSO corpSO = AssetDatabase.LoadAssetAtPath<OrganizationSO>(corpPath);
                if (corpSO == null)
                {
                    corpSO = ScriptableObject.CreateInstance<OrganizationSO>();
                    AssetDatabase.CreateAsset(corpSO, corpPath);
                }

                corpSO.organizationName = corpName;
                corpSO.type = OrganizationType.Corporation;
                corpSO.allegiance = factionSO;
                corpSO.leader = lSO;
                lSO.allegiance = corpSO;

                if (!factionSO.memberOrganizations.Contains(corpSO))
                    factionSO.memberOrganizations.Add(corpSO);

                if (i == 0) factionSO.leader = lSO;
                
                EditorUtility.SetDirty(lSO);
                EditorUtility.SetDirty(corpSO);
            }
            EditorUtility.SetDirty(factionSO);
        }
    }

    private static void ImportGalaxies(string galaxiesPath, string outputFolder)
    {
        string path = Path.Combine(Application.dataPath, "../", galaxiesPath);
        if (!File.Exists(path)) { Debug.LogError($"[Importer] Galaxies file not found at {path}"); return; }

        string json = File.ReadAllText(path);
        GalaxyCollection data = JsonUtility.FromJson<GalaxyCollection>(json);

        string folder = outputFolder + "/Galaxies";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(outputFolder, "Galaxies");

        foreach (var g in data.galaxies)
        {
            string assetPath = $"{folder}/{g.name.Replace(" ", "_")}.asset";
            GalaxyDataSO so = AssetDatabase.LoadAssetAtPath<GalaxyDataSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<GalaxyDataSO>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            so.galaxyId = g.id;
            so.galaxyName = g.name;
            so.dustParticles = g.dustParticles;
            so.seed = g.seed;
            so.maxSystems = g.maxSystems;

            EditorUtility.SetDirty(so);
        }
    }

    private static void ImportResources(string resourcesPath, string outputFolder)
    {
        string path = Path.Combine(Application.dataPath, "../", resourcesPath);
        if (!File.Exists(path)) { Debug.LogError($"[Importer] Resources file not found at {path}"); return; }

        string json = File.ReadAllText(path);
        ResourceCollection data = JsonUtility.FromJson<ResourceCollection>(json);

        string folder = outputFolder + "/Resources";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(outputFolder, "Resources");

        foreach (var r in data.resources)
        {
            string assetPath = $"{folder}/{r.resourceName.Replace(" ", "_")}.asset";
            ResourceSO so = AssetDatabase.LoadAssetAtPath<ResourceSO>(assetPath);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<ResourceSO>();
                AssetDatabase.CreateAsset(so, assetPath);
            }

            so.resourceId = r.resourceID;
            so.resourceName = r.resourceName;
            so.resourceDescription = r.resourceDescription;
            so.minQuantity = r.minQuantity;
            so.maxQuantity = r.maxQuantity;
            so.resourceWeight = r.resourceWeight;
            
            if (System.Enum.TryParse(r.rarity.ToUpper(), out ResourceRarity rarity))
                so.rarity = rarity;
            else if (r.rarity == "VeryRare") so.rarity = ResourceRarity.VERYRARE;

            so.appliesTo.Clear();
            if (r.appliesTo != null)
            {
                foreach (var t in r.appliesTo)
                    if (System.Enum.TryParse(t.ToUpper(), out CelestialType ct)) so.appliesTo.Add(ct);
            }

            so.environments.Clear();
            if (r.environment != null)
            {
                foreach (var e in r.environment)
                    if (System.Enum.TryParse(e.ToUpper(), out CelestialEnvironment ce)) so.environments.Add(ce);
            }

            so.allowedFactions = r.allowedFactions;
            EditorUtility.SetDirty(so);
        }
    }
}

[System.Serializable]
public class GateDataJSON
{
    public string from;
    public string to;
}

[System.Serializable]
public class FactionDataJSON
{
    public int factionId;
    public string factionName;
    public string homeGalaxy;
    public List<string> corporations;
    public List<string> leaderNames;
    public List<string> starNames;
    public bool requiresNebulas;
    public string factionColor;
    public int seed;
    public List<GateDataJSON> gates;
}

[System.Serializable]
public class FactionCollection
{
    public List<FactionDataJSON> factions;
}

[System.Serializable]
public class GalaxyDataJSON
{
    public int id;
    public string name;
    public int dustParticles;
    public int seed;
    public int maxSystems;
}

[System.Serializable]
public class GalaxyCollection
{
    public List<GalaxyDataJSON> galaxies;
}
