using Unity.Netcode;
using UnityEngine;

public class TestSwitchShips : NetworkBehaviour
{
    void Update()
    {
        if (!Application.isFocused || !IsOwner) return;

        if (Input.GetKeyDown(KeyCode.U)) // Press U to switch to Fighter
        {
            Debug.Log("U key pressed - requesting Fighter switch");
            RequestSwitchShipServerRpc(ControllerType.Fighter);
        }
        if (Input.GetKeyDown(KeyCode.J)) // Press J to switch to Big Ship
        {
            Debug.Log("J key pressed - requesting BigShip switch");
            RequestSwitchShipServerRpc(ControllerType.BigShip);
        }
        if (Input.GetKeyDown(KeyCode.K)) // Press K to switch to FPS Crew
        {
            Debug.Log("K key pressed - requesting FPSCrew switch");
            RequestSwitchShipServerRpc(ControllerType.FPSCrew);
        }
    }

    [ServerRpc]
    void RequestSwitchShipServerRpc(ControllerType shipType, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"ServerRpc received for ship type: {shipType}");
        ulong clientID = rpcParams.Receive.SenderClientId;
        ServerManager serverManager = FindAnyObjectByType<ServerManager>();
        if (serverManager != null)
        {
            Debug.Log("ServerManager found, calling SwitchPlayerPrefab");
            serverManager.SwitchPlayerPrefab(clientID, shipType);
            return;
        }

        // Bootstrap scene setup: NetworkAutoStart sits on the persistent NetworkManager object
        NetworkAutoStart autoStart = FindAnyObjectByType<NetworkAutoStart>();
        if (autoStart != null)
        {
            autoStart.SwitchPlayerPrefab(clientID, shipType);
        }
        else
        {
            Debug.LogWarning("Neither ServerManager nor NetworkAutoStart found.");
        }
    }
}
