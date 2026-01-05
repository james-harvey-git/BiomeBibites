# The BIOME Algorithm: A Complete Technical Reference (v3)

## What is BIOME?

**BIOME** stands for **B**iologically-**I**nspired **O**pen-ended **M**odular **E**volution.

It is a complete evolutionary algorithm designed by Leo Caussan to replace the current rt-NEAT-based system in The Bibites simulation. The name is not related to ecological biomes (forests, deserts, etc.) — it's an acronym.

### Why Replace rt-NEAT?

The current system has fundamental limitations:
1. **Genes and brain are separate** — they're inherited separately, mutated differently, and don't interact
2. **Fixed I/O** — bibites start with ALL input/output nodes connected to predetermined senses, preventing them from evolving new senses or multiple instances of the same capability (e.g., multiple internal clocks)
3. **No modularity** — can't evolve reusable structures
4. **Performance issues** — the implementation limits population size and biodiversity

BIOME aims to solve all of these by unifying everything into one evolvable structure.

---

## The Core Principles

### Principle 1: NO Separate Genes Structure
In BIOME, there is no separate "gene list" or "genome struct". **ALL genes are nodes** in the network. A gene is simply a node with high Genetic affinity whose bias represents the gene value.

**Wrong approach:**
```csharp
bibite.Genes.Diet = 0.3f;
bibite.Genes.ViewRadius = 100f;
```

**Correct BIOME approach:**
```csharp
// Diet is a node with Genetic affinity
Node { Name="Diet", Affinity=GENETIC, Bias=0.3f, Output=0.3f }
// ViewRadius is a node with Genetic affinity  
Node { Name="ViewRadius", Affinity=GENETIC, Bias=100f, Output=100f }
```

### Principle 2: NO Module Parameters — Configuration via Input Nodes
Modules do NOT have hardcoded configuration parameters. Instead, modules expose **input nodes** for any configurable value. These input nodes can be connected to **any other node** in the network.

**Wrong approach:**
```csharp
ClockModule { Period = 1.0f }  // Hardcoded parameter on module - WRONG!
```

**Correct BIOME approach:**
```csharp
// Clock Module has "Period" as an INPUT NODE
ClockModule {
    InputNodes: [En, Period]
    OutputNodes: [Clk, Counter]
    Logic: if (En > 0) val += dt; Clk = val > Period.output
}

// A Gene node exists in the network
Node { Name="Gene_ClockPeriod", Affinity=GENETIC, Bias=1.0f }

// A CONNECTION links the Gene node to the module's input node
Connection { From=Gene_ClockPeriod, To=Clock.Period, Weight=1.0 }
```

**Why this is more powerful:**
- Connecting Period to a Gene node = static configuration (like traditional genes)
- Connecting Period to a Behavioural node = dynamic, controllable period!
- Evolution decides the wiring — a species could evolve to change its clock period based on hunger, danger, etc.

### Principle 3: Sensors/Actuators are Physics Hooks
Modules contain **hardcoded transduction logic** — the physics of how sensors work (raycasting) and how actuators work (applying forces). Evolution does NOT invent new physics. Evolution only controls:
- Which modules are instantiated
- How module nodes are wired together
- What nodes feed into module configuration inputs

### Principle 4: Sparse Instantiation
Not every bibite has every possible node or module. The simulation maintains a **catalogue** of all possible node types. Each bibite instantiates only a sparse subset. Mutations can add new nodes from the catalogue.

### Principle 5: Fixed Output Activation Functions
Output node activation functions are determined by their type and **cannot mutate**. This is faithful to the original Bibites. Hidden node activation functions CAN mutate.

---

## The Three Components

BIOME networks consist of exactly three types of components:

### 1. Nodes

**Nodes are the fundamental computing units.** They replace both genes AND neurons.

Each node has:
- **id** — unique identifier
- **name** — human-readable name
- **affinity** — GENETIC, BIOLOGICAL, or BEHAVIOURAL
- **activation_function** — how to process the activation
- **bias** — default/baseline value (for genes: this IS the gene value)
- **activation_value** — accumulated input stimulation this tick
- **output_value** — the result: `ActFunc(activation_value + bias)`
- **previous_output** — for stateful functions (Latch, Differential, Integrator)

