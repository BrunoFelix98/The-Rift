using UnityEngine;
using System.Collections.Generic;

public class SpaceObjectManager : MonoBehaviour
{
    public ShipPooling shipPool; // Assign the pooling singleton or instance
    // You may later generalize this for asteroids, stations, etc.

    // Tracking loaded objects (for demo, use ships only; expand as needed)
    private Dictionary<int, ShipStatsAndBehaviors> loadedShips = new Dictionary<int, ShipStatsAndBehaviors>();

    /// <summary>
    /// Loads a ship instance and shows it in the scene.
    /// </summary>
    public void LoadShip(int shipId, ShipSO shipData, Vector3 position, Quaternion rotation)
    {
        if (!loadedShips.ContainsKey(shipId))
        {
            ShipStatsAndBehaviors ship = shipPool.GetShip(shipData, position, rotation);
            loadedShips[shipId] = ship;
        }
    }

    /// <summary>
    /// Unloads a ship and returns it to the pool.
    /// </summary>
    public void UnloadShip(int shipId)
    {
        if (loadedShips.TryGetValue(shipId, out ShipStatsAndBehaviors ship))
        {
            shipPool.ReturnToPool(ship);
            loadedShips.Remove(shipId);
        }
    }

    /// <summary>
    /// Example: Call this repeatedly as ships enter/exit area (replace with sector streaming triggers).
    /// </summary>
    public void UpdateShipLoadedState(int shipId, bool isLoaded, ShipSO shipData, Vector3 position, Quaternion rotation)
    {
        if (isLoaded)
            LoadShip(shipId, shipData, position, rotation);
        else
            UnloadShip(shipId);
    }
}
