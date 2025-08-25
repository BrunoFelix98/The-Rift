using System.Collections.Generic;
using UnityEngine;

public class SectorManager : MonoBehaviour
{
    public SpaceObjectManager spaceObjectManager;

    private List<SystemEntity> loadedSectors = new List<SystemEntity>();
    //private List<ShipSystemsData> allShips; // Your global list of all ships and their sectors

    public Transform playerTransform;

    void Update()
    {
        UpdateLoadedSectors();

        foreach (var sector in loadedSectors)
        {
            foreach (var ship in sector.ShipsInSystem)
            {
                //spaceObjectManager.UpdateShipLoadedState(ship.id, true, ship.shipSO, ship.position, ship.rotation);
            }
        }

        // Determine ships that left sectors and unload them similarly
        // ...
    }

    void UpdateLoadedSectors()
    {
        // Implement checking player's position relative to sectors, load/unload sectors accordingly

        // When player enters a new sector:
        /*foreach (var ship in shipsInThisSector)
        {
            spaceObjectManager.UpdateShipLoadedState(ship.id, true, ship.shipSO, ship.position, ship.rotation);
        }

        // When player leaves a sector:
        foreach (var ship in shipsJustLeftBehind)
        {
            spaceObjectManager.UpdateShipLoadedState(ship.id, false, null, Vector3.zero, Quaternion.identity);
        }*/
    }
}
