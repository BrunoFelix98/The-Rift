#if UNITY_EDITOR

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

[Serializable]
public class GalaxyJsonData
{
    public string name;
    public int dustParticles;
}

[Serializable]
public class GalaxyJsonContainer
{
    public List<GalaxyJsonData> galaxies;
}

[System.Serializable]
public class ResourceData
{
    public string resourceName;
    public string resourceDescription;
    public int minQuantity;
    public int maxQuantity;
    public float resourceWeight;
    public List<string> category;
    public List<string> celestialType;
    public List<string> allowedFactions;
}

[Serializable]
public class ResourceListData
{
    public List<ResourceData> resources;
}

[Serializable]
public class FactionsListData
{
    public List<FactionData> factions;
}

[Serializable]
public struct GenerationCosts
{
    public int system;
    public int star;
    public int planet;
    public int moon;
    public int asteroidBelt;
    public int asteroid;
}

public class RuleBasedGalaxyGenerator : MonoBehaviour
{
    //Base SOs
    [Header("Base Scriptable Objects")]
    public PlanetSO basePlanetSO;
    public MoonSO baseMoonSO;
    public StarSO baseStarSO;
    public AsteroidSO baseAsteroidSO;
    public AsteroidBeltSO baseAsteroidBeltSO;
    public GateSO baseGateSO;
    public StationSO baseStationSO;

    //Resources
    [Header("Resources")]
    public List<ResourceData> planetResources;
    public List<ResourceData> moonResources;
    public List<ResourceData> asteroidResources;

    //Stations per galaxy limit
    [Header("Limits")]
    public int maxStationsPerGalaxy = 3;

    //Universal generation costs
    [Header("Generation parameters")]
    [SerializeField]
    private GenerationCosts generationCosts = new GenerationCosts
    {
        system = 10,
        star = 5,
        planet = 3,
        moon = 1,
        asteroidBelt = 2,
        asteroid = 1
    };

    //Input folders & loaded variables
    [Header("Galaxy Input")]
    public TextAsset galaxiesJsonFile;
    [Header("Faction Input")]
    public TextAsset factionsJsonFile;
    public NebulaSO baseNebulaSO;
    [Header("Resource Input")]
    public TextAsset resourcesJsonFile;

    //SO output folder
    [Header("Output")]
    public string outputFolder = "Assets/RuleBasedGeneratedGalaxy";

    //Prefab output folder
    [Header("Prefab Output")]
    public string prefabOutputFolder = "Assets/RuleBasedGeneratedGalaxy/Prefabs";

    //Orbit creation and data
    private ulong currentOrbitId = 1;
    private readonly ConcurrentDictionary<ulong, OrbitParams> allOrbits = new ConcurrentDictionary<ulong, OrbitParams>();

    //Unity random replacement
    private static readonly ThreadLocal<System.Random> threadRandom = new ThreadLocal<System.Random>(() => new System.Random());

    private readonly object orbitIdLock = new object();

    private NativeList<GalaxyGenerationData> galaxies;
    private NativeList<SystemGenerationData> systems;
    private NativeList<StarGenerationData> stars;
    private NativeList<PlanetGenerationData> planets;
    private NativeList<MoonGenerationData> moons;
    private NativeList<AsteroidBeltGenerationData> belts;
    private NativeList<AsteroidGenerationData> asteroids;
    private NativeList<StationGenerationData> stations;
    private NativeList<GateGenerationData> gates;

    private Dictionary<string, int> factionNameToId;
    private Dictionary<int, ConceptSO> idToConceptSO;

    private ulong GenerateOrbitId()
    {
        lock (orbitIdLock)
        {
            return currentOrbitId++;
        }
    }

