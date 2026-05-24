using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkManager))]
public class NetworkManagerPersistence : MonoBehaviour
{
    private static NetworkManagerPersistence _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("[NetworkManagerPersistence] Destroying duplicate NetworkManager.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
