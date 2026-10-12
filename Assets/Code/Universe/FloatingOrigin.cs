using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-side floating origin. Transforms are in "render space" = world - Offset, kept near the
/// local player so float precision stays high where the camera is. Authoritative/simulated
/// positions (movement states, celestial orbits, server logic) stay in world space; convert with
/// ToRender/ToWorld. A dedicated server never shifts (Offset stays zero).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class FloatingOrigin : MonoBehaviour
{
    private static FloatingOrigin instance;

    public static Vector3d Offset { get; private set; } = Vector3d.zero;
    public static Vector3 OffsetF => Offset.ToVector3();

    /// <summary>Raised after a shift with the render-space delta that was removed.</summary>
    public static event Action<Vector3> Shifted;

    [Tooltip("Recentre once the local player is this far from the render origin.")]
    public float threshold = 2000f;

    public static void EnsureExists()
    {
        if (instance != null) return;
        instance = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
        DontDestroyOnLoad(instance.gameObject);
    }

    public static Vector3 ToRender(Vector3d world) => (world - Offset).ToVector3();
    public static Vector3 ToRender(Vector3 world) => (new Vector3d(world) - Offset).ToVector3();
    public static Vector3d ToWorld(Vector3 render) => new Vector3d(render) + Offset;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Early in the frame, before movement smoothing and the celestial pass place things for this frame
    private void Update()
    {
        var netManager = NetworkManager.Singleton;
        if (netManager == null || !netManager.IsClient)
        {
            if (!(netManager != null && netManager.IsListening)) ResetOrigin();
            return;
        }

        NetworkObject player = netManager.LocalClient?.PlayerObject;
        if (player == null) return;

        Vector3 renderPos = player.transform.position;
        if (renderPos.sqrMagnitude < threshold * threshold) return;

        Offset = Offset + new Vector3d(renderPos);
        Shifted?.Invoke(renderPos);
    }

    private static void ResetOrigin()
    {
        if (Offset.x == 0.0 && Offset.y == 0.0 && Offset.z == 0.0) return;
        Vector3 delta = -OffsetF;
        Offset = Vector3d.zero;
        Shifted?.Invoke(delta);
    }
}
