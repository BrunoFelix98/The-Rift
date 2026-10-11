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
    
    [Tooltip("Material used for rendering stars (should be an Unlit/Additive shader).")]
    public Material starMaterial;

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
        UpdateStarPositions();
    }

    /// <summary>
    /// Synchronizes a list of system metadata (IDs and Absolute Positions).
    /// Used by Clients who don't have the full 'UniverseMap.bin' yet.
    /// </summary>
    public void SyncAllSystems(List<SystemMetadata> systems)
    {
        foreach (var s in systems)
        {
            if (!_cachedSystems.Exists(x => x.id == s.id))
            {
                _cachedSystems.Add(s);
            }
        }
        UpdateStarPositions();
    }

    /// <summary>
    /// Updates the player's current system location and refreshes the relative star positions.
    /// </summary>
    public void SetCurrentSystem(ulong systemId, Vector3d absolutePosition)
    {
        currentSystemId = systemId;
        currentSystemAbsolutePosition = absolutePosition;
        UpdateStarPositions();
    }

    /// <summary>
    /// Re-calculates the relative positions of all star systems and rebuilds the star mesh.
    /// </summary>
    private void UpdateStarPositions()
    {
        double time = GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;
        List<Vector3> positions = new List<Vector3>();

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
                    Vector3d direction = systemPos - currentSystemAbsolutePosition;
                    
                    Vector3 dir = direction.ToVector3().normalized;
                    positions.Add(dir * skyboxRadius);
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

                Vector3d direction = system.absolutePosition - currentSystemAbsolutePosition;
                Vector3 dir = direction.ToVector3().normalized;
                positions.Add(dir * skyboxRadius);
             }
        }

        if (positions.Count == 0) 
        {
            if (starMesh != null) starMesh.Clear();
            return;
        }

        // Rebuild the mesh with billboarding quads for each star
        BuildStarMesh(positions);
    }

    private void BuildStarMesh(List<Vector3> positions)
    {
        starMesh.Clear();
        Vector3[] vertices = new Vector3[positions.Count * 4];
        Vector2[] uvs = new Vector2[positions.Count * 4];
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

            vertices[v + 0] = pos + (-right - up) * starSize;
            vertices[v + 1] = pos + (right - up) * starSize;
            vertices[v + 2] = pos + (right + up) * starSize;
            vertices[v + 3] = pos + (-right + up) * starSize;

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
        starMesh.triangles = tris;
        starMesh.bounds = new Bounds(Vector3.zero, Vector3.one * skyboxRadius * 2.5f);
    }

    void LateUpdate()
    {
        // Throttled update of relative star positions
        if (Time.time >= nextUpdateTime)
        {
            UpdateStarPositions();
            nextUpdateTime = Time.time + updateInterval;
        }

        // Render the stars centered on the main camera
        if (Camera.main != null && starMesh != null && starMesh.vertexCount > 0)
        {
            if (starMaterial == null)
            {
                // Safety fallback material
                starMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                starMaterial.color = Color.white;
            }

            // Lock the skybox mesh to the camera's position
            Vector3 camPos = Camera.main.transform.position;
            Graphics.DrawMesh(starMesh, camPos, Quaternion.identity, starMaterial, 0);
        }
    }
}
