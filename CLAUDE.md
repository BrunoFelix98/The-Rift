# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

**Opening the project**
- Unity Hub: *Add* the folder `D:\The Rift\The-Rift` or click *Open* on the folder.
- CLI (Windows):
  ```
  "C:\Program Files\Unity\Hub\Editor\<UnityVersion>\Editor\Unity.exe" -projectPath "D:\The Rift\The-Rift"
  ```
  Replace `<UnityVersion>` with the installed Unity version (the project targets Unity 6).

**Unity command‑line shortcuts**
- **Run Play‑mode tests** (EditMode or PlayMode):
  ```
  Unity.exe -batchmode -quit -projectPath "D:\The Rift\The-Rift" \
    -runTests -testPlatform PlayMode -logFile test.log
  ```
- **Build a standalone player** (requires a custom build method; the repository does not ship one yet, so use the Editor UI *File → Build Settings*). If you later add a static build entry point you can invoke it with:
  ```
  Unity.exe -batchmode -quit -projectPath "D:\The Rift\The-Rift" \
    -executeMethod BuildScript.PerformBuild -buildTarget StandaloneWindows64 -logFile build.log
  ```

**Editor utilities (menu items)**
- `The Rift → Setup Universe Scene` – creates or finds a `UniverseManager` GameObject, configures pools, registers celestial templates, and creates material assets for stars and nebulae. It no longer adds a `NetworkManager`; networking lives in `Assets/Scenes/Bootstrap.unity`.
- `Tools → Universe → Bake Universe (Jobs)` – launches the `RevisedUniverseBakerEditor` workflow. It parses JSON files under `Assets/Code/Backend/DataSets/Behavior/JSONData`, runs a burst‑compiled `RevisedGenerateSystemsJob` to compute system layout, then creates ScriptableObject assets (galaxies, systems, stars, planets, moons, asteroid belts, gates, etc.) under `Assets/RuleBasedGeneratedGalaxy`.

**Play testing**
- Start from `Assets/Scenes/Bootstrap.unity` (build index 0). It holds the only `NetworkManager` plus `NetworkAutoStart` and `NetworkDebuggerUI`, all persisted across scene loads.
- The server loads `MainGameScene` through `NetworkManager.SceneManager`; clients follow via NGO scene sync. Never put a `NetworkManager` or a second copy of the gameplay scene elsewhere.
- Role: `-server` / `-client` CLI flags, else the Multiplayer Role (Play Mode scenarios `Assets/Settings/PlayMode/Server.asset` = server editor + client Player 2), else `NetworkAutoStart.isServer`.
- Address: `-address <ip>` / `-port <n>`; the editor defaults clients to `127.0.0.1`, builds use `NetworkAutoStart.address`.

## Architecture Overview

The project follows a strict data‑logic / visual‑frontend split.

### Backend (`Assets/Code/Backend/`)
- **Data model** – Core entities (`Ship`, `Station`, `Gate`, `Resource`, etc.) live in `DataSets/Entities`. They are plain C# POCO classes used by server‑side logic and by generation code.
- **ScriptableObject assets** – Static game data (galaxies, systems, stars, planets, moons, resources, modules, etc.) are defined as `ScriptableObject` classes under `DataSets/Scriptable Objects/`. The generation pipeline creates these assets at editor‑time.
- **Generation pipeline** –
  - `RuleBasedGalaxyGenerator` (runtime) parses JSON, schedules a burst‑compiled `GenerateAllJobs` job chain, then calls `ConvertAllNativeDataToAssets` to materialise the results as SOs and prefabs.
  - `RevisedUniverseBakerEditor` (editor‑time) provides a menu‑driven, job‑based bake that outputs the same asset hierarchy under `Assets/RuleBasedGeneratedGalaxy`.
- **Job systems** – Heavy‑weight calculations (orbit parameters, system layout, resource allocation) are performed with Unity Jobs + Burst (`GenerateAllJobs`, `RevisedGenerateSystemsJob`).
- **Server side** – Code under `Backend/Server Versions/ServerSideCommunication/` implements authoritative networking (`ServerManager`, `OnScreenConsole`, client‑side setup scripts). All mutable state (movement, damage, ship stats) is synchronised via `NetworkVariable` on `NetworkBehaviour` components.

