using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fixed-size location markers for celestials in the current system that are too small to see.
/// Stations and gates (navigation targets) also get an edge marker when off-screen.
/// Asteroids and nebulae are left out to keep the view readable.
/// </summary>
public class CelestialMarkerOverlay : MonoBehaviour
{
    private static CelestialMarkerOverlay instance;

    [Tooltip("Celestials smaller than this on screen (pixels) get a marker.")]
    public float minVisiblePixels = 24f;
    public float markerSize = 10f;
    public float edgeMargin = 48f;
    [Tooltip("Moons further away than this are not marked, there are many of them.")]
    public float moonMarkerRange = 100000f;

    private struct Marker
    {
        public Vector2 guiPosition;
        public bool offScreen;
        public Color color;
        public string label;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private readonly Dictionary<ulong, string> nameCache = new Dictionary<ulong, string>();
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    private static readonly Color StarColor = new Color(1f, 0.85f, 0.4f, 0.9f);
    private static readonly Color PlanetColor = new Color(0.55f, 0.85f, 1f, 0.9f);
    private static readonly Color MoonColor = new Color(0.75f, 0.75f, 0.8f, 0.75f);
    private static readonly Color StationColor = new Color(1f, 0.6f, 0.25f, 0.95f);
    private static readonly Color GateColor = new Color(0.9f, 0.45f, 1f, 0.95f);

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
        float pixelsPerUnitAtOne = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

        foreach (var node in visualizer.CurrentNodes)
        {
            CelestialType type = node.Params.Type;
            if (!TryGetStyle(type, out Color color, out bool edgeWhenOffScreen)) continue;

            Vector3 worldPos = node.CurrentWorldPosition;
            float distance = Vector3.Distance(camPos, worldPos);
            if (type == CelestialType.MOON && distance > moonMarkerRange) continue;

            // Unit-sphere meshes scaled by visualRadius
            float worldRadius = node.visualRadius * scaleMultiplier * 0.5f;
            if (distance <= worldRadius) continue;

            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            bool behind = screen.z < 0f;
            bool onScreen = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;

            if (onScreen)
            {
                float pixels = 2f * worldRadius / distance * pixelsPerUnitAtOne;
                if (pixels >= minVisiblePixels) continue;
            }
            else if (!edgeWhenOffScreen)
            {
                continue;
            }

            Vector2 pos = onScreen ? new Vector2(screen.x, screen.y) : ClampToScreenEdge(screen, behind);
            markers.Add(new Marker
            {
                guiPosition = new Vector2(pos.x, Screen.height - pos.y),
                offScreen = !onScreen,
                color = color,
                label = $"{GetName(node)}  {FormatDistance(distance)}"
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

    private static string FormatDistance(float distance)
    {
        return distance < 1000f ? $"{distance:0} m" : $"{distance / 1000f:0.0} km";
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || markers.Count == 0) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        Color previous = GUI.color;
        foreach (var marker in markers)
        {
            GUI.color = marker.color;
            float size = marker.offScreen ? markerSize * 1.3f : markerSize;
            DrawDiamondOutline(marker.guiPosition, size);

            var labelRect = new Rect(marker.guiPosition.x - 120f, marker.guiPosition.y + size * 0.5f + 4f, 240f, 24f);
            GUI.color = Color.white;
            labelStyle.normal.textColor = marker.color;
            GUI.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), marker.label, shadowStyle);
            GUI.Label(labelRect, marker.label, labelStyle);
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
