#if UNITY_EDITOR

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

[System.Serializable]
public class GenerationCosts
{
    public int system = 10;
    public int star = 5;
    public int planet = 3;
    public int moon = 1;
    public int asteroidBelt = 2;
    public int asteroid = 1;
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
    public GenerationCosts generationCosts;

    //Input folders & loaded variables
    [Header("Galaxy Input")]
    public TextAsset galaxiesJsonFile;
    private GalaxyJsonContainer galaxyJsonContainer;
    [SerializeField]
    private List<GalaxyJsonData> loadedGalaxies = new List<GalaxyJsonData>();
    [Header("Faction Input")]
    public TextAsset factionsJsonFile;
    [SerializeField]
    private List<FactionData> loadedFactions = new List<FactionData>();
    public NebulaSO baseNebulaSO;
    [Header("Resource Input")]
    public TextAsset resourcesJsonFile;
    [SerializeField]
    private List<ResourceData> loadedResources = new List<ResourceData>();

    //SO output folder
    [Header("Output")]
    public string outputFolder = "Assets/RuleBasedGeneratedGalaxy";
    private List<SystemSO> allGeneratedSystems = new List<SystemSO>();

    //Prefab output folder
    [Header("Prefab Output")]
    public string prefabOutputFolder = "Assets/RuleBasedGeneratedGalaxy/Prefabs";

    //Orbit creation and data
    private ulong currentOrbitId = 1;
    private readonly ConcurrentDictionary<ulong, OrbitParams> allOrbits = new ConcurrentDictionary<ulong, OrbitParams>();

    //Unity random replacement
    private static readonly ThreadLocal<System.Random> threadRandom = new ThreadLocal<System.Random>(() => new System.Random());

    private readonly object orbitIdLock = new object();

    private ulong GenerateOrbitId()
    {
        lock (orbitIdLock)
        {
            return currentOrbitId++;
        }
    }

