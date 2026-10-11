using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gate jumps. Each request moves only the requesting player into the destination system;
/// everyone else stays where they are.
/// </summary>
public class JumpManager : NetworkBehaviour
{
    public static JumpManager Instance { get; private set; }

    [Tooltip("Server-side limit for how far a player may be from the gate they use (covers latency).")]
    public float maxGateUseDistance = 3000f;

    public delegate void JumpEvent(ulong systemId);
    public event JumpEvent OnJumpStarted;
    public event JumpEvent OnJumpCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestJumpByGateServerRpc(ulong gateId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[JumpManager] Server: Client {clientId} requests jump via Gate ID {gateId}");

        var visualizer = SystemCenteredVisualizer.Instance;
        if (visualizer == null || visualizer.universeMap == null)
        {
            Debug.LogError("[JumpManager] Server: Universe Map not loaded! Cannot resolve gate.");
            return;
        }

        // 1. The gate must be in the system this player is actually in
        var currentSystem = visualizer.FindSystemById(visualizer.GetClientSystem(clientId));
        var gate = currentSystem?.celestials.Find(c => c.orbit.NetworkId == gateId && c.orbit.Type == CelestialType.GATE);
        if (gate == null)
        {
            Debug.LogWarning($"[JumpManager] Server: Gate {gateId} is not in client {clientId}'s system.");
            return;
        }

        // 2. ...and the player must be near it
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null
            && visualizer.TryGetCelestialWorldPosition(currentSystem, gateId, out Vector3 gatePos))
        {
            float distance = Vector3.Distance(client.PlayerObject.transform.position, gatePos);
            if (distance > maxGateUseDistance)
            {
                Debug.LogWarning($"[JumpManager] Server: Client {clientId} is {distance:0} away from gate '{gate.orbit.Name}', too far.");
                return;
            }
        }

        ulong targetStarId = gate.destinationId;
        if (targetStarId == 0)
        {
            Debug.LogWarning($"[JumpManager] Server: Gate '{gate.orbit.Name}' has no destination.");
            return;
        }

        // 3. Resolve which system contains the destination star
        ulong targetSystemId = 0;
        foreach (var galaxy in visualizer.universeMap.galaxies)
        {
            foreach (var system in galaxy.systems)
            {
                if (system.celestials.Exists(c => c.orbit.NetworkId == targetStarId))
                {
                    targetSystemId = system.id;
                    Debug.Log($"[JumpManager] Server: Resolved destination to System '{system.name}' ({targetSystemId})");
                    break;
                }
            }
            if (targetSystemId != 0) break;
        }

        if (targetSystemId != 0)
        {
            StartJump(clientId, targetSystemId);
        }
        else
        {
            Debug.LogError($"[JumpManager] Server: Gate found, but Destination Star {targetStarId} does not belong to any known system!");
        }
    }

    private void StartJump(ulong clientId, ulong targetSystemId)
    {
        if (!IsServer) return;

        // Only the jumping player gets the warp effect and the new system
        StartWarpClientRpc(targetSystemId, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        SystemCenteredVisualizer.Instance.MoveClientToSystem(clientId, targetSystemId);
    }

    [ClientRpc]
    private void StartWarpClientRpc(ulong systemId, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[JumpManager] Starting Warp to System {systemId}");

        // Trigger event - FighterPiloting will hear this and freeze itself
        OnJumpStarted?.Invoke(systemId);

        if (WarpEffectManager.Instance != null)
        {
            WarpEffectManager.Instance.PlayWarpEffect();
        }
    }

    public void NotifyJumpCompleted(ulong systemId)
    {
        Debug.Log($"[JumpManager] Jump to System {systemId} Completed");

        // Trigger event - FighterPiloting will hear this and unfreeze itself
        OnJumpCompleted?.Invoke(systemId);

        if (WarpEffectManager.Instance != null)
        {
            WarpEffectManager.Instance.StopWarpEffect();
        }
    }
}
