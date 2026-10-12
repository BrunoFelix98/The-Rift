using UnityEngine;

public class CelestialData : MonoBehaviour
{
    public CelestialPersistenceData data;

    public void Initialize(CelestialPersistenceData persistenceData)
    {
        data = persistenceData;
    }
}
