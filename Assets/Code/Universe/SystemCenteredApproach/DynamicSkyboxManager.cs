using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the dynamic rendering of distant star systems in the skybox.
/// Instead of a static texture, this script generates a procedural mesh where each star 
/// represents a real system in the 'universeMap', correctly positioned relative to the player's current system.
/// </summary>
public class DynamicSkyboxManager : MonoBehaviour
{
    public static DynamicSkyboxManager Instance { get; private set; }

    [Header("Skybox Visual Settings")]
    [Tooltip("Distance from the camera where stars are rendered.")]
    public float skyboxRadius;
    
    [Tooltip("Visual size of each distant star quad.")]
    public float starSize;
    
    [Tooltip("Systems at this distance or further get the dimmest, smallest stars.")]
    public float farSystemDistance = 120000f;

    // Additive point-star shader drawn at the far plane (Resources/Celestial/SystemStars)
    private Material starMaterial;

    [Header("Dependencies")]
    [Tooltip("Reference to the SystemCenteredVisualizer to get current system data.")]
    public SystemCenteredVisualizer visualizer;

    [Header("Update Performance")]
    [Tooltip("Time interval (in seconds) between skybox position updates.")]
    public float updateInterval;
    
    // Internal state
    private float nextUpdateTime = 0f;
    private ulong currentSystemId;
    private Vector3d currentSystemAbsolutePosition;
    private Mesh starMesh;
    private List<SystemMetadata> _cachedSystems = new List<SystemMetadata>();
    private HashSet<ulong> _cachedSystemIds = new HashSet<ulong>();
    // Metadata arrives in many RPC batches; rebuild once per frame at most instead of once per batch
    private bool _starsDirty;

    public int StarCount => starMesh != null ? starMesh.vertexCount / 4 : 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize the mesh for star rendering
        starMesh = new Mesh();
        starMesh.name = "DynamicSkybox_Stars";
        starMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        starMaterial = CelestialVisuals.GetMaterial("SystemStars");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Sets the entire universe map and triggers a visual update.
    /// Usually called by SystemCenteredVisualizer on the Server or on local file load.
    /// </summary>
    public void SetUniverseMap(UniverseMapDataV2 map)
    {
        if (visualizer != null) visualizer.universeMap = map;
        _cachedSystems.Clear();
        _cachedSystemIds.Clear();
        _starsDirty = true;
    }

    /// <summary>
    /// Synchronizes a list of system metadata (IDs and Absolute Positions).
    /// Used by Clients who don't have the full 'UniverseMap.bin' yet.
    /// </summary>
    public void SyncAllSystems(List<SystemMetadata> systems)
    {
        // The full map (server/host) already has every system
        if (visualizer != null && visualizer.universeMap != null) return;

        foreach (var s in systems)
        {
            if (_cachedSystemIds.Add(s.id))
            {
                _cachedSystems.Add(s);
                _starsDirty = true;
            }
        }
    }

    /// <summary>
    /// Updates the player's current system location and refreshes the relative star positions.
    /// </summary>
    public void SetCurrentSystem(ulong systemId, Vector3d absolutePosition)
    {
        currentSystemId = systemId;
        currentSystemAbsolutePosition = absolutePosition;
        _starsDirty = true;
    }

    /// <summary>
    /// Re-calculates the relative positions of all star systems and rebuilds the star mesh.
    /// </summary>
    private void UpdateStarPositions()
    {
        double time = GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;
        List<Vector3> positions = new List<Vector3>();
        List<Color> colors = new List<Color>();

        // Scenario A: Full Universe Map is available (Server or Local Load)
        if (visualizer != null && visualizer.universeMap != null)
        {
            // 1. Refresh our own absolute position based on current galactic time
            foreach (var galaxy in visualizer.universeMap.galaxies)
            {
                Vector3d galaxyPos = galaxy.GetAbsolutePosition(time);
                foreach (var system in galaxy.systems)
                {
                    if (system.id == currentSystemId)
                    {
                        currentSystemAbsolutePosition = system.GetAbsolutePosition(time, galaxyPos);
                        break;
                    }
                }
            }

            // 2. Calculate relative directions for all OTHER systems
            foreach (var galaxy in visualizer.universeMap.galaxies)
            {
                Vector3d galaxyPos = galaxy.GetAbsolutePosition(time);
                foreach (var system in galaxy.systems)
                {
                    if (system.id == currentSystemId) continue;

                    Vector3d systemPos = system.GetAbsolutePosition(time, galaxyPos);
                    AddStar(positions, colors, system.id, systemPos - currentSystemAbsolutePosition);
                }
            }
        }
        // Scenario B: Only metadata is available (Client awaiting full sync)
        else if (_cachedSystems != null && _cachedSystems.Count > 0)
        {
             var mySystem = _cachedSystems.Find(x => x.id == currentSystemId);
             if (mySystem.id != 0) 
             {
                 currentSystemAbsolutePosition = mySystem.absolutePosition;
             }

             foreach (var system in _cachedSystems)
             {
                if (system.id == currentSystemId) continue;

                AddStar(positions, colors, system.id, system.absolutePosition - currentSystemAbsolutePosition);
             }
        }

        if (positions.Count == 0) 
        {
            if (starMesh != null) starMesh.Clear();
            return;
        }

        // Rebuild the mesh with billboarding quads for each star
        BuildStarMesh(positions, colors);
    }

