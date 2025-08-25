using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class FighterAIController : MonoBehaviour
{
    public Rigidbody rb;
    public float currentSpeed;

    [Header("Movement Settings")]
    public float moveSpeed = 20f;
    public float strafeSpeed = 15f;
    public float turnSpeed = 100f; // Should exceed maxForwardSpeed for proper turning
    public float acceleration = 30f;
    public float deceleration = 40f;
    public float maxForwardSpeed = 80f;
    public float maxBackwardSpeed = -30f;

    [Header("AI Behavior")]
    public Transform target;
    public Vector3 patrolPoint;
    public float pursueDistance = 150f;
    public float arrivalThreshold = 5f;

    [Header("Orbit Settings")]
    public float desiredOrbitRadius = 40f;
    public float orbitStrafeSpeed = 0.6f;
    public float orbitApproachBuffer = 5f;
    public float tooCloseDistance = 15f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentSpeed = moveSpeed;
        AssignRandomPatrolPoint();
    }

    private void FixedUpdate()
    {
        // Ensure there's a valid target; find nearest if null
        if (target == null)
            FindNearestTarget();

        if (target != null && Vector3.Distance(transform.position, target.position) <= pursueDistance)
            PursueTargetWithOrbit();
        else
            PatrolTowardsPoint();

        // Apply smooth banking roll based on yaw input
        float targetRoll = -desiredYaw * 0.5f;
        float currentRoll = transform.localEulerAngles.z;
        if (currentRoll > 180) currentRoll -= 360;
        float rollDelta = Mathf.MoveTowardsAngle(currentRoll, targetRoll, turnSpeed * Time.fixedDeltaTime);
        Vector3 euler = transform.localEulerAngles;
        euler.z = rollDelta;
        transform.localEulerAngles = euler;
    }

    // AI control inputs
    private float desiredYaw;
    private float desiredPitch;
    private float desiredRoll;
    private float desiredStrafeX;
    private float desiredStrafeY;
    private float desiredAcceleration;

    void PursueTargetWithOrbit()
    {
        Vector3 toTarget = target.position - transform.position;
        float distance = toTarget.magnitude;
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, transform.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);

        float angleToTarget = Vector3.Angle(transform.forward, toTarget.normalized);

        if (distance < tooCloseDistance)
        {
            // Too close! Brake, or possibly back away slightly
            desiredAcceleration = -1f; // Brake hard or try moving backward if you support it
            desiredYaw = 0f;
            desiredPitch = 0f;
            desiredRoll = 0f;
            desiredStrafeX = orbitStrafeSpeed * 1.2f; // Optional: strafe more for evasion
            desiredStrafeY = 0f;
            ApplyMovement();
            return; // Prevent normal orbit logic when too close
        }

        if (angleToTarget < 5f)
        {
            float distanceOffset = distance - desiredOrbitRadius;
            if (distanceOffset > orbitApproachBuffer)
                desiredAcceleration = 1f;
            else if (distanceOffset < -orbitApproachBuffer)
                desiredAcceleration = -0.5f;
            else
                desiredAcceleration = 0f;
            desiredYaw = 0f;
            desiredPitch = 0f;
            desiredRoll = 0f;
            desiredStrafeX = orbitStrafeSpeed;
            desiredStrafeY = 0f;
            ApplyMovement();
        }
        else
        {
            desiredYaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up) * 2f, -turnSpeed, turnSpeed);
            desiredPitch = 0f;
            desiredRoll = 0f;
            desiredStrafeX = 0f;
            desiredStrafeY = 0f;
            desiredAcceleration = 1f;
            ApplyMovement();
        }
    }

    void PatrolTowardsPoint()
    {
        Vector3 toPatrol = patrolPoint - transform.position;
        float distance = toPatrol.magnitude;
        Vector3 dir = toPatrol.normalized;

        Quaternion targetRot = Quaternion.LookRotation(dir, transform.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);

        float angleToPatrol = Vector3.Angle(transform.forward, dir);

        if (distance > arrivalThreshold)
        {
            desiredYaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, dir, Vector3.up) * 1.5f, -turnSpeed, turnSpeed);
            desiredPitch = 0f;
            desiredRoll = 0f;
            desiredStrafeX = 0f;
            desiredStrafeY = 0f;
            desiredAcceleration = 1f;

            ApplyMovement();
        }
        else
        {
            AssignRandomPatrolPoint();

            desiredYaw = 0f;
            desiredPitch = 0f;
            desiredRoll = 0f;
            desiredStrafeX = 0f;
            desiredStrafeY = 0f;
            desiredAcceleration = 0f;

            ApplyMovement();
        }
    }

    void ApplyMovement()
    {
        // Smooth speed control
        if (desiredAcceleration > 0)
            currentSpeed = Mathf.Min(currentSpeed + acceleration * Time.fixedDeltaTime, maxForwardSpeed);
        else if (desiredAcceleration < 0)
            currentSpeed = Mathf.Max(currentSpeed - deceleration * Time.fixedDeltaTime, maxBackwardSpeed);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, deceleration * Time.fixedDeltaTime);

        // Apply movement velocity + strafing
        Vector3 moveDirection =
            transform.forward * currentSpeed +
            transform.right * desiredStrafeX * strafeSpeed +
            transform.up * desiredStrafeY * strafeSpeed;

        rb.linearVelocity = moveDirection;
        rb.angularVelocity = Vector3.zero;
    }

    void AssignRandomPatrolPoint()
    {
        GameObject[] patrolPoints = GameObject.FindGameObjectsWithTag("PatrolPoint");
        if (patrolPoints.Length > 0)
        {
            int i = Random.Range(0, patrolPoints.Length);
            patrolPoint = patrolPoints[i].transform.position;
        }
        else
        {
            patrolPoint = new Vector3(
                Random.Range(-500, 500),
                Random.Range(-500, 500),
                Random.Range(-500, 500));
        }
    }

    void FindNearestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform bestTarget = null;
        float closestDistanceSqr = Mathf.Infinity;
        Vector3 currentPosition = transform.position;
        foreach (GameObject enemy in enemies)
        {
            float distanceSqr = (enemy.transform.position - currentPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                bestTarget = enemy.transform;
            }
        }
        target = bestTarget;
    }

}
