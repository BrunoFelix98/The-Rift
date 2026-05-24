using UnityEngine;

[CreateAssetMenu(fileName = "RevisedGateData", menuName = "Universe/Revised Gate Data", order = 5)]
public class RevisedGateData : ScriptableObject
{
    [Header("Gate Identity")]
    public string gateName;
    public string gateID; // unique ID for referencing, optional if you want to use GUIDs

    [Header("Connection")]
    public RevisedSystemData originSystem;
    public RevisedSystemData targetSystem;
    public bool isBidirectional = true;

    [Header("Ownership & Control")]
    public RevisedFactionData controllingFaction; // can be null for neutral gates
    public bool isPlayerBuilt = false;

    [Header("Travel & Access Rules")]
    public bool restrictedAccess; // locked gates or faction-only

    [Header("Gate Stats (optional for gameplay depth)")]
    public RevisedResistanceProfile resistances; // reuse from StationData for destructible gates
}