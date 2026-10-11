/**
 * Shared type definitions for The Rift project.
 */

/** Core application state representing the current game condition */
export interface GameState {
  /** Current health points (0-100) */
  health: number;

  /** Energy resource level (0-100) */
  energy: number;

  /** Sanity/madness indicator (-100 to 100, negative is worse) */
  sanity: number;

  /** Current location string identifier */
  location: Location;

  /** Time elapsed in hours since game start */
  time: number;
}

/** Available locations in the Rift */
export type Location =
  | 'entrance'     // Main entrance area
  | 'dormitory'    // Player sleeping quarters
  | 'cafeteria'    // Food and rest stop
  | 'library'      // Information source
  | 'armory'       // Weapon and equipment access
  | 'infirmary'    // Healing services
  | 'ritual_circle' // Ancient ritual site
  | 'bunker'       // Secret military facility
  | 'ruins'        // Abandoned settlement
  | 'forest'       // Overgrown wilderness
  | 'underground'  // Subterranean passages
  | 'surface'      // Exterior world areas
  | null;          // No location currently active

/** Available item categories */
export type ItemCategory =
  | 'weapon'       // Combat equipment
  | 'consumable'   // Food, medicine, etc.
  | 'tool'         // Utility items
  | 'armor'        // Protective gear
  | 'artifact'     // Ancient/magical objects
  | 'document'     // Informational texts
  | 'currency';    // Game money

/** Base item interface */
export interface Item {
  /** Unique item identifier */
  id: string;

  /** Display name */
  name: string;

  /** Brief description of what this item is or does */
  description: string;

  /** What category of item this belongs to */
  category: ItemCategory;

  /** Whether this item can be used multiple times */
  stackable: boolean;

  /** Maximum quantity for stackable items */
  maxStack?: number;

  /** Value in game currency */
  value: number;
}

/** Weapon-specific properties */
export interface WeaponItem extends Item {
  category: 'weapon';

  /** Base damage dealt by this weapon */
  damage: number;

  /** Whether the weapon is two-handed (occupies both hands) */
  twoHanded: boolean;

  /** Special ability or effect when attacking */
  special?: string;
}

/** Consumable-specific properties */
export interface ConsumableItem extends Item {
  category: 'consumable';

  /** Health restored when used */
  heal?: number;

  /** Energy restored when used */
  energize?: number;

  /** Sanity effect (positive or negative) */
  sanityEffect?: number;

  /** Duration in hours that the effect lasts */
  duration?: number;
}

/** Tool-specific properties */
export interface ToolItem extends Item {
  category: 'tool';

  /** Durability remaining (when this property exists, tool breaks) */
  durability?: number;

  /** Maximum durability of the tool */
  maxDurability?: number;

  /** What kind of resource the tool gathers/uses */
  action?: string;
}

/** Armor-specific properties */
export interface ArmorItem extends Item {
  category: 'armor';

  /** Defense rating of this armor piece */
  defense: number;

  /** Specific slot this armor occupies (head, chest, legs, etc.) */
  slot: string;
}

/** Artifact-specific properties */
export interface ArtifactItem extends Item {
  category: 'artifact';

  /** Special ability or effect triggered when possessing this artifact */
  ability?: string;

  /** Cost in sanity to activate the ability */
  cost?: number;

  /** Effectiveness rating (0-10) */
  effectiveness?: number;
}

/** Document-specific properties */
export interface DocumentItem extends Item {
  category: 'document';

  /** What information this document reveals or teaches */
  knowledge?: string;
}

/** Currency item */
export interface CurrencyItem extends Item {
  category: 'currency';
  name: 'Gold' | 'Silver' | 'Copper';
}

/** Complete inventory containing all items and quantities */
export type Inventory = Record<string, number>; // itemId -> quantity

/** Equipment slot enumeration */
export type EquipSlot =
  | 'weapon'   // Main weapon hand
  | 'secondary' // Off-hand or offhand weapon
  | 'head'     // Helmets, hats
  | 'chest'    // Torso armor
  | 'legs'     // Pants, leggings
  | 'feet'     // Boots, shoes
  | 'hands'    // Gloves, gauntlets
  | 'ring'     // Rings
  | 'neck';    // Necklaces, amulets

