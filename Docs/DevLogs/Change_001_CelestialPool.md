# Change 001 - CelestialPool Implementation

## Description
Created a generic `CelestialPool` script to handle object pooling for celestial objects (stars, planets, moons, etc.). This aims to reduce instantiation overhead and memory churn.

## Files Created
- `Assets/Code/Frontend/Game Behavior/GameOptimization/CelestialPool.cs`

## Impact
- Provides a centralized pool for all celestial object prefabs.
- Supports different configurations for each `CelestialType`.
- Allows for efficient activation/deactivation of objects based on proximity.
