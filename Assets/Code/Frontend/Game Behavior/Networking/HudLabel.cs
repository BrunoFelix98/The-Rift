using UnityEngine;

/// <summary>IMGUI text on a dark backdrop, so overlay labels stay readable over bright stars and planets.</summary>
public static class HudLabel
{
    private static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.6f);
    private const float PadX = 5f;
    private const float PadY = 1f;

    /// <summary>Draws text centred horizontally on topCenter, its top edge at topCenter.y.</summary>
    public static void Draw(Vector2 topCenter, string text, GUIStyle style, Color textColor)
    {
        Vector2 size = style.CalcSize(new GUIContent(text));
        var textRect = new Rect(topCenter.x - size.x * 0.5f, topCenter.y, size.x, size.y);

        Color previous = GUI.color;
        GUI.color = Backdrop;
        GUI.DrawTexture(new Rect(textRect.x - PadX, textRect.y - PadY, textRect.width + PadX * 2f, textRect.height + PadY * 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        Color previousText = style.normal.textColor;
        style.normal.textColor = textColor;
        GUI.Label(textRect, text, style);
        style.normal.textColor = previousText;
        GUI.color = previous;
    }
}