    void Start()
    {
        // 1. Parse JSON into POCO lists
        var galaxyPocos = ParseGalaxyJson(galaxiesJsonFile.text);
        var factionPocos = ParseFactionJson(factionsJsonFile.text);
        var resourcePocos = ParseResourceJson(resourcesJsonFile.text);

        planetResources = resourcePocos.Where(r => r.category.Any(c => string.Equals(c, "Planet", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Universal", StringComparison.OrdinalIgnoreCase))).ToList();
        moonResources = resourcePocos.Where(r => r.category.Any(c => string.Equals(c, "Moon", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Universal", StringComparison.OrdinalIgnoreCase))).ToList();
        asteroidResources = resourcePocos.Where(r => r.category.Any(c => string.Equals(c, "Asteroid", StringComparison.OrdinalIgnoreCase) || string.Equals(c, "Universal", StringComparison.OrdinalIgnoreCase))).ToList();
        
        // 2. Create ConceptSO assets and map faction names IDs
        var conceptGen = InitializeConceptSOGenerator();
        conceptGen.CreateConceptsFromFactions(factionPocos);
        var factionConcepts = conceptGen.GetFactionConceptDictionary();
        BuildFactionMappings(factionConcepts);

        // 3. Build NebulaSO map (if factions require nebulas)
        //    Assign each NebulaSO an integer ID (0 = none)
        var nebulaMap = new Dictionary<int, NebulaSO> { [0] = null };
        int nextNebulaId = 1;
        foreach (var faction in factionPocos)
        {
            if (faction.requiresNebulas && !nebulaMap.ContainsKey(nextNebulaId))
            {
                var neb = Instantiate(baseNebulaSO);
                neb.name = $"{faction.factionName} Nebula";
                nebulaMap[nextNebulaId] = neb;
                nextNebulaId++;
            }
        }
        
        // 5. Convert native data assets & prefabs
        //ConvertAllNativeDataToAssets.Run(galaxies, systems, stars, planets, moons, belts, asteroids, stations, gates, idToConceptSO, nebulaMap, planetResources, moonResources, asteroidResources, outputFolder, prefabOutputFolder);

        // 4. Allocate and run generation jobs
        InitializeAndScheduleJobs(galaxyPocos, factionPocos);
    }

    void BuildFactionMappings(Dictionary<string, ConceptSO> factionConcepts)
    {
        factionNameToId = new Dictionary<string, int>();
        idToConceptSO = new Dictionary<int, ConceptSO>();
        int nextId = 1; // 0 = no allegiance

        foreach (var kv in factionConcepts)
        {
            factionNameToId[kv.Key] = nextId;
            idToConceptSO[nextId] = kv.Value;
            nextId++;
        }
    }

    void InitializeAndScheduleJobs(List<GalaxyJsonData> galaxyPocos, List<FactionData> factionPocos)
    {
        // 4. Allocate native lists
        galaxies = new NativeList<GalaxyGenerationData>(Allocator.Persistent);
        systems = new NativeList<SystemGenerationData>(Allocator.Persistent);
        stars = new NativeList<StarGenerationData>(Allocator.Persistent);
        planets = new NativeList<PlanetGenerationData>(Allocator.Persistent);
        moons = new NativeList<MoonGenerationData>(Allocator.Persistent);
        belts = new NativeList<AsteroidBeltGenerationData>(Allocator.Persistent);
        asteroids = new NativeList<AsteroidGenerationData>(Allocator.Persistent);
        stations = new NativeList<StationGenerationData>(Allocator.Persistent);
        gates = new NativeList<GateGenerationData>(Allocator.Persistent);

        // 5. Populate galaxies container
        for (int i = 0; i < galaxyPocos.Count; i++)
        {
            var g = galaxyPocos[i];
            galaxies.Add(new GalaxyGenerationData
            {
                galaxyName = new FixedString64Bytes(g.name),
                dustParticles = g.dustParticles,
                systemListStartIndex = 0,
                systemCount = 0,
                orbitParams = CalculateGalaxyOrbit(i)
            });
        }

        // 6. Prepare NativeHashMap for faction IDs
        var nativeFactionMap = new NativeParallelHashMap<FixedString64Bytes, int>(
            factionNameToId.Count, Allocator.TempJob);
        foreach (var kv in factionNameToId)
            nativeFactionMap.Add(new FixedString64Bytes(kv.Key), kv.Value);

        int factionCount = factionPocos.Count;

        var nativeHomeGalaxies = new NativeArray<FixedString64Bytes>(factionCount, Allocator.TempJob);
        for (int i = 0; i < factionCount; i++)
            nativeHomeGalaxies[i] = new FixedString64Bytes(factionPocos[i].homeGalaxy);

        var nativeStarNames = new NativeArray<FixedList4096Bytes<FixedString64Bytes>>(factionCount, Allocator.TempJob);
        for (int i = 0; i < factionCount; i++)
        {
            var names = factionPocos[i].starNames;
            var list = new FixedList4096Bytes<FixedString64Bytes>();
            foreach (var s in names)
                list.Add(new FixedString64Bytes(s));
            nativeStarNames[i] = list;
        }

        var nativeRequiresNeb = new NativeArray<bool>(factionCount, Allocator.TempJob);
        for (int i = 0; i < factionCount; i++)
            nativeRequiresNeb[i] = factionPocos[i].requiresNebulas;

        var nativeFactionIds = new NativeArray<int>(factionCount, Allocator.TempJob);
        for (int i = 0; i < factionCount; i++)
            nativeFactionIds[i] = factionNameToId[factionPocos[i].factionName];

        // 7. Schedule generation job
        var job = new GenerateAllJobs
        {
            galaxies = galaxies,
            systems = systems,
            stars = stars,
            planets = planets,
            moons = moons,
            asteroidBelts = belts,
            asteroids = asteroids,
            stations = stations,
            gates = gates,
            costs = generationCosts,
            factionMap = nativeFactionMap,
            factionHomeGalaxy = nativeHomeGalaxies,
            factionStarNames = nativeStarNames,
            factionRequiresNeb = nativeRequiresNeb,
            factionIds = nativeFactionIds,
            maxStations = maxStationsPerGalaxy,
            randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue)
        };
        var handle = job.Schedule();
        handle.Complete();

        nativeFactionMap.Dispose();

        // 8. Convert native data back into ScriptableObjects
        ConvertAllNativeDataToAssets.Run(galaxies, systems, stars, planets, moons, belts, asteroids, stations, gates, idToConceptSO, new Dictionary<int, NebulaSO>(), planetResources, moonResources, asteroidResources, outputFolder, prefabOutputFolder);

        // 9. Dispose native lists
        galaxies.Dispose();
        systems.Dispose();
        stars.Dispose();
        planets.Dispose();
        moons.Dispose();
        belts.Dispose();
        asteroids.Dispose();
        stations.Dispose();
        gates.Dispose();
    }

    //------------------------File loading---------------------------

    //Loads galaxies from a JSON file
    private List<GalaxyJsonData> ParseGalaxyJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("Galaxy JSON is empty!");
            return new List<GalaxyJsonData>();
        }
        var galaxyList = JsonUtility.FromJson<GalaxyJsonContainer>(json);
        return galaxyList?.galaxies ?? new List<GalaxyJsonData>();
    }

    //Loads factions from a JSON file
    private List<FactionData> ParseFactionJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("Faction JSON is empty!");
            return new List<FactionData>();
        }
        var factionList = JsonUtility.FromJson<FactionsListData>(json);
        return factionList?.factions ?? new List<FactionData>();
    }

    //Loads resources from a JSON file
    private List<ResourceData> ParseResourceJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("Resource JSON is empty!");
            return new List<ResourceData>();
        }
        var resourceList = JsonUtility.FromJson<ResourceListData>(json);
        return resourceList?.resources ?? new List<ResourceData>();
    }

    //------------------------End of file loading---------------------------

    //------------------------Main functions---------------------------

    private OrbitParams CalculateGalaxyOrbit(int index)
    {
        // Generate a unique network ID for this galaxy
        var networkId = GenerateOrbitId();

        // All galaxies share the same parent ID of 0
        ulong parentId = 0;

        // Center at world origin
        Vector3 center = Vector3.zero;

        // Radius between 1000 and 5000 (randomized)
        float radius = (float)(threadRandom.Value.NextDouble() * (5000f - 1000f) + 1000f);

        // Angular speed between 0.005 and 0.02
        float angularSpeed = (float)(threadRandom.Value.NextDouble() * (0.02f - 0.005f) + 0.005f);

        // Random phase offset
        float phase = (float)(threadRandom.Value.NextDouble() * Mathf.PI * 2f);

        // Galaxies are always networked
        bool requiresNetworking = true;

        return new OrbitParams(networkId, parentId, center, radius, angularSpeed, phase, CelestialType.GALAXY, requiresNetworking);
    }

    private ConceptSOGenerator InitializeConceptSOGenerator()
    {
        return new ConceptSOGenerator
        {
            outputFolder = Path.Combine(outputFolder, "Concepts")
        };
    }

    // General helper to ensure a folder exists inside Unity's AssetDatabase
    private void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogError("EnsureFolder: Folder path is null or empty.");
            return;
        }

        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parentFolder = Path.GetDirectoryName(folder);
        string newFolderName = Path.GetFileName(folder);

        if (!string.IsNullOrEmpty(parentFolder) && !string.IsNullOrEmpty(newFolderName))
        {
            EnsureFolder(parentFolder); // Recursively ensure parent folders exist
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
            Debug.Log($"Created folder: {folder}");
        }
    }

    //------------------------End of helper functions---------------------------
}
#endif