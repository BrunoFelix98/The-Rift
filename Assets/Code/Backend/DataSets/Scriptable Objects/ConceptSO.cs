using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewConcept", menuName = "Game Data/Concept")]
public class ConceptSO : ScriptableObject
{
    public string conceptName;
    [TextArea]
    public string description;

    // Allegiance to another Concept (e.g., a corporation's allegiance to an alliance)
    public ConceptSO allegiance;

    // Leader of this concept (link to a LivingSO)
    public LivingSO leader;

    // Optional list of members or member corporations if this concept is a corporation/alliance
    public List<ConceptSO> memberCorporations = new List<ConceptSO>(); //Used only as Alliance
    public List<LivingSO> members = new List<LivingSO>(); //Used only as corporation
}