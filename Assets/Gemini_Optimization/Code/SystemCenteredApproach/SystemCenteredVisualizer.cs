using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using System.Linq;

/// <summary>
/// The core visualizer for the 'System Centered Approach'. 
/// Unlike the global visualizer, this only manages the celestial bodies within the player's CURRENT star system.
/// It handles server-authoritative system switching and synchronizes system data to clients via custom NGO messages.
/// </summary>
public class SystemCenteredVisualizer : NetworkBehaviour
{
    public static SystemCenteredVisualizer Instance { get; private set; }

    [Header("Distance & LOD Settings")]
    [Tooltip("Maximum distance at which planets and moons are instantiated.")]
    public float localViewDistance = 500000f; 
    
    [Tooltip("Distance at which stars from other systems are rendered in the skybox.")]
    public float skyboxStarDistance = 1000000f;

    [Header("Visualization Tuning")]
    [Tooltip("Visual scales and multipliers for celestial bodies.")]
    public UniverseVisualizationSettings settings;

    // Server only. ~230k objects: serializing it would hang the Inspector and bloat the scene
    [NonSerialized] public UniverseMapDataV2 universeMap;
    private bool universeLoaded;
    
    [Header("Network State")]
    [Tooltip("The ID of the star system currently being visualized.")]
    public NetworkVariable<ulong> currentSystemId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Internal State
    private List<CelestialHierarchyNode> rootNodes = new List<CelestialHierarchyNode>();
    private Dictionary<ulong, CelestialHierarchyNode> nodeMap = new Dictionary<ulong, CelestialHierarchyNode>();
    private SystemPersistenceData currentSystemData;
    private bool playerTeleported = false;
    private int frameCounter = 0;

    private const string SYSTEM_SYNC_MESSAGE = "SystemSyncMessage";

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
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            // Register for detailed system data messages from the server
            RegisterCustomMessages();
            
