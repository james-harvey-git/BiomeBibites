using Unity.Entities;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Initializes reproduction components on bibites that don't have them.
    /// Ensures backwards compatibility with existing bibites.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ReproductionInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (tag, entity) 
                in SystemAPI.Query<RefRO<BibiteTag>>()
                .WithNone<ReproductionState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new ReproductionState
                {
                    EggProgress = 0f,
                    EggsStored = 0,
                    ReadyToLay = false
                });
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Renders eggs in the simulation (adds to renderer)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EggVisualizationSystem : ISystem
    {
        // Eggs are rendered by SimulationRenderer if it queries for Egg components
        // This system could add visual effects like pulsing as eggs near hatching
        
        public void OnUpdate(ref SystemState state)
        {
            // Future: Add egg animation/effects
        }
    }
}
