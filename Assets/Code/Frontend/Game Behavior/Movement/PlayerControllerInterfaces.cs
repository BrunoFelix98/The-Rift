using UnityEngine;

/// <summary>
/// Player controllers that keep a predicted/authoritative state must be moved through these,
/// otherwise the next simulation tick puts the player straight back.
/// </summary>
public interface ITeleportable
{
    // World-space positions (see FloatingOrigin)
    void ServerTeleport(Vector3 position, Quaternion rotation);
    void OwnerTeleport(Vector3 position, Quaternion rotation);

    /// <summary>Simulated world position, independent of this instance's floating origin.</summary>
    Vector3d WorldPosition { get; }
}

/// <summary>Data the ship HUD reads from the local player's ship.</summary>
public interface IShipHudSource
{
    Transform HudTransform { get; }
    Vector3 HudVelocity { get; }
}
