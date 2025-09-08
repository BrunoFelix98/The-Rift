using UnityEngine;

public class OrbitComponent : MonoBehaviour
{
    public OrbitParams orbitParams;

    void Update()
    {
        // Example: simple circular motion
        float angle = Time.time * orbitParams.AngularSpeed + orbitParams.PhaseOffset;
        var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * orbitParams.Radius;
        transform.position = orbitParams.CenterPos + offset;
    }
}