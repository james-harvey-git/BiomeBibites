# BIOME-Faithful Implementation Plan v4

## Executive Summary

This document outlines a complete redesign of our Bibites-BIOME implementation to be truly faithful to the BIOME algorithm. The key insight is that BIOME unifies **everything** into a single evolvable network - there is no separate "genome" and "brain". All genes become nodes, all parameters become node biases, and modules are thin wrappers around hardcoded physics that expose nodes for evolution to wire up.

---

## Core Principles (Non-Negotiable)

### 1. NO Separate Genes Structure
**Wrong:** `bibite.Genes.Diet = 0.3f`  
**Right:** A node with `{Name="Diet", Affinity=GENETIC, Bias=0.3f, Output=0.3f}`

Every gene from the original Bibites becomes a node in the BIOME network. The node's **bias IS the gene value**. Genetic nodes use `Identity` activation and their output equals their bias (fixed at birth).

### 2. NO Module Parameters — Configuration via Input Nodes
Modules do NOT have hardcoded configuration parameters. Instead, modules expose **input nodes** for any configurable value. These input nodes can be connected to **any other node** in the network.

**Wrong:** `ClockModule { Period = 1.0f }` — hardcoded parameter  
**Right:** Clock module has `Period` input node, which is connected to `Gene_ClockPeriod` node

**Why this is powerful:**
- Connecting Period to a Gene node = static configuration (traditional gene)
- Connecting Period to a Behavioural node = dynamic, controllable period!
- Evolution decides the wiring — a species could evolve to change its clock period based on hunger, danger, etc.

This is more flexible than "modules read from Gene nodes" — the configuration can come from ANY node type.

### 3. Nodes Represent Everything
- **Genes** = Genetic-affinity nodes (bias = gene value, never changes)
- **Internal State** = Biological-affinity nodes (updated slowly: fullness, health ratio, etc.)
- **Behaviours** = Behavioural-affinity nodes (updated every frame: sensor outputs, motor commands)
- **Hidden Neurons** = Behavioural nodes that process signals

### 4. Sparse Instantiation
Not every bibite has every possible node. The simulation maintains a **catalogue** of possible node types. Bibites only instantiate a subset. Mutations can **add new interface nodes** from the catalogue.

### 5. Fixed Output Activation Functions
Output node activation functions are determined by their type and cannot mutate. Hidden node activation functions CAN mutate.

### 6. Sensors/Actuators are Physics Hooks
Modules contain **hardcoded transduction logic** (raycasting, force application). Evolution does NOT invent new physics - it evolves how signals are wired and used.

---

## Data Model

### Node
```csharp
struct BiomeNode
{
    int Id;
    string Name;
    NodeAffinity Affinity;      // GENETIC, BIOLOGICAL, BEHAVIOURAL
    ActivationFunction ActFunc;
    float Bias;                 // For genes: this IS the gene value
    float Activation;           // Accumulated input this tick
    float Output;               // ActFunc(Activation + Bias)
    float PreviousOutput;       // For stateful functions (Latch, Differential, etc.)
    int LastUpdateFrame;        // For affinity-based update timing
}
```

### Connection
```csharp
struct BiomeConnection
{
    int FromNode;
    int ToNode;
    float Weight;
    bool Enabled;
}
```

### Module
```csharp
class BiomeModule
{
    ModuleType Type;            // INPUT, OUTPUT, FUNCTIONAL, META
    string Name;
    List<int> ExposedNodeIds;   // Nodes this module owns/exposes
    bool Enabled;               // Can be disabled by energy gating
    
    // For FUNCTIONAL modules: internal state
    Dictionary<string, float> InternalState;
    
    // For META modules: reference to template
    int? TemplateId;
}
```

---

## Affinity System

### Update Rates
| Affinity | Update Frequency | Purpose |
|----------|------------------|---------|
| **BEHAVIOURAL** | Every frame (~60Hz) | Fast control signals, sensor outputs, motor commands |
| **BIOLOGICAL** | Slow tick (~1-5Hz) | Internal state, growth, digestion, hormones |
| **GENETIC** | Never (fixed at birth) | Gene values, body plan parameters |

### Connection Effectiveness Matrix
Connections between nodes of different affinities have different effectiveness:

| From ↓ To → | Genetic | Biological | Behavioural |
|-------------|---------|------------|-------------|
| **Genetic** | 1.0 | 0.8 | 0.3 |
| **Biological** | 0.05 | 1.0 | 1.0 |
| **Behavioural** | 0.01 | 0.3 | 1.0 |

