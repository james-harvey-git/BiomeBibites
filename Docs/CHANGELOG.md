# BIOME Bibites - Development Changelog

## Overview
This changelog tracks all changes made to the BiomeBibites Unity DOTS project.

**Current Status:** ARCHITECTURE REDESIGN IN PROGRESS

We discovered fundamental issues with our BIOME implementation and are now redesigning to be truly BIOME-faithful. See `BIOME_Implementation_Plan_v4.md` for the new approach.

**Reference Documents:**
- `BIOME_Algorithm_Explained_v3.md` - How BIOME actually works (corrected)
- `BIOME_Implementation_Plan_v4.md` - New implementation plan
- `Original_Bibites_Reference.md` - Original Bibites simulation mechanics

---

## 2026-01-05 - Phase 1: Core Data Structures (COMPLETE)

### New Files Created

**Core Data Structures (`Assets/Scripts/BIOME/Core/`):**

1. **BiomeNode.cs** - The fundamental computing unit in BIOME
   - `BiomeNode` struct with all required fields (Id, CatalogueId, Affinity, ActivationFunction, Bias, Activation, Output, PreviousOutput, LastUpdateFrame)
   - `NodeAffinity` enum (Genetic, Biological, Behavioural)
   - `ActivationFunctionType` enum (14 types including Identity, Sigmoid, TanH, Latch, Integrator, etc.)
   - Factory methods: `Create()`, `CreateGene()`, `CreateHidden()`

2. **BiomeConnection.cs** - Links nodes together
   - `BiomeConnection` struct with FromNodeId, ToNodeId, Weight, Enabled, InnovationNumber
   - `GetEffectiveWeight()` method that applies affinity effectiveness matrix

3. **AffinitySystem.cs** - Manages update rates and connection effectiveness
   - Connection effectiveness matrix (Genetic→Behavioural=0.3, Behavioural→Genetic=0.01, etc.)
   - Mutation prior matrix for biologically plausible connection mutations
   - `ShouldUpdateNode()` for affinity-based update timing
   - BIOLOGICAL nodes update at ~5 Hz (every 12 frames)

4. **ActivationFunctions.cs** - All 14 activation function implementations
   - Identity, Sigmoid, Linear, TanH, Sine, ReLU, Gaussian
   - Stateful functions: Latch, Differential, Integrator, Inhibitory, SoftLatch
   - Mult, Abs
   - `Apply()` method handles all function types with deltaTime support

**Node Catalogue (`Assets/Scripts/BIOME/Catalogue/`):**

5. **NodeCatalogue.cs** - Complete catalogue of all possible node types
   - 35+ Gene nodes (appearance, metabolism, WAG organs, vision, clock, herding, growth, etc.)
   - 7 Internal sensor nodes (BIOLOGICAL affinity: EnergyRatio, LifeRatio, Fullness, etc.)
   - 25+ External sensor nodes (BEHAVIOURAL affinity: vision, pheromones, movement, etc.)
   - 15 Output nodes with FIXED activation functions (Accelerate, Rotate, Want2Eat, etc.)
   - Constant nodes for module enable signals
   - `NodeDefinition` struct and `NodeCategory` enum
   - Public API: `GetDefinition()`, `CreateNodeFromCatalogue()`, `GetGeneNodeIds()`, etc.

**Project Configuration:**

6. **CLAUDE.md** - Instructions for AI-assisted development
   - Changelog update requirement after each change

### Key Design Decisions

- **All constants use meaningful IDs**: Genes 0-99, Internal sensors 100-149, External sensors 150-249, Outputs 300-399
- **Affinity effectiveness matrix** prevents non-biological wiring (Behavioural→Genetic = 0.01)
- **Output nodes have FIXED activation functions** matching original Bibites (e.g., Accelerate uses TanH with default bias 0.45)
- **Gene nodes use Identity activation** where Output = Bias (the gene value)

### Phase 1 Status: ✅ COMPLETE

All core data structures are implemented. Ready for Phase 2 (Gene System as Nodes).

---

## 2026-01-05 - MAJOR ARCHITECTURE RETHINK

### The Problem We Discovered

After reviewing Leo Caussan's original BIOME documentation and diagrams, we identified fundamental issues with our implementation:

1. **We had TWO separate systems that didn't integrate properly:**
   - Old BiomeBrain with InputNeurons (ID 0-32) and OutputNeurons (ID 1000+)
   - Modules with separate nodes (ID 3000+)
   - These didn't connect properly - signals weren't flowing through

2. **Modules had hardcoded parameters instead of input nodes:**
   - WRONG: `ClockModule { Period = 1.0f }` 
   - RIGHT: Clock has `Period` INPUT NODE that can be connected to ANY node

3. **Genes were NOT nodes:**
   - We still had separate gene concepts
   - In true BIOME, ALL genes are nodes with GENETIC affinity

4. **No sparse instantiation:**
   - Every bibite had every module
   - Should only instantiate what's needed; mutations add more

