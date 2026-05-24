using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkAutoStart : MonoBehaviour
{
    [Header("Default Settings")]
    public bool autoStart = false;
    public bool isServer = true;
    public ushort port = 30000;
    public string address = "127.0.0.1";

    [Header("Player Prefabs")]
    public GameObject fpsCrewPlayerPrefab;
    public GameObject fighterPlayerPrefab;
    public GameObject bigShipPlayerPrefab;

    private void Start()
    {
        ParseCommandLineArguments();

        if (autoStart)
        {
            StartNetworking(isServer, address, port);
        }
    }

    [ContextMenu("Start Host")]
    public void StartHostInspector() => StartNetworking(true, address, port);

    [ContextMenu("Start Client")]
    public void StartClientInspector() => StartNetworking(false, address, port);

    [ContextMenu("Start Server Only")]
    public void StartServerInspector() => StartNetworking(true, address, port);

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

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    public void SwitchPlayerPrefab(ulong clientId, ControllerType newShipType)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client)) return;

        NetworkObject currentPlayer = client.PlayerObject;
        if (currentPlayer == null) return;

        Vector3 position = currentPlayer.transform.position;
        Quaternion rotation = currentPlayer.transform.rotation;
        currentPlayer.Despawn();

        GameObject newPrefab = GetPrefabForShipType(newShipType);
        if (newPrefab == null) return;

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
