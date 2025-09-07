using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    [Header("Build type check")]
    public bool isServer = true;

    [Header("Player Prefabs")]
    public GameObject fpsCrewPlayerPrefab;
    public GameObject fighterPlayerPrefab;
    public GameObject bigShipPlayerPrefab;

    private void Start()
    {
        try
        {
            if (isServer)
            {
                Debug.Log("Starting Dedicated Server...");
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                    NetworkManager.Singleton.StartServer();
                }
                else
                {
                    Debug.LogError("NetworkManager.Singleton is null. Cannot start server.");
                }
            }
            else
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.StartClient();
                }
                else
                {
                    Debug.LogError("NetworkManager.Singleton is null. Cannot start client.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Network Error] Failed to start networking: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
    }

    public void SwitchPlayerPrefab(ulong clientId, ControllerType newShipType)
    {
        Debug.Log($"SwitchPlayerPrefab called for client {clientId} to {newShipType}");

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogWarning($"Client ID {clientId} not connected");
            return;
        }

        // Get current player object
        NetworkObject currentPlayer = client.PlayerObject;
        if (currentPlayer == null)
        {
            Debug.LogWarning($"No player object found for client {clientId}");
            return;
        }

        Debug.Log($"Current player found, despawning...");

        // Store position and rotation
        Vector3 position = currentPlayer.transform.position;
        Quaternion rotation = currentPlayer.transform.rotation;

        // Despawn current player
        currentPlayer.Despawn();

        Debug.Log($"Player despawned, getting new prefab for {newShipType}");

        // Get new prefab
        GameObject newPrefab = GetPrefabForShipType(newShipType);
        if (newPrefab == null)
        {
            Debug.LogError($"No prefab found for ship type {newShipType}");
            return;
        }

        Debug.Log($"New prefab found: {newPrefab.name}, instantiating...");

        // Spawn new player prefab
        GameObject newPlayerObj = Instantiate(newPrefab, position, rotation);
        NetworkObject newNetworkObj = newPlayerObj.GetComponent<NetworkObject>();

        if (newNetworkObj != null)
        {
            Debug.Log($"Spawning new player object for client {clientId}");
            newNetworkObj.SpawnAsPlayerObject(clientId);
            Debug.Log($"Successfully switched client {clientId} to {newShipType} prefab");
        }
        else
        {
            Debug.LogError("New player prefab doesn't have NetworkObject component");
            Destroy(newPlayerObj);
        }
    }

    private GameObject GetPrefabForShipType(ControllerType shipType)
    {
        switch (shipType)
        {
            case ControllerType.Fighter:
                return fighterPlayerPrefab;
            case ControllerType.FPSCrew:
                return fpsCrewPlayerPrefab;
            case ControllerType.BigShip:
                return bigShipPlayerPrefab;
            default:
                return null;
        }
    }
}
