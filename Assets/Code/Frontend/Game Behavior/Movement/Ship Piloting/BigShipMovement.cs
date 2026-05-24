using Unity.Netcode;
using UnityEngine;

public class BigShipMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public Camera mainCamera;
    public float moveSpeed = 40f;
    public float rotationSpeed = 120f;
    public Transform shipModel;
    public float stopDistance = 1f;
    public float doubleClickThreshold = 0.3f;
    private float lastClickTime = -10f;
    private float targetDistance = 150f;

    // Server-authoritative target position and rotation, synced to all clients
    private NetworkVariable<Vector3> targetPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> hasTargetPosition = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Quaternion> shipRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Vector3 clientTargetPosition;
    private bool clientHasTargetPosition;

    [Header("Sync Settings")]
    public float positionLerpSpeed = 10f;
    public float rotationLerpSpeed = 10f;
    private Vector3 targetNetPosition;

    private void Update()
    {
        if (IsOwner && Application.isFocused)
        {
            HandleInput();
            ClientMove();
        }
        else if (!IsOwner)
        {
            // Smoothly interpolate position and rotation for other clients
            if (targetNetPosition != Vector3.zero)
            {
                transform.position = Vector3.Lerp(transform.position, targetNetPosition, positionLerpSpeed * Time.deltaTime);
            }
            
            if (shipModel != null)
            {
                shipModel.rotation = Quaternion.Slerp(shipModel.rotation, shipRotation.Value, rotationLerpSpeed * Time.deltaTime);
            }
        }
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time - lastClickTime <= doubleClickThreshold)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                Vector3 destination = ray.origin + ray.direction * targetDistance;

                // Send target position to server for authoritative movement
                SetTargetPositionServerRpc(destination);

                // Update client-side prediction
                clientTargetPosition = destination;
                clientHasTargetPosition = true;
            }
            lastClickTime = Time.time;
        }
    }

    private void ClientMove()
    {
        if (!clientHasTargetPosition)
            return;

        Vector3 direction = clientTargetPosition - transform.position;
        if (direction.magnitude <= stopDistance)
        {
            clientHasTargetPosition = false; // Clear the target
        }
        else
        {
            float angleToTarget = 0f;
            if (shipModel != null)
            {
                Vector3 currentForward = shipModel.forward;
                Vector3 targetDir = direction.normalized;
                angleToTarget = Vector3.Angle(currentForward, targetDir);
            }

            float speedFactor = Mathf.Lerp(1f, 0.3f, angleToTarget / 180f);
            transform.position += direction.normalized * moveSpeed * speedFactor * Time.deltaTime;

            // Handle rotation for owning client
            if (shipModel != null && direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                shipModel.rotation = Quaternion.RotateTowards(shipModel.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    [ServerRpc]
    private void SetTargetPositionServerRpc(Vector3 target)
    {
        targetPosition.Value = target;
        hasTargetPosition.Value = true;
    }

    private void FixedUpdate()
    {
        if (IsServer)
        {
            ServerMove();
        }
    }

    private void ServerMove()
    {
        if (!hasTargetPosition.Value)
            return;

        Vector3 direction = targetPosition.Value - transform.position;
        if (direction.magnitude <= stopDistance)
        {
            hasTargetPosition.Value = false; // Clear the target
        }
        else
        {
            float angleToTarget = 0f;
            if (shipModel != null)
            {
                Vector3 currentForward = shipModel.forward;
                Vector3 targetDir = direction.normalized;
                angleToTarget = Vector3.Angle(currentForward, targetDir);
            }

            float speedFactor = Mathf.Lerp(1f, 0.3f, angleToTarget / 180f);
            transform.position += direction.normalized * moveSpeed * speedFactor * Time.fixedDeltaTime;

            // Handle rotation on server and sync to all clients
            if (shipModel != null && direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                shipModel.rotation = Quaternion.RotateTowards(shipModel.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

                // Update the networked rotation variable
                shipRotation.Value = shipModel.rotation;
            }
        }

        // Synchronize position with all clients (rotation is handled by NetworkVariable)
        UpdatePositionClientRpc(transform.position);
    }

    [ClientRpc]
    private void UpdatePositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
        {
            // Update target position for non-owning clients for interpolation in Update()
            targetNetPosition = position;
        }
    }
}