using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewResource", menuName = "Game Data/Resource")]
public class ResourceSO : ScriptableObject
{
    public int resourceId;
    public string resourceName;

    [TextArea]
    public string resourceDescription;

    // Range for spawn quantity
    public int minQuantity;
    public int maxQuantity;
    
    // Weight in metric tonnes
    public float resourceWeight;

    public ResourceRarity rarity;

    // List of allowed celestial types (Planet, Moon, etc)
    public List<CelestialType> appliesTo = new List<CelestialType>();
    
    // Environments where it can appear
    public List<CelestialEnvironment> environments = new List<CelestialEnvironment>();
    
    // List of allowed factions (names or "Universal")
    public List<string> allowedFactions = new List<string>();

    [Header("Runtime Info")]
    public int quantity; // Can be used for instances
}
