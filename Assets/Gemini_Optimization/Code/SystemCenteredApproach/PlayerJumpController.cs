using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerJumpController : NetworkBehaviour
{
    [Header("Settings")]
    public float interactionRange;
    public KeyCode jumpKey = KeyCode.Z; // Default to 'Z' for Jump

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(jumpKey))
        {
            TryJump();
        }
    }

    private void TryJump()
    {
        // 1. Find the nearest gate
        GateData closestGate = null;
        float minDistance = interactionRange;

        var gates = FindObjectsByType<GateData>();
        foreach (var gate in gates)
        {
            float dist = Vector3.Distance(transform.position, gate.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestGate = gate;
            }
        }

        if (closestGate != null)
        {
            ulong gateId = closestGate.Node.Params.NetworkId;
            Debug.Log($"[JumpController] Requesting jump via gate {closestGate.gameObject.name} ({gateId})");
            
            // Send the Gate ID to the server, NOT the destination ID
            JumpManager.Instance.RequestJumpByGateServerRpc(gateId);
        }
        else
        {
            Debug.Log("[JumpController] No jump gate in range.");
        }
    }
}
