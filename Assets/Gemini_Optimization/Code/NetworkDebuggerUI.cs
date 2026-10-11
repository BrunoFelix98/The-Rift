using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class NetworkDebuggerUI : MonoBehaviour
{
    private string _status = "Initializing...";
    private List<string> _logs = new List<string>();
    private bool _showLogs = true;

    private string _targetAddress = "0.0.0.0";
    private string _targetPort = "30000";
    private string _publicIP = "Unknown (Click Fetch)";

    private NetworkAutoStart _autoStart;

    private void Awake()
    {
        Debug.Log("[NetworkDebuggerUI] Script Awake - Initializing UI...");
        _autoStart = Object.FindAnyObjectByType<NetworkAutoStart>();
        
        // Load the last used IP from memory
        string defaultAddress = Application.isEditor ? "127.0.0.1" : (_autoStart != null ? _autoStart.address : "127.0.0.1");
        _targetAddress = PlayerPrefs.GetString("LastUsedIP", defaultAddress);
        _targetPort = _autoStart != null ? _autoStart.port.ToString() : "30000";
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        _logs.Add($"[{type}] {logString}");
        if (_logs.Count > 10) _logs.RemoveAt(0);
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null)
        {
            _status = "No NetworkManager found";
            return;
        }

        string visualizerInfo = "";
        if (SystemCenteredVisualizer.Instance != null)
        {
            ulong sysId = SystemCenteredVisualizer.Instance.currentSystemId.Value;
            // Clients never load the map; the server pushes their system and skybox data
            string mapState = !NetworkManager.Singleton.IsServer ? "server-side"
                : SystemCenteredVisualizer.Instance.universeMap != null ? "OK" : "LOADING/MISSING";
            visualizerInfo = $"\nVisualizer: Active, SysID: {sysId}, Map: {mapState}";
        }
        else
        {
            visualizerInfo = "\nVisualizer: NOT FOUND IN SCENE";
        }

        // KEYBOARD SHORTCUTS (Fallback if mouse is stuck)
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            if (Input.GetKeyDown(KeyCode.H)) StartHost();
            if (Input.GetKeyDown(KeyCode.J)) StartClient();
            if (Input.GetKeyDown(KeyCode.S)) StartServerOnly();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = (Cursor.lockState != CursorLockMode.Locked);
        }

        if (NetworkManager.Singleton.IsServer)
            _status = NetworkManager.Singleton.IsHost ? "Running as HOST" : "Running as SERVER";
        else if (NetworkManager.Singleton.IsClient)
            _status = "Connected as CLIENT";
        else
            _status = "Disconnected / Idle (Press H:Host, J:Join, S:Server)";

        _status += visualizerInfo;
    }

    private void LateUpdate()
    {
        // FORCE UNLOCK: If we aren't connected, we MUST see the mouse
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void OnGUI()
    {
        GUI.Window(99, new Rect(10, 10, 360, 420), DrawWindow, "Network Debugger (H=Host, J=Join, S=Server)");
    }

    private void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        
        GUILayout.Label($"<b>Status:</b> {_status}");
        GUILayout.Label($"<b>My Public IP:</b> {_publicIP}");
        if (GUILayout.Button("Fetch My Public IP"))
        {
            StartCoroutine(GetPublicIP());
        }

        GUILayout.Space(10);

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(60));
            _targetAddress = GUILayout.TextField(_targetAddress);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", GUILayout.Width(60));
            _targetPort = GUILayout.TextField(_targetPort);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("START CLIENT (Join Friend) [J]"))
            {
                StartClient();
            }
            
            if (GUILayout.Button("START HOST (I am the Server) [H]"))
            {
                StartHost();
            }

            if (GUILayout.Button("START SERVER ONLY [S]"))
            {
                StartServerOnly();
            }
        }
        else if (NetworkManager.Singleton != null)
        {
            if (GUILayout.Button("Shutdown Network / Disconnect"))
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        GUILayout.Space(10);
        _showLogs = GUILayout.Toggle(_showLogs, "Show Recent Logs");
        if (_showLogs)
        {
            foreach (var log in _logs)
            {
                GUILayout.Label(log, GUILayout.Height(30));
            }
        }

        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void StartHost()
    {
        if (ushort.TryParse(_targetPort, out ushort port))
        {
            if (_autoStart != null)
            {
                _autoStart.StartHost(_targetAddress, port);
                return;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = _targetAddress;
                transport.ConnectionData.Port = port;
                transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            }
            NetworkManager.Singleton.StartHost();
        }
    }

    private void StartClient()
    {
        if (ushort.TryParse(_targetPort, out ushort port))
        {
            PlayerPrefs.SetString("LastUsedIP", _targetAddress);
            PlayerPrefs.Save();
            if (_autoStart != null) _autoStart.StartNetworking(false, _targetAddress, port);
            else NetworkManager.Singleton.StartClient();
        }
    }

    private void StartServerOnly()
    {
        if (ushort.TryParse(_targetPort, out ushort port))
        {
            if (_autoStart != null) _autoStart.StartNetworking(true, _targetAddress, port);
            else NetworkManager.Singleton.StartServer();
        }
    }

    private IEnumerator GetPublicIP()
    {
        _publicIP = "Fetching...";
        using (UnityWebRequest webRequest = UnityWebRequest.Get("https://api.ipify.org"))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                _publicIP = webRequest.downloadHandler.text;
            }
            else
            {
                _publicIP = "Error fetching IP";
            }
        }
    }
}
