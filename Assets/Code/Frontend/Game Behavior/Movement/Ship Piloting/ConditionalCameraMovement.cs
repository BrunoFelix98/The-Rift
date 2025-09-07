using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConditionalCameraMovement : MonoBehaviour
{
    public InputActionReference rightClickAction;

    private CinemachineInputAxisController inputController;

    void Start()
    {
        inputController = GetComponent<CinemachineInputAxisController>();
    }

    void Update()
    {
        bool rightClickHeld = rightClickAction.action.ReadValue<float>() > 0.5f;
        inputController.enabled = rightClickHeld;
    }
}