            // Re-sync whenever we finish loading a scene
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnClientSceneEvent;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            LoadUniverseDataAsync();
        }
    }

    // Only the server needs the full map; clients get their current system and the skybox metadata pushed
    private async void LoadUniverseDataAsync()
    {
        string path = UniversePersistenceManager.GetPath();
        UniverseMapDataV2 map = null;
        try
        {
            map = await Task.Run(() => UniversePersistenceManager.LoadAndConvertV2(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SystemCentered] Universe map load failed: {e}");
        }

        if (this == null || !IsSpawned) return;

        ApplyUniverseData(map);
        InitializeServerSystem();
        universeLoaded = true;

        // Clients that connected while the map was loading haven't received anything yet
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.ServerClientId) SendInitialSync(client.ClientId);
        }
    }

    private void InitializeServerSystem()
    {
        // Default to 'Lorynth' system if no system is active
        if (currentSystemId.Value == 0)
        {
            ulong lorynthId = FindSystemIdByName("Lorynth");
            if (lorynthId != 0)
            {
                currentSystemId.Value = lorynthId;
                Debug.Log($"[SystemCentered] Defaulting to Lorynth System ({lorynthId})");
            }
            else if (universeMap != null && universeMap.galaxies.Count > 0 && universeMap.galaxies[0].systems.Count > 0)
            {
                // Fallback to first system found
                currentSystemId.Value = universeMap.galaxies[0].systems[0].id;
            }
        }
        
        // Initialize the server's local visual state
        if (currentSystemId.Value != 0) SwitchSystem(currentSystemId.Value);
    }

    private void OnClientSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.ClientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("[SystemCentered] Scene load complete. Requesting system sync from server...");
            RequestSystemSyncServerRpc();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.CustomMessagingManager != null)
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(SYSTEM_SYNC_MESSAGE);
            
            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnClientSceneEvent;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // The host's own client renders from the server state directly; others wait for the map load to finish
        if (clientId == NetworkManager.ServerClientId || !universeLoaded) return;
        SendInitialSync(clientId);
    }

    private void SendInitialSync(ulong clientId)
    {
        // System data first: metadata shares the same ordered reliable channel and would delay it
        if (currentSystemId.Value != 0)
        {
            var data = FindSystemById(currentSystemId.Value);
            if (data != null)
            {
                Debug.Log($"[SystemCentered] Server: Sending system '{data.name}' details to client {clientId}.");
                SendSystemDataViaCustomMessage(clientId, data);
            }
        }

        SendUniverseMetadataToClient(clientId);
    }

    /// <summary>
    /// Sends lightweight metadata (IDs and Positions) for ALL star systems to a client.
    /// This is used primarily by the DynamicSkyboxManager to render distant stars.
    /// </summary>
    private void SendUniverseMetadataToClient(ulong clientId)
    {
        if (universeMap == null) return;

        List<SystemMetadata> metadata = new List<SystemMetadata>();
        double time = GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;
        
        foreach (var g in universeMap.galaxies)
        {
            Vector3d gPos = g.GetAbsolutePosition(time);
            foreach (var s in g.systems)
            {
                metadata.Add(new SystemMetadata { 
                    id = s.id, 
                    absolutePosition = s.GetAbsolutePosition(time, gPos) 
                });
            }
        }

        // Batch the metadata to stay within NGO packet limits
        int batchSize = 100;
        for (int i = 0; i < metadata.Count; i += batchSize)
        {
            int count = Mathf.Min(batchSize, metadata.Count - i);
            SyncAllSystemsMetadataClientRpc(new SystemMetadataListWrapper { systems = metadata.GetRange(i, count) }, 
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }
    }

    [ClientRpc]
    private void SyncAllSystemsMetadataClientRpc(SystemMetadataListWrapper wrapper, ClientRpcParams params_ = default)
    {
        if (IsServer) return;
        Debug.Log($"[SystemCentered] Received metadata batch: {wrapper.systems.Count} systems.");
        if (DynamicSkyboxManager.Instance != null)
        {
            DynamicSkyboxManager.Instance.SyncAllSystems(wrapper.systems);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSystemSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!universeLoaded) return;
        SendInitialSync(rpcParams.Receive.SenderClientId);
    }

    private void SendSystemDataViaCustomMessage(ulong clientId, SystemPersistenceData data)
    {
        // We use Custom Messages for large system data blobs that exceed standard RPC limits
        FastBufferWriter writer = new FastBufferWriter(256000, Unity.Collections.Allocator.Temp);
        using (writer)
        {
            writer.WriteNetworkSerializable(data);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(SYSTEM_SYNC_MESSAGE, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        
        TeleportPlayerToCenter(clientId);
    }

    private void RegisterCustomMessages()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(SYSTEM_SYNC_MESSAGE, (senderId, reader) => 
        {
            SystemPersistenceData data = new SystemPersistenceData();
            reader.ReadNetworkSerializable(out data);
            Debug.Log($"[SystemCentered] Client received system '{data.name}' data. Updating visuals...");
            
            SwitchSystem(data.id, data);
        });
    }

    public void ForceSyncSystem(ulong systemId)
    {
        if (!IsServer) return;
        
        var data = FindSystemById(systemId);
        if (data == null) return;

        // Metadata is static, clients already have it from their initial sync
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId == NetworkManager.ServerClientId) continue;
            SendSystemDataViaCustomMessage(client.ClientId, data);
        }
    }

    private void ApplyUniverseData(UniverseMapDataV2 map)
    {
        universeMap = map;
        
        if (universeMap != null)
        {
            Debug.Log($"[SystemCentered] Universe Map loaded successfully from binary.");
            if (DynamicSkyboxManager.Instance != null)
            {
                DynamicSkyboxManager.Instance.SetUniverseMap(universeMap);
            }
        }
        else
        {
            Debug.LogWarning("[SystemCentered] No UniverseMap.bin found. Awaiting network sync or manual generation.");
        }
    }

    private ulong FindSystemIdByName(string name)
    {
        if (universeMap == null) return 0;
        foreach (var galaxy in universeMap.galaxies)
        {
            foreach (var system in galaxy.systems)
            {
                if (system.name.Contains(name, System.StringComparison.OrdinalIgnoreCase)) return system.id;
            }
        }
        return 0;
    }

    public void SwitchSystem(ulong systemId, SystemPersistenceData systemData = null)
    {
        Debug.Log($"[SystemCentered] Visualizer switching to System ID: {systemId}");
        ClearCurrentSystem();
        
        currentSystemData = systemData ?? FindSystemById(systemId);
        if (currentSystemData != null)
        {
            InitializeSystem(currentSystemData);
            
            double time = GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;
            Vector3d absolutePos = currentSystemData.absolutePosition; 
            
            // Calculate true absolute position relative to galaxy
            if (universeMap != null && universeMap.galaxies != null)
            {
                foreach(var g in universeMap.galaxies) {
                    if (g.id == currentSystemData.parentId) {
                        absolutePos = currentSystemData.GetAbsolutePosition(time, g.GetAbsolutePosition(time));
                        break;
                    }
                }
            }

            if (DynamicSkyboxManager.Instance != null)
            {
                DynamicSkyboxManager.Instance.SetCurrentSystem(systemId, absolutePos);
            }
            
            if (IsServer)
            {
                // Ensure currentSystemId is updated for late-joiners
                currentSystemId.Value = systemId;
                
                // Teleport all connected clients to the new system origin
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    TeleportPlayerToCenter(client.ClientId);
                }
            }
        }
        else
        {
            Debug.LogError($"[SystemCentered] FAILED to switch system: Data for ID {systemId} not found.");
        }

        if (JumpManager.Instance != null)
        {
            JumpManager.Instance.NotifyJumpCompleted(systemId);
        }
    }

    private void TeleportPlayerToCenter(ulong clientId)
    {
        if (!IsServer) return;
        
        // Offset slightly from zero to avoid being stuck inside the sun
        Vector3 targetPos = Vector3.zero + Vector3.up * 500f; 

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
        {
            GameObject player = client.PlayerObject.gameObject;
            if (player.TryGetComponent(out FighterPiloting fighter))
            {
                fighter.ServerTeleport(targetPos);
            }
            else
            {
                player.transform.position = targetPos;
                if (player.TryGetComponent(out Rigidbody body)) body.position = targetPos;
            }
        }
        
        ClientRpcParams rpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
        TeleportPlayerClientRpc(targetPos, rpcParams);
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 position, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            player.transform.position = position;
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.position = position;
                body.linearVelocity = Vector3.zero;
            }
            playerTeleported = true;
            Debug.Log($"[SystemCentered] Local player teleported to {position}");
        }
    }

    private void ClearCurrentSystem()
    {
        foreach (var node in rootNodes)
        {
            node.DeactivateAllChildren(CelestialPool.Instance);
            if (node.ActiveInstance != null) CelestialPool.Instance.ReturnToPool(node.Params.Type, node.ActiveInstance);
        }
        rootNodes.Clear();
        nodeMap.Clear();
        playerTeleported = false;
    }

    private void InitializeSystem(SystemPersistenceData data)
    {
        if (data == null || data.celestials == null) return;
        
        // Build the local hierarchy nodes for the new system
        foreach (var celestial in data.celestials)
        {
            var node = new CelestialHierarchyNode(celestial);
            nodeMap[celestial.orbit.NetworkId] = node;
        }

        foreach (var node in nodeMap.Values)
        {
            if (node.Params.ParentId != 0 && nodeMap.TryGetValue(node.Params.ParentId, out var parentNode))
            {
                node.Parent = parentNode;
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }
    }

    private SystemPersistenceData FindSystemById(ulong id)
    {
        if (universeMap == null || universeMap.galaxies == null) return null;
        foreach (var galaxy in universeMap.galaxies)
        {
            if (galaxy.systems == null) continue;
            foreach (var system in galaxy.systems)
            {
                if (system.id == id) return system;
            }
        }
        return null;
    }

    void Update()
    {
        if (!IsClient || currentSystemData == null) return;
        if (CelestialPool.Instance == null) return;

        double time = GalacticTimeManager.Instance != null ? GalacticTimeManager.Instance.GalacticTime : 0;
        frameCounter++;

        Vector3 playerPos = Vector3.zero;
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            playerPos = NetworkManager.Singleton.LocalClient.PlayerObject.transform.position;
        }

        Plane[] frustumPlanes = null;
        if (Camera.main != null) frustumPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        // Update visibility and orbits for all root-level objects in this system (usually just the Star)
        foreach (var node in rootNodes)
        {
            float starDist = settings != null ? settings.starViewDistance : skyboxStarDistance;
            node.UpdateRecursive((float)time, playerPos, localViewDistance, starDist, CelestialPool.Instance, frustumPlanes, false, frameCounter);
        }
    }
}
