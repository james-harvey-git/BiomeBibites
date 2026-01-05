# BIOME Bibites - Development Changelog

## Overview
This changelog tracks all changes made to the BiomeBibites Unity DOTS project.
Check this file and BIOME_Implementation_Plan.md before making any changes.

---

## 2026-01-04 - Sparse BIOME Networks Implementation

### Context
The original Bibites simulation uses sparse neural networks where:
- Only INPUT neurons that have connections are included in the network
- Only OUTPUT neurons that are explicitly needed exist
- Hidden neurons don't exist until mutation adds them
- Output neurons have default biases that determine their value when no inputs connect

Example: Basic Bibite has 13 nodes and 3 connections (not all 50+ possible nodes)

### Changes Made

#### BiomeBrain.cs - UPDATED
- Changed from `List<BiomeNode>` to `Dictionary<int, BiomeNode>` for sparse storage
- Node IDs use separate ranges:
  - Input neurons: 0-32 (InputNeurons.COUNT = 33)
  - Output neurons: 1000-1016 (OutputNeurons.OFFSET = 1000, COUNT = 17)
  - Hidden neurons: 2000+ (HiddenNeurons.OFFSET = 2000)
- Added `_usedInputs` and `_usedOutputs` HashSets to track which IDs are in use
- `AddConnection()` auto-creates input nodes when referenced
- `AddOutputNode()` creates output nodes with default biases
- `Process()` returns default values for outputs not in the network

#### Files Requiring Updates (TODO)
The following files use the old List-based API and need updating:
1. BiomeGenome.cs - Uses `brain.Nodes[i]` integer indexing
2. EnhancedMutation.cs - Uses `brain.Nodes[i]` integer indexing
3. SimulationBootstrap.cs - Creates brains and accesses nodes
4. HatchingSystem.cs - Clones brain nodes
5. VisionModule.cs - References BiomeNode.CreateHidden
6. SimulationUI.cs - References GetEnabledConnectionCount

### API Changes Required
Old API (List-based):
```csharp
brain.Nodes[i]  // Access by index
brain.Nodes.Add(node)  // Add node
brain.Nodes.Count  // Get count
brain.Nodes.RemoveAt(i)  // Remove by index
```

New API (Dictionary-based):
```csharp
brain.Nodes[nodeId]  // Access by ID (int key)
brain.Nodes[nodeId] = node  // Set node
brain.Nodes.ContainsKey(nodeId)  // Check existence
brain.Nodes.Count  // Get count
brain.Nodes.Remove(nodeId)  // Remove by ID
brain.AddInputNode(inputId)  // Helper to add input
brain.AddOutputNode(outputId)  // Helper to add output with default bias
brain.AddHiddenNode()  // Helper to add hidden node (returns ID)
```

### Supporting Types Added to BiomeBrain.cs
- `ModuleCategory` enum
- `ConnectionLikelihood` static class
- `ConnectionTypeLikelihood` static class
- Extended `BiomeNode` struct with `UpdateInterval`, `ModuleId`, `ModuleTier`
- Extended `BiomeModule` class with `ModuleId`, `Category`, `NodeIndices`, etc.
- Added `BiomeConnection` constructor

---

## Previous Changes (from earlier sessions)

### Phase 6 - Combat, Grab, Brain Updates
- CombatSystem.cs - Continuous damage like mosquito
- GrabSystem.cs - Single Grab output (-1 to 1)
- CollisionSystem.cs - Gentle physics, pause support
- MovementSystem.cs - Directional drag, pause support

### Phase 5 - Reproduction
- Egg-based reproduction
- Sexual reproduction with NEAT crossover

### Phase 4 - Metabolism  
- Kleiber's law metabolism
- Digestion, fat storage, growth, death systems

### Phases 1-3 - Foundation
- Core ECS setup
- Movement, energy, brain, eating systems
- GL-based rendering

---

## Next Steps
1. ~~Update BiomeGenome.cs for sparse Dictionary API~~ ✓
2. ~~Update EnhancedMutation.cs for sparse Dictionary API~~ ✓
3. ~~Update SimulationBootstrap.cs for sparse brain creation~~ ✓
4. ~~Update HatchingSystem.cs for sparse brain cloning~~ ✓
5. ~~Add missing helper classes to BiomeBrain.cs~~ ✓
6. ~~Update VisionModule.cs for Dictionary-based nodes~~ ✓
7. Test compilation and runtime behavior

---

## Files Updated This Session

### Core BIOME Files:
- **BiomeBrain.cs** - Added ModuleCategory, ConnectionLikelihood, ConnectionTypeLikelihood, extended BiomeNode with UpdateInterval/ModuleId/ModuleTier, extended BiomeModule, added BiomeConnection constructor, added GetEnabledConnectionCount alias
- **BiomeGenome.cs** - Converted all List-based node access to Dictionary-based with proper ID handling
- **EnhancedMutation.cs** - Converted all List-based node access to Dictionary-based with proper ID handling
- **BrainPresets.cs** - Already uses sparse API correctly

### System Files:
- **SimulationBootstrap.cs** - Updated CloneBrain for Dictionary-based nodes, fixed BiomeModule constructor order
- **HatchingSystem.cs** - Updated CloneBrain for Dictionary-based nodes, fixed BiomeModule constructor order
- **VisionModule.cs** - Converted brain.Nodes.Add() to brain.Nodes[nodeId] = node