    void Start()
    {
        if (galaxiesJsonFile == null || factionsJsonFile == null || resourcesJsonFile == null)
        {
            Debug.LogWarning("Galaxy JSON, factions JSON, or resources JSON not assigned!");
            return;
        }

        string galaxiesJsonText = galaxiesJsonFile.text;
        string factionJsonText = factionsJsonFile.text;
        string resourceJsonText = resourcesJsonFile.text;

        Task.Run(() =>
        {
            try
            {
                // Parse galaxies JSON on background thread
                var galaxies = ParseGalaxyJson(galaxiesJsonText);
                var factions = ParseFactionJson(factionJsonText);
                var resources = ParseResourceJson(resourceJsonText);

                UnityEditor.EditorApplication.delayCall += () =>
                {
                    loadedGalaxies = galaxies;
                    Debug.Log("Galaxies loaded: " + (galaxies?.Count ?? 0));
                    loadedFactions = factions;
                    Debug.Log("Factions loaded: " + (factions?.Count ?? 0));
                    loadedResources = resources;
                    Debug.Log("Resources loaded: " + (resources?.Count ?? 0));
                    _ = GenerateAllGalaxiesAsync();
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in Task.Run: {ex}");
            }
        });
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

    public async Task GenerateAllGalaxiesAsync()
    {
        Debug.Log("Generating Universe");

        try
        {
            allGeneratedSystems.Clear();

            // Step 1: Create concepts on main thread
            var conceptGenerator = InitializeConceptGenerator();
            conceptGenerator.CreateConceptsFromFactions(loadedFactions);
            Dictionary<string, ConceptSO> factionConcepts = conceptGenerator.GetFactionConceptDictionary();

            // Step 2: Generate galaxies in parallel (background threads)
            var galaxyTasks = loadedGalaxies.Select(galaxyData =>
                Task.Run(() => GenerateGalaxyData(galaxyData, factionConcepts))
            ).ToArray();

            var galaxyResults = await Task.WhenAll(galaxyTasks);

            // Step 3: Save all results on main thread
            foreach (var galaxyResult in galaxyResults)
            {
                SaveGalaxyAssetsToUnity(galaxyResult.galaxyName, galaxyResult.systemsData, galaxyResult.galaxyOrbit, galaxyResult.galaxyFolder);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Galaxy generation complete");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during galaxy generation: {ex.Message}");
        }
    }

    private (string galaxyName, List<SystemGenerationData> systemsData, OrbitParams galaxyOrbit, string galaxyFolder) GenerateGalaxyData(GalaxyJsonData galaxyData, Dictionary<string, ConceptSO> factionConcepts)
    {
        try
        {
            Debug.Log($"Generating galaxy: {galaxyData.name} with {galaxyData.dustParticles} dust");

            string galaxyFolder = Path.Combine(outputFolder, SanitizeFileName(galaxyData.name));

            // Generate galaxy orbit params (no ScriptableObject creation)
            ulong galaxyOrbitId = GenerateOrbitId();
            Vector3 universeCenter = Vector3.zero;
            float galaxyRadius = (float)(threadRandom.Value.NextDouble() * (5000f - 1000f) + 1000f);
            float galaxyAngularSpeed = (float)(threadRandom.Value.NextDouble() * (0.02f - 0.005f) + 0.005f);
            float galaxyPhase = (float)(threadRandom.Value.NextDouble() * Mathf.PI * 2f);

            var galaxyOrbit = new OrbitParams(galaxyOrbitId, 0, universeCenter, galaxyRadius, galaxyAngularSpeed, galaxyPhase, CelestialType.GALAXY, requiresNetworking: false);

            int dustLeft = galaxyData.dustParticles;
            int stationCount = 0;

            // Generate systems data (background thread)
            var systemsData = GenerateSystemsData(galaxyData, factionConcepts, ref dustLeft, ref stationCount, galaxyOrbit);

            Debug.Log($"Galaxy '{galaxyData.name}' data generated successfully.");

            return (galaxyData.name, systemsData, galaxyOrbit, galaxyFolder);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating galaxy {galaxyData.name}: {ex.Message}");
            throw;
        }
    }

    private List<SystemGenerationData> GenerateSystemsData(GalaxyJsonData galaxyData, Dictionary<string, ConceptSO> factionConcepts, ref int dustLeft, ref int stationCount, OrbitParams galaxyOrbit)
    {
        var systemsList = new List<SystemGenerationData>();
        int maxSystemsPerGalaxy = 10;

        // Faction-based systems
        List<FactionData> galaxyFactions = loadedFactions.FindAll(f => f.homeGalaxy == galaxyData.name);

        if (galaxyFactions != null)
        {
            foreach (var faction in galaxyFactions)
            {
                foreach (var sysName in faction.starNames)
                {
                    if (systemsList.Exists(s => s.systemName == sysName))
                        continue;

                    if (systemsList.Count >= maxSystemsPerGalaxy)
                        break;                    

                    var systemData = new SystemGenerationData();
                    systemData.systemName = sysName;

                    ulong systemOrbitId = GenerateOrbitId();
                    OrbitParams systemOrbit = CalculateOrbit(systemOrbitId, galaxyOrbit.NetworkId, galaxyOrbit.CenterPos, CelestialType.SYSTEM, systemsList.Count, true);
                    allOrbits[systemOrbitId] = systemOrbit;
                    systemData.orbitParams = systemOrbit;

                    ConceptSO factionConcept = null;
                    if (factionConcepts.TryGetValue(faction.factionName, out factionConcept) && factionConcept != null)
                    {
                        systemData.allegiance = factionConcept;
                    }

                    systemData.requiresNebula = faction.requiresNebulas;

                    // Generate system bodies data
                    CreateSystemBodiesData(systemData, dustLeft, ref stationCount, factionConcept);

                    systemsList.Add(systemData);
                }

                
            }
        }

        // Random systems
        while (dustLeft >= generationCosts.system + generationCosts.star && systemsList.Count < maxSystemsPerGalaxy)
        {
            dustLeft -= generationCosts.system + generationCosts.star;

            string systemName = $"{galaxyData.name} System {systemsList.Count + 1}";

            var systemData = new SystemGenerationData();
            systemData.systemName = systemName;

            ulong systemOrbitId = GenerateOrbitId();
            OrbitParams systemOrbit = CalculateOrbit(systemOrbitId, galaxyOrbit.NetworkId, galaxyOrbit.CenterPos, CelestialType.SYSTEM, systemsList.Count, true);
            allOrbits[systemOrbitId] = systemOrbit;
            systemData.orbitParams = systemOrbit;

            //20% chance of nebula in random system
            systemData.requiresNebula = threadRandom.Value.NextDouble() < 0.20;

            CreateSystemBodiesData(systemData, dustLeft, ref stationCount, null);

            systemsList.Add(systemData);
        }

        Debug.Log($"System generation complete. Remaining dust: {dustLeft}");
        return systemsList;
    }

    private void CreateSystemBodiesData(SystemGenerationData systemData, int dustLeft, ref int stationCount, ConceptSO allegiance)
    {
        // Initialize lists
        systemData.stars = new List<StarGenerationData>();
        systemData.planets = new List<PlanetGenerationData>();
        systemData.asteroidBelts = new List<AsteroidBeltGenerationData>();
        systemData.gates = new List<GateGenerationData>();
        systemData.stations = new List<StationGenerationData>();

        // Create Stars data
        int numberOfStars = threadRandom.Value.NextDouble() < 0.1 ? 2 : 1;
        for (int i = 0; i < numberOfStars; i++)
        {
            string starName = $"{systemData.systemName} Star {i + 1}";
            var starData = new StarGenerationData();
            starData.name = starName;

            ulong starOrbitId = GenerateOrbitId();
            OrbitParams starOrbit = CalculateOrbit(starOrbitId, systemData.orbitParams.NetworkId, systemData.orbitParams.CenterPos, CelestialType.STAR, systemData.stars.Count, false);
            allOrbits[starOrbitId] = starOrbit;
            starData.orbitParams = starOrbit;

            systemData.stars.Add(starData);
        }

        // Create Planets data
        if (allegiance != null)
        {
            int planetCount = Mathf.Min(dustLeft / generationCosts.planet, 5);
            bool stationPlaced = false;

            for (int i = 0; i < planetCount; i++)
            {
                if (dustLeft < generationCosts.planet) break;
                dustLeft -= generationCosts.planet;

                string planetName = $"{systemData.systemName} Planet {i + 1}";
                CelestialEnvironment planetType = GetRandomCelestialTypeBackground();

                var planetData = new PlanetGenerationData();
                planetData.planetName = planetName;
                planetData.type = planetType;

                ulong planetOrbitId = GenerateOrbitId();
                OrbitParams planetOrbit = CalculateOrbit(planetOrbitId, systemData.orbitParams.NetworkId, systemData.orbitParams.CenterPos, CelestialType.PLANET, systemData.planets.Count, false);
                allOrbits[planetOrbitId] = planetOrbit;
                planetData.orbitParams = planetOrbit;

                planetData.moons = new List<MoonGenerationData>();
                int moonCount = Mathf.Min(dustLeft / generationCosts.moon, 3);

                for (int m = 0; m < moonCount; m++)
                {
                    if (dustLeft < generationCosts.moon) break;
                    dustLeft -= generationCosts.moon;

                    string moonName = $"{planetData.planetName} Moon {m + 1}";
                    CelestialEnvironment moonType = GetRandomCelestialTypeBackground();

                    var moonData = new MoonGenerationData();
                    moonData.moonName = moonName;
                    moonData.type = moonType;

                    ulong moonOrbitId = GenerateOrbitId();
                    OrbitParams moonOrbit = CalculateOrbit(moonOrbitId, planetData.orbitParams.NetworkId, planetData.orbitParams.CenterPos, CelestialType.MOON, planetData.moons.Count, false);
                    allOrbits[moonOrbitId] = moonOrbit;
                    moonData.orbitParams = moonOrbit;

                    if (!stationPlaced && baseStationSO != null)
                    {
                        moonData.stations = new List<StationGenerationData>();
                        ulong stationOrbitId = GenerateOrbitId();
                        OrbitParams stationOrbit = CalculateOrbit(stationOrbitId, moonData.orbitParams.NetworkId, moonData.orbitParams.CenterPos, CelestialType.STATION, 0, true);
                        allOrbits[stationOrbitId] = stationOrbit;

                        var stationData = new StationGenerationData();
                        stationData.stationName = $"{moonData.moonName} Station";
                        stationData.orbitParams = stationOrbit;
                        stationData.allegiance = allegiance;

                        moonData.stations.Add(stationData);
                        systemData.stations.Add(stationData);
                        stationPlaced = true;
                        stationCount++;
                    }

                    planetData.moons.Add(moonData);
                }

                systemData.planets.Add(planetData);
            }
        }
        else
        {
            // Random system planets
            int planetCount = Mathf.Min(dustLeft / generationCosts.planet, 5);

            for (int i = 0; i < planetCount; i++)
            {
                if (dustLeft < generationCosts.planet) break;
                dustLeft -= generationCosts.planet;

                string planetName = $"{systemData.systemName} Planet {i + 1}";
                CelestialEnvironment planetType = GetRandomCelestialTypeBackground();

                var planetData = new PlanetGenerationData();
                planetData.planetName = planetName;
                planetData.type = planetType;

                ulong planetOrbitId = GenerateOrbitId();
                OrbitParams planetOrbit = CalculateOrbit(planetOrbitId, systemData.orbitParams.NetworkId, systemData.orbitParams.CenterPos, CelestialType.PLANET, systemData.planets.Count, false);
                allOrbits[planetOrbitId] = planetOrbit;
                planetData.orbitParams = planetOrbit;

                planetData.moons = new List<MoonGenerationData>();
                int moonCount = Mathf.Min(dustLeft / generationCosts.moon, 3);

                for (int m = 0; m < moonCount; m++)
                {
                    if (dustLeft < generationCosts.moon) break;
                    dustLeft -= generationCosts.moon;

                    string moonName = $"{planetData.planetName} Moon {m + 1}";
                    CelestialEnvironment moonType = GetRandomCelestialTypeBackground();

                    var moonData = new MoonGenerationData();
                    moonData.moonName = moonName;
                    moonData.type = moonType;

                    ulong moonOrbitId = GenerateOrbitId();
                    OrbitParams moonOrbit = CalculateOrbit(moonOrbitId, planetData.orbitParams.NetworkId, planetData.orbitParams.CenterPos, CelestialType.MOON, planetData.moons.Count, false);
                    allOrbits[moonOrbitId] = moonOrbit;
                    moonData.orbitParams = moonOrbit;

                    planetData.moons.Add(moonData);
                }

                systemData.planets.Add(planetData);
            }
        }

        // Create asteroid belts data
        int beltCount = allegiance != null ? threadRandom.Value.Next(2, 6) : Mathf.Min(dustLeft / generationCosts.asteroidBelt, 3);

        for (int b = 0; b < beltCount; b++)
        {
            if (dustLeft < generationCosts.asteroidBelt && allegiance == null) break;
            dustLeft -= generationCosts.asteroidBelt;

            string beltName = $"{systemData.systemName} Belt {b + 1}";
            double roll = threadRandom.Value.NextDouble();
            CelestialEnvironment beltType = roll < 0.7f ? CelestialEnvironment.ROCKY : CelestialEnvironment.GAS;

            var beltData = new AsteroidBeltGenerationData();
            beltData.name = beltName;
            beltData.type = beltType;
            beltData.asteroids = new List<AsteroidGenerationData>();

            ulong beltOrbitId = GenerateOrbitId();
            OrbitParams beltOrbit = CalculateOrbit(beltOrbitId, systemData.orbitParams.NetworkId, systemData.orbitParams.CenterPos, CelestialType.ASTEROIDBELT, systemData.asteroidBelts.Count, false);
            allOrbits[beltOrbitId] = beltOrbit;
            beltData.orbitParams = beltOrbit;

            int asteroidCount = threadRandom.Value.Next(5, 16);
            for (int a = 0; a < asteroidCount && dustLeft >= generationCosts.asteroid; a++)
            {
                dustLeft -= generationCosts.asteroid;
                string asteroidName = $"{beltName} Asteroid {a + 1}";

                var asteroidData = new AsteroidGenerationData();
                asteroidData.name = asteroidName;
                asteroidData.type = beltType;

                ulong asteroidOrbitId = GenerateOrbitId();
                OrbitParams asteroidOrbit = CalculateOrbit(asteroidOrbitId, beltData.orbitParams.NetworkId, beltData.orbitParams.CenterPos, CelestialType.ASTEROID, beltData.asteroids.Count, true);
                allOrbits[asteroidOrbitId] = asteroidOrbit;
                asteroidData.orbitParams = asteroidOrbit;

                beltData.asteroids.Add(asteroidData);
            }

            systemData.asteroidBelts.Add(beltData);
        }

        // Create gates data
        if (allegiance != null || threadRandom.Value.NextDouble() < 0.3)
        {
            int gateCount = threadRandom.Value.Next(1, 5);

            for (int g = 0; g < gateCount; g++)
            {
                string gateName = $"{systemData.systemName} Gate {g + 1}";
                var gateData = new GateGenerationData();
                gateData.gateName = gateName;

                ulong gateOrbitId = GenerateOrbitId();
                OrbitParams gateOrbit = CalculateOrbit(gateOrbitId, systemData.orbitParams.NetworkId, systemData.orbitParams.CenterPos, CelestialType.GATE, systemData.gates.Count, true);
                allOrbits[gateOrbitId] = gateOrbit;
                gateData.orbitParams = gateOrbit;

                systemData.gates.Add(gateData);
            }
        }

        systemData.allegiance = allegiance;
    }

    private void SaveGalaxyAssetsToUnity(string galaxyName, List<SystemGenerationData> systemsData, OrbitParams galaxyOrbitParams, string galaxyFolder)
    {
        try
        {
            EnsureFolder(galaxyFolder);

            // Create galaxy ScriptableObject on main thread
            GalaxySO galaxySO = ScriptableObject.CreateInstance<GalaxySO>();
            galaxySO.galaxyName = galaxyName;
            galaxySO.orbitParams = galaxyOrbitParams;
            galaxySO.systems = new List<SystemSO>();

            // Save galaxy
            string galaxyAssetPath = Path.Combine(galaxyFolder, $"{SanitizeFileName(galaxyName)}.asset");
            

            // Convert SystemData to SystemSO and save
            foreach (var systemData in systemsData)
            {
                string systemFolder = Path.Combine(galaxyFolder, SanitizeFileName(systemData.systemName));
                EnsureFolder(systemFolder);

                string systemAssetPath = Path.Combine(systemFolder, $"{SanitizeFileName(systemData.systemName)}.asset");

                // Create SystemSO on main thread
                SystemSO systemSO = ScriptableObject.CreateInstance<SystemSO>();
                systemSO.systemName = systemData.systemName;
                systemSO.orbitParams = systemData.orbitParams;
                systemSO.allegiance = systemData.allegiance;
                GameObject systemPrefab = CreateAndSaveCelestialPrefab(systemSO.systemName, CelestialType.SYSTEM, CelestialEnvironment.ROCKY, null, systemSO, systemSO.orbitParams);
                AssetDatabase.CreateAsset(systemSO, systemAssetPath);
                systemSO.prefabReference = systemPrefab;

                if (systemData.requiresNebula)
                {
                    CreateAndAssignNebula(systemSO, systemSO.systemName, systemSO.allegiance?.name, systemFolder, systemSO.orbitParams);
                }

                // Convert and create all child objects
                ConvertAndSaveSystemChildren(systemSO, systemData, systemFolder);
                galaxySO.systems.Add(systemSO);
                EditorUtility.SetDirty(systemSO);
                EditorUtility.SetDirty(galaxySO);
            }

            // Create galaxy prefab
            GameObject galaxyPrefab = CreateAndSaveCelestialPrefab(galaxySO.galaxyName, CelestialType.GALAXY, CelestialEnvironment.ROCKY, null, galaxySO, galaxySO.orbitParams);
            AssetDatabase.CreateAsset(galaxySO, galaxyAssetPath);
            galaxySO.prefabReference = galaxyPrefab;

            ConnectGatesWithinGalaxy(galaxySO, galaxyFolder);
            EditorUtility.SetDirty(galaxySO);

            Debug.Log($"Galaxy '{galaxySO.galaxyName}' saved successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving galaxy {galaxyName}: {ex.Message}");
        }
    }

    private void ConvertAndSaveSystemChildren(SystemSO systemSO, SystemGenerationData systemData, string systemFolder)
    {
        // Convert stars
        systemSO.stars = new List<StarSO>();
        if (systemData.stars != null)
        {
            string starFolder = Path.Combine(systemFolder, "Stars");
            EnsureFolder(starFolder);

            foreach (var starData in systemData.stars)
            {
                string starAssetPath = Path.Combine(starFolder, $"{SanitizeFileName(starData.name)}.asset");

                StarSO starSO = ScriptableObject.CreateInstance<StarSO>();
                starSO.name = starData.name;
                starSO.orbitParams = starData.orbitParams;

                GameObject starPrefab = CreateAndSaveCelestialPrefab(starSO.name, CelestialType.STAR, CelestialEnvironment.GAS, null, starSO, starSO.orbitParams);
                AssetDatabase.CreateAsset(starSO, starAssetPath);
                starSO.prefabReference = starPrefab;

                systemSO.stars.Add(starSO);
                EditorUtility.SetDirty(starSO);
            }
        }

        // Convert planets
        systemSO.planets = new List<PlanetSO>();
        if (systemData.planets != null)
        {
            string planetsFolder = Path.Combine(systemFolder, "Planets");
            EnsureFolder(planetsFolder);

            foreach (var planetData in systemData.planets)
            {
                string planetFolder = Path.Combine(planetsFolder, SanitizeFileName(planetData.planetName));
                EnsureFolder(planetFolder);

                string planetAssetPath = Path.Combine(planetFolder, $"{SanitizeFileName(planetData.planetName)}.asset");

                PlanetSO planetSO = ScriptableObject.CreateInstance<PlanetSO>();
                planetSO.name = planetData.planetName;
                planetSO.planetName = planetData.planetName;
                planetSO.type = planetData.type;
                planetSO.orbitParams = planetData.orbitParams;

                var resources = GenerateResources(loadedResources, planetFolder, "Planet", planetData.type);
                planetSO.resources = resources;

                // Convert moons
                planetSO.moons = new List<MoonSO>();
                if (planetData.moons != null)
                {
                    string moonsFolder = Path.Combine(planetFolder, "Moons");
                    EnsureFolder(moonsFolder);

                    foreach (var moonData in planetData.moons)
                    {
                        string moonAssetPath = Path.Combine(moonsFolder, $"{SanitizeFileName(moonData.moonName)}.asset");

                        MoonSO moonSO = ScriptableObject.CreateInstance<MoonSO>();
                        
                        moonSO.name = moonData.moonName;
                        moonSO.moonName = moonData.moonName;
                        moonSO.type = moonData.type;
                        moonSO.orbitParams = moonData.orbitParams;

                        var moonResources = GenerateResources(loadedResources, moonsFolder, "Moon", moonData.type);
                        moonSO.resources = moonResources;

                        // Convert stations
                        moonSO.stations = new List<StationSO>();
                        if (moonData.stations != null)
                        {
                            string stationsFolder = Path.Combine(moonsFolder, "Stations");
                            EnsureFolder(stationsFolder);

                            foreach (var stationData in moonData.stations)
                            {
                                string stationAssetPath = Path.Combine(stationsFolder, $"{SanitizeFileName(stationData.stationName)}.asset");

                                StationSO stationSO = ScriptableObject.CreateInstance<StationSO>();
                                
                                stationSO.name = stationData.stationName;
                                stationSO.stationName = stationData.stationName;
                                stationSO.orbitParams = stationData.orbitParams;
                                stationSO.allegiance = stationData.allegiance;

                                GameObject stationPrefab = CreateAndSaveCelestialPrefab(stationSO.stationName, CelestialType.STATION, CelestialEnvironment.ROCKY, null, stationSO, stationSO.orbitParams);
                                AssetDatabase.CreateAsset(stationSO, stationAssetPath);
                                stationSO.prefabReference = stationPrefab;

                                moonSO.stations.Add(stationSO);
                                EditorUtility.SetDirty(stationSO);
                            }
                        }
                        GameObject moonPrefab = CreateAndSaveCelestialPrefab(moonSO.moonName, CelestialType.MOON, moonSO.type, moonResources, moonSO, moonSO.orbitParams);
                        AssetDatabase.CreateAsset(moonSO, moonAssetPath);
                        moonSO.prefabReference = moonPrefab;

                        planetSO.moons.Add(moonSO);
                        EditorUtility.SetDirty(moonSO);
                    }
                }

                GameObject planetPrefab = CreateAndSaveCelestialPrefab(planetSO.planetName, CelestialType.PLANET, planetSO.type, resources, planetSO, planetSO.orbitParams);
                AssetDatabase.CreateAsset(planetSO, planetAssetPath);
                planetSO.prefabReference = planetPrefab;

                systemSO.planets.Add(planetSO);
                EditorUtility.SetDirty(planetSO);
            }
        }

        // Convert asteroid belts (similar pattern)
        systemSO.asteroidBelts = new List<AsteroidBeltSO>();
        if (systemData.asteroidBelts != null)
        {
            string beltFolder = Path.Combine(systemFolder, "AsteroidBelts");
            EnsureFolder(beltFolder);

            foreach (var beltData in systemData.asteroidBelts)
            {
                string currentBeltFolder = Path.Combine(beltFolder, SanitizeFileName(beltData.name));
                EnsureFolder(currentBeltFolder);

                string beltAssetPath = Path.Combine(currentBeltFolder, $"{SanitizeFileName(beltData.name)}.asset");

                AsteroidBeltSO beltSO = ScriptableObject.CreateInstance<AsteroidBeltSO>();
                beltSO.name = beltData.name;
                beltSO.type = beltData.type;
                beltSO.orbitParams = beltData.orbitParams;
                beltSO.asteroids = new List<AsteroidSO>();

                // Convert asteroids
                if (beltData.asteroids != null)
                {
                    string asteroidsFolder = Path.Combine(currentBeltFolder, "Asteroids");
                    EnsureFolder(asteroidsFolder);

                    foreach (var asteroidData in beltData.asteroids)
                    {
                        string asteroidAssetPath = Path.Combine(asteroidsFolder, $"{SanitizeFileName(asteroidData.name)}.asset");
                        AsteroidSO asteroidSO = ScriptableObject.CreateInstance<AsteroidSO>();
                        
                        asteroidSO.name = asteroidData.name;
                        asteroidSO.type = asteroidData.type;
                        asteroidSO.orbitParams = asteroidData.orbitParams;

                        var asteroidResources = GenerateResources(loadedResources, asteroidsFolder, "Asteroid", beltData.type);
                        asteroidSO.resources = asteroidResources;

                        GameObject asteroidPrefab = CreateAndSaveCelestialPrefab(asteroidSO.name, CelestialType.ASTEROID, beltSO.type, asteroidResources, asteroidSO, asteroidSO.orbitParams);
                        AssetDatabase.CreateAsset(asteroidSO, asteroidAssetPath);
                        asteroidSO.prefabReference = asteroidPrefab;

                        beltSO.asteroids.Add(asteroidSO);
                        EditorUtility.SetDirty(asteroidSO);
                    }
                }
                GameObject beltPrefab = CreateAndSaveCelestialPrefab(beltSO.name, CelestialType.ASTEROIDBELT, beltSO.type, null, beltSO, beltSO.orbitParams);
                AssetDatabase.CreateAsset(beltSO, beltAssetPath);
                beltSO.prefabReference = beltPrefab;

                systemSO.asteroidBelts.Add(beltSO);
                EditorUtility.SetDirty(beltSO);
            }
        }

        // Convert gates
        systemSO.gates = new List<GateSO>();
        systemSO.stations = new List<StationSO>();
        if (systemData.gates != null)
        {
            string gatesFolder = Path.Combine(systemFolder, "Gates");
            EnsureFolder(gatesFolder);

            foreach (var gateData in systemData.gates)
            {
                string gateAssetPath = Path.Combine(gatesFolder, $"{SanitizeFileName(gateData.gateName)}.asset");

                GateSO gateSO = ScriptableObject.CreateInstance<GateSO>();
                gateSO.name = gateData.gateName;
                gateSO.gateName = gateData.gateName;
                gateSO.orbitParams = gateData.orbitParams;

                GameObject gatePrefab = CreateAndSaveCelestialPrefab(gateSO.gateName, CelestialType.GATE, CelestialEnvironment.ROCKY, null, gateSO, gateSO.orbitParams);
                AssetDatabase.CreateAsset(gateSO, gateAssetPath);
                gateSO.prefabReference = gatePrefab;

                systemSO.gates.Add(gateSO);
                EditorUtility.SetDirty(gateSO);
            }
        }

        // Add stations from systemData.stations to systemSO.stations
        if (systemData.stations != null)
        {
            foreach (var stationData in systemData.stations)
            {
                // Find the corresponding StationSO that was already created in moon processing
                foreach (var planetSO in systemSO.planets)
                {
                    foreach (var moonSO in planetSO.moons)
                    {
                        foreach (var stationSO in moonSO.stations)
                        {
                            if (stationSO.stationName == stationData.stationName)
                            {
                                systemSO.stations.Add(stationSO);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private ConceptSOGenerator InitializeConceptGenerator()
    {
        return new ConceptSOGenerator
        {
            outputFolder = Path.Combine(outputFolder, "Concepts")
        };
    }

    private CelestialEnvironment GetRandomCelestialTypeBackground()
    {
        // For background threads - use System.Random
        var values = System.Enum.GetValues(typeof(CelestialEnvironment));
        return (CelestialEnvironment)values.GetValue(threadRandom.Value.Next(values.Length));
    }

    private void ConnectGatesWithinGalaxy(GalaxySO galaxy, string galaxyFolder)
    {
        int totalSystems = galaxy.systems.Count;
        for (int i = 0; i < totalSystems; i++)
        {
            var systemA = galaxy.systems[i];
            int gateCount = UnityEngine.Random.Range(1, 4);
            string systemAFolder = AssetDatabase.GetAssetPath(systemA);
            systemAFolder = Path.GetDirectoryName(systemAFolder);
            string gatesFolderA = Path.Combine(systemAFolder, "Gates");
            EnsureFolder(Path.Combine(gatesFolderA, systemA.name));

            for (int g = 0; g < gateCount; g++)
            {
                int targetIndex = UnityEngine.Random.Range(0, totalSystems);
                if (targetIndex == i)
                    targetIndex = (targetIndex + 1) % totalSystems;
                var systemB = galaxy.systems[targetIndex];
                if (systemA.gates.Exists(g => g.connectedSystem == systemB))
                    continue;
                var gateSO = Instantiate(baseGateSO);
                gateSO.gateName = $"{systemA.systemName} <-> {systemB.systemName} Gate";
                gateSO.connectedSystem = systemB;
                systemA.gates.Add(gateSO);
                string gatePathA = Path.Combine(gatesFolderA, SanitizeFileName(gateSO.gateName) + ".asset");
                AssetDatabase.CreateAsset(gateSO, gatePathA);
                EditorUtility.SetDirty(gateSO);
                string systemBFolder = AssetDatabase.GetAssetPath(systemB);
                systemBFolder = Path.GetDirectoryName(systemBFolder);
                string gatesFolderB = Path.Combine(systemBFolder, "Gates");
                EnsureFolder(Path.Combine(gatesFolderB, systemB.name));

                var gateSO_B = Instantiate(baseGateSO);
                gateSO_B.gateName = $"{systemB.systemName} <-> {systemA.systemName} Gate";
                gateSO_B.connectedSystem = systemA;
                systemB.gates.Add(gateSO_B);
                string gatePathB = Path.Combine(gatesFolderB, SanitizeFileName(gateSO_B.gateName) + ".asset");
                AssetDatabase.CreateAsset(gateSO_B, gatePathB);
                EditorUtility.SetDirty(gateSO_B);
                EditorUtility.SetDirty(systemA);
                EditorUtility.SetDirty(systemB);
            }
        }
        AssetDatabase.SaveAssets();
    }

    private List<ResourceSO> GenerateResources(List<ResourceData> resources, string targetFolder, string category, CelestialEnvironment bodyType)
    {
        // Validate inputs
        if (resources == null || resources.Count == 0)
        {
            Debug.LogWarning("No resource templates provided. Returning an empty resource list.");
            return new List<ResourceSO>();
        }

        if (string.IsNullOrEmpty(targetFolder))
        {
            Debug.LogError("Target folder is null or empty. Cannot generate resources.");
            return new List<ResourceSO>();
        }

        // Ensure the Resources folder exists
        string resourcesFolder = Path.Combine(targetFolder, "Resources");
        EnsureFolder(resourcesFolder);

        var resourceList = new List<ResourceSO>();

        // Filter resources by category and celestial type
        var filteredTemplates = resources.FindAll(resource => resource.category != null && resource.category.Exists(c => c == category || c == "Universal") && IsResourceAllowedForCelestialType(resource, bodyType));

        if (filteredTemplates.Count == 0)
        {
            Debug.LogWarning($"No valid resource templates found for category '{category}' and celestial type '{bodyType}'.");
            return resourceList;
        }

        Debug.Log($"Filtered {filteredTemplates.Count} templates for category '{category}' and celestial type '{bodyType}'.");

        foreach (var template in filteredTemplates)
        {
            try
            {
                // Create a new Resource ScriptableObject
                var resourceSO = ScriptableObject.CreateInstance<ResourceSO>();
                resourceSO.resourceName = template.resourceName;
                resourceSO.resourceDescription = template.resourceDescription;
                resourceSO.minQuantity = template.minQuantity;
                resourceSO.maxQuantity = template.maxQuantity;
                resourceSO.resourceWeight = template.resourceWeight;

                List<ResourceCategory> categories = new List<ResourceCategory>();

                foreach (var categoryItem in template.category)
                {
                    if (Enum.TryParse(categoryItem, true, out ResourceCategory resourceCategoryEnum))
                    {
                        categories.Add(resourceCategoryEnum);
                    }
                    else
                    {
                        Debug.LogWarning($"Invalid resource category strings '{template.category}' in resource template '{template.resourceName}'.");
                    }
                }

                resourceSO.resourceCategory = categories;
                // Assign celestial type
                resourceSO.celestialType = template.celestialType != null ? new List<string>(template.celestialType) : new List<string>();

                // Assign allowed factions
                resourceSO.allowedFactions = template.allowedFactions != null ? new List<string>(template.allowedFactions) : new List<string>();

                // Generate a unique asset file name
                string assetFileName = $"{SanitizeFileName(resourceSO.resourceName)}_{Guid.NewGuid()}.asset";
                string resourceAssetPath = Path.Combine(resourcesFolder, assetFileName);

                // Save the resource asset
                AssetDatabase.CreateAsset(resourceSO, resourceAssetPath);
                EditorUtility.SetDirty(resourceSO);

                // Add the resource to the list
                resourceList.Add(resourceSO);

                Debug.Log($"Successfully created resource: {resourceSO.resourceName} at {resourceAssetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error creating resource from template {template.resourceName}: {ex.Message}");
            }
        }

        return resourceList;
    }

    private bool IsResourceAllowedForCelestialType(ResourceData resource, CelestialEnvironment bodyType)
    {
        if (resource.celestialType == null || resource.celestialType.Count == 0)
            return false;

        string celesitalType = bodyType.ToString();

        foreach (var celestialTypeString in resource.celestialType)
        {
            if (celesitalType.Equals(celestialTypeString))
            {
                Debug.Log($"'{celesitalType}' celestial successfully added: '{celestialTypeString}' to celestial");
                return true;
            }
            else
            {
                Debug.LogWarning($"'{celesitalType}' CAUSED Invalid celestial type string '{celestialTypeString}' in resource template '{resource.resourceName}'.");
            }
        }

        return false;
    }

    private GameObject CreateCelestialGameObject(string name, CelestialType type, CelestialEnvironment environment = CelestialEnvironment.ROCKY, object dataSource = null)
    {
        GameObject go = new GameObject(name);

        // Add appropriate components based on type
        switch (type)
        {
            case CelestialType.STAR:
                if (dataSource is StarSO starData)
                {
                    AddStarComponents(go, environment, starData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.PLANET:
                if (dataSource is PlanetSO planetData)
                {
                    AddPlanetComponents(go, environment, planetData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.MOON:
                if (dataSource is MoonSO moonData)
                {
                    AddMoonComponents(go, environment, moonData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.NEBULA:
                if (dataSource is NebulaSO nebulaData)
                {
                    AddNebulaComponents(go, nebulaData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.ASTEROID:
                go.AddComponent<NetworkObject>(); // Asteroids are networked (can be mined)
                if (dataSource is AsteroidSO asteroidData)
                {
                    AddAsteroidComponents(go, environment, asteroidData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.ASTEROIDBELT:
                if (dataSource is AsteroidBeltSO beltData)
                {
                    AddAsteroidBeltComponents(go, environment, beltData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.STATION:
                go.AddComponent<NetworkObject>(); // Stations are networked
                if (dataSource is StationSO stationData)
                {
                    AddStationComponents(go, stationData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.GATE:
                go.AddComponent<NetworkObject>(); // Gates are networked
                if (dataSource is GateSO gateData)
                {
                    AddGateComponents(go, gateData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.SYSTEM:
                go.AddComponent<NetworkObject>(); // Systems are networked (allegiance can change)
                if (dataSource is SystemSO systemData)
                {
                    AddSystemComponents(go, systemData);
                    EditorUtility.SetDirty(go);
                }
                break;

            case CelestialType.GALAXY:
                go.AddComponent<NetworkObject>(); // Galaxies might be networked for control changes
                if (dataSource is GalaxySO galaxyData)
                {
                    AddGalaxyComponents(go, galaxyData);
                    EditorUtility.SetDirty(go);
                }
                break;
        }

        return go;
    }

    private void CreateAndAssignNebula(SystemSO systemSO, string systemName, string allegianceName, string systemFolder, OrbitParams orbit = null)
    {
        // Ensure subfolder for nebulas exists
        string nebulaFolder = Path.Combine(systemFolder, "Nebulas");
        EnsureFolder(nebulaFolder);

        // Create NebulaSO instance
        NebulaSO nebulaSO;
        if (baseNebulaSO != null)
        {
            nebulaSO = ScriptableObject.Instantiate(baseNebulaSO);
            nebulaSO.name = systemName + " Nebula";
        }
        else
        {
            nebulaSO = ScriptableObject.CreateInstance<NebulaSO>();
            nebulaSO.name = systemName + " Nebula";
        }

        // Optionally assign allegiance name (if relevant in your NebulaSO)
        // nebulaSO.allegiance = allegianceName; // Uncomment if NebulaSO supports it

        nebulaSO.orbitParams = orbit ?? systemSO.orbitParams;

        // Save NebulaSO asset
        string nebulaAssetPath = Path.Combine(nebulaFolder, $"{SanitizeFileName(nebulaSO.name)}.asset");
        AssetDatabase.CreateAsset(nebulaSO, nebulaAssetPath);

        // Create Nebula prefab for visuals
        GameObject nebulaPrefab = CreateAndSaveCelestialPrefab(nebulaSO.name, CelestialType.NEBULA, CelestialEnvironment.GAS, null, nebulaSO, nebulaSO.orbitParams);
        nebulaSO.prefabReference = nebulaPrefab;

        // Assign to systemSO
        systemSO.nebula = nebulaSO;

        EditorUtility.SetDirty(nebulaSO);
        EditorUtility.SetDirty(systemSO);

        Debug.Log($"Created and assigned nebula '{nebulaSO.name}' to system '{systemSO.systemName}'.");
    }

    private void AddStarComponents(GameObject go, CelestialEnvironment environment, StarSO starSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var starData = go.AddComponent<StarData>();
        starData.data = starSO;
        var light = go.AddComponent<Light>();

        // Configure light based on star type
        switch (environment)
        {
            case CelestialEnvironment.TEMPERATE: // Hot stars
                light.color = Color.white;
                light.intensity = 3f;
                break;
            case CelestialEnvironment.GAS: // Very hot stars
                light.color = Color.blue;
                light.intensity = 5f;
                break;
            case CelestialEnvironment.ICE: // Cooler red dwarf
                light.color = Color.red;
                light.intensity = 1.5f;
                break;
            case CelestialEnvironment.ROCKY: // Old star
                light.color = Color.orange;
                light.intensity = 2f;
                break;
            default:
                light.color = Color.yellow;
                light.intensity = 2f;
                break;
        }

        light.type = LightType.Point;
        light.range = 100f;

        // Add particle system for solar flares/corona effects
        var particleSystem = go.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.startColor = light.color;
        main.startLifetime = 5f;
        main.startSpeed = 2f;
        main.maxParticles = 100;

        // Add audio source for ambient star sounds
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.3f;
        audioSource.spatialBlend = 1f; // 3D sound
    }

    private void AddPlanetComponents(GameObject go, CelestialEnvironment environment, PlanetSO planetSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var sphereCollider = go.AddComponent<SphereCollider>();
        var planetData = go.AddComponent<PlanetData>();
        planetData.data = planetSO;

        // Configure scale based on environment
        float scale = environment switch
        {
            CelestialEnvironment.GAS => UnityEngine.Random.Range(3f, 6f),     // Gas giants are huge
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.8f, 1.5f),  // Ice planets are smaller
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(1f, 2f),   // Rocky planets are medium
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(2f, 2.5f), // Volcanic planets
            _ => UnityEngine.Random.Range(1f, 2f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add atmosphere effect for gas planets
        if (environment == CelestialEnvironment.GAS)
        {
            var atmosphereGO = new GameObject("Atmosphere");
            atmosphereGO.transform.SetParent(go.transform);
            var atmMeshFilter = atmosphereGO.AddComponent<MeshFilter>();
            var atmMeshRenderer = atmosphereGO.AddComponent<MeshRenderer>();
            atmosphereGO.transform.localScale = Vector3.one * 1.1f; // Slightly larger than planet
            atmosphereGO.transform.localPosition = Vector3.zero;
        }

        // Add particle effects for certain environments
        if (environment == CelestialEnvironment.ICE)
        {
            var particleSystem = go.AddComponent<ParticleSystem>();
            var main = particleSystem.main;

            main.startColor = Color.cyan;
            main.startLifetime = 10f;

            main.maxParticles = 50;
            main.startSpeed = 0.5f;
        }

        // Add rotation component for planet spinning
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, UnityEngine.Random.Range(5f, 15f), 0);
    }

    private void AddMoonComponents(GameObject go, CelestialEnvironment environment, MoonSO moonSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var sphereCollider = go.AddComponent<SphereCollider>();
        var moonData = go.AddComponent<MoonData>();
        moonData.data = moonSO;

        // Moons are generally smaller than planets
        float scale = environment switch
        {
            CelestialEnvironment.GAS => UnityEngine.Random.Range(0.3f, 0.6f),
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.2f, 0.5f),
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(0.3f, 0.7f),
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(0.2f, 0.3f),
            _ => UnityEngine.Random.Range(0.3f, 0.6f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add rotation (moons typically rotate slower than planets)
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, UnityEngine.Random.Range(2f, 8f), 0);

        // Add crater effects for rocky moons
        if (environment == CelestialEnvironment.ROCKY)
        {
            // You could add crater decals or additional geometry here
        }
    }

    private void AddNebulaComponents(GameObject go, NebulaSO nebulaSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var particleSystem = go.AddComponent<ParticleSystem>();
        var nebulaData = go.AddComponent<NebulaData>();
        nebulaData.data = nebulaSO;

        // Configure particle system for nebula effects
        var main = particleSystem.main;
        main.startLifetime = float.MaxValue; // Particles don't die
        main.startSpeed = 0.1f;
        main.maxParticles = 1000;
        main.startSize = UnityEngine.Random.Range(0.5f, 2f);
        main.startColor = new Color(
            UnityEngine.Random.Range(0.3f, 1f),
            UnityEngine.Random.Range(0.1f, 0.8f),
            UnityEngine.Random.Range(0.5f, 1f),
            0.3f // Semi-transparent
        );

        // Add shape module for volume emission
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 10f, 20f);

        // Add velocity over lifetime for gentle movement
        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        // Scale up the nebula
        go.transform.localScale = Vector3.one * UnityEngine.Random.Range(5f, 15f);

        // Add audio for ambient nebula sounds
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.1f;
        audioSource.spatialBlend = 1f;
    }

    private void AddAsteroidComponents(GameObject go, CelestialEnvironment environment, AsteroidSO asteroidSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var meshCollider = go.AddComponent<MeshCollider>();
        meshCollider.convex = true; // Required for rigidbodies

        // Add rigidbody for physics interactions
        var rigidbody = go.AddComponent<Rigidbody>();
        rigidbody.mass = UnityEngine.Random.Range(10f, 100f);
        rigidbody.linearDamping = 0.5f;
        rigidbody.angularDamping = 0.5f;

        // Add mining-related components
        var asteroidData = go.AddComponent<AsteroidData>();
        asteroidData.data = asteroidSO;

        // Vary size based on environment
        float scale = environment switch
        {
            CelestialEnvironment.ROCKY => UnityEngine.Random.Range(0.5f, 2f),
            CelestialEnvironment.GAS => UnityEngine.Random.Range(0.3f, 1.5f),
            CelestialEnvironment.ICE => UnityEngine.Random.Range(0.4f, 1.8f),
            CelestialEnvironment.TEMPERATE => UnityEngine.Random.Range(0.6f, 1.2f),
            _ => UnityEngine.Random.Range(0.5f, 1.5f)
        };
        go.transform.localScale = Vector3.one * scale;

        // Add rotation for tumbling effect
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(
            UnityEngine.Random.Range(-30f, 30f),
            UnityEngine.Random.Range(-30f, 30f),
            UnityEngine.Random.Range(-30f, 30f)
        );
    }

    private void AddStationComponents(GameObject go, StationSO stationSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var boxCollider = go.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // Allow ships to dock

        // Add multiple lights for station visibility
        var mainLight = go.AddComponent<Light>();
        mainLight.type = LightType.Point;
        mainLight.color = Color.cyan;
        mainLight.intensity = 2f;
        mainLight.range = 20f;

        // Add blinking navigation lights
        for (int i = 0; i < 4; i++)
        {
            var navLightGO = new GameObject($"NavLight_{i}");
            navLightGO.transform.SetParent(go.transform);
            navLightGO.transform.localPosition = new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-2f, 2f)
            );

            var navLight = navLightGO.AddComponent<Light>();
            navLight.type = LightType.Point;
            navLight.color = Color.red;
            navLight.intensity = 0.5f;
            navLight.range = 5f;

            var blinker = navLightGO.AddComponent<LightBlinker>();
            blinker.blinkInterval = UnityEngine.Random.Range(1f, 3f);
        }

        // Add docking bay trigger
        var dockingBayGO = new GameObject("DockingBay");
        dockingBayGO.transform.SetParent(go.transform);
        dockingBayGO.transform.localPosition = new Vector3(0, -1f, 0);
        var dockingCollider = dockingBayGO.AddComponent<BoxCollider>();
        dockingCollider.isTrigger = true;
        dockingCollider.size = new Vector3(5f, 2f, 5f);

        // Add station behavior component
        var stationData = go.AddComponent<StationData>();
        stationData.data = stationSO;

        // Add audio for station ambience
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.volume = 0.4f;
        audioSource.spatialBlend = 1f;

        // Add rotation (stations typically rotate for artificial gravity)
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, 5f, 0);
    }

    private void AddGateComponents(GameObject go, GateSO gateSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var boxCollider = go.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // Gates should be triggers for travel
        boxCollider.size = new Vector3(10f, 10f, 2f); // Large trigger area

        // Add gate-specific visual effects
        var particleSystem = go.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.startColor = Color.magenta;
        main.startLifetime = 2f;
        main.startSpeed = 3f;
        main.maxParticles = 200;
        main.startSize = 0.5f;

        // Configure shape for gate effect
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 5f;
        shape.donutRadius = 0.5f;

        // Add distinctive lighting
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.magenta;
        light.intensity = 3f;
        light.range = 25f;

        // Add pulsing light effect
        var lightPulser = go.AddComponent<LightPulser>();
        lightPulser.pulseSpeed = 2f;
        lightPulser.minIntensity = 1f;
        lightPulser.maxIntensity = 5f;

        // Add gate behavior component
        var gateData = go.AddComponent<GateData>();
        gateData.data = gateSO;

        // Add audio for gate activation
        var audioSource = go.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; // Only play when activated
        audioSource.volume = 0.8f;
        audioSource.spatialBlend = 1f;

        // Add rotation for gate spinning effect
        var rotator = go.AddComponent<SimpleRotator>();
        rotator.rotationSpeed = new Vector3(0, 0, 20f); // Spin around Z-axis
    }

    private void AddAsteroidBeltComponents(GameObject go, CelestialEnvironment environment, AsteroidBeltSO asteroidBeltSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();
        var particleSystem = go.AddComponent<ParticleSystem>();

        // Configure belt particle system
        var main = particleSystem.main;
        main.startLifetime = float.MaxValue; // Particles persist
        main.startSpeed = 0f; // Stationary particles
        main.maxParticles = 2000;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);

        // Configure particles based on environment
        if (environment == CelestialEnvironment.ROCKY)
        {
            main.startColor = new Color(0.6f, 0.5f, 0.4f, 0.8f); // Rocky brown
        }
        else // GAS or other
        {
            main.startColor = new Color(0.8f, 0.6f, 1f, 0.5f); // Ethereal purple
        }

        // Configure belt shape
        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 15f;
        shape.donutRadius = 3f;
        shape.arc = 360f;

        // Add slow rotation to the belt
        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.orbitalX = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        // Add belt behavior component
        var asteroidBeltData = go.AddComponent<AsteroidBeltData>();
        asteroidBeltData.data = asteroidBeltSO;

        // Scale the belt
        go.transform.localScale = Vector3.one * UnityEngine.Random.Range(1f, 2f);
    }

    private void AddSystemComponents(GameObject go, SystemSO systemSO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        // Systems are typically invisible organizational nodes, but add a subtle marker
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.white;
        light.intensity = 0.5f;
        light.range = 5f;
        light.enabled = false; // Disabled by default, can be enabled for debugging

        // Add system behavior for managing allegiances
        var systemData = go.AddComponent<SystemData>();
        systemData.data = systemSO;
        // Very small scale since it's just an organizational node
        go.transform.localScale = Vector3.one * 0.1f;
    }

    private void AddGalaxyComponents(GameObject go, GalaxySO galaxySO)
    {
        var meshFilter = go.AddComponent<MeshFilter>();
        var meshRenderer = go.AddComponent<MeshRenderer>();

        // Galaxies are even more abstract - just organizational nodes
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.yellow;
        light.intensity = 0.3f;
        light.range = 10f;
        light.enabled = false; // Disabled by default

        // Add galaxy behavior for large-scale management
        var galaxyData = go.AddComponent<GalaxyData>();
        galaxyData.data = galaxySO;
        // Very small scale
        go.transform.localScale = Vector3.one * 0.05f;
    }

    private GameObject CreateAndSaveCelestialPrefab(string name, CelestialType type, CelestialEnvironment environment, List<ResourceSO> resources = null, object celestialSO = null, OrbitParams orbitParams = null)
    {
        GameObject go = CreateCelestialGameObject(name, type, environment, celestialSO);

        // Add resource data component if needed
        if (resources != null && resources.Count > 0)
        {
            var resourceComponent = go.AddComponent<CelestialResources>();
            resourceComponent.resources = resources;
        }

        if (orbitParams != null)
        {
            var orbitComponent = go.AddComponent<OrbitComponent>();
            orbitComponent.orbitParams = orbitParams;
        }

        // Save as prefab
        string prefabPath = Path.Combine(prefabOutputFolder, type.ToString(), $"{SanitizeFileName(name)}.prefab");
        string prefabDirectory = Path.GetDirectoryName(prefabPath);
        EnsureFolder(prefabDirectory);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go); // Clean up the temp GameObject

        return prefab;
    }

    //Create orbits for celestials, gates and stations
    private OrbitParams CalculateOrbit(ulong networkId, ulong parentId, Vector3 parentCenter, CelestialType type, int sequenceIndex, bool requiresNetworking)
    {
        Vector3 centerPos = parentCenter; // Position of the parent orbit center

        float radius = 0f;
        float angularSpeed = 0f;

        // Example orbit rules matching your visualizer settings
        switch (type)
        {
            case CelestialType.GALAXY:
                radius = (float)(threadRandom.Value.NextDouble() * (1000f - 100f) + 100f) * 1f;
                angularSpeed = (float)(threadRandom.Value.NextDouble() * (0.045f - 0.01f) + 0.01f);
                break;

            case CelestialType.SYSTEM:
                radius = 16f + (sequenceIndex * 7.5f); // systemBase + idx*systemStep
                angularSpeed = 0.13f;                   // systemSpeed
                break;

            case CelestialType.STAR:
                radius = 4.5f;      // starRadius
                angularSpeed = 0.17f; // starSpeed
                break;

            case CelestialType.PLANET:
                radius = 7f + (sequenceIndex * 3f);       // planetBase + idx*planetStep
                angularSpeed = 7f / radius;                // planetBase / radius (simplified)
                break;

            case CelestialType.MOON:
                radius = 2f + (sequenceIndex * 1.2f);    // moonBase + idx*moonStep
                angularSpeed = 1.2f / radius;             // moonStep / radius
                break;

            case CelestialType.STATION:
                radius = 0.4f;         // stationRadius
                angularSpeed = 0.13f;  // systemSpeed (example)
                break;

            case CelestialType.ASTEROIDBELT:
                radius = 12f + (sequenceIndex * 1.8f);  // beltBase + idx*beltStep
                angularSpeed = 1.8f / radius;
                break;

            case CelestialType.ASTEROID:
                radius = 0.5f;
                angularSpeed = (float)(threadRandom.Value.NextDouble() * (0.6f - 0.3f) + 0.3f);
                break;

            case CelestialType.GATE:
                radius = 2f;
                angularSpeed = 0f;
                break;

            case CelestialType.NEBULA:
                radius = 25f;
                angularSpeed = 0.05f;
                break;

            default:
                radius = 1f;
                angularSpeed = 0.1f;
                break;
        }

        float phaseOffset = (float)(threadRandom.Value.NextDouble() * Mathf.PI * 2f);

        return new OrbitParams(networkId, parentId, centerPos, radius, angularSpeed, phaseOffset, type, requiresNetworking);
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

    // Cleans up a string to be a valid filename by removing invalid characters.
    // If input is null/empty/whitespace or results in invalid filename, returns a unique fallback name.
    private string SanitizeFileName(string input, string fallbackName = "UnnamedAsset")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            string fallback = fallbackName + Guid.NewGuid().ToString("N").Substring(0, 6);
            Debug.LogWarning($"SanitizeFileName: Input is null or whitespace. Using fallback name: {fallback}");
            return fallback;
        }

        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (Array.IndexOf(invalidChars, c) < 0)
                sb.Append(c);
        }

        var result = sb.ToString().Trim();

        if (string.IsNullOrEmpty(result))
        {
            string fallback = fallbackName + Guid.NewGuid().ToString("N").Substring(0, 6);
            Debug.LogWarning($"SanitizeFileName: '{input}' produced an empty or invalid filename. Using fallback name: {fallback}");
            return fallback;
        }

        Debug.Log($"Sanitized filename: {result}");
        return result;
    }

    //------------------------End of helper functions---------------------------
}
#endif