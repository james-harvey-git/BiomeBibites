# Claude Code Instructions - BiomeBibites Project

## Project Context

You are continuing development on **BiomeBibites**, a Unity DOTS implementation of The Bibites artificial life simulation using the BIOME evolutionary algorithm.

**CRITICAL:** We recently discovered our implementation was NOT faithful to the true BIOME algorithm. We're now in the middle of an architecture redesign. Read this document carefully before making any changes.

## Current Status

### What's Working (Keep These)
The following ECS systems are functional and should be preserved:
- `MovementSystem.cs` - Physics-based movement
- `EatingSystem.cs` - Pellet consumption
- `DigestionSystem.cs` - Food to energy conversion
- `MetabolismSystem.cs` - Energy consumption
- `FatStorageSystem.cs` - Long-term energy storage
- `GrowthSystem.cs` - Bibite growth over time
- `EggProductionSystem.cs` - Egg creation
- `HatchingSystem.cs` - Egg hatching (needs brain update)
- `CombatSystem.cs` - Biting/damage
- `GrabSystem.cs` - Grabbing objects
- `CollisionSystem.cs` - Physics collisions
- `DeathSystem.cs` - Death and meat pellet creation
- `PelletSpawnSystem.cs` - Plant pellet spawning
- `PheromoneSystem.cs` - Grid-based pheromones (needs integration update)
- `SimulationRenderer.cs` - GL-based rendering

These systems read from `BrainState` component, which will still be populated by the new BIOME brain.

### What Needs Rewriting (BIOME Core)
- `BiomeBrain.cs` - Needs complete rewrite
- `BiomeModules.cs` - Needs complete rewrite  
- `BiomeBrainSystem.cs` - Needs complete rewrite
- Mutation system - Needs all 7 mutation types
- Node catalogue - New file needed

## Key Documents to Read

**READ THESE BEFORE MAKING CHANGES:**

1. **`BIOME_Algorithm_Explained_v3.md`** - How BIOME actually works
   - Core principles (no separate genes, no module parameters, etc.)
   - Module types and how they work
   - The affinity system
   - Examples from Leo's original diagram

2. **`BIOME_Implementation_Plan_v4.md`** - Implementation roadmap
   - Complete node catalogue (all genes, inputs, outputs)
   - Module definitions
   - Mutation types
   - Phase-by-phase plan

3. **`Original_Bibites_Reference.md`** - Original Bibites mechanics
   - All input/output neurons
   - Gene list
   - Activation functions
   - Default biases

4. **`CHANGELOG.md`** - What's been done and current status

## The BIOME Architecture (Correct Understanding)

### Principle 1: All Genes Are Nodes
```csharp
// WRONG - separate gene storage
bibite.Genes.Diet = 0.3f;

// RIGHT - gene is a node
Node { Name="Gene_Diet", Affinity=GENETIC, Bias=0.3f, Output=0.3f }
```

### Principle 2: Module Configuration via Input Nodes
```csharp
// WRONG - hardcoded parameter
ClockModule { Period = 1.0f }

// RIGHT - Period is an INPUT NODE
ClockModule {
    InputNodes: [En, Period]  // Period receives connections
    OutputNodes: [Clk, Counter]
}
// Gene_ClockPeriod connects TO Clock.Period
```

**Why this matters:** Connecting Period to a Gene node = static. Connecting to a Behavioural node = dynamic control! Evolution decides.

### Principle 3: Sparse Instantiation
Not every bibite has every node. Start with minimal set, mutations add more from the catalogue.

### Principle 4: Affinity System
| Affinity | Update Rate | Purpose |
|----------|-------------|---------|
| GENETIC | Never | Gene values |
| BIOLOGICAL | ~1-5 Hz | Internal state |
| BEHAVIOURAL | Every frame | Fast control |

Connection effectiveness matrix prevents non-biological wiring (e.g., Behaviour→Genetic is very weak).

## Implementation Phases

We're currently at **Phase 1** (not started):

1. **Phase 1: Core Data Structures** ← START HERE
   - `BiomeNode` struct
   - `BiomeConnection` struct
   - `NodeCatalogue` with all node types
   - `AffinitySystem` for update rates/effectiveness

2. **Phase 2: Gene System as Nodes**
   - All 40+ genes from original Bibites as GENETIC nodes

3. **Phase 3: Input Modules**
   - VisionModule, ClockModule, BodyStateModule
   - With input nodes for configuration

4. **Phase 4: Output Modules**
   - MovementModule, FeedingModule, ReproductionModule

5. **Phase 5: Network Processing**
   - Affinity-based update timing
   - Connection propagation with effectiveness scaling

6. **Phase 6: Mutation System**
   - All 7 mutation types including "Add Interface Node"

7. **Phase 7: META-modules**

8. **Phase 8: Energy & Polish**

## File Locations

```
Assets/Scripts/
├── BIOME/
│   ├── Core/           # BiomeNode.cs, BiomeConnection.cs, BiomeNetwork.cs
│   ├── Modules/        # ClockModule.cs, VisionModule.cs, etc.
│   ├── Mutations/      # All mutation operators
│   └── Catalogue/      # NodeCatalogue.cs, StarterBrain.cs
├── Components/         # ECS components (mostly keep)
├── Systems/            # ECS systems (mostly keep, update BiomeBrainSystem)
└── Core/               # Bootstrap, Renderer, UI
```

## Code Style

- Use Unity DOTS patterns (IComponentData, SystemBase, etc.)
- Prefer structs for data (BiomeNode, BiomeConnection)
- Classes for complex state (BiomeModule, BiomeNetwork)
- Use Burst-compatible code where possible
- Comment complex logic

## Testing

After changes:
1. Check for compilation errors
2. Enter Play mode in Unity
3. Verify bibites spawn and move
4. Check that brain outputs affect behavior

## Common Pitfalls

1. **Don't add module parameters** - Use input nodes connected to Gene nodes
2. **Don't create separate gene storage** - Genes ARE nodes
3. **Don't make all modules mandatory** - Sparse instantiation
4. **Don't forget affinity effectiveness** - Behavioural→Genetic should be very weak

## Questions to Ask

Before implementing a feature, ask:
1. Does this align with BIOME principles?
2. Is configuration coming through input nodes (not parameters)?
3. Are genes represented as GENETIC-affinity nodes?
4. Is this sparse (only instantiate what's needed)?

## Getting Help

If unsure about BIOME architecture:
1. Check `BIOME_Algorithm_Explained_v3.md`
2. Look at Leo's diagram (described in that document)
3. Review `BIOME_Implementation_Plan_v4.md` for specifics

Good luck! The goal is true open-ended evolution.
