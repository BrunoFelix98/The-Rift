using UnityEngine;

public class ShipCameraTarget : MonoBehaviour
{
    public Transform shipTransform;

    void LateUpdate()
    {
        // Match position, but maintain identity rotation
        transform.position = shipTransform.position;
        transform.rotation = Quaternion.identity;
    }
}
