using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ResourceDTO
{
    public string resourceName;

    [TextArea]
    public string resourceDescription;

    // For variable quantity range as per JSON
    public int minQuantity;
    public int maxQuantity;
    public int quantity;

    public float resourceWeight;
    public List<ResourceCategory> resourceCategory;

    // List of factions or concepts allowed to have this resource, empty = available globally
    public List<string> celestialType = new List<string>();
    public List<string> allowedFactions = new List<string>();
}
