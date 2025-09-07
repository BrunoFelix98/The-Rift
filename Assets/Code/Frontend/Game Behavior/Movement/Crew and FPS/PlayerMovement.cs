using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2.5f;
    public float gravityValue = -9.81f;

    [Header("Jump Buffer Settings")]
    public float jumpBufferTime = 0.15f;
    public float groundedGracePeriod = 0.2f;

    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private float lastGroundedTime = 0f;

    public InputActionReference moveAction;
    public InputActionReference jumpAction;

    private float serverYaw;
    private PlayerLook playerLook;

    // Mixed approach: Event-based + buffering
    private bool jumpRequested = false;
    private float jumpRequestTime = 0f;
    private bool jumpConsumed = false;

    private struct InputState
    {
        public float time;
        public Vector2 move;
        public bool jump;
        public bool wasGroundedAtInput;
        public float yaw;
    }

    private List<InputState> inputBuffer = new List<InputState>();

    // Server-side variables
    private Vector2 serverMovementInput;
    private bool serverJumpInput;
    private bool serverGroundedState;
    private float serverLastJumpTime = -1f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerLook = GetComponent<PlayerLook>();
    }

    void Update() // Handle input in Update for responsiveness
    {
        if (IsOwner && Application.isFocused)
        {
            // Event-based jump detection - much more reliable than triggered
            if (jumpAction.action.WasPressedThisFrame())
            {
                jumpRequested = true;
                jumpRequestTime = Time.time;
                jumpConsumed = false;
            }

            // Clear expired jump requests
            if (jumpRequested && (Time.time - jumpRequestTime) > jumpBufferTime)
            {
                jumpRequested = false;
                jumpConsumed = true;
            }
        }
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
            bool isGrounded = controller.isGrounded;
            float currentYaw = playerLook != null ? playerLook.transform.rotation.eulerAngles.y : transform.rotation.eulerAngles.y;

            // Only send jump if it's a fresh, unconsumed request
            bool sendJump = jumpRequested && !jumpConsumed;

            // Buffer input
            InputState state = new InputState
            {
                time = Time.time,
                move = moveInput,
                jump = sendJump,
                wasGroundedAtInput = isGrounded,
                yaw = currentYaw
            };
            inputBuffer.Add(state);

            // Local prediction
            PredictMove(state);

            // Send input to server (SINGLE CALL - fixes your duplicate RPC issue)
            SendInputServerRpc(moveInput, sendJump, isGrounded, inputBuffer.Count - 1, currentYaw);

            // Mark jump as consumed after sending
            if (sendJump)
            {
                jumpConsumed = true;
            }
        }
    }

    void PredictMove(InputState state)
    {
        groundedPlayer = controller.isGrounded;
        if (groundedPlayer)
            lastGroundedTime = Time.time;

        // More forgiving jump conditions
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

        // Improved server-side jump logic with double protection
        bool canJump = serverGroundedState || (Time.time - lastGroundedTime) <= groundedGracePeriod;

        // Prevent rapid-fire jumps with server-side cooldown
        bool jumpCooldownExpired = (Time.time - serverLastJumpTime) > 0.1f;

        if (canJump && serverJumpInput && jumpCooldownExpired)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityValue);
            serverLastJumpTime = Time.time;
            serverJumpInput = false; // Reset jump input immediately
        }

        // Apply server yaw rotation
        transform.rotation = Quaternion.Euler(0f, serverYaw, 0f);

        // Process movement
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
            // Non-owning clients get authoritative position applied directly
            controller.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            controller.enabled = true;
        }
        else
        {
            // Owner reconciles predicted position
            controller.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            controller.enabled = true;

            // Reapply unprocessed inputs for smooth prediction
            for (int i = serverProcessedInputIndex + 1; i < inputBuffer.Count; i++)
            {
                PredictMove(inputBuffer[i]);
            }

            // Clean up acknowledged inputs
            inputBuffer.RemoveRange(0, Mathf.Min(serverProcessedInputIndex + 1, inputBuffer.Count));
        }
    }
}