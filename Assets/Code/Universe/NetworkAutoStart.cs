using Unity.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkAutoStart : MonoBehaviour
{
    [Header("Default Settings")]
    public bool autoStart = true;
    [Tooltip("Fallback role when neither -server/-client nor a Multiplayer Role decides it.")]
    public bool isServer = false;
    public ushort port = 30000;
    [Tooltip("Address clients connect to in builds. The editor uses 127.0.0.1 unless -address is passed.")]
    public string address = "127.0.0.1";

    [Tooltip("Frame cap for a dedicated server. Uncapped it pins a CPU core, and the resulting stalls hit every client at once.")]
    public int serverFrameRate = 60;

    [Header("Scenes")]
    [Tooltip("Loaded by the server through NGO scene management; clients follow automatically.")]
    public string gameplaySceneName = "MainGameScene";

    [Header("Player Prefabs")]
    public GameObject fpsCrewPlayerPrefab;
    public GameObject fighterPlayerPrefab;
    public GameObject bigShipPlayerPrefab;

    private bool addressFromCommandLine;

    private void Start()
    {
        ApplyMultiplayerRole();
        ParseCommandLineArguments();
        if (Application.isEditor && !addressFromCommandLine) address = "127.0.0.1";

        if (autoStart)
        {
            StartNetworking(isServer, address, port);
        }
    }

    [ContextMenu("Start Host")]
    public void StartHostInspector() => StartHost(address, port);

    [ContextMenu("Start Client")]
    public void StartClientInspector() => StartNetworking(false, address, port);

    [ContextMenu("Start Server Only")]
    public void StartServerInspector() => StartNetworking(true, address, port);

    private void ApplyMultiplayerRole()
    {
        // ClientAndServer means roles don't decide (e.g. a regular editor session), keep the inspector value
        MultiplayerRoleFlags role = MultiplayerRolesManager.ActiveMultiplayerRoleMask;
        if (role == MultiplayerRoleFlags.Server) isServer = true;
        else if (role == MultiplayerRoleFlags.Client) isServer = false;
    }

    private void ParseCommandLineArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        if (args == null) return;

        foreach (string arg in args)
        {
            if (arg == "-server")
            {
                isServer = true;
                autoStart = true;
            }
            if (arg == "-client")
            {
                isServer = false;
                autoStart = true;
            }
        }

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-address" && i + 1 < args.Length)
            {
                address = args[i + 1];
                addressFromCommandLine = true;
            }
            if (args[i] == "-port" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p))
            {
                port = p;
            }
        }
    }

    public void StartNetworking(bool asServer, string targetAddress, ushort targetPort)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkAutoStart] No NetworkManager found!");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = targetAddress;
            transport.ConnectionData.Port = targetPort;

            if (asServer)
            {
                transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            }
            
            Debug.Log($"[NetworkAutoStart] Configured Transport - Address: {targetAddress}, Port: {targetPort}, ListenAddress: {transport.ConnectionData.ServerListenAddress}");
        }

        try
        {
            if (asServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnServerStarted += LoadGameplayScene;
                if (!Application.isEditor)
                {
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = serverFrameRate;
                }
                NetworkManager.Singleton.StartServer();
            }
            else
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Network Error] Failed to start networking: {ex.Message}");
        }
    }

    public void StartHost(string targetAddress, ushort targetPort)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkAutoStart] No NetworkManager found!");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = targetAddress;
            transport.ConnectionData.Port = targetPort;
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
        }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnServerStarted += LoadGameplayScene;
        NetworkManager.Singleton.StartHost();
    }

    private void LoadGameplayScene()
    {
        NetworkManager.Singleton.OnServerStarted -= LoadGameplayScene;

        if (SceneManager.GetActiveScene().name == gameplaySceneName) return;

        var status = NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError($"[NetworkAutoStart] Failed to load '{gameplaySceneName}': {status}. Is it in Build Settings?");
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnServerStarted -= LoadGameplayScene;
        }
    }

    public void SwitchPlayerPrefab(ulong clientId, ControllerType newShipType)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return;

        NetworkObject currentPlayer = client.PlayerObject;
        if (currentPlayer == null) return;

        GameObject newPrefab = GetPrefabForShipType(newShipType);
        if (newPrefab == null) return;

        Vector3 position = currentPlayer.transform.position;
        Quaternion rotation = currentPlayer.transform.rotation;
        currentPlayer.Despawn();

        GameObject newPlayerObj = Instantiate(newPrefab, position, rotation);
        NetworkObject newNetworkObj = newPlayerObj.GetComponent<NetworkObject>();

        if (newNetworkObj != null)
        {
            newNetworkObj.SpawnAsPlayerObject(clientId);
        }
        else
        {
            Destroy(newPlayerObj);
        }
    }

    private GameObject GetPrefabForShipType(ControllerType shipType)
    {
        switch (shipType)
        {
            case ControllerType.Fighter: return fighterPlayerPrefab;
            case ControllerType.FPSCrew: return fpsCrewPlayerPrefab;
            case ControllerType.BigShip: return bigShipPlayerPrefab;
            default: return null;
        }
    }
}
