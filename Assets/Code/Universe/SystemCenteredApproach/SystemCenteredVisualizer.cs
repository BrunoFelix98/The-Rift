using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using System.Linq;

/// <summary>
/// The core visualizer for the 'System Centered Approach'.
/// Unlike the global visualizer, this only manages the celestial bodies within the player's CURRENT star system.
/// The server tracks which system every player is in (players jump individually) and pushes system data
/// to each client via custom NGO messages; players in different systems are hidden from each other.
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

    // Server: each player is in their own system; gates move one player at a time
    private readonly Dictionary<ulong, ulong> clientSystems = new Dictionary<ulong, ulong>();
    private ulong defaultSystemId;

    // Client/host: the system this instance is rendering
    public ulong LocalSystemId { get; private set; }
    public IEnumerable<CelestialHierarchyNode> CurrentNodes => nodeMap.Values;

    // Internal State
    private List<CelestialHierarchyNode> rootNodes = new List<CelestialHierarchyNode>();
    private Dictionary<ulong, CelestialHierarchyNode> nodeMap = new Dictionary<ulong, CelestialHierarchyNode>();
    private SystemPersistenceData currentSystemData;
    private int frameCounter = 0;

    // Client: re-request the skybox metadata if it hasn't shown up a while after the system data
    private bool metadataReceived;
    private float metadataRequestTime = -1f;
    private const float MetadataRetryDelay = 5f;

    private const string SYSTEM_SYNC_MESSAGE = "SystemSyncMessage";
    private const string METADATA_MESSAGE = "SystemMetadataMessage";

    [Header("Spawn")]
    [Tooltip("Distance from the station where players appear.")]
    public float spawnStationDistance = 600f;
    [Tooltip("Fallback distance above the star when a system has no station.")]
    public float spawnStarFallbackDistance = 12000f;

    private const float SpawnRingRadius = 20f;
    private const float SpawnRingSpacing = 10f;
    private const int SpawnSlotsPerRing = 8;
    // Clients 1 and 2 (first two joiners on a dedicated server) land opposite each other
    private static readonly int[] SpawnSlotOrder = { 2, 0, 4, 6, 1, 5, 3, 7 };

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
            RegisterCustomMessages();

            // Re-sync whenever we finish loading a scene
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnClientSceneEvent;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
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
        defaultSystemId = FindDefaultSystemId();
        universeLoaded = true;

        if (IsHost) MoveClientToSystem(NetworkManager.ServerClientId, GetClientSystem(NetworkManager.ServerClientId));

        // Clients that connected while the map was loading haven't received anything yet
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != NetworkManager.ServerClientId) SendInitialSync(client.ClientId);
        }
    }

    private ulong FindDefaultSystemId()
    {
        ulong lorynthId = FindSystemIdByName("Lorynth");
        if (lorynthId != 0)
        {
            Debug.Log($"[SystemCentered] Defaulting to Lorynth System ({lorynthId})");
            return lorynthId;
        }
        if (universeMap != null && universeMap.galaxies.Count > 0 && universeMap.galaxies[0].systems.Count > 0)
        {
            // Fallback to first system found
            return universeMap.galaxies[0].systems[0].id;
        }
        return 0;
    }

    public ulong GetClientSystem(ulong clientId)
    {
        return clientSystems.TryGetValue(clientId, out ulong systemId) ? systemId : defaultSystemId;
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
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(SYSTEM_SYNC_MESSAGE);
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(METADATA_MESSAGE);
            }

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnClientSceneEvent;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // The host is placed when the map finishes loading; others wait for that too
        if (clientId == NetworkManager.ServerClientId || !universeLoaded) return;
        SendInitialSync(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        clientSystems.Remove(clientId);
    }

    private void SendInitialSync(ulong clientId)
    {
        // System data first: metadata shares the same ordered reliable channel and would delay it
        MoveClientToSystem(clientId, GetClientSystem(clientId));
        SendUniverseMetadataToClient(clientId);
    }

    /// <summary>
    /// Puts one player into a system: their data, a spawn near the station, and who they can see.
    /// </summary>
    public void MoveClientToSystem(ulong clientId, ulong systemId)
    {
        if (!IsServer) return;

        var data = FindSystemById(systemId);
        if (data == null)
        {
            Debug.LogError($"[SystemCentered] Server: system {systemId} not found for client {clientId}.");
            return;
        }

        clientSystems[clientId] = systemId;

        if (clientId == NetworkManager.ServerClientId)
        {
            // The host renders straight from the server's map
            SwitchSystem(systemId, data);
        }
        else
        {
            Debug.Log($"[SystemCentered] Server: Sending system '{data.name}' details to client {clientId}.");
            SendSystemDataViaCustomMessage(clientId, data);
        }

        TeleportPlayerToSpawn(clientId, data);
        RefreshPlayerVisibility();
    }

    // Players in other systems share the same coordinates, so they must not exist for each other
    private void RefreshPlayerVisibility()
    {
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        foreach (var owner in clients)
        {
            NetworkObject playerObject = owner.PlayerObject;
            if (playerObject == null || !playerObject.IsSpawned) continue;
            ulong ownerSystem = GetClientSystem(owner.ClientId);

            foreach (var viewer in clients)
            {
                if (viewer.ClientId == owner.ClientId) continue;
                bool sameSystem = GetClientSystem(viewer.ClientId) == ownerSystem;

                if (viewer.ClientId == NetworkManager.ServerClientId)
                {
                    // The server can't hide objects from itself; a host just stops rendering them
                    if (IsHost) SetRenderersVisible(playerObject, sameSystem);
                    continue;
                }

                bool visible = playerObject.IsNetworkVisibleTo(viewer.ClientId);
                if (sameSystem && !visible) playerObject.NetworkShow(viewer.ClientId);
                else if (!sameSystem && visible) playerObject.NetworkHide(viewer.ClientId);
            }
        }
    }

    private static void SetRenderersVisible(NetworkObject obj, bool visible)
    {
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
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

        // One fragmented message instead of dozens of RPCs, so the skybox gets everything at once
        var wrapper = new SystemMetadataListWrapper { systems = metadata };
        using (var writer = new FastBufferWriter(64 + metadata.Count * 40, Unity.Collections.Allocator.Temp))
        {
            writer.WriteNetworkSerializable(wrapper);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(METADATA_MESSAGE, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMetadataServerRpc(ServerRpcParams rpcParams = default)
    {
        if (universeLoaded) SendUniverseMetadataToClient(rpcParams.Receive.SenderClientId);
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
            if (!metadataReceived) metadataRequestTime = Time.time;
        });

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(METADATA_MESSAGE, (senderId, reader) =>
        {
            reader.ReadNetworkSerializable(out SystemMetadataListWrapper wrapper);
            int count = wrapper.systems?.Count ?? 0;
            Debug.Log($"[SystemCentered] Received skybox metadata: {count} systems.");
            if (count == 0) return;

            metadataReceived = true;
            if (DynamicSkyboxManager.Instance != null)
            {
                DynamicSkyboxManager.Instance.SyncAllSystems(wrapper.systems);
            }
            else
            {
                Debug.LogWarning("[SystemCentered] No DynamicSkyboxManager in the scene; skybox metadata dropped.");
            }
        });
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

    /// <summary>Visual switch for this instance (client, or the host's own view).</summary>
    public void SwitchSystem(ulong systemId, SystemPersistenceData systemData = null)
    {
        Debug.Log($"[SystemCentered] Visualizer switching to System ID: {systemId}");
        ClearCurrentSystem();

        currentSystemData = systemData ?? FindSystemById(systemId);
        if (currentSystemData != null)
        {
            LocalSystemId = systemId;
            InitializeSystem(currentSystemData);

            // Celestial shaders light from this; StarData only refreshes it while the star is instantiated
            RefreshNodePositions();
            foreach (var node in rootNodes)
            {
                if (node.Params.Type != CelestialType.STAR) continue;
                Shader.SetGlobalVector(CelestialVisuals.StarPosId, node.RenderPosition);
                break;
            }

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

    // Server: world position of a celestial at the current galactic time, without any nodes
    public bool TryGetCelestialWorldPosition(SystemPersistenceData system, ulong celestialId, out Vector3 position)
    {
        position = Vector3.zero;
        if (system == null || system.celestials == null) return false;

        var byId = new Dictionary<ulong, CelestialPersistenceData>(system.celestials.Count);
        foreach (var c in system.celestials) byId[c.orbit.NetworkId] = c;
        if (!byId.TryGetValue(celestialId, out var target)) return false;

        float time = GalacticTimeManager.Instance != null ? (float)GalacticTimeManager.Instance.GalacticTime : 0f;
        var vizSettings = settings ?? new UniverseVisualizationSettings();

        // Same rule as the client hierarchy: a celestial without a parent in this system is a root at CenterPos
        Vector3 offset = Vector3.zero;
        var current = target;
        for (int depth = 0; depth < 16; depth++)
        {
            offset += CelestialHierarchyNode.ComputeOrbitOffset(current.orbit, time, vizSettings);
            if (current.orbit.ParentId == 0 || !byId.TryGetValue(current.orbit.ParentId, out var parent))
            {
                position = current.orbit.CenterPos + offset;
                return true;
            }
            current = parent;
        }
        position = current.orbit.CenterPos + offset;
        return true;
    }

    private void GetSpawnAnchor(SystemPersistenceData data, out Vector3 center, out Vector3 lookTarget)
    {
        Vector3 starPos = Vector3.zero;
        var star = data.celestials?.Find(c => c.orbit.Type == CelestialType.STAR);
        if (star != null) TryGetCelestialWorldPosition(data, star.orbit.NetworkId, out starPos);

        var station = data.celestials?.Find(c => c.orbit.Type == CelestialType.STATION);
        if (station != null && TryGetCelestialWorldPosition(data, station.orbit.NetworkId, out Vector3 stationPos))
        {
            // On the far side from the star, so the station is lit and the star isn't in the way
            Vector3 away = stationPos - starPos;
            away = away.sqrMagnitude > 1f ? away.normalized : Vector3.forward;
            center = stationPos + away * spawnStationDistance;
            lookTarget = stationPos;
            return;
        }

        center = starPos + Vector3.up * spawnStarFallbackDistance;
        lookTarget = starPos;
    }

    private void TeleportPlayerToSpawn(ulong clientId, SystemPersistenceData data)
    {
        if (!IsServer) return;

        GetSpawnAnchor(data, out Vector3 spawnCenter, out Vector3 lookTarget);
        GetSpawnPose(clientId, spawnCenter, lookTarget, out Vector3 targetPos, out Quaternion targetRot);

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null)
        {
            GameObject player = client.PlayerObject.gameObject;
            if (player.TryGetComponent(out ITeleportable teleportable))
            {
                teleportable.ServerTeleport(targetPos, targetRot);
            }
            else
            {
                player.transform.SetPositionAndRotation(FloatingOrigin.ToRender(targetPos), targetRot);
            }
        }

        ClientRpcParams rpcParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };
        TeleportPlayerClientRpc(targetPos, targetRot, rpcParams);
    }

    // Spread players on rings around the anchor (perpendicular to the view), all facing the target,
    // so they don't spawn inside each other's hulls
    private static void GetSpawnPose(ulong clientId, Vector3 center, Vector3 lookTarget, out Vector3 position, out Quaternion rotation)
    {
        int ring = (int)(clientId / SpawnSlotsPerRing);
        int slot = SpawnSlotOrder[clientId % SpawnSlotsPerRing];
        float angle = (slot * (360f / SpawnSlotsPerRing) + ring * (180f / SpawnSlotsPerRing)) * Mathf.Deg2Rad;
        float radius = SpawnRingRadius + ring * SpawnRingSpacing;

        Vector3 forward = lookTarget - center;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(Vector3.forward, forward);
        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right);

        position = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
        rotation = Quaternion.LookRotation(lookTarget - position, up);
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            GameObject player = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            if (player.TryGetComponent(out ITeleportable teleportable))
            {
                // Resets the predicted state too, otherwise the next tick moves it straight back
                teleportable.OwnerTeleport(position, rotation);
            }
            else
            {
                player.transform.SetPositionAndRotation(FloatingOrigin.ToRender(position), rotation);
            }
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

    /// <summary>Updates every node's world position (parents first), regardless of view distance.</summary>
    public void RefreshNodePositions()
    {
        float time = GalacticTimeManager.Instance != null ? (float)GalacticTimeManager.Instance.GalacticTime : 0f;
        foreach (var root in rootNodes) RefreshNodePositionsRecursive(root, time);
    }

    private static void RefreshNodePositionsRecursive(CelestialHierarchyNode node, float time)
    {
        node.UpdatePositionOnly(time);
        foreach (var child in node.Children) RefreshNodePositionsRecursive(child, time);
    }

    public SystemPersistenceData FindSystemById(ulong id)
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
        if (!IsClient) return;

        if (!IsServer && !metadataReceived && metadataRequestTime >= 0f && Time.time - metadataRequestTime > MetadataRetryDelay)
        {
            Debug.LogWarning("[SystemCentered] No skybox metadata yet, requesting it again.");
            metadataRequestTime = Time.time;
            RequestMetadataServerRpc();
        }

        if (currentSystemData == null || CelestialPool.Instance == null) return;

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
