# GEMINI.md - The Rift

## Project Overview
**The Rift** is a space-themed sandbox and simulation game built with **Unity 6**. It features a procedurally generated galaxy, networked multiplayer, and deep ship customization systems. The project is designed with a clear separation between backend data (entities, stats, enums) and frontend behaviors (visualizers, movement, networking).

### Key Technologies
- **Engine:** Unity 6 (Version 6000.4.2f1)
- **Networking:** Unity Netcode for GameObjects (NGO)
- **Rendering:** Universal Render Pipeline (URP)
- **AI:** Unity Behavior package
- **Input:** Unity Input System
- **Camera:** Cinemachine

## Core Architecture

### Backend (Data & Entities)
Located in `Assets/Code/Backend/`, this layer defines the game's data model.
- **Entities:** `Ship`, `Station`, `Gate`, `Resource`, `Module`. Ships use a modular slot system (High, Medium, Low, Rig, Capital) and a resistance-based damage model.
- **DataSets:** ScriptableObjects (`GalaxySO`, `BlueprintSO`) are used to define static data for the galaxy and entity blueprints.
- **Enums:** Defines core game concepts like `ShipWeightClass`, `CelestialType`, `DamageType`, etc.

### Frontend (Behaviors & Visuals)
Located in `Assets/Code/Frontend/`, this layer handles Unity-specific logic.
- **Galaxy Visualization:** `SystemCenteredVisualizer` manages the procedural instantiation and networking of celestial bodies within the player's current star system.
- **Dynamic Skybox:** `DynamicSkyboxManager` renders distant star systems as a procedural background mesh.
- **Movement Systems:**
    - **Big Ship Piloting:** Server-authoritative "point-and-click" movement with client-side prediction.
    - **Fighter Piloting:** More direct control model (likely physics-based or arcade-style).
    - **Crew & FPS:** Support for internal ship gameplay or station interaction.
- **Networked Objects:** Celestial bodies and ships use `NetworkBehaviour` to synchronize state across clients.

### Directory Structure Highlights
- `Assets/Code/Backend`: Core logic and data structures.
- `Assets/Code/Frontend`: Unity-specific behaviors and systems.
- `Assets/Prefabs/Celestial Prefabs`: Visual assets for space objects.
- `Assets/Scenes`: Test and production scenes (e.g., `GalaxyBuilder`, `MultiplayerTestScene`).
- `ProjectSettings`: Unity project configurations.

## Building and Running
- **Build Target:** Windows (primary), with profiles for Mobile and PC.
- **Key Scenes:**
    - `Assets/Scenes/GalaxyBuilder.unity`: Likely used for environment testing.
    - `Assets/Scenes/MultiplayerTestScene.unity`: Main testing ground for networking features.
- **Build Profile:** `Assets/Settings/Build Profiles/Windows.asset`.

### Development Commands (TODO)
- Currently, build and test processes are handled within the Unity Editor.
- TODO: Identify or create CI/CD scripts for automated builds if needed.

## Development Conventions
- **Networking:** Always use server-authoritative logic for critical state (movement, damage). Use `NetworkVariable` for synchronization.
- **Modularity:** Keep entity logic in the `Backend` namespace and visual/input logic in the `Frontend` namespace.
- **Data-Driven:** Prefer `ScriptableObjects` for defining new ships, items, or galaxy parameters.
- **Naming:** Follow standard C# and Unity naming conventions (PascalCase for classes/methods, camelCase for private fields).
