using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the binary persistence of the entire universe map.
/// It also handles the conversion of the flat binary list into a hierarchical V2 structure (Galaxy -> System -> Celestials).
/// </summary>
public static class UniversePersistenceManager
{
    // Touches Application paths, so call on the main thread and pass the result to background loads
    public static string GetPath()
    {
        // StreamingAssets ships with builds and is writable from the editor for saving
        return Path.Combine(Application.streamingAssetsPath, "UniverseMap.bin");
    }

    /// <summary>
    /// Saves a flat list of celestial data to the binary file.
    /// </summary>
    public static void Save(List<CelestialPersistenceData> dataList)
    {
        string path = GetPath();
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                writer.Write(dataList.Count);
                foreach (var data in dataList)
                {
                    data.WriteBinary(writer);
                }
            }
            Debug.Log($"[Persistence] Universe map saved to {path} ({dataList.Count} objects)");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Persistence] Failed to save universe map: {e.Message}");
        }
    }

    /// <summary>
    /// Loads the flat list of celestial data from the binary file.
    /// </summary>
    public static List<CelestialPersistenceData> Load(string path = null)
    {
        path ??= GetPath();
        if (!File.Exists(path))
        {
            // Fallback to legacy JSON if binary doesn't exist
            string jsonPath = Path.ChangeExtension(path, ".json");
            if (File.Exists(jsonPath))
            {
                Debug.Log("[Persistence] Loading legacy JSON universe map...");
                string json = File.ReadAllText(jsonPath);
                var data = JsonUtility.FromJson<UniverseMapData>(json);
                return data?.objects;
            }
            return null;
        }

        try
        {
            // Read-only + shared so several local instances (server, clients, Play Mode clones) can load at once
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                int count = reader.ReadInt32();
                List<CelestialPersistenceData> list = new List<CelestialPersistenceData>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(CelestialPersistenceData.ReadBinary(reader));
                }
                Debug.Log($"[Persistence] Universe map loaded from {path} ({count} objects)");
                return list;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Persistence] Failed to load universe map: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Loads the binary data and converts it into the hierarchical UniverseMapDataV2 structure.
    /// </summary>
    public static UniverseMapDataV2 LoadAndConvertV2(string path = null)
    {
        List<CelestialPersistenceData> flatData = Load(path);
        if (flatData == null || flatData.Count == 0) return null;

        Dictionary<ulong, CelestialPersistenceData> allObjectsMap = flatData.ToDictionary(d => d.orbit.NetworkId, d => d);
        
        UniverseMapDataV2 v2Map = new UniverseMapDataV2();
        Dictionary<ulong, GalaxyPersistenceData> galaxyMap = new Dictionary<ulong, GalaxyPersistenceData>();
        Dictionary<ulong, SystemPersistenceData> systemMap = new Dictionary<ulong, SystemPersistenceData>();
        List<CelestialPersistenceData> otherObjects = new List<CelestialPersistenceData>();

        // 1. Pass: Extract Galaxies and Systems
        foreach (var data in flatData)
        {
            if (data.orbit.Type == CelestialType.GALAXY)
            {
                var galaxy = new GalaxyPersistenceData
                {
                    id = data.orbit.NetworkId,
                    name = data.orbit.Name.ToString(),
                    absolutePosition = new Vector3d(data.orbit.CenterPos),
                    orbitRadius = data.orbit.Radius,
                    orbitSpeed = data.orbit.AngularSpeed,
                    tiltAngle = data.orbit.TiltAngle,
                    tiltAxis = data.orbit.TiltAxis
                };
                galaxyMap[galaxy.id] = galaxy;
                v2Map.galaxies.Add(galaxy);
            }
            else if (data.orbit.Type == CelestialType.SYSTEM)
            {
                var system = new SystemPersistenceData
                {
                    id = data.orbit.NetworkId,
                    name = data.orbit.Name.ToString(),
                    parentId = data.orbit.ParentId,
                    absolutePosition = new Vector3d(data.orbit.CenterPos),
                    orbitRadius = data.orbit.Radius,
                    orbitSpeed = data.orbit.AngularSpeed,
                    phaseOffset = data.orbit.PhaseOffset,
                    tiltAngle = data.orbit.TiltAngle,
                    tiltAxis = data.orbit.TiltAxis
                };
                systemMap[system.id] = system;
            }
            else
            {
                otherObjects.Add(data);
            }
        }

        // 2. Pass: Link Systems to Galaxies
        foreach (var system in systemMap.Values)
        {
            if (galaxyMap.TryGetValue(system.parentId, out var galaxy))
            {
                galaxy.systems.Add(system);
            }
        }

        // 3. Pass: Assign other celestial bodies (Planets, Moons, etc.) to their respective systems
        Dictionary<ulong, ulong> parentToRootSystemCache = new Dictionary<ulong, ulong>();
        foreach (var obj in otherObjects)
        {
            ulong rootSystemId = FindRootSystem(obj.orbit.ParentId, allObjectsMap, parentToRootSystemCache);
            if (rootSystemId != 0 && systemMap.TryGetValue(rootSystemId, out var system))
            {
                system.celestials.Add(obj);
            }
        }

        Debug.Log($"[Persistence] Conversion complete: {v2Map.galaxies.Count} galaxies, {systemMap.Count} systems.");
        return v2Map;
    }

    private static ulong FindRootSystem(ulong parentId, Dictionary<ulong, CelestialPersistenceData> map, Dictionary<ulong, ulong> cache)
    {
        if (parentId == 0) return 0;
        if (cache.TryGetValue(parentId, out ulong cachedId)) return cachedId;

        ulong currentId = parentId;
        int depthLimit = 100;

        while (currentId != 0 && depthLimit > 0)
        {
            if (map.TryGetValue(currentId, out var current))
            {
                if (current.orbit.Type == CelestialType.SYSTEM)
                {
                    cache[parentId] = currentId;
                    return currentId;
                }
                currentId = current.orbit.ParentId;
            }
            else break;
            depthLimit--;
        }

        cache[parentId] = 0;
        return 0;
    }
}
