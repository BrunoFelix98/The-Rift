using Unity.Netcode;
using UnityEngine;

public class JumpManager : NetworkBehaviour
{
    public static JumpManager Instance { get; private set; }

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

    public override void OnNetworkSpawn()
    {
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestJumpServerRpc(ulong targetSystemId)
    {
        StartJump(targetSystemId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestJumpByGateServerRpc(ulong gateId)
    {
        if (!IsServer) return;

        Debug.Log($"[JumpManager] Server: Received jump request for Gate ID {gateId}");

        if (SystemCenteredVisualizer.Instance == null || SystemCenteredVisualizer.Instance.universeMap == null)
        {
            Debug.LogError("[JumpManager] Server: Universe Map not loaded! Cannot resolve gate.");
            return;
        }

        ulong targetStarId = 0;
        ulong targetSystemId = 0;

        // 1. Find the gate in the full map to get its destination
        foreach (var galaxy in SystemCenteredVisualizer.Instance.universeMap.galaxies)
        {
            foreach (var system in galaxy.systems)
            {
                var gate = system.celestials.Find(c => c.orbit.NetworkId == gateId);
                if (gate != null)
                {
                    targetStarId = gate.destinationId;
                    Debug.Log($"[JumpManager] Server: Found gate '{gate.orbit.Name}'. Destination Star ID: {targetStarId}");
                    break;
                }
            }
            if (targetStarId != 0) break;
        }

        if (targetStarId == 0)
        {
            Debug.LogWarning($"[JumpManager] Server: Could not find Gate {gateId} in any star system.");
            return;
        }

        // 2. Resolve which system contains that destination star
        foreach (var galaxy in SystemCenteredVisualizer.Instance.universeMap.galaxies)
        {
            foreach (var system in galaxy.systems)
            {
                // Check if this system is the one we are looking for
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
            StartJump(targetSystemId);
        }
        else
        {
            Debug.LogError($"[JumpManager] Server: Gate found, but Destination Star {targetStarId} does not belong to any known system!");
        }
    }

    private void StartJump(ulong targetSystemId)
    {
        if (!IsServer) return;

        // 1. Notify all clients to start warp effect
        StartWarpClientRpc(targetSystemId);

        // 2. Swap the system in place; reloading the scene would destroy the in-scene NetworkObject
        if (SystemCenteredVisualizer.Instance != null)
        {
            SystemCenteredVisualizer.Instance.SwitchSystem(targetSystemId);
            SystemCenteredVisualizer.Instance.ForceSyncSystem(targetSystemId);
        }
    }

    [ClientRpc]
    private void StartWarpClientRpc(ulong systemId)
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
