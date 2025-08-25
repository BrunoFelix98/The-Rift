using UnityEngine;

public enum EntityType
{
    // Biological / Characters
    LIVING, // NPCs, Players

    // Space Objects
    CELESTIAL, // Planets, Moons, Stars, Systems, Galaxies
    MOVING, // Ships (player or NPC controlled)

    // Man-Made Objects
    ARTIFICIAL, // Stations, Gates, Structures

    // Gear / Items
    EQUIPMENT, // Weapons, Armor, Modules, Consumables

    // Abstract / Non-Physical
    FACTION, // Factions, NPC corporations, Alliances
    CONCEPT // Currency, Quests, Other abstract entities
}