**Interpretation:**
- Genetic → Genetic: Genes can influence other gene expressions (development)
- Genetic → Biological: Genes strongly influence body state
- Genetic → Behavioural: Genes weakly influence behaviour directly
- Biological → Genetic: Body state almost never changes genes (Lamarckism blocked)
- Behavioural → Genetic: Behaviour cannot change genes

### Mutation Priors
When adding new connections, favor biologically plausible wiring:
- **High probability:** Genetic→Biological, Biological→Behavioural, Behavioural→Behavioural
- **Low probability:** Behavioural→Biological (feedback loops)
- **Very low probability:** Anything→Genetic (except other Genetic)

---

## Complete Node Catalogue

### Gene Nodes (Affinity: GENETIC)
All genes from the original Bibites, plus BIOME additions:

#### Appearance Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_ColorRed` | 0.5 | Red pigment (0-1) |
| `Gene_ColorGreen` | 0.5 | Green pigment (0-1) |
| `Gene_ColorBlue` | 0.5 | Blue pigment (0-1) |
| `Gene_EyeHueOffset` | 0.0 | Eye hue shift |

#### Size & Metabolism Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_SizeRatio` | 1.0 | Adult size multiplier |
| `Gene_MetabolismSpeed` | 1.0 | Process speed multiplier |
| `Gene_Diet` | 0.0 | 0=herbivore, 1=carnivore |

#### Mutation Rate Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_AvgGeneMutations` | 1.0 | Expected gene mutations per reproduction |
| `Gene_GeneMutationVariance` | 0.1 | Std dev of gene mutation magnitude |
| `Gene_AvgBrainMutations` | 3.0 | Expected brain mutations per reproduction |
| `Gene_BrainMutationVariance` | 0.5 | Std dev of brain mutation magnitude |

#### Reproduction Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_LayTime` | 10.0 | Seconds to produce an egg at full activation |
| `Gene_BroodTime` | 5.0 | Affects maturity at birth |
| `Gene_HatchTime` | 15.0 | Seconds for egg to hatch |

#### Vision Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_ViewRadius` | 100.0 | Maximum vision distance |
| `Gene_ViewAngle` | 2.0 | Field of view in radians |

#### Clock Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_ClockPeriod` | 1.0 | Period of internal clock |

#### Pheromone Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_PheromoneRadius` | 50.0 | Pheromone sensing radius |

#### Herding Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_HerdSeparationWeight` | 1.0 | Separation force weight |
| `Gene_HerdAlignmentWeight` | 1.0 | Alignment force weight |
| `Gene_HerdCohesionWeight` | 1.0 | Cohesion force weight |
| `Gene_HerdVelocityWeight` | 1.0 | Velocity matching weight |
| `Gene_HerdSeparationDistance` | 15.0 | Target separation distance |

#### Growth Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_GrowthScaleFactor` | 1.0 | Growth curve scale |
| `Gene_GrowthMaturityFactor` | 1.0 | Maturity influence on growth |
| `Gene_GrowthMaturityExponent` | 2.0 | Growth curve shape |

#### Organ WAG Genes (Weighted Apportionment)
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_WAG_ArmMuscles` | 1.0 | Movement muscle share |
| `Gene_WAG_Stomach` | 1.0 | Stomach share |
| `Gene_WAG_EggOrgan` | 1.0 | Reproductive organ share |
| `Gene_WAG_FatOrgan` | 1.0 | Fat storage share |
| `Gene_WAG_Armor` | 1.0 | Armor share |
| `Gene_WAG_Throat` | 1.0 | Throat share |
| `Gene_WAG_JawMuscles` | 1.0 | Jaw muscle share |

#### Fat Metabolism Genes
| Node Name | Default Bias | Description |
|-----------|--------------|-------------|
| `Gene_FatStorageThreshold` | 0.7 | Energy ratio to start storing fat |
| `Gene_FatStorageDeadband` | 0.1 | Neutral band width |

---

### Input Nodes (Affinity: BIOLOGICAL or BEHAVIOURAL)
Sensor outputs that modules populate:

#### Internal State Sensors (BIOLOGICAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_EnergyRatio` | BIO | Current/Max energy |
| `Sense_LifeRatio` | BIO | Current/Max health |
| `Sense_Fullness` | BIO | Stomach fullness |
| `Sense_Maturity` | BIO | Growth maturity (0-1) |
| `Sense_EggStored` | BIO | Number of eggs stored |
| `Sense_FatRatio` | BIO | Fat storage level |

