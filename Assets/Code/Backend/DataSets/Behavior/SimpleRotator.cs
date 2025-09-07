using UnityEngine;

public class SimpleRotator : MonoBehaviour
{
    public Vector3 rotationSpeed = Vector3.zero;

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}