# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands
This project is developed primarily within the Unity Editor. Build, linting, and testing processes are handled via the Unity Editor interface. No external CLI build or test scripts are currently defined.

## Architecture Overview
The project follows a strict separation between data/logic (Backend) and Unity-specific behaviors/visuals (Frontend).

### Backend (`Assets/Code/Backend/`)
- **Purpose**: Defines the game's data model and core logic, independent of Unity's scene graph where possible.
- **Entities**: Core game objects like `Ship`, `Station`, `Gate`, and `Resource`.
- **Data Storage**: Uses `ScriptableObjects` (e.g., `GalaxySO`, `BlueprintSO`) to define static game data and entity blueprints.

### Frontend (`Assets/Code/Frontend/`)
- **Purpose**: Handles Unity-specific implementation, visual representation, and user input.
- **Systems**:
    - **Visualization**: Manages procedural instantiation of celestial bodies and dynamic skyboxes.
    - **Movement**: Implements various piloting models (point-and-click for big ships, direct control for fighters) with client-side prediction.
    - **Networking**: Implements `NetworkBehaviour` for synchronizing state.

## Technical Stack
- **Engine**: Unity 6
- **Networking**: Unity Netcode for GameObjects (NGO) - Server-authoritative model.
- **Rendering**: Universal Render Pipeline (URP).
- **Input**: Unity Input System.

## Coding Conventions
- **Separation of Concerns**: Keep entity logic in the `Backend` namespace and visual/input logic in the `Frontend` namespace.
- **Networking**: Critical state (movement, damage) must be server-authoritative. Use `NetworkVariable` for state synchronization.
- **Data-Driven**: Prefer `ScriptableObjects` for defining new content (ships, items, galaxy parameters).
- **Naming**: 
    - PascalCase for classes and methods.
    - camelCase for private fields.