### Frontend (`Assets/Code/Frontend/`)
- **Visualization** – `GalaxyVisualizer`, `SystemCenteredVisualizer`, `DynamicSkyboxManager` instantiate prefabs for celestial bodies, apply materials, and manage camera/skybox transitions.
- **Movement & input** – Player piloting scripts (`FighterPiloting`, `BigShipMovement`, `PlayerMovement`, `PlayerLook`) inherit from `NetworkBehaviour` and use `NetworkVariable` for server‑authoritative state. Input actions are wired via the Unity Input System (`InputActionReference`).
- **Networking wrappers** – `NetworkedAsteroidData`, `NetworkedPlanetData`, `NetworkedStationData`, `NetworkedCelestial` expose the data components that need to be synchronised across clients.
- **Object pooling** – `NetworkedObjectPoolManager`, `CelestialPool`, `SpaceObjectManager` recycle instantiated GameObjects to keep runtime allocations low.
- **Utility systems** – `JumpManager`/`PlayerJumpController` handle fast‑travel between systems, `GalacticTimeManager` tracks simulation time, and `SpaceEnvironmentManager` applies environmental effects.

### Universe runtime (`Assets/Code/Universe/`)
- `SystemCenteredApproach/SystemCenteredVisualizer` – server tracks each player's system (gates move one player), pushes system data + skybox metadata via named messages, hides players in other systems, spawns players near the station.
- `UniversePersistenceManager` loads `Assets/StreamingAssets/UniverseMap.bin` (server only, on a worker thread).
- `NetworkAutoStart`, `NetworkDebuggerUI` live on the Bootstrap scene's NetworkManager.
- `UniverseSetupHelper`, `SystemCenteredSetupHelper` and `Editor/` are editor-only menu tools (`#if UNITY_EDITOR`).

### Player movement
- `FighterPiloting`, `BigShipMovement`, `PlayerMovement` share one model: the owner simulates each tick's input with a deterministic `Step`, the server runs the same step and publishes a state `NetworkVariable`, the owner reconciles by replaying unacknowledged inputs, other clients smooth toward the state. Move players only through `ITeleportable`.
- HUD and markers (`ShipHud`, `PlayerMarkerOverlay`, `CelestialMarkerOverlay`) are IMGUI overlays created by `PlayerSetup` for the local player.
- Celestial looks come from `Assets/Shaders/Celestial` via `CelestialBodyVisuals` / `StarData`.
- **Floating origin** (`Assets/Code/Universe/FloatingOrigin.cs`): transforms on clients are render space = world - `FloatingOrigin.Offset`, recentred near the local player. Simulation states hold world positions as `Vector3d`; convert with `ToRender`/`ToWorld`. Server logic must use world positions (`ITeleportable.WorldPosition`, `TryGetCelestialWorldPosition`), never transforms. Celestials use `CelestialHierarchyNode.RenderPosition` for drawing.
- **Sky**: `SpaceEnvironmentManager` sets the procedural `SpaceSkybox` material; `DynamicSkyboxManager` draws the real neighbouring systems with `SystemStars` at the far plane.
- **Distances** are shown via `DistanceFormat` (m below 1000 units, then AU; 1 AU = 10,000 units).

## Key Build / Run Flow
1. **Setup** – Run *The Rift → Setup Universe Scene* to create the manager hierarchy and asset folders.
2. **Generate** – Choose *Tools → Universe → Bake Universe (Jobs)* to read JSON, run the burst job, and produce the full galaxy asset hierarchy.
3. **Play** – Open `Bootstrap.unity` (or run a Play Mode scenario) and press Play; the server loads `MainGameScene`, the pooling system pulls prefabs from `Assets/RuleBasedGeneratedGalaxy/Prefabs`, and the visualization system renders the current system.
4. **Iterate** – Edit JSON files, re‑run the bake menu, then restart Play.

## Development Tips Specific to This Repo
- **ScriptableObject workflow** – When adding new content (e.g., a new ship class or resource type) create a corresponding SO file in the appropriate sub‑folder (`Scriptable Objects`). The generation pipeline will pick it up automatically if referenced in the JSON.
- **Job‑debugging** – Burst jobs can be inspected via the Unity Profiler. The `GenerateAllJobs` struct contains the full pipeline; you can attach a debugger by disabling Burst (`[BurstCompile]` → comment) if you need step‑through behaviour.
- **Network authority** – All mutable state (position, velocity, health) must be written **only** on the server (`NetworkVariableWritePermission.Server`). Client‑side prediction is implemented in the piloting scripts via input buffers.
- **Resource assignment** – `ConvertAllNativeDataToAssets.GenerateResources` assigns resources to planets, moons, asteroids based on the `category` and `celestialType` fields in the JSON. If you add new resource categories, ensure the JSON and the `ResourceData` definition stay in sync.
- **Prefab generation** – The conversion step creates a prefab for each celestial object type (`CreatePrefabForSO`). Custom visual components are added in `CelestialPrefabBuilder` based on the object's `CelestialEnvironment`.

---
*This CLAUDE.md focuses on the concrete commands, data‑generation pipeline, and the split between backend logic and frontend visual/network code. It deliberately omits generic development best‑practices that are already obvious from the Unity workflow.*
