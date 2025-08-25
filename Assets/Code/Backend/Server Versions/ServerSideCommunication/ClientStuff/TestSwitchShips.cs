using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class TestSwitchShips : NetworkBehaviour
{
    [Header("Assign these in inspector")]
    public Camera fpsVirtualCamera;
    public Camera fighterVirtualCamera;
    public CinemachineCamera bigShipVirtualCamera;

    // A variable to track the current ship type on the server
    private NetworkVariable<ControllerType> currentShipType = new NetworkVariable<ControllerType>(ControllerType.FPSCrew, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Start()
    {
        // Subscribe to changes in the currentShipType variable
        currentShipType.OnValueChanged += OnShipTypeChanged;
    }

    public override void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        currentShipType.OnValueChanged -= OnShipTypeChanged;

        // Call the base class's OnDestroy method to ensure proper cleanup
        base.OnDestroy();
    }

    void Update()
    {
        if (!Application.isFocused || !IsOwner) return;

        if (Input.GetKeyDown(KeyCode.U)) // Press U to switch to Fighter
        {
            RequestSwitchShipServerRpc(ControllerType.Fighter);
        }

        if (Input.GetKeyDown(KeyCode.J)) // Press J to switch to Big Ship
        {
            RequestSwitchShipServerRpc(ControllerType.BigShip);
        }

        if (Input.GetKeyDown(KeyCode.K)) // Press K to switch to FPS Crew
        {
            RequestSwitchShipServerRpc(ControllerType.FPSCrew);
        }
    }

    [ServerRpc]
    void RequestSwitchShipServerRpc(ControllerType shipType)
    {
        ServerManager serverManager = FindAnyObjectByType<ServerManager>();
        if (serverManager != null)
        {
            switch (shipType)
            {
                case ControllerType.Fighter:
                    serverManager.SwitchToFighter(NetworkManager.LocalClientId);
                    break;
                case ControllerType.BigShip:
                    serverManager.SwitchToBigShip(NetworkManager.LocalClientId);
                    break;
                case ControllerType.FPSCrew:
                    serverManager.SwitchToFPSCrew(NetworkManager.LocalClientId);
                    break;
                default:
                    break;
            }

            // Update the current ship type on the server
            currentShipType.Value = shipType;
        }
        else
        {
            Debug.LogWarning("ServerManager not found in scene.");
        }
    }

    // Called whenever the currentShipType changes
    private void OnShipTypeChanged(ControllerType oldShipType, ControllerType newShipType)
    {
        // Update the cameras locally for all clients
        SwitchCameraLocal(newShipType);
    }

    // Enable/Disable virtual cameras locally on all clients
    private void SwitchCameraLocal(ControllerType shipType)
    {
        // Get references to the root GameObjects of each movement type
        Transform fpsCrewMovement = transform.Find("FPSCrewMovement");
        Transform fighterMovement = transform.Find("FighterMovement");
        Transform bigShipMovement = transform.Find("BigShipMovement");

        // Disable all movement type GameObjects
        if (fpsCrewMovement != null) fpsCrewMovement.gameObject.SetActive(false);
        if (fighterMovement != null) fighterMovement.gameObject.SetActive(false);
        if (bigShipMovement != null) bigShipMovement.gameObject.SetActive(false);

        // Enable the relevant movement type GameObject
        switch (shipType)
        {
            case ControllerType.Fighter:
                if (fighterMovement != null) fighterMovement.gameObject.SetActive(true);
                break;
            case ControllerType.FPSCrew:
                if (fpsCrewMovement != null) fpsCrewMovement.gameObject.SetActive(true);
                break;
            case ControllerType.BigShip:
                if (bigShipMovement != null) bigShipMovement.gameObject.SetActive(true);
                break;
        }

        fpsVirtualCamera.gameObject.SetActive(shipType == ControllerType.FPSCrew);
        fighterVirtualCamera.gameObject.SetActive(shipType == ControllerType.Fighter);
        bigShipVirtualCamera.gameObject.SetActive(shipType == ControllerType.BigShip);

        // Optionally lock cursor only when camera is enabled
        if (shipType == ControllerType.FPSCrew || shipType == ControllerType.Fighter)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}