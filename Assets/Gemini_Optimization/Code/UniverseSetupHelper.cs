using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

#if UNITY_EDITOR
public class UniverseSetupHelper : MonoBehaviour
{
    [MenuItem("The Rift/Setup Universe Scene")]
    public static void SetupScene()
    {
        // 1. Create Managers Object
        GameObject mgr = GameObject.Find("UniverseManager");
        if (mgr == null) mgr = new GameObject("UniverseManager");

        // 2. Add NetworkManager
        var netMgr = GameObject.FindObjectOfType<NetworkManager>();
        if (netMgr == null)
        {
            // Second check by name just in case FindObjectOfType missed it
            GameObject existing = GameObject.Find("NetworkManager");
            if (existing != null) netMgr = existing.GetComponent<NetworkManager>();

            if (netMgr == null)
            {
                GameObject nObj = new GameObject("NetworkManager");
                netMgr = nObj.AddComponent<NetworkManager>();
            }
        }

        // Ensure persistence and singleton behavior
        if (netMgr.GetComponent<NetworkManagerPersistence>() == null)
        {
            netMgr.gameObject.AddComponent<NetworkManagerPersistence>();
        }

        // Add AutoStart script
        if (mgr.GetComponent<NetworkAutoStart>() == null) mgr.AddComponent<NetworkAutoStart>();

        // 3. Configure NetworkManager
        string playerPath = "Assets/Prefabs/Player Prefabs/PlayerFighterPiloting.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (playerPrefab != null)
        {
            netMgr.NetworkConfig.PlayerPrefab = playerPrefab;
        }

        // Add Transport if missing (Standard Unity Transport)
        if (netMgr.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>() == null)
        {
            netMgr.gameObject.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        }

        // 4. Add Components
        var pool = mgr.GetComponent<CelestialPool>() ?? mgr.AddComponent<CelestialPool>();
        var visualizer = mgr.GetComponent<SystemCenteredVisualizer>() ?? mgr.AddComponent<SystemCenteredVisualizer>();
        var generator = mgr.GetComponent<UniverseGenerator>() ?? mgr.AddComponent<UniverseGenerator>();
        var registry = mgr.GetComponent<CelestialAssetRegistry>() ?? mgr.AddComponent<CelestialAssetRegistry>();
        var env = mgr.GetComponent<SpaceEnvironmentManager>() ?? mgr.AddComponent<SpaceEnvironmentManager>();

        // Create Star Materials
        CreateStarMaterials();
        CreateNebulaMaterial();

        // Auto-populate Registry (Updated names)
        registry.starTemplates = FindAssets<StarSO>("t:StarSO");
        registry.planetTemplates = FindAssets<PlanetSO>("t:PlanetSO");
        registry.moonTemplates = FindAssets<MoonSO>("t:MoonSO");
        registry.stationTemplates = FindAssets<StationSO>("t:StationSO");
        registry.asteroidTemplates = FindAssets<AsteroidSO>("t:AsteroidSO");
        registry.gateTemplates = FindAssets<GateSO>("t:GateSO");
        registry.nebulaTemplates = FindAssets<NebulaSO>("t:NebulaSO");

        // 5. Configure Pool Prefabs (Auto-linking)
        string prefabPath = "Assets/Prefabs/Celestial Prefabs/";
        pool.poolConfigs = new List<CelestialPool.PoolConfig>
        {
            CreateConfig(CelestialType.STAR, prefabPath + "StarPrefab.prefab", 10),
            CreateConfig(CelestialType.PLANET, prefabPath + "PlanetPrefab.prefab", 20),
            CreateConfig(CelestialType.MOON, prefabPath + "MoonPrefab.prefab", 50),
            CreateConfig(CelestialType.ASTEROID, prefabPath + "AsteroidPrefab.prefab", 200),
            CreateConfig(CelestialType.STATION, prefabPath + "StationPrefab.prefab", 10),
            CreateConfig(CelestialType.GATE, prefabPath + "GatePrefab.prefab", 5),
            CreateConfig(CelestialType.ASTEROIDBELT, prefabPath + "AsteroidBeltPrefab.prefab", 10),
            CreateConfig(CelestialType.NEBULA, prefabPath + "NebulaPrefab.prefab", 5)
        };

        // 6. No global light - stars handle local lighting
        GameObject oldLight = GameObject.Find("Directional Light");
        if (oldLight != null)
        {
            DestroyImmediate(oldLight);
            Debug.Log("Cleaned up global Directional Light as stars now provide local system lighting.");
        }

        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(pool);
        EditorUtility.SetDirty(netMgr);

        Debug.Log("Scene 'GalaxyBuilder' setup complete! Please check the UniverseManager object in the Inspector.");
        Selection.activeObject = mgr;
    }

    private static void CreateStarMaterials()
    {
        string dir = "Assets/Prefabs/Celestial Prefabs/Materials/Stars";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        Shader starShader = Shader.Find("Custom/StandardStars");
        if (starShader == null) return;

        // Red Giant
        string redPath = dir + "/RedGiant.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(redPath) == null)
        {
            Material mat = new Material(starShader);
            mat.SetColor("_Color", new Color(1f, 0.4f, 0.1f));
            mat.SetColor("_GlowColor", new Color(1f, 0.2f, 0f));
            mat.SetFloat("_Emission", 25.0f);
            mat.SetFloat("_GlowPower", 1.5f);
            AssetDatabase.CreateAsset(mat, redPath);
        }

        // White Dwarf
        string whitePath = dir + "/WhiteDwarf.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(whitePath) == null)
        {
            Material mat = new Material(starShader);
            mat.SetColor("_Color", new Color(0.8f, 0.9f, 1f));
            mat.SetColor("_GlowColor", new Color(0.4f, 0.6f, 1f));
            mat.SetFloat("_Emission", 20.0f);
            mat.SetFloat("_GlowPower", 2.0f);
            AssetDatabase.CreateAsset(mat, whitePath);
        }

        AssetDatabase.SaveAssets();
    }

    private static void CreateNebulaMaterial()
    {
        string dir = "Assets/Prefabs/Celestial Prefabs/Materials";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        Shader nebShader = Shader.Find("Custom/VolumetricNebula");
        if (nebShader == null) return;

        string path = dir + "/DefaultNebula.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
        {
            Material mat = new Material(nebShader);
            mat.SetColor("_Color1", new Color(0.2f, 0.5f, 1.0f, 0.5f));
            mat.SetColor("_Color2", new Color(0.5f, 0.1f, 0.8f, 0.3f));
            mat.SetFloat("_Density", 2.0f);
            mat.SetFloat("_FresnelPower", 1.5f);
            AssetDatabase.CreateAsset(mat, path);
        }

        AssetDatabase.SaveAssets();
    }

    private static List<T> FindAssets<T>(string filter) where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();
        string[] guids = AssetDatabase.FindAssets(filter);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) assets.Add(asset);
        }
        return assets;
    }

    private static CelestialPool.PoolConfig CreateConfig(CelestialType type, string path, int size)
    {
        return new CelestialPool.PoolConfig
        {
            type = type,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
            initialSize = size
        };
    }
}
#endif
