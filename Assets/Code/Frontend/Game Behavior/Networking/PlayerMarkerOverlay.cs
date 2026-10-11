using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Draws a fixed-size screen marker over other players whose ship is too small (or off-screen) to see.
/// Created on demand by the local player; nothing needs to be placed in a scene.
/// </summary>
public class PlayerMarkerOverlay : MonoBehaviour
{
    private static PlayerMarkerOverlay instance;

    [Tooltip("Ships smaller than this on screen (pixels) get a marker.")]
    public float minVisiblePixels = 16f;
    public float markerSize = 22f;
    public float edgeMargin = 32f;
    public Color markerColor = new Color(0.3f, 0.9f, 1f, 0.9f);

    private struct Marker
    {
        public Vector2 guiPosition;
        public bool offScreen;
        public string label;
    }

    private readonly List<Marker> markers = new List<Marker>();
    private readonly Dictionary<NetworkObject, Renderer[]> rendererCache = new Dictionary<NetworkObject, Renderer[]>();
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    public static void EnsureExists()
    {
        if (instance != null) return;
        instance = new GameObject("PlayerMarkerOverlay").AddComponent<PlayerMarkerOverlay>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        markers.Clear();

        var netManager = NetworkManager.Singleton;
        Camera cam = Camera.main;
        if (netManager == null || !netManager.IsClient || netManager.SpawnManager == null || cam == null) return;

        Vector3 camPos = cam.transform.position;
        float pixelsPerUnitAtOne = Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

        foreach (var netObj in netManager.SpawnManager.SpawnedObjectsList)
        {
            if (netObj == null || !netObj.IsPlayerObject || netObj.OwnerClientId == netManager.LocalClientId) continue;
            // A host hides players in other systems by disabling their renderers
            if (netManager.IsServer && !HasVisibleRenderer(netObj)) continue;

            Vector3 worldPos = netObj.transform.position;
            float distance = Vector3.Distance(camPos, worldPos);
            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            bool behind = screen.z < 0f;
            bool onScreen = !behind && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;

            if (onScreen && GetScreenSize(netObj, distance, pixelsPerUnitAtOne) >= minVisiblePixels) continue;

            Vector2 pos = onScreen ? new Vector2(screen.x, screen.y) : ClampToScreenEdge(screen, behind);
            markers.Add(new Marker
            {
                // GUI space has y pointing down
                guiPosition = new Vector2(pos.x, Screen.height - pos.y),
                offScreen = !onScreen,
                label = $"P{netObj.OwnerClientId}  {DistanceFormat.Format(distance)}"
            });
        }
    }

    private Renderer[] GetRenderers(NetworkObject netObj)
    {
        if (!rendererCache.TryGetValue(netObj, out var renderers) || renderers == null)
        {
            // Hidden/re-shown players come back as new objects; drop entries for destroyed ones
            if (rendererCache.Count > 64)
            {
                var dead = new List<NetworkObject>();
                foreach (var key in rendererCache.Keys) if (key == null) dead.Add(key);
                foreach (var key in dead) rendererCache.Remove(key);
            }
            renderers = netObj.GetComponentsInChildren<Renderer>(true);
            rendererCache[netObj] = renderers;
        }
        return renderers;
    }

    private bool HasVisibleRenderer(NetworkObject netObj)
    {
        foreach (var r in GetRenderers(netObj)) if (r != null && r.enabled) return true;
        return false;
    }

    private float GetScreenSize(NetworkObject netObj, float distance, float pixelsPerUnitAtOne)
    {
        var renderers = GetRenderers(netObj);

        float radius = 0f;
        foreach (var r in renderers)
        {
            if (r != null && r.enabled) radius = Mathf.Max(radius, r.bounds.extents.magnitude);
        }
        return distance > 0.001f ? 2f * radius / distance * pixelsPerUnitAtOne : float.MaxValue;
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
            labelStyle.normal.textColor = markerColor;
            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        Color previous = GUI.color;
        foreach (var marker in markers)
        {
            GUI.color = markerColor;
            if (marker.offScreen) DrawDiamond(marker.guiPosition, markerSize * 0.6f);
            else DrawBrackets(marker.guiPosition, markerSize);

            var labelRect = new Rect(marker.guiPosition.x - 100f, marker.guiPosition.y + markerSize * 0.5f + 2f, 200f, 24f);
            GUI.color = Color.white;
            GUI.Label(new Rect(labelRect.x + 1f, labelRect.y + 1f, labelRect.width, labelRect.height), marker.label, shadowStyle);
            GUI.Label(labelRect, marker.label, labelStyle);
        }
        GUI.color = previous;
    }

    private static void DrawBrackets(Vector2 c, float size)
    {
        float h = size * 0.5f;
        float len = size * 0.35f;
        const float t = 2f;
        Texture tex = Texture2D.whiteTexture;

        // Four corner brackets
        GUI.DrawTexture(new Rect(c.x - h, c.y - h, len, t), tex);
        GUI.DrawTexture(new Rect(c.x - h, c.y - h, t, len), tex);
        GUI.DrawTexture(new Rect(c.x + h - len, c.y - h, len, t), tex);
        GUI.DrawTexture(new Rect(c.x + h - t, c.y - h, t, len), tex);
        GUI.DrawTexture(new Rect(c.x - h, c.y + h - t, len, t), tex);
        GUI.DrawTexture(new Rect(c.x - h, c.y + h - len, t, len), tex);
        GUI.DrawTexture(new Rect(c.x + h - len, c.y + h - t, len, t), tex);
        GUI.DrawTexture(new Rect(c.x + h - t, c.y + h - len, t, len), tex);
    }

    private static void DrawDiamond(Vector2 c, float size)
    {
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), Texture2D.whiteTexture);
        GUI.matrix = previous;
    }
}
