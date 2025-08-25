using System;
using System.Collections.Generic;

[Serializable]
public class BlueprintDTO
{
    public string blueprintName;
    public string buildableEntityId; // Use identifier instead of ScriptableObject reference
    public List<ResourceDTO> resourceRequirements = new List<ResourceDTO>();
    public float buildTimeSeconds;
}