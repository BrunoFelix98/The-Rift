using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RevisedResourceData", menuName = "Universe/Revised Resource Data", order = 3)]
public class RevisedResourceData : ScriptableObject
{
    [Header("Basic Info")]
    public string resourceName;
    [TextArea] public string resourceDescription;
    public ResourceRarity rarity = ResourceRarity.COMMON;

    [Header("Quantities")]
    public int minQuantity;
    public int maxQuantity;
    public float resourceWeight;

    [Header("Applicability")]
    public List<CelestialType> appliesTo = new();
    public List<CelestialEnvironment> environment = new();
    public List<string> allowedFactions = new();
}