#### Node Affinities

Every node has one of three affinities that determines its update rate:

| Affinity | Update Speed | Purpose |
|----------|-------------|---------|
| **GENETIC** | Never (fixed at birth) | Gene values — inherited traits |
| **BIOLOGICAL** | Slow (~1-5 Hz) | Internal state — growth, hunger, health |
| **BEHAVIOURAL** | Every frame (~60 Hz) | Fast control — sensors, motors, neurons |

**Key insight:** A "gene" is just a node with GENETIC affinity. A "neuron" is just a node with BEHAVIOURAL affinity. They exist in the same network and can connect to each other!

**Examples:**
- `Gene_Diet` — GENETIC affinity, bias=0.3, output always equals bias
- `Sense_Fullness` — BIOLOGICAL affinity, updated by metabolism system
- `Output_Accelerate` — BEHAVIOURAL affinity, computed every frame

### 2. Connections

**Connections link nodes together**, allowing one node's output to influence another node's activation.

Each connection has:
- **from_node** — the source node ID
- **to_node** — the destination node ID
- **weight** — how strongly the signal is scaled
- **enabled** — can be toggled on/off

#### Connection Effectiveness Matrix

Connections between nodes of different affinities have different effectiveness. This enforces biological realism:

| From ↓ To → | Genetic | Biological | Behavioural |
|-------------|---------|------------|-------------|
| **Genetic** | 1.0 | 0.8 | 0.3 |
| **Biological** | 0.05 | 1.0 | 1.0 |
| **Behavioural** | 0.01 | 0.3 | 1.0 |

