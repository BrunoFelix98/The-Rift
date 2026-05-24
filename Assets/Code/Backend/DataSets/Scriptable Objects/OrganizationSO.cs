using System;
using System.Collections.Generic;
using UnityEngine;

public enum OrganizationType
{
    Faction,
    Alliance,
    Corporation
}

[CreateAssetMenu(fileName = "NewOrganization", menuName = "Game Data/Organization")]
public class OrganizationSO : ScriptableObject
{
    [Header("Identity")]
    public string organizationName;
    public OrganizationType type;
    [TextArea]
    public string description;

    [Header("Hierarchy")]
    public OrganizationSO allegiance; // Parent organization
    public LivingSO leader;
    public List<OrganizationSO> memberOrganizations = new List<OrganizationSO>();
    public List<LivingSO> members = new List<LivingSO>();

    [Header("Generation Settings")]
    public int organizationId; // Unique ID
    public string homeGalaxy;
    public List<string> starNames = new List<string>();
    public bool requiresNebulas;
    public Color organizationColor = Color.white;
    public int seed;
    public List<GatePair> gates = new List<GatePair>();
}
