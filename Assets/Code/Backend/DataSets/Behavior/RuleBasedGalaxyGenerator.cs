#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[Serializable]
public class GalaxyData
{
    public string name;
    public int dustParticles;
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
public class FactionData
{
    public string factionName;
    public string homeGalaxy;
    public List<string> corporations;
    public List<string> leaderNames;
    public List<string> starNames;
    public bool requiresNebulas;
}

[Serializable]
public class FactionsListData
{
    public List<FactionData> factions;
}

public interface IAllegianceAssignable
{
    void AssignAllegiance(ConceptSO allegiance);
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
    [Header("Galaxy Input")]
    public List<GalaxyData> galaxiesInput;
    [Header("Base Scriptable Objects")]
    public PlanetSO basePlanetSO;
    public MoonSO baseMoonSO;
    public StarSO baseStarSO;
    public AsteroidSO baseAsteroidSO;
    public AsteroidBeltSO baseAsteroidBeltSO;
    public GateSO baseGateSO;
    public StationSO baseStationSO;
    [Header("Resources")]
    public List<ResourceData> planetResources;
    public List<ResourceData> moonResources;
    public List<ResourceData> asteroidResources;
    [Header("Limits")]
    public int maxStationsPerGalaxy = 3;
    [Header("Generation parameters")]
    public GenerationCosts generationCosts;
    [Header("Output")]
    public string outputFolder = "Assets/RuleBasedGeneratedGalaxy";
    private List<SystemSO> allGeneratedSystems = new List<SystemSO>();
    [Header("Faction Input")]
    public TextAsset factionsJsonFile;
    [SerializeField]
    private List<FactionData> loadedFactions = new List<FactionData>();
    public NebulaSO baseNebulaSO;
    [Header("Resource Input")]
    public TextAsset resourcesJsonFile;
    [SerializeField]
    private List<ResourceData> loadedResources = new List<ResourceData>();

