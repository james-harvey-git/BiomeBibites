# BiomeBibites

A Unity DOTS implementation of The Bibites artificial life simulation, rebuilt using the **BIOME** (Biologically-Inspired Open-ended Modular Evolution) algorithm.

## Overview

This project aims to recreate [The Bibites](https://leocaussan.itch.io/the-bibites) simulation by Leo Caussan, but with the next-generation BIOME evolutionary algorithm that he designed to replace the original rt-NEAT system.

**Key difference from original Bibites:** In BIOME, genes and neurons are unified into a single evolvable network. A "gene" is just a node with high genetic affinity, and a "neuron" is just a node with high behavioral affinity. They can connect to each other!

## Project Status

🔄 **Architecture Redesign in Progress**

We're currently implementing the true BIOME architecture based on Leo's original design documents and diagrams. The core ECS systems (movement, metabolism, reproduction, etc.) are working, but the brain/evolution system needs rewriting to be BIOME-faithful.

## BIOME Algorithm

BIOME stands for **B**iologically-**I**nspired **O**pen-ended **M**odular **E**volution.

### Core Principles

1. **NO separate genes** - All genes are nodes in the network with GENETIC affinity
2. **NO module parameters** - Configuration comes through input nodes that can connect to any other node
3. **Sparse instantiation** - Bibites only have the nodes/modules they need; mutations add more
4. **Affinity system** - Nodes have Genetic/Biological/Behavioral affinities that control update rates and connection effectiveness

### Three Components

1. **Nodes** - Fundamental computing units (replace both genes AND neurons)
   - Have bias (the "gene value"), activation function, and affinity
   - GENETIC nodes: Fixed at birth, output = bias
   - BIOLOGICAL nodes: Update slowly (~1-5 Hz)
   - BEHAVIOURAL nodes: Update every frame (~60 Hz)

2. **Connections** - Link nodes together
   - Have weight and enabled/disabled state
   - Effectiveness scaled by affinity compatibility

3. **Modules** - Wrappers around hardcoded physics
   - Expose input nodes (for configuration/commands)
   - Expose output nodes (for sensor values)
   - Contain hardcoded logic (raycasting, force application, etc.)

### Key Innovation

Module configuration (like clock period, view radius) comes through **input nodes** that can be connected to:
- **Gene nodes** → Static configuration (traditional gene behavior)
- **Behavioural nodes** → Dynamic, controllable configuration!

Evolution decides the wiring. A species could evolve to change its clock period based on hunger, or its vision radius based on energy level.

## Documentation

| Document | Description |
|----------|-------------|
| `BIOME_Algorithm_Explained_v3.md` | Complete technical reference for how BIOME works |
| `BIOME_Implementation_Plan_v4.md` | Detailed implementation plan with phases |
| `Original_Bibites_Reference.md` | Original Bibites simulation mechanics |
| `CHANGELOG.md` | Development history and current status |
| `CLAUDE_CODE_INSTRUCTIONS.md` | Instructions for AI-assisted development |

## Project Structure

```
BiomeBibites/
├── Assets/
│   ├── Scripts/
│   │   ├── BIOME/           # BIOME algorithm implementation
│   │   │   ├── Core/        # Node, Connection, Network
│   │   │   ├── Modules/     # Clock, Vision, Movement, etc.
│   │   │   ├── Mutations/   # All 7 mutation types
│   │   │   └── Catalogue/   # Node type definitions
│   │   ├── Components/      # ECS components
│   │   ├── Systems/         # ECS systems
│   │   └── Core/            # Bootstrap, Renderer, UI
│   ├── Scenes/
│   └── Shaders/
└── Docs/                    # Reference documents
```

## Technology

- **Unity 6** (6000.0.25f1)
- **Unity DOTS** (Entities 1.3.5)
- **Burst Compiler** for performance
- **GL-based rendering** for thousands of entities

## Building

1. Open project in Unity 6
2. Open `Assets/Scenes/SimulationScene.unity`
3. Press Play

## Contributing

This is primarily an AI-assisted development project using Claude. See `CLAUDE_CODE_INSTRUCTIONS.md` for context on the development approach.

## References

- [The Bibites](https://leocaussan.itch.io/the-bibites) - Original simulation by Leo Caussan
- [BIOME Algorithm Discord Posts](https://discord.gg/thebibites) - Leo's explanations of BIOME
- [rt-NEAT](http://nn.cs.utexas.edu/downloads/papers/stanley.ec02.pdf) - Original neural evolution algorithm

## License

This is a fan recreation for educational purposes. The Bibites is created by Leo Caussan.
