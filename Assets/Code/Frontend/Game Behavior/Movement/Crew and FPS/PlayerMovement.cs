using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.LowLevel;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2.5f;
    public float gravityValue = -9.81f;
    public float jumpBufferTime = 0.2f;

    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private float groundedGracePeriod = 0.2f;
    private float lastGroundedTime = 0f;

    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    private float serverYaw;
    private PlayerLook playerLook;

    private struct InputState
    {
        public float time;
        public Vector2 move;
        public bool jump;
        public bool wasGroundedAtInput;
        public float yaw;
        // For true FPS logic, add rotation here too.
    }

    private List<InputState> inputBuffer = new List<InputState>();

    // Server-side variables
    private Vector2 serverMovementInput;
    private bool serverJumpInput;
    private bool serverGroundedState;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerLook = GetComponent<PlayerLook>();
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            ServerMove();
        }

        if (IsOwner && Application.isFocused)
        {
            // Capture inputs
            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool jumpInput = jumpAction.action.triggered;
            bool isGrounded = controller.isGrounded;
            float currentYaw = playerLook != null ? playerLook.transform.rotation.eulerAngles.y : transform.rotation.eulerAngles.y;

            // Buffer input and send with wasGroundedAtInput
            InputState state = new InputState
            {
                time = Time.time,
                move = moveInput,
                jump = jumpInput,
                wasGroundedAtInput = controller.isGrounded,
                yaw = currentYaw
            };

            inputBuffer.Add(state);

            SendInputServerRpc(moveInput, jumpInput, controller.isGrounded, inputBuffer.Count - 1, currentYaw);

            // Local prediction
            PredictMove(state);

            // Send input to the server (with buffer index or timestamp for reconciliation)
            SendInputServerRpc(moveInput, jumpInput, isGrounded, inputBuffer.Count - 1, currentYaw);
        }
    }

    void PredictMove(InputState state)
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer)
            lastGroundedTime = Time.time;

        bool canJump = (Time.time - lastGroundedTime) <= groundedGracePeriod;
        if (canJump && state.jump)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
        }

        Vector3 move = new Vector3(state.move.x, 0, state.move.y);
        move = Vector3.ClampMagnitude(move, 1f);
        Quaternion rotation = Quaternion.Euler(0f, state.yaw, 0f);
        move = rotation * move;
        controller.Move(move * moveSpeed * Time.deltaTime);

        playerVelocity.y += gravityValue * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime);
    }

    [ServerRpc]
    void SendInputServerRpc(Vector2 input, bool jump, bool wasGroundedAtInput, int inputIndex, float yaw)
    {
        serverMovementInput = input;
        serverJumpInput = jump;
        serverGroundedState = wasGroundedAtInput;
        serverYaw = yaw;

        ServerMove();

        UpdatePositionClientRpc(transform.position, transform.rotation, inputIndex);
    }

    void ServerMove()
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer)
            lastGroundedTime = Time.time;

        bool canJump = serverGroundedState;

        if (canJump && serverJumpInput)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
            serverJumpInput = false;
        }

        // Apply server yaw rotation
        transform.rotation = Quaternion.Euler(0f, serverYaw, 0f);

        // Process movement and jumping as before using serverYaw to rotate input move vector
        Vector3 move = new Vector3(serverMovementInput.x, 0, serverMovementInput.y);
        move = Vector3.ClampMagnitude(move, 1f);
        move = transform.rotation * move;
        controller.Move(move * moveSpeed * Time.fixedDeltaTime);
        playerVelocity.y += gravityValue * Time.fixedDeltaTime;
        controller.Move(playerVelocity * Time.fixedDeltaTime);
    }

    [ClientRpc]
    void UpdatePositionClientRpc(Vector3 position, Quaternion rotation, int serverProcessedInputIndex)
    {
        if (!IsOwner)
        {
            // Non-owning clients get authoritative rotation & position applied directly
            controller.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            controller.enabled = true;
        }
        else
        {
            // Owner reconciles predicted position & rotation
            controller.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            controller.enabled = true;

            // Reapply unprocessed inputs for position and rotation prediction
            for (int i = serverProcessedInputIndex + 1; i < inputBuffer.Count; i++)
            {
                PredictMove(inputBuffer[i]);
            }

            // Remove acknowledged inputs
            inputBuffer.RemoveRange(0, Mathf.Min(serverProcessedInputIndex + 1, inputBuffer.Count));
        }
    }
}