#### Movement Sensors (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_Speed` | BEHAV | Current forward speed |
| `Sense_RotationSpeed` | BEHAV | Current angular velocity |
| `Sense_IsGrabbing` | BEHAV | 1 if grabbing, else 0 |
| `Sense_AttackedDamage` | BEHAV | Damage taken this frame |

#### Vision - Plants (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_PlantCloseness` | BEHAV | Proximity to nearest plant (0-1) |
| `Sense_PlantAngle` | BEHAV | Angle to nearest plant (-1 to 1) |
| `Sense_NPlants` | BEHAV | Number of visible plants |

#### Vision - Meat (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_MeatCloseness` | BEHAV | Proximity to nearest meat |
| `Sense_MeatAngle` | BEHAV | Angle to nearest meat |
| `Sense_NMeats` | BEHAV | Number of visible meats |

#### Vision - Bibites (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_BibiteCloseness` | BEHAV | Proximity to nearest bibite |
| `Sense_BibiteAngle` | BEHAV | Angle to nearest bibite |
| `Sense_NBibites` | BEHAV | Number of visible bibites |
| `Sense_BibiteRed` | BEHAV | Red color of nearest bibite |
| `Sense_BibiteGreen` | BEHAV | Green color of nearest bibite |
| `Sense_BibiteBlue` | BEHAV | Blue color of nearest bibite |

#### Clock Sensors (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_Tic` | BEHAV | Rapid clock pulse (0 or 1) |
| `Sense_Minute` | BEHAV | Slow counter (0-1 over 60s) |
| `Sense_TimeAlive` | BIO | Normalized age |

#### Pheromone Sensors (BEHAVIOURAL)
| Node Name | Affinity | Description |
|-----------|----------|-------------|
| `Sense_Phero1Intensity` | BEHAV | Red pheromone strength |
| `Sense_Phero1Angle` | BEHAV | Red pheromone direction |
| `Sense_Phero1Heading` | BEHAV | Red pheromone trail heading |
| `Sense_Phero2Intensity` | BEHAV | Green pheromone strength |
| `Sense_Phero2Angle` | BEHAV | Green pheromone direction |
| `Sense_Phero2Heading` | BEHAV | Green pheromone trail heading |
| `Sense_Phero3Intensity` | BEHAV | Blue pheromone strength |
| `Sense_Phero3Angle` | BEHAV | Blue pheromone direction |
| `Sense_Phero3Heading` | BEHAV | Blue pheromone trail heading |

---

### Output Nodes (Affinity: BEHAVIOURAL)
Action commands that modules read. **Activation functions are FIXED.**

| Node Name | ActFunc | Default Bias | Description |
|-----------|---------|--------------|-------------|
| `Output_Accelerate` | TanH | 0.45 | Forward/backward thrust |
| `Output_Rotate` | TanH | 0.0 | Turn left/right |
| `Output_Herding` | TanH | 0.0 | Herding behaviour blend |
| `Output_EggProduction` | TanH | 0.2 | Egg production rate |
| `Output_Want2Lay` | Sigmoid | 0.0 | Lay eggs trigger |
| `Output_Want2Eat` | TanH | 1.23 | Swallow/vomit |
| `Output_Digestion` | Sigmoid | -2.07 | Digestion speed |
| `Output_Grab` | TanH | 0.0 | Grab/throw |
| `Output_Want2Attack` | Sigmoid | 0.0 | Bite strength |
| `Output_Want2Grow` | Sigmoid | 0.0 | Growth rate multiplier |
| `Output_ClkReset` | Sigmoid | 0.0 | Reset clock trigger |
| `Output_PhereOut1` | ReLU | 0.0 | Red pheromone emission |
| `Output_PhereOut2` | ReLU | 0.0 | Green pheromone emission |
| `Output_PhereOut3` | ReLU | 0.0 | Blue pheromone emission |
| `Output_Want2Heal` | Sigmoid | 0.0 | Healing rate |

---

### Hidden Node Activation Functions
Hidden nodes can use any of these; activation function can mutate:

| Function | Behaviour | Output Range | Default Bias |
|----------|-----------|--------------|--------------|
| **Sigmoid** | Logistic function | 0 to 1 | 0.0 |
| **Linear** | Identity sum | -∞ to ∞ | 0.0 |
| **TanH** | Hyperbolic tangent | -1 to 1 | 0.0 |
| **Sine** | sin(x) | -1 to 1 | 0.0 |
| **ReLU** | max(0, x) | 0 to ∞ | 0.0 |
| **Gaussian** | 1/(x²+1) | 0 to 1 | 0.0 |
| **Latch** | Binary memory | 0 to 1 | 0.0 |
| **Differential** | Rate of change | -∞ to ∞ | 0.0 |
| **Abs** | Absolute value | 0 to ∞ | 0.0 |
| **Mult** | Product of inputs | 0 to 1 | 1.0 |
| **Integrator** | Accumulator | -∞ to ∞ | 0.0 |
| **Inhibitory** | Self-decaying | -∞ to ∞ | 1.0 |
| **SoftLatch** | Hysteresis | 0 to 1 | 5.0 |

---

## Module System

Modules are **thin wrappers around hardcoded physics**. They expose input and output nodes. Configuration comes through **input nodes** that can be connected to any other node (typically Gene nodes for static config, but could be Behavioural nodes for dynamic control).

**Critical insight from Leo's diagram:** Module configuration (like Period, ViewRadius) comes through INPUT NODES, not hardcoded parameters. These input nodes can be connected to Gene nodes (static) OR other node types (dynamic). Evolution decides the wiring!

### Module Types

#### INPUT Modules (Sensors)
- Expose **output nodes** (sensor values)
- May have **input nodes** for configuration
- Hardcoded logic reads from environment/state

#### OUTPUT Modules (Actuators)
- Expose **input nodes** (commands)
- Hardcoded logic applies forces/actions

#### FUNCTIONAL Modules
- Expose both input AND output nodes
- Have internal state and deterministic logic
- Examples: Clock (accumulator), Memory (latch)

#### META Modules
- Created by Modularization Mutation
- Have internal subnetwork that can evolve
- All instances share the same template

### Base Module Set

#### Gene Nodes (Not a Module)
Gene nodes are just nodes with GENETIC affinity that exist in the network:
- Bias = gene value
- Output = bias (via Identity activation)
- Never change during lifetime
- Can be connected to module input nodes for static configuration

#### ClockModule (FUNCTIONAL)
Based on Leo's original diagram:

**Input Nodes:**
- `En` — Enable (when > 0, clock runs)
- `Period` — Oscillation period

**Output Nodes:**
- `Clk` — Pulse output (0 or 1)
- `Counter` — Counts cycles

**Internal State:** `val` (accumulator)

