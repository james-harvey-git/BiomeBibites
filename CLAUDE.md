# Claude Code Instructions

## Development Guidelines

### Changelog Requirements
- **Update CHANGELOG.md after each change made** - Every modification to the codebase should be documented in the changelog with a clear description of what was changed and why.

### Project Context
- This is a Unity DOTS implementation of The Bibites using the BIOME algorithm
- Read `Docs/CLAUDE_CODE_INTIAL_INSTRUCTIONS.md` for full project context
- Read `Docs/BIOME_Algorithm_Explained_v3.md` for algorithm details
- Read `Docs/BIOME_Implementation_Plan_v4.md` for implementation roadmap

### Key BIOME Principles (Never Violate)
1. **All genes are nodes** - No separate gene storage
2. **Module config via input nodes** - No hardcoded module parameters
3. **Sparse instantiation** - Bibites don't have all nodes
4. **Affinity system** - GENETIC/BIOLOGICAL/BEHAVIOURAL update rates
