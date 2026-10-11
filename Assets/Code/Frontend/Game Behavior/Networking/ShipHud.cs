using UnityEngine;

/// <summary>
/// Digital flight HUD for the local player's ship: pitch ladder and horizon relative to the system
/// plane, heading tape, roll scale, flight-path marker and speed/attitude readouts.
/// Reference frame: the system's ecliptic is the world XZ plane, "up" is world +Y, heading 000 is +Z.
/// </summary>
public class ShipHud : MonoBehaviour
{
    private static ShipHud instance;

    public Color hudColor = new Color(0.35f, 1f, 0.6f, 0.9f);
    public float ladderHalfHeight = 170f;
    public float ladderLineWidth = 120f;
    public float headingTapeWidth = 420f;
    public float headingPixelsPerDegree = 4f;

    private IShipHudSource source;
    private GUIStyle labelStyle;
    private GUIStyle centeredStyle;

    public static void Attach(IShipHudSource hudSource)
    {
        if (instance == null) instance = new GameObject("ShipHud").AddComponent<ShipHud>();
        instance.source = hudSource;
    }

    public static void Detach(IShipHudSource hudSource)
    {
        if (instance != null && instance.source == hudSource) Destroy(instance.gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        Transform ship = source != null && !(source is Object o && o == null) ? source.HudTransform : null;
        Camera cam = Camera.main;
        if (ship == null || cam == null) return;

        EnsureStyles();

        Vector3 forward = ship.forward;
        Vector3 up = ship.up;
        Vector3 right = ship.right;

        float pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float heading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);

        // Where world-up appears on screen, measured clockwise from screen-up
        Vector3 worldUpInView = Vector3.ProjectOnPlane(Vector3.up, forward);
        float roll = worldUpInView.sqrMagnitude > 1e-6f
            ? Mathf.Atan2(Vector3.Dot(worldUpInView, right), Vector3.Dot(worldUpInView, up)) * Mathf.Rad2Deg
            : 0f;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float pixelsPerDegree = Screen.height / cam.fieldOfView;

        Color previous = GUI.color;
        GUI.color = hudColor;

        DrawPitchLadder(center, pitch, roll, pixelsPerDegree);
        DrawBoresight(center);
        DrawRollScale(center, roll);
        DrawHeadingTape(new Vector2(center.x, 48f), heading);
        DrawFlightPathMarker(cam, ship.position);
        DrawReadouts(FloatingOrigin.ToWorld(ship.position).ToVector3(), pitch, heading, roll);

        GUI.color = previous;
    }

    private void EnsureStyles()
    {
        if (labelStyle != null) return;
        labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        labelStyle.normal.textColor = hudColor;
        centeredStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
    }

    private void DrawPitchLadder(Vector2 center, float pitch, float roll, float pixelsPerDegree)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(roll, center);

        int first = Mathf.CeilToInt((pitch - ladderHalfHeight / pixelsPerDegree) / 10f) * 10;
        int last = Mathf.FloorToInt((pitch + ladderHalfHeight / pixelsPerDegree) / 10f) * 10;
        for (int angle = Mathf.Max(first, -90); angle <= Mathf.Min(last, 90); angle += 10)
        {
            // Nose above a line puts that line below the centre
            float y = center.y + (pitch - angle) * pixelsPerDegree;
            if (angle == 0)
            {
                float half = ladderLineWidth * 1.6f;
                Bar(center.x - half, y - 1f, half - 30f, 2f);
                Bar(center.x + 30f, y - 1f, half - 30f, 2f);
                continue;
            }

            float w = ladderLineWidth * 0.5f;
            float gap = 24f;
            if (angle > 0)
            {
                Bar(center.x - gap - w, y - 1f, w, 2f);
                Bar(center.x + gap, y - 1f, w, 2f);
                // Ticks point toward the horizon
                Bar(center.x - gap - w, y, 2f, 8f);
                Bar(center.x + gap + w - 2f, y, 2f, 8f);
            }
            else
            {
                // Dashed below the horizon
                for (int i = 0; i < 3; i++)
                {
                    float segment = w / 3f;
                    Bar(center.x - gap - w + i * segment, y - 1f, segment * 0.6f, 2f);
                    Bar(center.x + gap + i * segment, y - 1f, segment * 0.6f, 2f);
                }
                Bar(center.x - gap - w, y - 8f, 2f, 8f);
                Bar(center.x + gap + w - 2f, y - 8f, 2f, 8f);
            }

            string label = angle.ToString();
            GUI.Label(new Rect(center.x - gap - w - 44f, y - 12f, 40f, 24f), label, labelStyle);
            GUI.Label(new Rect(center.x + gap + w + 6f, y - 12f, 40f, 24f), label, labelStyle);
        }

