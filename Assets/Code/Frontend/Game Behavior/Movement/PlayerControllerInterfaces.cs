using UnityEngine;

/// <summary>
/// Player controllers that keep a predicted/authoritative state must be moved through these,
/// otherwise the next simulation tick puts the player straight back.
/// </summary>
public interface ITeleportable
{
    void ServerTeleport(Vector3 position, Quaternion rotation);
    void OwnerTeleport(Vector3 position, Quaternion rotation);
}

/// <summary>Data the ship HUD reads from the local player's ship.</summary>
public interface IShipHudSource
{
    Transform HudTransform { get; }
    Vector3 HudVelocity { get; }
}
