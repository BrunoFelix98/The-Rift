# Change 002 - NetworkedGalaxyVisualizer Refactor

## Description
Refactored `NetworkedGalaxyVisualizer` to use a hierarchical proximity-based pooling system. This eliminates the "lag" caused by thousands of active GameObjects and per-frame orbital calculations for non-visible objects.

## Key Changes
- **Hierarchical Nodes**: Introduced `CelestialHierarchyNode` to manage parent-child relationships and world position calculation.
- **Deterministic Orbits**: Orbital positions are now calculated using `NetworkManager.Singleton.ServerTime.Time`, ensuring synchronization without continuous network updates for visuals.
- **Object Pooling**: Integrates with `CelestialPool` to activate/deactivate celestial bodies based on distance to the local player.
- **Tree Pruning**: The recursive update mechanism allows for efficient pruning (though currently it iterates through all nodes, it provides the structure for further bounding-box optimizations).
- **Cleaned Up Logic**: Removed redundant `UpdateLOD` and `SpawnObjectsInBatches` for non-networked objects.

## Impact
- **Performance**: Significant reduction in CPU and GPU overhead in large systems.
- **Scalability**: Allows for much larger universes with millions of objects, as only those within `viewDistance` are active.
- **Networking**: Reduces initial spawn burst and ongoing position sync for celestial visuals.
