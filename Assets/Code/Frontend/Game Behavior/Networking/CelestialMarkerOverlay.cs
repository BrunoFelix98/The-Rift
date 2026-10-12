using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed-size location markers for every celestial in the current system until you are close to it.
/// Stations and gates (navigation targets) also get an edge marker when off-screen. Asteroids get
/// small markers, labelled only at short range so dozens of them stay readable. Nebulae are skipped.
/// </summary>
public class CelestialMarkerOverlay : MonoBehaviour
{
    private static CelestialMarkerOverlay instance;

    [Tooltip("Markers hide once you are within this many body radii of the surface (at least closeDistance).")]
    public float closeRadii = 1.5f;
    public float closeDistance = 1500f;
    public float asteroidCloseDistance = 400f;
    [Tooltip("Asteroid labels only show within this distance.")]
    public float asteroidLabelRange = 5000f;
    public float markerSize = 10f;
    public float asteroidMarkerSize = 5f;
    public float edgeMargin = 48f;

    private struct Marker
    {
        public Vector2 guiPosition;
        public bool offScreen;
        public Color color;
        public string label;
        public float size;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private readonly Dictionary<ulong, string> nameCache = new Dictionary<ulong, string>();
    private GUIStyle labelStyle;

    private static readonly Color StarColor = new Color(1f, 0.85f, 0.4f, 0.9f);
    private static readonly Color PlanetColor = new Color(0.55f, 0.85f, 1f, 0.9f);
    private static readonly Color MoonColor = new Color(0.75f, 0.75f, 0.8f, 0.75f);
    private static readonly Color StationColor = new Color(1f, 0.6f, 0.25f, 0.95f);
    private static readonly Color GateColor = new Color(0.9f, 0.45f, 1f, 0.95f);
    private static readonly Color AsteroidColor = new Color(0.7f, 0.62f, 0.5f, 0.7f);

    public static void EnsureExists()
    {
        if (instance != null) return;
        instance = new GameObject("CelestialMarkerOverlay").AddComponent<CelestialMarkerOverlay>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        markers.Clear();

        var visualizer = SystemCenteredVisualizer.Instance;
        Camera cam = Camera.main;
        if (visualizer == null || cam == null) return;

        // Distant nodes skip position updates in the LOD pass; markers need all of them current
        visualizer.RefreshNodePositions();

        float scaleMultiplier = visualizer.settings != null ? visualizer.settings.modelScaleMultiplier : 1f;
        Vector3 camPos = cam.transform.position;

        foreach (var node in visualizer.CurrentNodes)
        {
            CelestialType type = node.Params.Type;
            if (!TryGetStyle(type, out Color color, out bool edgeWhenOffScreen)) continue;

            Vector3 worldPos = node.RenderPosition;
            float distance = Vector3.Distance(camPos, worldPos);

            // Unit-sphere meshes scaled by visualRadius
            float worldRadius = node.visualRadius * scaleMultiplier * 0.5f;
            bool asteroid = type == CelestialType.ASTEROID;
            float close = asteroid ? Mathf.Max(worldRadius * 2f, asteroidCloseDistance) : Mathf.Max(worldRadius * closeRadii, closeDistance);
            if (distance - worldRadius <= close) continue;

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            bool behind = screen.z < 0f;
            bool onScreen = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (!onScreen && !edgeWhenOffScreen) continue;

            Vector2 pos = onScreen ? new Vector2(screen.x, screen.y) : ClampToScreenEdge(screen, behind);
            bool labelled = !asteroid || distance <= asteroidLabelRange;
            markers.Add(new Marker
            {
                guiPosition = new Vector2(pos.x, Screen.height - pos.y),
                offScreen = !onScreen,
                color = color,
                size = asteroid ? asteroidMarkerSize : (onScreen ? markerSize : markerSize * 1.3f),
                label = labelled ? $"{GetName(node)}  {DistanceFormat.Format(distance)}" : null
            });
        }
    }

    private static bool TryGetStyle(CelestialType type, out Color color, out bool edgeWhenOffScreen)
    {
        edgeWhenOffScreen = false;
        switch (type)
        {
            case CelestialType.STAR: color = StarColor; return true;
            case CelestialType.PLANET: color = PlanetColor; return true;
            case CelestialType.MOON: color = MoonColor; return true;
            case CelestialType.STATION: color = StationColor; edgeWhenOffScreen = true; return true;
            case CelestialType.GATE: color = GateColor; edgeWhenOffScreen = true; return true;
            case CelestialType.ASTEROID: color = AsteroidColor; return true;
            default: color = Color.clear; return false;
        }
    }

    private string GetName(CelestialHierarchyNode node)
    {
        ulong id = node.Params.NetworkId;
        if (!nameCache.TryGetValue(id, out string name))
        {
            name = node.Params.Name.ToString();
            if (string.IsNullOrEmpty(name)) name = node.Params.Type.ToString();
            nameCache[id] = name;
        }
        return name;
    }

    private Vector2 ClampToScreenEdge(Vector3 screen, bool behind)
    {
        Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 dir = new Vector2(screen.x, screen.y) - center;
        // Projection mirrors points behind the camera
        if (behind) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;

        float halfW = center.x - edgeMargin;
        float halfH = center.y - edgeMargin;
        float scale = Mathf.Min(halfW / Mathf.Max(Mathf.Abs(dir.x), 0.0001f), halfH / Mathf.Max(Mathf.Abs(dir.y), 0.0001f));
        return center + dir * scale;
    }


    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || markers.Count == 0) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
        }

        Color previous = GUI.color;
        foreach (var marker in markers)
        {
            GUI.color = marker.color;
            float size = marker.size;
            DrawDiamondOutline(marker.guiPosition, size);
            if (marker.label == null) continue;

            // Opaque text: the marker colours are partly transparent and would wash out on the backdrop
            Color textColor = marker.color;
            textColor.a = 1f;
            HudLabel.Draw(new Vector2(marker.guiPosition.x, marker.guiPosition.y + size * 0.5f + 5f), marker.label, labelStyle, textColor);
        }
        GUI.color = previous;
    }

    // Hollow diamond, distinct from the player brackets
    private static void DrawDiamondOutline(Vector2 c, float size)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        float h = size * 0.5f;
        const float t = 2f;
        Texture tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(c.x - h, c.y - h, size, t), tex);
        GUI.DrawTexture(new Rect(c.x - h, c.y + h - t, size, t), tex);
        GUI.DrawTexture(new Rect(c.x - h, c.y - h, t, size), tex);
        GUI.DrawTexture(new Rect(c.x + h - t, c.y - h, t, size), tex);
        GUI.matrix = saved;
    }
}
