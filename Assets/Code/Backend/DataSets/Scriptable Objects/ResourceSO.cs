using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewResource", menuName = "Game Data/Resource")]
public class ResourceSO : ScriptableObject
{
    public string resourceName;

    [TextArea]
    public string resourceDescription;

    // Range for spawn quantity
    public int minQuantity;
    public int maxQuantity;

    public float resourceWeight;

    public ResourceCategory resourceCategory;

    // List of allowed factions by their unique IDs or names
    public List<string> celestialType = new List<string>();
    public List<string> allowedFactions = new List<string>();
}