/** Equipment state for the current character */
export interface Equipment {
  [slot in EquipSlot]?: string; // itemId or undefined
}

/** Monster/Enemy types that can be encountered */
export type Monster =
  | 'rat'          // Basic rat
  | 'wolf'         // Forest wolf
  | 'goblin'       // Small humanoid enemy
  | 'ogre'         // Large brute
  | 'shadow_walker' // Stealthy night monster
  | 'spirit'       // Ethereal entity
  | 'guardian'     // Ancient protector
  | 'boss';        // Encountered boss

/** Monster encounter data */
export interface MonsterData {
  id: string;

  /** Base health points */
  maxHealth: number;

  /** Damage this monster deals with attacks */
  damage: number;

  /** Chance to hit the player (as percentage) */
  accuracy: number;

  /** What kind of loot drops from this monster */
  lootTable: string[]; // Item IDs

  /** Monster name for display */
  displayName: string;

  /** Brief description of the monster */
  flavorText: string;
}

/** NPC types that provide services or quest information */
export type NPC =
  | 'innkeeper'    // Runs the inn/cafeteria
  | 'blacksmith'   // Creates and sells weapons
  | 'merchant'     // General goods vendor
  | 'scholar'      // Provides lore and info
  | 'doctor'       // Healing services
  | 'guard';        // Security NPC

/** Quest data structure */
export interface Quest {
  id: string;

  /** Quest title displayed to the player */
  title: string;

  /** Detailed quest objectives and steps */
  description: string;

  /** Which NPC gives this quest */
  giver: NPC;

  /** Whether this quest can still be accepted (true) or completed (false) */
  active: boolean;

  /** XP reward for completing the quest */
  rewardXp: number;

  /** Gold reward for completion */
  rewardGold: number;

  /** Array of item requirements to accept */
  requirements?: string[];

  /** What happens if you die while doing this quest */
  failureConsequence?: string;

  /** Quest stages/progression points */
  stages?: {
    title: string;
    description: string;
    completed: boolean;
  }[];
}

/** Main player character class */
export interface Player extends GameState, Equipment, Inventory {
  /** Current level (player strength and abilities) */
  level: number;

  /** Experience points toward next level */
  xp: number;

  /** XP required for next level */
  xpNeeded: number;

  /** Name chosen by the player */
  name: string;

  /** Whether this is a new character or returning */
  isNewPlayer: boolean;

  /** Current quest being pursued (or null if none) */
  currentQuestId?: string;
}

/** Main game configuration object */
export interface GameConfig {
  /** Maximum health points */
  maxHealth: number;

  /** Maximum energy capacity */
  maxEnergy: number;

  /** Maximum sanity capacity */
  maxSanity: number;

  /** Starting gold amount */
  startingGold: number;

  /** Damage reduction per armor point */
  damageReduction: number;

  /** XP gained from killing a monster (percentage of monster health) */
  xpMultiplier: number;

  /** Gold earned from killing monsters (percentage of gold in loot) */
  goldMultiplier: number;

  /** Whether game difficulty scales with playtime */
  scaleDifficulty: boolean;
}

/** Log entry for the game event log */
export interface LogEntry {
  /** Event category (combat, discovery, dialogue, etc.) */
  type: string;

  /** Timestamp of the event */
  timestamp: string;

  /** Detailed message about what happened */
  message: string;
}

/** Complete game state combining all elements */
export interface GameStateFull extends Player {
  questLog: Quest[]; // All available quests

  monsterEncounters: Map<string, MonsterData>; // id -> MonsterData

  items: Map<ItemCategory, Map<string, Item>>; // category -> itemId -> Item

  locations: Record<Location, string>; // location -> description

  logs: LogEntry[]; // Recent events
}

/** Game statistics tracked across sessions */
export interface GameStats {
  monstersKilled: number;
  questsCompleted: number;
  xpGained: number;
  itemsCollected: Set<string>;
  locationsExplored: Location[];
  totalPlayTimeHours: number;
}
