using System.Collections.Generic;
using UnityEngine;

public class ShipPooling : MonoBehaviour
{
    public static ShipPooling Instance { get; private set; }

    [Tooltip("Prefab of the ship with ShipStatsAndBehaviors component")]
    public ShipStatsAndBehaviors shipPrefab;

    [Tooltip("Initial number of ships to pre-instantiate in the pool")]
    public int poolSize = 10;

    private Queue<ShipStatsAndBehaviors> pool = new Queue<ShipStatsAndBehaviors>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            ShipStatsAndBehaviors ship = Instantiate(shipPrefab, Vector3.zero, Quaternion.identity, transform);
            ship.gameObject.SetActive(false);
            pool.Enqueue(ship);
        }
    }

    /// <summary>
    /// Get a ship from the pool with given ShipSO data, position and rotation
    /// </summary>
    public ShipStatsAndBehaviors GetShip(ShipSO shipData, Vector3 position, Quaternion rotation)
    {
        ShipStatsAndBehaviors ship;
        if (pool.Count > 0)
        {
            ship = pool.Dequeue();
            ship.gameObject.SetActive(true);
        }
        else
        {
            // Pool exhausted, instantiate a new one
            ship = Instantiate(shipPrefab, Vector3.zero, Quaternion.identity, transform);
        }

        ship.transform.position = position;
        ship.transform.rotation = rotation;
        ship.shipData = shipData;     // Set the SO reference
        ship.InitializeShip();        // Copies SO data to runtime data & resets state
        return ship;
    }

    /// <summary>
    /// Return a ship to the pool for reuse
    /// </summary>
    public void ReturnToPool(ShipStatsAndBehaviors ship)
    {
        ship.gameObject.SetActive(false);
        pool.Enqueue(ship);
    }
}
