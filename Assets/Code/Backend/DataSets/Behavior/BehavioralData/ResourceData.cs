using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ResourceData
{
    public string resourceName;
    public string resourceDescription;
    public int minQuantity;
    public int maxQuantity;
    public float resourceWeight;
    public List<string> category;
    public List<string> celestialType;
    public List<string> allowedFactions;
}