        GUI.matrix = saved;
    }

    private void DrawBoresight(Vector2 c)
    {
        // Nose marker: a "W" made of short bars
        Bar(c.x - 22f, c.y - 1f, 12f, 2f);
        Bar(c.x + 10f, c.y - 1f, 12f, 2f);
        Bar(c.x - 1f, c.y - 1f, 2f, 2f);
        Bar(c.x - 10f, c.y - 1f, 2f, 8f);
        Bar(c.x + 8f, c.y - 1f, 2f, 8f);
    }

    private void DrawRollScale(Vector2 center, float roll)
    {
        float radius = ladderHalfHeight + 20f;
        foreach (int mark in new[] { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 })
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(mark, center);
            float length = mark % 30 == 0 ? 14f : 8f;
            Bar(center.x - 1f, center.y - radius - length, 2f, length);
            GUI.matrix = saved;
        }

        // Pointer rides on the scale: world-up direction
        Matrix4x4 s = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Clamp(roll, -70f, 70f), center);
        Bar(center.x - 6f, center.y - radius + 4f, 12f, 3f);
        Bar(center.x - 1f, center.y - radius + 4f, 2f, 10f);
        GUI.matrix = s;
    }

    private void DrawHeadingTape(Vector2 top, float heading)
    {
        float half = headingTapeWidth * 0.5f;
        Bar(top.x - half, top.y + 22f, headingTapeWidth, 2f);

        int firstTick = Mathf.CeilToInt((heading - half / headingPixelsPerDegree) / 5f) * 5;
        int lastTick = Mathf.FloorToInt((heading + half / headingPixelsPerDegree) / 5f) * 5;
        for (int deg = firstTick; deg <= lastTick; deg += 5)
        {
            float x = top.x + (deg - heading) * headingPixelsPerDegree;
            bool major = Mathf.RoundToInt(Mathf.Repeat(deg, 360f)) % 30 == 0;
            float length = major ? 12f : 6f;
            Bar(x - 1f, top.y + 22f - length, 2f, length);
            if (major)
            {
                int value = Mathf.RoundToInt(Mathf.Repeat(deg, 360f));
                GUI.Label(new Rect(x - 24f, top.y - 10f, 48f, 24f), value.ToString("000"), centeredStyle);
            }
        }

        // Caret and current heading box
        Bar(top.x - 1f, top.y + 24f, 2f, 10f);
        GUI.Label(new Rect(top.x - 34f, top.y + 32f, 68f, 24f), heading.ToString("000"), centeredStyle);
    }

    private void DrawFlightPathMarker(Camera cam, Vector3 shipPos)
    {
        Vector3 velocity = source.HudVelocity;
        if (velocity.sqrMagnitude < 1f) return;

        Vector3 screen = cam.WorldToScreenPoint(cam.transform.position + velocity.normalized * 1000f);
        if (screen.z <= 0f) return;

        Vector2 p = new Vector2(screen.x, Screen.height - screen.y);
        // Circle-ish ring with wings and a fin: where the ship is actually going
        Bar(p.x - 6f, p.y - 6f, 12f, 2f);
        Bar(p.x - 6f, p.y + 4f, 12f, 2f);
        Bar(p.x - 6f, p.y - 6f, 2f, 12f);
        Bar(p.x + 4f, p.y - 6f, 2f, 12f);
        Bar(p.x - 16f, p.y - 1f, 10f, 2f);
        Bar(p.x + 6f, p.y - 1f, 10f, 2f);
        Bar(p.x - 1f, p.y - 14f, 2f, 8f);
    }

    private void DrawReadouts(Vector3 position, float pitch, float heading, float roll)
    {
        float speed = source.HudVelocity.magnitude;
        float left = Screen.width * 0.5f - ladderLineWidth * 2.4f - 120f;
        float right = Screen.width * 0.5f + ladderLineWidth * 2.4f;
        float y = Screen.height * 0.5f - 40f;

        GUI.Label(new Rect(left, y, 160f, 24f), $"SPD {speed / DistanceFormat.UnitsPerAU:0.0000}", labelStyle);
        GUI.Label(new Rect(left, y + 26f, 160f, 24f), "AU/s", labelStyle);

        GUI.Label(new Rect(right, y, 160f, 24f), $"ELV {position.y,7:+0;-0;0}", labelStyle);
        GUI.Label(new Rect(right, y + 26f, 160f, 24f), $"PIT {pitch,4:+0;-0;0}°", labelStyle);
        GUI.Label(new Rect(right, y + 52f, 160f, 24f), $"ROL {-roll,4:+0;-0;0}°", labelStyle);
        GUI.Label(new Rect(right, y + 78f, 160f, 24f), $"HDG {heading:000}", labelStyle);
    }

    private static void Bar(float x, float y, float w, float h)
    {
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
    }
}