**Logic (from Leo's diagram):**
```
if (En > 0): val += deltaTime
Clk = val > Period
if (val > Period):
    Clk = 1
    Counter += 1
    val -= Period
else:
    Clk = 0
```

**Typical wiring:**
```
Gene_ClockPeriod → Clock.Period (static period)
Constant_1 → Clock.En (always enabled)
```

#### VisionModule (INPUT)
**Input Nodes (configuration):**
- `ViewRadius` — Maximum sight distance
- `ViewAngle` — Field of view

**Output Nodes (sensor values):**
- `PlantCloseness`, `PlantAngle`, `NPlants`
- `MeatCloseness`, `MeatAngle`, `NMeats`
- `BibiteCloseness`, `BibiteAngle`, `NBibites`, `BibiteRed/Green/Blue`

**Logic:** Spatial query within radius/angle, aggregate results

**Typical wiring:**
```
Gene_ViewRadius → Vision.ViewRadius
Gene_ViewAngle → Vision.ViewAngle
```

#### BodyStateModule (FUNCTIONAL)
**Input Nodes:** WAG gene connections for capacity calculations

**Output Nodes:**
- `EnergyRatio`, `LifeRatio`, `Fullness`, `Maturity`, `FatRatio`

**Internal State:** energy, health, fat, stomach contents

**Logic:** Metabolism simulation

#### MovementModule (OUTPUT)
**Input Nodes:**
- `Move` — Forward/backward acceleration
- `Rotation` — Turn rate
- `Herding` — Herding behavior blend

**Logic:** Apply physics forces (from Leo's diagram: `Agent.Velocity.Linear += Forward * Move.value * dt`)

#### FeedingModule (OUTPUT + FUNCTIONAL)
**Input Nodes:**
- `Want2Eat` — Swallow/vomit
- `Digestion` — Digestion speed
- `Want2Attack` — Bite strength

**Logic:** Bite/swallow physics

#### ReproductionModule (OUTPUT + FUNCTIONAL)
**Input Nodes:**
- `EggProduction` — Egg production rate
- `Want2Lay` — Lay eggs trigger

**Output Nodes:**
- `EggStored` — Number of eggs ready

**Logic:** Egg production/laying

#### PheromoneModule (INPUT + OUTPUT)
**Input Nodes (config):**
- `PheromoneRadius` — Sensing radius

**Input Nodes (control):**
- `PhereOut1/2/3` — Emission rates

**Output Nodes:**
- `Phero1Intensity/Angle/Heading` (×3 channels)

**Logic:** Grid sampling for sensing, grid writing for emission

---

## Starter Brain Configuration

New bibites start with a minimal but functional brain:

### Instantiated Nodes at Birth
**Gene Nodes (all essential):**
- All appearance, size, metabolism, reproduction genes
- All WAG genes
- Vision, clock, pheromone genes

**Input Nodes (sparse subset):**
- Sense_PlantCloseness
- Sense_PlantAngle
- Sense_Fullness
- Sense_EnergyRatio

**Output Nodes (all, but most unconnected):**
- Output_Accelerate (connected)
- Output_Rotate (connected)
- Output_Digestion (connected)
- Output_Want2Eat (unconnected - uses default bias)
- Output_Want2Attack (unconnected)
- Output_Want2Lay (unconnected)
- Output_Want2Grow (unconnected)
- Output_Want2Heal (unconnected)
- Output_EggProduction (unconnected)
- Output_ClkReset (unconnected)
- Output_PhereOut1/2/3 (unconnected)

### Seed Connections (Bibites-faithful)
```
Sense_PlantAngle → Output_Rotate (weight: +1.0)
Sense_PlantCloseness → Output_Accelerate (weight: -1.0)
Sense_Fullness → Output_Digestion (weight: +1.0)
```

This creates the basic "food seeker" behaviour:
- Turn toward plants
- Move faster when plants are far (low closeness = negative × negative = positive)
- Digest faster when stomach is full

Unconnected outputs use their default bias through their fixed activation function.

---

## Mutation System

### 1. Weight Mutation
- Select random enabled connection
- Perturb weight: `weight += N(0, variance)`
- Variance scaled by Gene_BrainMutationVariance

### 2. Bias Mutation
- Select random node
- Perturb bias: `bias += N(0, variance)`
- For Gene nodes: this changes the gene value
- Variance scaled by Gene_GeneMutationVariance

### 3. Add Connection Mutation
- Select two nodes (respecting affinity priors)
- Add new connection with random weight
- Check for duplicate connections

### 4. Add Hidden Node (NEAT Split)
- Select random enabled connection A→B
- Disable A→B
- Create new hidden node H
- Add A→H (weight 1.0)
- Add H→B (weight = old weight)
- Hidden node gets random activation function

### 5. Add Interface Node Mutation
- Select a node type from the catalogue not yet instantiated
- Instantiate that node
- For sensors: module will start populating it
- For outputs: starts at default bias
- New node can be wired by future mutations

### 6. Module Duplication Mutation
- Select an existing module instance (e.g., ClockModule)
- Duplicate it (creates second clock with its own nodes)
- The duplicate starts with same wiring as original
- Allows evolution of multiple clocks, multiple sensors, etc.

### 7. Modularization Mutation (META-module creation)
- Select a subgraph of nodes and connections
- Package as a META-module template
- Replace original with instance of that template
- Template mutations affect all instances
- Allows reusable circuits to evolve

---

## Energy Costs

### Node Costs
- Each node adds small upkeep: `0.001 * E/frame`
- Stateful nodes (Latch, Integrator) cost more: `0.002 * E/frame`

### Connection Costs  
- Each enabled connection: `0.0005 * |weight| * E/frame`
- Disabled connections: free

### Module Costs
- Each active module: base cost + complexity
- Modules can be disabled to save energy
- Disabled modules don't update or cost energy

### Energy Gating
If upkeep exceeds available energy:
1. Disable lowest-priority modules first
2. Outputs fall back to default bias
3. Prevents death spiral from brain complexity

---

## Implementation Phases

### Phase 1: Core Data Structures (Week 1-2)
- [ ] BiomeNode struct with all fields
- [ ] BiomeConnection struct
- [ ] BiomeModule base class
- [ ] Node catalogue (static definitions)
- [ ] Affinity update system (different tick rates)
- [ ] Connection effectiveness matrix

### Phase 2: Gene System as Nodes (Week 2-3)
- [ ] GeneBank module
- [ ] All Gene nodes from catalogue
- [ ] Gene node initialization (bias = value)
- [ ] WAG calculation from Gene nodes
- [ ] Remove old gene struct entirely

### Phase 3: Input Modules (Week 3-4)
- [ ] VisionModule (reads Gene_ViewRadius/Angle)
- [ ] BodyStateModule (reads WAG genes)
- [ ] ClockModule (reads Gene_ClockPeriod)
- [ ] PheromoneModule sensing (reads Gene_PheromoneRadius)
- [ ] All sensor nodes instantiation

### Phase 4: Output Modules (Week 4-5)
- [ ] MovementModule (reads herding/muscle genes)
- [ ] FeedingModule (reads throat/jaw genes)
- [ ] ReproductionModule (reads LayTime/EggOrgan genes)
- [ ] PheromoneModule emission
- [ ] All output nodes with fixed activation functions

### Phase 5: Network Processing (Week 5-6)
- [ ] Affinity-based update timing
- [ ] Connection propagation with effectiveness scaling
- [ ] Activation function application
- [ ] Stateful function implementation (Latch, Differential, etc.)
- [ ] Input module → Network → Output module pipeline

### Phase 6: Mutation System (Week 6-8)
- [ ] Weight mutation
- [ ] Bias mutation (genes and neurons)
- [ ] Add connection (with affinity priors)
- [ ] Add hidden node (NEAT split)
- [ ] Add interface node from catalogue
- [ ] Module duplication
- [ ] Basic testing of evolution

### Phase 7: META-modules (Week 8-10)
- [ ] META-module template structure
- [ ] Modularization mutation
- [ ] Template inheritance
- [ ] Instance updates from template mutations

### Phase 8: Energy & Polish (Week 10-12)
- [ ] Node/connection energy costs
- [ ] Module energy costs
- [ ] Energy gating system
- [ ] Performance optimization
- [ ] UI for brain visualization

---

## File Structure

```
Assets/Scripts/BIOME/
├── Core/
│   ├── BiomeNode.cs           # Node struct and catalogue
│   ├── BiomeConnection.cs     # Connection struct
│   ├── BiomeNetwork.cs        # Main network class
│   ├── AffinitySystem.cs      # Update timing, effectiveness matrix
│   └── ActivationFunctions.cs # All activation function implementations
├── Modules/
│   ├── BiomeModule.cs         # Base module class
│   ├── GeneBank.cs            # Gene node management
│   ├── VisionModule.cs        # Vision sensing
│   ├── BodyStateModule.cs     # Metabolism/organs
│   ├── ClockModule.cs         # Internal timer
│   ├── MovementModule.cs      # Movement actuator
│   ├── FeedingModule.cs       # Eating/biting
│   ├── ReproductionModule.cs  # Egg production
│   └── PheromoneModule.cs     # Pheromone I/O
├── Mutations/
│   ├── MutationOperators.cs   # All mutation implementations
│   ├── MutationConfig.cs      # Rates, priors, constraints
│   └── InnovationTracker.cs   # NEAT innovation numbers (optional)
├── Meta/
│   ├── MetaModuleTemplate.cs  # META-module templates
│   └── ModularizationMutation.cs
└── Catalogue/
    ├── NodeCatalogue.cs       # All possible node types
    └── StarterBrain.cs        # Default brain configuration
```

---

## Migration from Current Code

### Files to DELETE (completely)
- `InputNeurons`, `OutputNeurons` static classes
- Old `BiomeBrain.cs` (replace entirely)
- `BrainPresets.cs`
- Any file with "module parameters"

### Files to HEAVILY MODIFY
- `SimulationBootstrap.cs` - use new network creation
- `BiomeBrainSystem.cs` - new processing pipeline
- All existing module files - rewrite to read from Gene nodes

### Concepts to ELIMINATE
- `InputBuffer`, `OutputBuffer` arrays
- Module node IDs separate from brain node IDs
- Any `const int XXX_OFFSET = ...` patterns
- Gene struct / gene list on bibites

---

## Success Criteria

1. **All genes are nodes** - No separate gene storage
2. **Modules have no parameters** - All config from Gene nodes
3. **Sparse instantiation** - Bibites don't have all nodes
4. **Catalogue exists** - Mutations can add new node types
5. **Affinity system works** - Different update rates, effectiveness scaling
6. **Evolution produces diverse behaviours** - Not just weight changes
7. **META-modules can form** - Reusable circuits emerge
8. **Energy costs matter** - Complex brains have fitness cost

---

## References

- Original Bibites readme: `/home/claude/Reference/readme.md`
- BIOME algorithm document: `/mnt/user-data/outputs/BIOME_Algorithm_Explained.md`
- ChatGPT BIOME prompt: (in conversation)
