using UnityEngine;
using System.Text;

public class OnScreenConsole : MonoBehaviour
{
    private StringBuilder logBuilder = new StringBuilder();
    private Vector2 scrollPosition;
    public int maxLines = 20;  // Max lines to show on screen

    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Add new log message
        logBuilder.AppendLine(logString);

        // Limit the amount of text shown to max lines
        var lines = logBuilder.ToString().Split('\n');
        if (lines.Length > maxLines)
        {
            logBuilder.Clear();
            for (int i = lines.Length - maxLines; i < lines.Length; i++)
                logBuilder.AppendLine(lines[i]);
        }
    }

    void OnGUI()
    {
        // Draw a scrollable text area with log
        GUI.backgroundColor = new Color(0, 0, 0, 0.6f);
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 150));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.TextArea(logBuilder.ToString());
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
