using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the real neighbouring star systems as points in the sky.
/// The mesh stores each star's position relative to the current system (and, with the full map, its
/// orbital drift); the SystemStars shader aims at it from the viewer's actual position every frame.
/// So the sky moves smoothly with the player and with orbits, and the mesh only needs rebuilding when
/// the data changes (or every few seconds on a host, to re-anchor the drift).
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

    [Header("Dependencies")]
    [Tooltip("Reference to the SystemCenteredVisualizer to get current system data.")]
    public SystemCenteredVisualizer visualizer;

    [Header("Update Performance")]
    [Tooltip("Seconds between re-anchoring orbital drift (full map only); motion in between is extrapolated.")]
    public float updateInterval;

    private static readonly int ViewerOffsetId = Shader.PropertyToID("_ViewerOffset");
    private static readonly int ElapsedId = Shader.PropertyToID("_Elapsed");
    private static readonly int SkyRadiusId = Shader.PropertyToID("_SkyRadius");
    private static readonly int StarSizeId = Shader.PropertyToID("_StarSize");

    // Additive point-star shader drawn at the far plane (Resources/Celestial/SystemStars)
    private Material starMaterial;
    private MaterialPropertyBlock starProperties;

    private float nextUpdateTime = 0f;
    private double builtAtGalacticTime;
    private ulong currentSystemId;
    private Vector3d currentSystemAbsolutePosition;
    private Mesh starMesh;
    private List<SystemMetadata> _cachedSystems = new List<SystemMetadata>();
    private HashSet<ulong> _cachedSystemIds = new HashSet<ulong>();
    // Metadata can arrive in parts; rebuild once per frame at most
    private bool _starsDirty;

    private readonly List<Vector3> starPositions = new List<Vector3>();
    private readonly List<Vector3> starDrifts = new List<Vector3>();
    private readonly List<Color> starColors = new List<Color>();

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

        starMesh = new Mesh();
        starMesh.name = "DynamicSkybox_Stars";
        starMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        starMaterial = CelestialVisuals.GetMaterial("SystemStars");
        starProperties = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Sets the full universe map (server/host) and triggers a rebuild.</summary>
    public void SetUniverseMap(UniverseMapDataV2 map)
    {
        if (visualizer != null) visualizer.universeMap = map;
        _cachedSystems.Clear();
        _cachedSystemIds.Clear();
        _starsDirty = true;
    }

    /// <summary>System metadata (IDs and absolute positions) for clients without the map.</summary>
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

    public void SetCurrentSystem(ulong systemId, Vector3d absolutePosition)
    {
        currentSystemId = systemId;
        currentSystemAbsolutePosition = absolutePosition;
        _starsDirty = true;
    }

    private static double GalacticTime => GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;

    private void RebuildStars()
    {
        double time = GalacticTime;
        starPositions.Clear();
        starDrifts.Clear();
        starColors.Clear();

        if (visualizer != null && visualizer.universeMap != null)
        {
            // Full map: positions now and one second later give the drift the shader extrapolates
            Vector3d currentNow = currentSystemAbsolutePosition;
            Vector3d currentNext = currentSystemAbsolutePosition;
            foreach (var galaxy in visualizer.universeMap.galaxies)
            {
                foreach (var system in galaxy.systems)
                {
                    if (system.id != currentSystemId) continue;
                    currentNow = system.GetAbsolutePosition(time, galaxy.GetAbsolutePosition(time));
                    currentNext = system.GetAbsolutePosition(time + 1.0, galaxy.GetAbsolutePosition(time + 1.0));
                }
            }
            currentSystemAbsolutePosition = currentNow;

            foreach (var galaxy in visualizer.universeMap.galaxies)
            {
                Vector3d galaxyNow = galaxy.GetAbsolutePosition(time);
                Vector3d galaxyNext = galaxy.GetAbsolutePosition(time + 1.0);
                foreach (var system in galaxy.systems)
                {
                    if (system.id == currentSystemId) continue;
                    Vector3d now = system.GetAbsolutePosition(time, galaxyNow) - currentNow;
                    Vector3d next = system.GetAbsolutePosition(time + 1.0, galaxyNext) - currentNext;
                    AddStar(system.id, now, (next - now).ToVector3());
                }
            }
        }
        else if (_cachedSystems.Count > 0)
        {
            // Metadata is a snapshot, so no drift
            var mySystem = _cachedSystems.Find(x => x.id == currentSystemId);
            if (mySystem.id != 0) currentSystemAbsolutePosition = mySystem.absolutePosition;

            foreach (var system in _cachedSystems)
            {
                if (system.id == currentSystemId) continue;
                AddStar(system.id, system.absolutePosition - currentSystemAbsolutePosition, Vector3.zero);
            }
        }

        builtAtGalacticTime = time;
        BuildStarMesh();
    }

    // Tint from the system id (same on every client), brightness and size from distance
    private void AddStar(ulong id, Vector3d offset, Vector3 drift)
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

        starPositions.Add(offset.ToVector3());
        starDrifts.Add(drift);
        starColors.Add(tint);
    }

    private void BuildStarMesh()
    {
        starMesh.Clear();
        int count = starPositions.Count;
        if (count == 0) return;

        var vertices = new Vector3[count * 4];
        var corners = new Vector2[count * 4];
        var drifts = new Vector3[count * 4];
        var colors = new Color[count * 4];
        var tris = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            int v = i * 4;
            int t = i * 6;
            for (int k = 0; k < 4; k++)
            {
                vertices[v + k] = starPositions[i];
                drifts[v + k] = starDrifts[i];
                colors[v + k] = starColors[i];
            }

            // Quad corners; the shader expands them facing the camera
            corners[v + 0] = new Vector2(-1f, -1f);
            corners[v + 1] = new Vector2(1f, -1f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(-1f, 1f);

            tris[t + 0] = v + 0;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            tris[t + 3] = v + 0;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 2;
        }

        starMesh.vertices = vertices;
        starMesh.uv = corners;
        starMesh.SetUVs(1, drifts);
        starMesh.colors = colors;
        starMesh.triangles = tris;
        // Vertices hold system-space positions, not where they are drawn; never cull the sky
        starMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
    }

    void LateUpdate()
    {
        // Nothing to render without a camera (dedicated server)
        Camera cam = Camera.main;
        if (cam == null) return;

        bool hasFullMap = visualizer != null && visualizer.universeMap != null;
        if (_starsDirty || (hasFullMap && Time.time >= nextUpdateTime))
        {
            RebuildStars();
            _starsDirty = false;
            nextUpdateTime = Time.time + Mathf.Max(updateInterval, 0.5f);
        }

        if (starMesh.vertexCount == 0 || starMaterial == null) return;

        Vector3 camPos = cam.transform.position;
        // System-space viewer position: parallax as the player flies through the system
        starProperties.SetVector(ViewerOffsetId, FloatingOrigin.ToWorld(camPos).ToVector3());
        starProperties.SetFloat(ElapsedId, (float)(GalacticTime - builtAtGalacticTime));
        starProperties.SetFloat(SkyRadiusId, skyboxRadius);
        starProperties.SetFloat(StarSizeId, starSize);
        Graphics.DrawMesh(starMesh, camPos, Quaternion.identity, starMaterial, 0, null, 0, starProperties);
    }
}