    // Tint from the system id (same on every client), brightness and size from distance
    private void AddStar(List<Vector3> positions, List<Color> colors, ulong id, Vector3d offset)
    {
        double distance = offset.magnitude;
        if (distance < 1e-3) return;

        float near = 1f - Mathf.Clamp01((float)(distance / farSystemDistance));
        var rng = new CelestialVisuals.Rng(id, 0);
        float temperature = rng.Next();
        Color tint = temperature < 0.5f
            ? Color.Lerp(new Color(0.70f, 0.80f, 1.00f), new Color(1.00f, 0.97f, 0.92f), temperature * 2f)
            : Color.Lerp(new Color(1.00f, 0.97f, 0.92f), new Color(1.00f, 0.68f, 0.42f), (temperature - 0.5f) * 2f);
        tint.a = Mathf.Lerp(0.25f, 1.6f, near * near) * rng.Range(0.7f, 1.3f);

        positions.Add((offset * (1.0 / distance)).ToVector3() * skyboxRadius);
        colors.Add(tint);
    }

    private void BuildStarMesh(List<Vector3> positions, List<Color> colors)
    {
        starMesh.Clear();
        Vector3[] vertices = new Vector3[positions.Count * 4];
        Vector2[] uvs = new Vector2[positions.Count * 4];
        Color[] vertexColors = new Color[positions.Count * 4];
        int[] tris = new int[positions.Count * 6];

        for (int i = 0; i < positions.Count; i++)
        {
            int v = i * 4;
            int t = i * 6;
            Vector3 pos = positions[i];
            
            // Quad math: each quad faces the origin (0,0,0) where the camera will be
            Vector3 normal = pos.normalized;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(normal, up).normalized;
            if (right.sqrMagnitude < 0.01f)
            {
                up = Vector3.forward;
                right = Vector3.Cross(normal, up).normalized;
            }
            up = Vector3.Cross(right, normal).normalized;

            // Brighter (closer) systems also get a slightly larger glow
            Color c = colors[i];
            float size = starSize * Mathf.Lerp(0.7f, 1.4f, Mathf.Clamp01(c.a / 1.6f));
            vertices[v + 0] = pos + (-right - up) * size;
            vertices[v + 1] = pos + (right - up) * size;
            vertices[v + 2] = pos + (right + up) * size;
            vertices[v + 3] = pos + (-right + up) * size;
            vertexColors[v + 0] = c;
            vertexColors[v + 1] = c;
            vertexColors[v + 2] = c;
            vertexColors[v + 3] = c;

            uvs[v + 0] = new Vector2(0, 0);
            uvs[v + 1] = new Vector2(1, 0);
            uvs[v + 2] = new Vector2(1, 1);
            uvs[v + 3] = new Vector2(0, 1);

            tris[t + 0] = v + 0;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            tris[t + 3] = v + 0;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 2;
        }

        starMesh.vertices = vertices;
        starMesh.uv = uvs;
        starMesh.colors = vertexColors;
        starMesh.triangles = tris;
        starMesh.bounds = new Bounds(Vector3.zero, Vector3.one * skyboxRadius * 2.5f);
    }

    void LateUpdate()
    {
        // Nothing to render without a camera (dedicated server)
        if (Camera.main == null) return;

        // Orbits only move stars when the full map drives them; metadata positions are static
        bool hasFullMap = visualizer != null && visualizer.universeMap != null;
        if (_starsDirty || (hasFullMap && Time.time >= nextUpdateTime))
        {
            UpdateStarPositions();
            _starsDirty = false;
            nextUpdateTime = Time.time + updateInterval;
        }

        // Render the stars centered on the main camera
        if (starMesh != null && starMesh.vertexCount > 0 && starMaterial != null)
        {
            // Lock the skybox mesh to the camera's position
            Vector3 camPos = Camera.main.transform.position;
            Graphics.DrawMesh(starMesh, camPos, Quaternion.identity, starMaterial, 0);
        }
    }
}
