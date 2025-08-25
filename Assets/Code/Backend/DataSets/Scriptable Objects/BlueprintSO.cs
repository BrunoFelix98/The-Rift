using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBlueprint", menuName = "Game Data/Blueprint")]
public class BlueprintSO : ScriptableObject
{
    public string blueprintName;

    // Reference to what this blueprint builds, use Object as generic holder
    public ScriptableObject buildableEntity;

    // Resources needed to construct this entity
    public List<ResourceSO> resourceRequirements = new List<ResourceSO>();

    public float buildTimeSeconds; // Optional build time
}