**Why?**
- Genes strongly influence biology and weakly influence behavior directly
- Biology cannot easily change genes (blocks Lamarckism)
- Behavior cannot change genes (your thoughts don't rewrite your DNA)
- Biology strongly influences behavior (hormones affect mood)
- Behavior weakly influences biology (stress affects health, but slowly)

#### Mutation Priors

When mutations add new connections, they should favor biologically plausible wiring:
- **High probability:** Genetic→Biological, Biological→Behavioural, Behavioural→Behavioural
- **Medium probability:** Genetic→Genetic, Biological→Biological
- **Low probability:** Behavioural→Biological (feedback loops)
- **Very low probability:** Anything→Genetic

### 3. Modules

**Modules are thin wrappers around hardcoded physics.** They encapsulate sensor/actuator logic that cannot be represented by simple node activation functions.

**Critical:** Modules do NOT have hardcoded parameters. They expose **input nodes** for any configuration, which can be connected to any other node in the network.

Each module:
- **Exposes input nodes** for configuration and control (can connect to Gene nodes for static config, or Behavioural nodes for dynamic control)
- **Exposes output nodes** that provide sensor values or state
- Contains **hardcoded logic** for physics transduction
- Can be **enabled/disabled** for energy gating

#### Module Types

**INPUT Modules (Sensors):**
- Expose OUTPUT nodes (the sensor values they provide)
- May have INPUT nodes for configuration (like ViewRadius, ViewAngle)
- Contain hardcoded sensing logic (raycasts, spatial queries)
- Example: VisionModule exposes PlantCloseness, PlantAngle outputs; has ViewRadius, ViewAngle inputs

**OUTPUT Modules (Actuators):**
- Expose INPUT nodes (the commands they receive)
- Contain hardcoded action logic (force application)
- Example: MovementModule has Move, Rotation inputs; applies physics forces

**FUNCTIONAL Modules:**
- Expose both input AND output nodes
- Have internal state (accumulators, memory)
- Perform logic too complex for activation functions
- Example: ClockModule has En, Period inputs; Clk, Counter outputs

**META Modules (Evolved):**
- Created by the Modularization Mutation
- Have an internal subnetwork of nodes and connections
- All instances share the same template
- Template mutations affect all instances

---

## How Modules Work (Detailed)

### Example: Internal Clock Module (from Leo's diagram)

**Input nodes (exposed by module):**
- `En` (Enable) — when > 0, clock accumulates time
- `Period` — determines oscillation period

**Output nodes (exposed by module):**
- `Clk` — rapid pulse (0 or 1)
- `Counter` — counts number of clock cycles

**Internal state:**
- `val` — time accumulator

**Hardcoded logic (from diagram):**
```
var val;
if (En > 0): val += deltaTime;
Clk = val > Period;

if (val > Period):
    Clk = 1
    Counter += 1
    val -= Period
else:
    Clk = 0;
```

**How configuration works:**
- The `Period` input node can be connected to ANY node in the network
- Connect to a Gene node → static period (traditional gene behavior)
- Connect to a Behavioural node → dynamic, controllable period!
- Evolution decides the wiring

**Typical starter wiring:**
```
Gene_ClockPeriod (GENETIC, bias=1.0) --connection--> Clock.Period
Constant_1 (GENETIC, bias=1.0) --connection--> Clock.En
```

### Example: Vision Module

**Input nodes (for configuration):**
- `ViewRadius` — maximum sight distance
- `ViewAngle` — field of view

**Output nodes (sensor values):**
- `PlantCloseness` — proximity to nearest plant (0-1)
- `PlantAngle` — direction to nearest plant (-1 to 1)
- `NPlants` — count of visible plants
- (Similar for Meat, Bibites)

**Hardcoded logic:**
```
Each frame:
1. radius = ViewRadius.output  // Read from input node
2. angle = ViewAngle.output    // Read from input node
3. visible_plants = SpatialQuery(position, radius, angle, PLANT)
4. nearest = FindNearest(visible_plants)
5. PlantCloseness.output = 1 - nearest.distance/radius
6. PlantAngle.output = nearest.angle / PI
7. NPlants.output = visible_plants.count / 4
```

**Typical starter wiring:**
```
Gene_ViewRadius (GENETIC, bias=100.0) --connection--> Vision.ViewRadius
Gene_ViewAngle (GENETIC, bias=2.0) --connection--> Vision.ViewAngle
```

### Example: Skin Color Module (from Leo's diagram)

**Input nodes:** None

**Output nodes:**
- `Color Red` — red pigment value
- `Color Green` — green pigment value  
- `Color Blue` — blue pigment value

**Hardcoded logic:** None — these are pure Gene nodes exposed through a module

This is essentially a container for Gene nodes that define appearance. The output nodes ARE the genes — their bias values determine the color.

---

## Network Topology

Based on Leo's original diagram, here's how BIOME networks are structured:

**Key insight from the diagram:** Nodes sit BETWEEN modules, connected by connections. Modules expose nodes at their boundaries.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           BIOME NETWORK                                     │
│                                                                             │
│  ┌──────────────────┐                      ┌──────────────────────────┐    │
│  │ Health Sense     │                      │ Internal Timer Module    │    │
│  │ Module           │                      │                          │    │
│  │ ┌──────────────┐ │      ┌──────┐        │ ┌───────┐     ┌───────┐ │    │
│  │ │   Health     │─┼──────│ node │───────►│ │ Reset │     │ Timer │─┼──► │
│  │ └──────────────┘ │      └──────┘        │ └───────┘     └───────┘ │    │
│  │                  │         ▲            │                          │    │
│  │ Health.value =   │         │            │ Timer.value += deltaTime │    │
│  │ Agent.Health/Max │         │            │ if (Reset>0): Timer = 0  │    │
│  └──────────────────┘         │            └──────────────────────────┘    │
│                               │                                             │
│  ┌──────────────────┐         │                                             │
│  │ Touch Sensor     │         │                                             │
│  │ Module           │      ┌──────┐                                         │
│  │ ┌──────────────┐ │      │ node │                                         │
│  │ │  isTouched   │─┼──────│      │────────────────────────────────────┘    │
│  │ └──────────────┘ │      └──────┘                                         │
│  │                  │                                                       │
│  │ isTouched.value =│                                                       │
│  │ trigger.contains │                                                       │
│  └──────────────────┘                                                       │
│                                                                             │
│  ═══════════════════════════════════════════════════════════════════════   │
│                                                                             │
│  ┌──────────────────────┐  ┌─────────────────────────┐  ┌────────────────┐ │
│  │ Internal Clock       │  │ Landmark Positioning    │  │ Skin Color     │ │
│  │ Module               │  │ Module                  │  │ Module         │ │
│  │                      │  │                         │  │                │ │
│  │ ►En          Clk►    │  │ ►Mark      Distance►    │  │    Color Red►  │ │
│  │ ►Period   Counter►   │  │          Orientation►   │  │   Color Green► │ │
│  │                      │  │                         │  │    Color Blue► │ │
│  │ if(En>0): val+=dt    │  │ if(Mark>0): place=pos   │  │                │ │
│  │ Clk = val > Period   │  │ Dist = (place-pos).len  │  │ (pure Gene     │ │
│  │ if(val>Period):      │  │ Orient = AngleTo(place) │  │  output nodes) │ │
│  │   Counter += 1       │  │                         │  │                │ │
│  │   val -= Period      │  │                         │  │                │ │
│  └──────────────────────┘  └─────────────────────────┘  └────────────────┘ │
│                                                                             │
│  Legend:  ► = Input node (receives connections)                             │
│           ►= Output node (sends connections)                                │
│           ──── = Connection between nodes                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Critical observations from Leo's diagram:**
1. **Nodes exist at module boundaries** — modules don't connect directly; they expose nodes
2. **Connections link nodes** — a connection goes from one node to another
3. **Hidden nodes can exist between modules** — see the `node` circles in the flow
4. **Modules have internal logic** — shown as text inside each module box
5. **Some modules are pure Gene containers** — like Skin Color Module with only outputs

---

## Module Tiers

Some modules can be **tiered**, meaning they start simple and can evolve more capabilities:

| Module | Tier 1 | Tier 2 | Tier 3 | Tier 4 |
|--------|--------|--------|--------|--------|
| **Vision** | Closeness only | + Angle | + Count | + Color (RGB) |
| **Clock** | Simple Tic | + Minute counter | + Reset input | + Multiple periods |

A bibite must first evolve a Tier 1 module, then mutations can upgrade it to higher tiers. This prevents bibites from suddenly evolving perfect vision — they must develop it incrementally.

---

## Evolution and Mutations

### 1. Bias Mutation
- Select a random node
- Perturb its bias: `bias += N(0, variance)`
- For Gene nodes: this changes the gene value!
- Variance controlled by `Gene_GeneMutationVariance`

### 2. Weight Mutation
- Select a random enabled connection
- Perturb its weight: `weight += N(0, variance)`
- Variance controlled by `Gene_BrainMutationVariance`

### 3. Add Connection Mutation
- Select two nodes (respecting affinity priors)
- Add new connection with random weight
- Check for duplicates

### 4. Add Hidden Node (NEAT Split)
- Select random enabled connection A→B
- Disable A→B
- Create new hidden node H (BEHAVIOURAL affinity)
- Add A→H (weight 1.0), H→B (weight = old weight)
- H gets random activation function

### 5. Add Interface Node Mutation
- Select a node type from catalogue not yet instantiated
- Create that node in the network
- For sensors: module will start populating it
- For outputs: starts outputting ActFunc(bias)
- Can be wired by future mutations

### 6. Module Duplication Mutation
- Select an existing module instance
- Duplicate it with all its nodes
- Allows multiple clocks, multiple vision systems, etc.

### 7. Modularization Mutation (META-module creation)
1. Select a subgraph of nodes and connections
2. Package as a META-module template
3. Replace original with instance of that template
4. Template mutations affect all instances
5. Allows reusable circuits to evolve

---

## Energy Costs

### Node Costs
- Each node adds upkeep: `~0.001 E/frame`
- Stateful nodes (Latch, Integrator) cost more: `~0.002 E/frame`

### Connection Costs
- Each enabled connection: `~0.0005 × |weight| E/frame`
- Disabled connections: free

### Module Costs
- Each active module: base cost + complexity
- Modules can be disabled to save energy
- Disabled modules don't update or consume energy

### Energy Gating
If upkeep exceeds available energy:
1. Disable lowest-priority modules first
2. Outputs fall back to default bias (ActFunc(bias))
3. Prevents death spiral from brain complexity

---

## Processing Order

Each frame, the BIOME network is processed:

1. **Gene nodes** output their bias (fixed, but available for reading)
2. **Input Modules** read environment/state and Gene nodes, set their output node values
3. **Connections propagate** (behavioural every frame, biological occasionally, genetic never)
4. **Nodes accumulate** activations from incoming connections
5. **Nodes apply** activation functions: `output = ActFunc(activation + bias)`
6. **Functional Modules** process their inputs (reading from network) and set their outputs
7. **Output Modules** read their input nodes and execute actions

---

## Starter Brain Configuration

New bibites start with a minimal but functional brain:

### Instantiated Nodes
**Gene Nodes:** All essential genes (Diet, Size, Metabolism, WAG, Vision, Clock, etc.)

**Input Nodes (sparse):**
- Sense_PlantCloseness
- Sense_PlantAngle
- Sense_Fullness

**Output Nodes (all, most unconnected):**
- Output_Accelerate, Output_Rotate, Output_Digestion (connected)
- Output_Want2Eat, Output_Want2Attack, Output_Want2Lay, etc. (unconnected)

### Seed Connections (Bibites-faithful)
```
Sense_PlantAngle      → Output_Rotate      (weight: +1.0)
Sense_PlantCloseness  → Output_Accelerate  (weight: -1.0)
Sense_Fullness        → Output_Digestion   (weight: +1.0)
```

This creates basic food-seeking behavior. Unconnected outputs use `ActFunc(bias)` as their default.

---

## Comparing Old vs BIOME

| Aspect | Old System | BIOME |
|--------|-----------|-------|
| Structure | Separate gene list + neural network | Single unified network |
| Genes | Fixed list of scalar values | Nodes with GENETIC affinity |
| Gene values | Stored in struct fields | Stored as node bias |
| Module config | Parameters on modules | Input nodes connected to Gene (or other) nodes |
| Config flexibility | Static, hardcoded | Evolvable — can be static OR dynamic |
| Neurons | Fixed input/output count | Dynamic, evolvable nodes |
| Senses | All bibites have all senses | Sparse — evolve what you need |
| Multiple clocks | Impossible | Duplicate ClockModule |
| Reusable circuits | Not possible | META-Modules via modularization |
| Gene-brain interaction | None | Genes are nodes, can connect to anything |

---

## Key Takeaways

1. **Unified representation**: Genes and neurons are the same thing (nodes) with different affinities
2. **No hardcoded module parameters**: Configuration comes via input nodes that can be connected to ANY node
3. **Evolvable configuration**: Connect config inputs to Gene nodes for static behavior, or Behavioural nodes for dynamic control
4. **Sparse instantiation**: Bibites don't start with everything
5. **Affinity system**: Controls update rates and connection effectiveness
6. **Modular evolution**: Duplicate modules, create META-modules
7. **Energy costs**: Complex brains have fitness costs
8. **Open-ended**: No fixed architecture — bibites can evolve entirely new subsystems

The goal is true open-ended evolution where bibites can develop arbitrarily complex structures over evolutionary time, rather than being constrained to a fixed topology.

---

## Implementation Checklist

For our Unity DOTS implementation:

- [ ] **BiomeNode** struct with id, name, affinity, activation, bias, output
- [ ] **BiomeConnection** struct with from, to, weight, enabled
- [ ] **BiomeModule** base class (exposes nodes, reads genes, hardcoded logic)
- [ ] **NodeCatalogue** — all possible node types
- [ ] **Affinity system** — different update rates, effectiveness matrix
- [ ] **Gene nodes** — all original Bibites genes as GENETIC nodes
- [ ] **Input modules** — Vision, BodyState, Clock, Pheromone (read Gene nodes!)
- [ ] **Output modules** — Movement, Feeding, Reproduction (read Gene nodes!)
- [ ] **All mutations** — bias, weight, add-connection, add-node, add-interface, duplication, modularization
- [ ] **Energy costs** — nodes, connections, modules
- [ ] **Starter brain** — seed connections for food-seeking
