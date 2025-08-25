using System;
using System.Collections.Generic;

[Serializable]
public class ConceptDTO
{
    public string conceptName;
    public string description;

    public string allegianceId; // ID referencing another ConceptDTO
    public string leaderId;     // ID referencing LivingDTO (or similar)

    public List<string> memberCorporationIds = new List<string>(); // IDs of member concepts
    public List<string> memberIds = new List<string>();            // IDs of member livings
}