using UnityEngine;

public class OrbitingObject
{
    public GameObject obj;          // Object that orbits
    public GameObject parentObj;    // Parent object to orbit around (optional)
    public Vector3? fixedCenter;    // Fixed point center to orbit around if parentObj is null
    public float radius;            // Orbit radius
    public float orbitSpeed;        // Orbit speed in radians per second
    public float currentAngle;      // Current angle in radians

    // Multiplier for slowing orbit speed (e.g., 0.2 means orbiting 5 times slower)
    private const float speedMultiplier = 0.2f;

    public OrbitingObject(GameObject obj, GameObject parentObj, float radius, float speed)
    {
        this.obj = obj;
        this.parentObj = parentObj;
        this.radius = radius;
        this.orbitSpeed = speed;
        this.currentAngle = Random.Range(0f, Mathf.PI * 2f);
        this.fixedCenter = null;
    }

    public OrbitingObject(GameObject obj, Vector3 fixedCenter, float radius, float speed)
    {
        this.obj = obj;
        this.fixedCenter = fixedCenter;
        this.radius = radius;
        this.orbitSpeed = speed;
        this.currentAngle = Random.Range(0f, Mathf.PI * 2f);
        this.parentObj = null;
    }

    public void UpdatePosition(float deltaTime)
    {
        // Increment current angle based on speed and multiplier
        currentAngle += orbitSpeed * speedMultiplier * deltaTime;

        // Determine center point, either parent object position or fixed center
        Vector3 center = parentObj != null ? parentObj.transform.position : fixedCenter ?? Vector3.zero;

        // Calculate new position on orbit circle around center, keeping original Y position
        float x = center.x + Mathf.Cos(currentAngle) * radius;
        float z = center.z + Mathf.Sin(currentAngle) * radius;
        obj.transform.position = new Vector3(x, obj.transform.position.y, z);
    }
}