### The Correct BIOME Architecture (from Leo's diagram)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           BIOME NETWORK                                  │
│                                                                          │
│  Gene Nodes (GENETIC affinity, bias = gene value, output = bias)         │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ Gene_ClockPeriod(1.0)  Gene_ViewRadius(100)  Gene_Diet(0.3)     │    │
│  └───────────┬─────────────────────┬───────────────────────────────┘    │
│              │ (connections)       │                                     │
│              ▼                     ▼                                     │
│  ┌─────────────────────┐    ┌─────────────────────┐                     │
│  │ Internal Clock      │    │ Vision Module       │                     │
│  │ Module              │    │                     │                     │
│  │ ►En          Clk►   │    │ ►ViewRadius  PlantCloseness►              │
│  │ ►Period   Counter►  │    │ ►ViewAngle   PlantAngle►                  │
│  └─────────────────────┘    └──────────┬──────────┘                     │
│                                        │ (connections)                   │
│                                        ▼                                 │
│                             ┌─────────────────────┐                     │
│                             │ Movement Module     │                     │
│                             │ ►Move    ►Rotation  │                     │
│                             └─────────────────────┘                     │
│                                                                          │
│  Legend: ► = Input node    ►= Output node    ─── = Connection           │
└──────────────────────────────────────────────────────────────────────────┘
```

**Key insight:** Module configuration (Period, ViewRadius, etc.) comes through INPUT NODES that can be connected to Gene nodes (static) OR Behavioural nodes (dynamic). Evolution decides the wiring!

### What Needs to Change

The existing code in `Assets/Scripts/` is based on the OLD incorrect architecture. We need to:

1. **Rewrite BiomeBrain.cs** - Unified network where genes ARE nodes
2. **Rewrite all modules** - Configuration via input nodes, not parameters
3. **Add Node Catalogue** - All possible node types that can be instantiated
4. **Add proper Affinity System** - Update rates and connection effectiveness
5. **Add all 7 mutation types** - Including "Add Interface Node" from catalogue

### Files to Reference (OLD - do not use as-is)
The current codebase has useful ECS systems (Movement, Eating, Collision, etc.) that can be kept, but the BIOME brain/module system needs complete rewrite.

---

## Previous Development (Before Architecture Rethink)

### 2026-01-05 - Attempted Unified Brain (Now Superseded)
- Created unified BiomeBrain.cs with modules
- Created BiomeBrainSystem.cs 
- **Issue:** Still had modules "reading from Gene nodes" instead of having input nodes

### 2026-01-05 - Pheromone System
- PheromoneSystem.cs - Grid-based 3-channel pheromones
- PheromoneModule.cs - Sensing and emission
- **Status:** Can be adapted to new architecture

### 2026-01-05 - Module Mutations
- ModuleMutations.cs - Duplication, tier upgrade, connections
- **Status:** Needs update for new architecture

### 2026-01-04 - Module System
- BiomeModules.cs - Tier-1 module definitions
- HatchingSystem.cs - Module inheritance
- **Status:** Needs rewrite - modules had parameters instead of input nodes

### Earlier Phases (Can Keep)
- **Phase 6:** CombatSystem, GrabSystem, CollisionSystem
- **Phase 5:** Reproduction, EggProductionSystem, HatchingSystem
- **Phase 4:** MetabolismSystem, DigestionSystem, FatStorageSystem, GrowthSystem
- **Phase 1-3:** Core ECS, MovementSystem, EnergySystem, EatingSystem

These systems read from `BrainState` component, which will still be populated by the new BIOME brain system.

---

## Implementation Status

### ✅ Complete (Can Keep)
- Core ECS architecture
- Entity components (Position, Velocity, Energy, Health, etc.)
- Movement, Eating, Collision physics
- Metabolism, Digestion, Fat storage
- Reproduction (egg-based)
- Pheromone grid (needs integration update)
- GL-based rendering

### ✅ Phase 1 Complete (BIOME Core Data Structures)
- BiomeNode.cs - Node struct with affinity system
- BiomeConnection.cs - Connection struct with effectiveness
- AffinitySystem.cs - Update rates and effectiveness matrix
- ActivationFunctions.cs - All 14 activation functions
- NodeCatalogue.cs - Complete node type catalogue

### 🔄 Needs Rewrite (BIOME Core - Phases 2-8)
- BiomeBrain.cs → New unified brain with genes as nodes
- BiomeModules.cs → Modules with input nodes for config
- BiomeBrainSystem.cs → Process the new brain
- Mutation system → Add all 7 mutation types

### ❌ To Be Deleted
- Old InputNeurons/OutputNeurons static classes
- BrainPresets.cs (merge into new BiomeBrain)
- Any file with "module parameters"

---

## Next Steps

See `BIOME_Implementation_Plan_v4.md` for detailed phases:

1. **Phase 1:** Core data structures (BiomeNode, BiomeConnection, NodeCatalogue)
2. **Phase 2:** Gene system as nodes (all original Bibites genes)
3. **Phase 3:** Input modules with configuration input nodes
4. **Phase 4:** Output modules
5. **Phase 5:** Network processing with affinity system
6. **Phase 6:** All 7 mutation types
7. **Phase 7:** META-modules
8. **Phase 8:** Energy costs and polish