    void Start()
    {
        if (factionsJsonFile == null || resourcesJsonFile == null)
        {
            Debug.LogWarning("JSON files not assigned!");
            return;
        }

        string factionJsonText = factionsJsonFile.text;        // Main thread read
        string resourceJsonText = resourcesJsonFile.text;      // Main thread read

        Task.Run(() =>
        {
            try
            {
                var factions = ParseFactionJson(factionJsonText);
                var resources = ParseResourceJson(resourceJsonText);

                UnityEditor.EditorApplication.delayCall += () =>
                {
                    loadedFactions = factions;
                    Debug.Log("Factions loaded: " + (factions?.Count ?? 0));
                    loadedResources = resources;
                    Debug.Log("Resources loaded: " + (resources?.Count ?? 0));
                    GenerateAllGalaxies();
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in Task.Run: {ex}");
            }
        });
    }

    //------------------------File loading---------------------------

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

    /*private T LoadFromJSON<T>(TextAsset jsonFile, string fileType) where T : class
    {
        if (jsonFile == null)
        {
            Debug.LogWarning($"No {fileType} JSON file assigned!");
            return null;
        }

        if (string.IsNullOrEmpty(jsonFile.text))
        {
            Debug.LogWarning($"{fileType} JSON file '{jsonFile.name}' is empty!");
            return null;
        }

        var data = JsonUtility.FromJson<T>(jsonFile.text);
        if (data == null)
        {
            Debug.LogWarning($"{fileType} JSON file '{jsonFile.name}' is badly formatted.");
        }

        return data;
    }*/

    //------------------------End of file loading---------------------------

    //------------------------Main functions---------------------------
    public void GenerateAllGalaxies()
    {
        Debug.Log("Generating Universe");
        try
        {
            // Clear previously generated systems and load necessary data
            allGeneratedSystems.Clear();

            // --- 1. Concept Generation ---
            var conceptGenerator = InitializeConceptGenerator();
            conceptGenerator.CreateConceptsFromFactions(loadedFactions);
            Dictionary<string, ConceptSO> factionConcepts = conceptGenerator.GetFactionConceptDictionary();

            // --- 2. Galaxy Generation ---
            foreach (var galaxyData in galaxiesInput)
            {
                try
                {
                    Debug.Log($"Generating galaxy: {galaxyData.name} with {galaxyData.dustParticles} dust");
                    string galaxyFolder = Path.Combine(outputFolder, SanitizeFileName(galaxyData.name));
                    EnsureFolder(galaxyFolder);

                    // Create Galaxy ScriptableObject
                    GalaxySO galaxySO = ScriptableObject.CreateInstance<GalaxySO>();
                    galaxySO.galaxyName = galaxyData.name;
                    galaxySO.systems = new List<SystemSO>();

                    int dustLeft = galaxyData.dustParticles;
                    int stationCount = 0;

                    // --- 2.1 Generate Faction Systems ---
                    GenerateSystems(galaxySO, galaxyData, galaxyFolder, factionConcepts, ref dustLeft, ref stationCount, isFactionBased: true);

                    // --- 2.2 Generate Random Systems ---
                    GenerateSystems(galaxySO, galaxyData, galaxyFolder, factionConcepts, ref dustLeft, ref stationCount, isFactionBased: false);

                    // --- 2.3 Finalize Galaxy ---
                    ConnectGatesWithinGalaxy(galaxySO, galaxyFolder);

                    SaveGalaxyAsset(galaxySO, galaxyFolder);

                    Debug.Log($"Galaxy '{galaxySO.galaxyName}' saved successfully.");

                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error generating galaxy {galaxyData.name}: {ex.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Galaxy generation complete");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during galaxy generation: {ex.Message}");
        }
    }

    private ConceptSOGenerator InitializeConceptGenerator()
    {
        return new ConceptSOGenerator
        {
            outputFolder = Path.Combine(outputFolder, "Concepts")
        };
    }

    private void SaveGalaxyAsset(GalaxySO galaxySO, string galaxyFolder)
    {
        string sanitizedName = SanitizeFileName(galaxySO.galaxyName);
        string galaxyAssetPath = Path.Combine(galaxyFolder, $"{sanitizedName}.asset");
        AssetDatabase.CreateAsset(galaxySO, galaxyAssetPath);
        EditorUtility.SetDirty(galaxySO);
    }

    //------------------------End of main functions---------------------------

    //------------------------Helper functions---------------------------

    private void GenerateSystems(GalaxySO galaxySO, GalaxyData galaxyData, string galaxyFolder, Dictionary<string, ConceptSO> factionConcepts, ref int dustLeft, ref int stationCount, bool isFactionBased = false)
    {
        // Define a maximum limit for systems to prevent runaway generation
        int maxSystemsPerGalaxy = 40; // Adjust this value as needed

        // Get factions for faction-based generation
        List<FactionData> galaxyFactions = isFactionBased ? loadedFactions.FindAll(f => f.homeGalaxy == galaxyData.name) : null;

        if (isFactionBased && galaxyFactions != null)
        {
            foreach (var faction in galaxyFactions)
            {
                foreach (var sysName in faction.starNames)
                {
                    // Check if the system already exists
                    if (galaxySO.systems.Exists(s => s.systemName == sysName))
                        continue;

                    // Stop if we exceed the maximum system limit
                    if (galaxySO.systems.Count >= maxSystemsPerGalaxy)
                    {
                        Debug.LogWarning("Reached the maximum system limit for the galaxy.");
                        return;
                    }

                    // Create the system folder
                    string systemFolder = Path.Combine(galaxyFolder, SanitizeFileName(sysName));
                    EnsureFolder(systemFolder);

                    // Create the system ScriptableObject
                    SystemSO systemSO = ScriptableObject.CreateInstance<SystemSO>();
                    systemSO.systemName = sysName;

                    // Assign allegiance if the faction concept exists
                    if (factionConcepts.TryGetValue(faction.factionName, out ConceptSO factionConcept) && factionConcept != null)
                    {
                        AssignAllegiance(systemSO, factionConcept);

                        // Create a nebula if required by the faction
                        if (faction.requiresNebulas)
                        {
                            CreateAndAssignNebula(systemSO, systemSO.systemName, systemSO.allegiance.conceptName, systemFolder);
                        }
                    }

                    // Generate celestial bodies for the system
                    CreateSystemBodies(systemSO, systemFolder, dustLeft, ref stationCount, factionConcept);

                    // Add the system to the galaxy
                    galaxySO.systems.Add(systemSO);
                    EditorUtility.SetDirty(galaxySO);

                    // Save the system asset
                    string systemAssetPath = Path.Combine(systemFolder, $"{SanitizeFileName(sysName)}.asset");
                    AssetDatabase.CreateAsset(systemSO, systemAssetPath);
                    EditorUtility.SetDirty(systemSO);
                }
            }
        }
        else
        {
            // Generate random systems until dust runs out or the system limit is reached
            while (dustLeft >= generationCosts.system + generationCosts.star && galaxySO.systems.Count < maxSystemsPerGalaxy)
            {
                // Deduct the cost for creating a system and a star
                dustLeft -= generationCosts.system + generationCosts.star;

                // Generate a unique system name
                string systemName = $"{galaxyData.name} System {galaxySO.systems.Count + 1}";
                string systemFolder = Path.Combine(galaxyFolder, SanitizeFileName(systemName));
                EnsureFolder(systemFolder);

                // Create the system ScriptableObject
                SystemSO systemSO = ScriptableObject.CreateInstance<SystemSO>();
                systemSO.systemName = systemName;

                // Generate celestial bodies for the system
                dustLeft = CreateSystemBodies(systemSO, systemFolder, dustLeft, ref stationCount, null);

                // Add the system to the galaxy
                galaxySO.systems.Add(systemSO);
                EditorUtility.SetDirty(galaxySO);

                // Save the system asset
                string systemAssetPath = Path.Combine(systemFolder, $"{SanitizeFileName(systemName)}.asset");
                AssetDatabase.CreateAsset(systemSO, systemAssetPath);
                EditorUtility.SetDirty(systemSO);
            }
        }

        // Log the remaining dust for debugging purposes
        Debug.Log($"System generation complete. Remaining dust: {dustLeft}");
    }

    private int CreateSystemBodies(SystemSO systemSO, string systemFolder, int dustLeft, ref int stationCount, ConceptSO allegiance)
    {
        // --- 1. Create Stars ---
        string starFolder = Path.Combine(systemFolder, "Stars");
        EnsureFolder(starFolder);

        // Two-star systems are rare (10% chance)
        int numberOfStars = UnityEngine.Random.value < 0.1f ? 2 : 1;

        for (int i = 0; i < numberOfStars; i++)
        {
            string starName = $"{systemSO.systemName} Star {i + 1}";
            StarSO starSO = CreateAndSaveStar(starName, starFolder);
            systemSO.stars.Add(starSO);
        }

        // --- 2. Planets and Moons ---
        string planetsFolder = Path.Combine(systemFolder, "Planets");
        EnsureFolder(planetsFolder);

        if (allegiance != null)
        {
            // Decide how many planets and moons to spawn (use dust allocation or a set number)
            int planetCount = Mathf.Min(dustLeft / generationCosts.planet, 5);
            bool stationPlaced = false;

            for (int i = 0; i < planetCount; i++)
            {
                if (dustLeft < generationCosts.planet) break;
                dustLeft -= generationCosts.planet;

                string planetName = $"{systemSO.systemName} Planet {i + 1}";
                CelestialType planetType = GetRandomCelestialType();
                string planetFolder = Path.Combine(planetsFolder, SanitizeFileName(planetName));
                EnsureFolder(planetFolder);

                PlanetSO planetSO = CreatePlanet(planetName, planetFolder);
                planetSO.type = planetType;
                EditorUtility.SetDirty(planetSO);
                if (planetSO.resources == null || planetSO.resources.Count == 0)
                {
                    planetSO.resources = GenerateResources(loadedResources, planetFolder, "Planet", planetSO.type);
                }
                systemSO.planets.Add(planetSO);

                string moonsFolder = Path.Combine(planetFolder, "Moons");
                EnsureFolder(moonsFolder);

                int moonCount = Mathf.Min(dustLeft / generationCosts.moon, 3);

                for (int m = 0; m < moonCount; m++)
                {
                    if (dustLeft < generationCosts.moon) break;
                    dustLeft -= generationCosts.moon;
                    string moonName = $"{planetSO.planetName} Moon {m + 1}";
                    CelestialType moonType = GetRandomCelestialType();
                    MoonSO moonSO = CreateMoon(moonName, moonsFolder);
                    moonSO.type = moonType;
                    EditorUtility.SetDirty(moonSO);
                    if (moonSO.resources == null || moonSO.resources.Count == 0)
                    {
                        moonSO.resources = GenerateResources(loadedResources, moonsFolder, "Moon", moonSO.type);
                    }
                    planetSO.moons.Add(moonSO);

                    // Place at least one station on a moon
                    if (!stationPlaced && baseStationSO != null)
                    {
                        CreateStationForMoon(moonSO, moonsFolder, systemSO, ref stationCount);
                        stationPlaced = true;
                    }
                }
            }
            // If you want 1 station per planet or per system, adjust stationPlaced logic accordingly
        }
        else
        {
            // Random system: Generate planets and moons based on dustLeft
            int planetCount = Mathf.Min(dustLeft / generationCosts.planet, 5);

            for (int i = 0; i < planetCount; i++)
            {
                if (dustLeft < generationCosts.planet)
                    break;

                dustLeft -= generationCosts.planet;
                string planetName = $"{systemSO.systemName} Planet {i + 1}";
                CelestialType planetType = GetRandomCelestialType(); // Assign a random type
                string planetFolder = Path.Combine(planetsFolder, SanitizeFileName(planetName));
                EnsureFolder(planetFolder);

                PlanetSO planetSO = CreatePlanet(planetName, planetFolder);
                planetSO.type = planetType;
                EditorUtility.SetDirty(planetSO);
                if (planetSO.resources == null || planetSO.resources.Count == 0)
                {
                    Debug.Log($"Generating resources for planet: {planetSO.name}");
                    planetSO.resources = GenerateResources(loadedResources, planetFolder, "Planet", planetSO.type);
                }
                else
                {
                    Debug.Log($"planetSO.resources is already initialized for planet: {planetSO.name}");
                }
                systemSO.planets.Add(planetSO);

                string moonsFolder = Path.Combine(planetFolder, "Moons");
                EnsureFolder(moonsFolder);

                int moonCount = Mathf.Min(dustLeft / generationCosts.moon, 3);

                for (int m = 0; m < moonCount; m++)
                {
                    if (dustLeft < generationCosts.moon)
                        break;

                    dustLeft -= generationCosts.moon;
                    string moonName = $"{planetSO.planetName} Moon {m + 1}";
                    CelestialType moonType = GetRandomCelestialType(); // Assign a random type
                    MoonSO moonSO = CreateMoon(moonName, moonsFolder);
                    moonSO.type = moonType;
                    EditorUtility.SetDirty(moonSO);
                    if (moonSO.resources == null || moonSO.resources.Count == 0)
                    {
                        Debug.Log($"Generating resources for moon: {moonSO.name}");
                        moonSO.resources = GenerateResources(loadedResources, moonsFolder, "Moon", moonSO.type);
                    }
                    else
                    {
                        Debug.Log($"moonSO.resources is already initialized for moon: {moonSO.name}");
                    }
                    planetSO.moons.Add(moonSO);
                }
            }
        }

        // --- 3. Asteroid Belts ---
        string beltFolder = Path.Combine(systemFolder, "AsteroidBelts");
        EnsureFolder(beltFolder);

        int beltCount = allegiance != null
            ? UnityEngine.Random.Range(2, 5) // Faction systems have more belts
            : Mathf.Min(dustLeft / generationCosts.asteroidBelt, 3);

        for (int b = 0; b < beltCount; b++)
        {
            if (dustLeft < generationCosts.asteroidBelt && allegiance == null)
                break;

            dustLeft -= generationCosts.asteroidBelt;
            string beltName = $"{systemSO.systemName} Belt {b + 1}";
            CelestialType beltType = GetRandomCelestialType(); // Assign a random type
            string currentBeltFolder = Path.Combine(beltFolder, SanitizeFileName(beltName));
            EnsureFolder(currentBeltFolder);

            int asteroidCount = UnityEngine.Random.Range(5, 15);
            AsteroidBeltSO beltSO = CreateAsteroidBelt(beltName, currentBeltFolder, asteroidCount, ref dustLeft, beltType);
            beltSO.type = beltType;
            systemSO.asteroidBelts.Add(beltSO);
        }

        // --- 4. Nebulas ---
        if (systemSO.planets.Count < 3 && UnityEngine.Random.value < 0.5f && systemSO.nebula == null)
        {
            CreateAndAssignNebula(systemSO, systemSO.systemName, allegiance?.name, systemFolder);
        }

        // --- 5. Assign Allegiance ---
        AssignAllegiance(systemSO, allegiance);

        EditorUtility.SetDirty(systemSO);
        return dustLeft;
    }

    private void CreateAndAssignNebula(SystemSO systemSO, string systemName, string allegianceName, string systemFolder)
    {
        string nebulaName = $"{systemName} Nebula";
        string nebulaFolder = Path.Combine(systemFolder, "Nebulas");

        NebulaSO nebulaSO = CreateAsset(nebulaName, nebulaFolder, baseNebulaSO);
        if (nebulaSO == null)
        {
            Debug.LogError("Failed to create nebula.");
            return;
        }

        systemSO.nebula = nebulaSO;
        Debug.Log($"Created and assigned nebula '{nebulaName}' to system '{systemSO.systemName}'");
    }

    private void CreateStationForMoon(MoonSO moonSO, string moonsFolder, SystemSO systemSO, ref int stationCount)
    {
        string stationsFolder = Path.Combine(moonsFolder, "Stations");
        EnsureFolder(Path.Combine(stationsFolder, moonSO.moonName));

        StationSO stationSO = Instantiate(baseStationSO);
        stationSO.stationName = $"{moonSO.moonName} Station";

        if (systemSO != null)
        {
            stationSO.allegiance = systemSO.allegiance;
        }

        string stationAssetPath = Path.Combine(stationsFolder, $"{SanitizeFileName(stationSO.stationName)}.asset");
        AssetDatabase.CreateAsset(stationSO, stationAssetPath);
        EditorUtility.SetDirty(stationSO);

        moonSO.stations = new List<StationSO> { stationSO };

        if (systemSO != null)
        {
            if (systemSO.stations == null)
            {
                systemSO.stations = new List<StationSO>();
            }
            systemSO.stations.Add(stationSO);
        }
        stationCount++;
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

    private StarSO CreateAndSaveStar(string name, string folder)
    {
        return CreateAsset(name, folder, baseStarSO);
    }

    private PlanetSO CreatePlanet(string name, string folder)
    {
        return CreateAsset(name, folder, basePlanetSO);
    }

    private MoonSO CreateMoon(string name, string folder)
    {
        return CreateAsset(name, folder, baseMoonSO);
    }

    private AsteroidSO CreateAsteroid(string name, string folder)
    {
        return CreateAsset(name, folder, baseAsteroidSO);
    }

    private AsteroidBeltSO CreateAsteroidBelt(string name, string folder, int asteroidCount, ref int dustLeft, CelestialType beltType)
    {
        var beltSO = CreateAsset(name, folder, baseAsteroidBeltSO);
        if (beltSO == null)
            return null;

        beltSO.type = beltType; // Assign the type
        beltSO.asteroids = new List<AsteroidSO>();

        string asteroidsFolder = Path.Combine(folder, "Asteroids");
        EnsureFolder(asteroidsFolder);

        for (int i = 0; i < asteroidCount && dustLeft >= generationCosts.asteroid; i++)
        {
            dustLeft -= generationCosts.asteroid;

            string asteroidName = $"{name} Asteroid {i + 1}";
            AsteroidSO asteroidSO = CreateAsteroid(asteroidName, asteroidsFolder);
            if (asteroidSO != null)
            {
                if (asteroidSO.resources == null || asteroidSO.resources.Count == 0)
                {
                    // Pass the beltType to GenerateResources
                    asteroidSO.resources = GenerateResources(loadedResources, asteroidsFolder, "Asteroid", beltType);
                }
                beltSO.asteroids.Add(asteroidSO);
            }
            else
            {
                Debug.LogWarning($"Failed to create asteroid: {asteroidName}");
            }
        }

        return beltSO;
    }

    private List<ResourceSO> GenerateResources(List<ResourceData> resources, string targetFolder, string category, CelestialType bodyType)
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

    private bool IsResourceAllowedForCelestialType(ResourceData resource, CelestialType bodyType)
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

    private CelestialType GetRandomCelestialType()
    {
        // Randomly pick a CelestialType
        return (CelestialType)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(CelestialType)).Length);
    }

    private void AssignAllegiance(UnityEngine.Object entity, ConceptSO allegiance)
    {
        if (allegiance == null)
        {
            Debug.LogWarning($"AssignAllegiance: Tried to assign null allegiance to {entity.name}");
            return;
        }

        switch (entity)
        {
            case SystemSO system:
                if (system.allegiance == null)
                {
                    system.allegiance = allegiance;
                    Debug.Log($"Assigned allegiance '{allegiance.name}' to system '{system.systemName}'");
                }
                break;

            case StationSO station:
                if (station.allegiance == null)
                {
                    station.allegiance = allegiance;
                    Debug.Log($"Assigned allegiance '{allegiance.name}' to station '{station.stationName}'");
                }
                break;

            default:
                Debug.LogWarning($"AssignAllegiance: Unsupported entity type {entity.GetType().Name}");
                break;
        }
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

    // General helper to create and save a ScriptableObject asset
    private T CreateAsset<T>(string name, string folder, T baseAsset, bool overwrite = false) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(folder) || baseAsset == null)
        {
            Debug.LogError($"Invalid inputs for creating asset: {name}");
            return null;
        }

        EnsureFolder(folder);

        var asset = Instantiate(baseAsset);
        asset.name = name;

        string assetPath = Path.Combine(folder, $"{SanitizeFileName(name)}.asset");

        if (overwrite && AssetDatabase.LoadAssetAtPath<T>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
            Debug.Log($"Overwriting existing asset at: {assetPath}");
        }

        var savedAsset = CreateAndSaveAsset(asset, assetPath);
        Debug.Log($"Asset '{name}' created successfully at '{folder}'.");
        return savedAsset;
    }

    private T CreateAndSaveAsset<T>(T asset, string path, bool overwrite = false) where T : ScriptableObject
    {
        if (overwrite && AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"Overwriting existing asset at: {path}");
        }

        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        Debug.Log($"Saved asset at: {path}");
        return asset;
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