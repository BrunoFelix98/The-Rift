using System;
using System.Collections.Generic;

[Serializable]
public class ResourceDTO
{
    public string resourceName;
    public string resourceDescription;

    // For variable quantity range as per JSON
    public int minQuantity;
    public int maxQuantity;

    public float resourceWeight;
    public ResourceCategory resourceCategory;

    // List of factions or concepts allowed to have this resource, empty = available globally
    public List<string> allowedFactions = new List<string>();
}
