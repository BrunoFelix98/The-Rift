using Unity.Netcode;
using UnityEngine;

public class ServerManager : MonoBehaviour
{
    public bool isServer = true;

    void Start()
    {
        if (isServer)
        {
            Debug.Log("Starting Dedicated Server...");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.StartServer();
        }
        else
        {
            NetworkManager.Singleton.StartClient();
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

    // Call this from somewhere that processes input server-side
    public void SwitchToFighter(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogWarning($"Client ID {clientId} not connected");
            return;
        }

        GameObject playerRoot = client.PlayerObject.gameObject;
        Transform fpsPrefab = playerRoot.transform.Find("FPSCrewMovement");
        Transform bigShipPrefab = playerRoot.transform.Find("BigShipMovement");
        Transform fighterPrefab = playerRoot.transform.Find("FighterMovement");

        if (fpsPrefab == null || bigShipPrefab == null || fighterPrefab == null)
        {
            Debug.LogWarning($"One or more required child prefabs not found under player root for client {clientId}");
            return;
        }

        if (fpsPrefab != null) fpsPrefab.gameObject.SetActive(false);
        if (fighterPrefab != null) fighterPrefab.gameObject.SetActive(true);
        if (bigShipPrefab != null) bigShipPrefab.gameObject.SetActive(false);

        if (fpsPrefab != null)
        {
            var cam = fpsPrefab.transform.Find("Camera");
            Debug.Log("Found fps camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fpsCam)) fpsCam.enabled = false;
        }
        if (fighterPrefab != null)
        {
            var cam = fighterPrefab.transform.Find("Camera");
            Debug.Log("Found fighter camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fighterCam)) fighterCam.enabled = true;
        }
        if (bigShipPrefab != null)
        {
            var cam = bigShipPrefab.transform.Find("Camera");
            Debug.Log("Found big ship camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var bigShipCam)) bigShipCam.enabled = false;
        }

        // Activate the fighter prefab
        fighterPrefab.gameObject.SetActive(true);

        Debug.Log($"Switched client {clientId} to fighter prefab by activating it and deactivating others.");
    }

    public void SwitchToBigShip(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogWarning($"Client ID {clientId} not connected");
            return;
        }

        GameObject playerRoot = client.PlayerObject.gameObject;
        Transform fpsPrefab = playerRoot.transform.Find("FPSCrewMovement");
        Transform bigShipPrefab = playerRoot.transform.Find("BigShipMovement");
        Transform fighterPrefab = playerRoot.transform.Find("FighterMovement");

        if (fpsPrefab == null || bigShipPrefab == null || fighterPrefab == null)
        {
            Debug.LogWarning($"One or more required child prefabs not found under player root for client {clientId}");
            return;
        }

        if (fpsPrefab != null) fpsPrefab.gameObject.SetActive(false);
        if (fighterPrefab != null) fighterPrefab.gameObject.SetActive(false);
        if (bigShipPrefab != null) bigShipPrefab.gameObject.SetActive(true);

        if (fpsPrefab != null)
        {
            var cam = fpsPrefab.transform.Find("Camera");
            Debug.Log("Found fps camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fpsCam)) fpsCam.enabled = false;
        }
        if (fighterPrefab != null)
        {
            var cam = fighterPrefab.transform.Find("Camera");
            Debug.Log("Found fighter camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fighterCam)) fighterCam.enabled = false;
        }
        if (bigShipPrefab != null)
        {
            var cam = bigShipPrefab.transform.Find("Camera");
            Debug.Log("Found big ship camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var bigShipCam)) bigShipCam.enabled = true;
        }

        // Activate the fighter prefab
        bigShipPrefab.gameObject.SetActive(true);

        Debug.Log($"Switched client {clientId} to big ship prefab by activating it and deactivating others.");
    }

    public void SwitchToFPSCrew(ulong clientId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            Debug.LogWarning($"Client ID {clientId} not connected");
            return;
        }

        GameObject playerRoot = client.PlayerObject.gameObject;
        Transform fpsPrefab = playerRoot.transform.Find("FPSCrewMovement");
        Transform bigShipPrefab = playerRoot.transform.Find("BigShipMovement");
        Transform fighterPrefab = playerRoot.transform.Find("FighterMovement");

        if (fpsPrefab == null || bigShipPrefab == null || fighterPrefab == null)
        {
            Debug.LogWarning($"One or more required child prefabs not found under player root for client {clientId}");
            return;
        }

        if (fpsPrefab != null) fpsPrefab.gameObject.SetActive(true);
        if (fighterPrefab != null) fighterPrefab.gameObject.SetActive(false);
        if (bigShipPrefab != null) bigShipPrefab.gameObject.SetActive(false);

        if (fpsPrefab != null)
        {
            var cam = fpsPrefab.transform.Find("Camera");
            Debug.Log("Found fps camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fpsCam)) fpsCam.enabled = true;
        }
        if (fighterPrefab != null)
        {
            var cam = fighterPrefab.transform.Find("Camera");
            Debug.Log("Found fighter camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var fighterCam)) fighterCam.enabled = false;
        }
        if (bigShipPrefab != null)
        {
            var cam = bigShipPrefab.transform.Find("Camera");
            Debug.Log("Found big ship camera: " + cam);
            if (cam != null && cam.TryGetComponent<Camera>(out var bigShipCam)) bigShipCam.enabled = false;
        }

        // Activate the fighter prefab
        fpsPrefab.gameObject.SetActive(true);

        Debug.Log($"Switched client {clientId} to fps crew prefab by activating it and deactivating others.");
    }
}