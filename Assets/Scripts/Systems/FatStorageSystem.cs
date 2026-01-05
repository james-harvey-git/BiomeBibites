using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles fat storage with hysteresis.
    /// 
    /// When energy is high (above threshold + deadband), excess is stored as fat.
    /// When energy is low (below threshold - deadband), fat is converted to energy.
    /// The deadband prevents constant oscillation between storing and using fat.
    /// 
    /// This mimics biological fat regulation where bodies resist small changes.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DigestionSystem))]
    public partial struct FatStorageSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (energy, fatStorage, organs) 
                in SystemAPI.Query<
                    RefRW<Energy>,
                    RefRW<FatStorage>,
                    RefRO<Organs>>()
                .WithAll<BibiteTag>())
            {
                float energyRatio = energy.ValueRO.Current / energy.ValueRO.Maximum;
                float threshold = fatStorage.ValueRO.Threshold;
                float deadband = fatStorage.ValueRO.Deadband;
                
                // Calculate fat storage capacity based on FatReserve organ
                float maxFat = 20f + organs.ValueRO.FatReserve * 80f; // 20-100 max fat
                
                // Fat storage rate (FatReserve organ makes storage faster)
                float storageRate = (2f + organs.ValueRO.FatReserve * 8f) * deltaTime; // 2-10 per second
                
                // === HYSTERESIS LOGIC ===
                
                // Store fat when energy is high (above threshold + deadband)
                if (energyRatio > threshold + deadband)
                {
                    if (fatStorage.ValueRO.Current < maxFat)
                    {
                        // Calculate how much excess energy to store
                        float excessEnergy = energy.ValueRO.Current - (energy.ValueRO.Maximum * threshold);
                        float toStore = math.min(excessEnergy, storageRate);
                        toStore = math.min(toStore, maxFat - fatStorage.ValueRO.Current);
                        
                        if (toStore > 0.01f)
                        {
                            // Store fat (85% efficient - some energy lost)
                            fatStorage.ValueRW.Current += toStore * 0.85f;
                            energy.ValueRW.Current -= toStore;
                        }
                    }
                }
                // Use fat when energy is low (below threshold - deadband)
                else if (energyRatio < threshold - deadband)
                {
                    if (fatStorage.ValueRO.Current > 0f)
                    {
                        // Calculate energy deficit
                        float deficit = (energy.ValueRO.Maximum * threshold) - energy.ValueRO.Current;
                        float toUse = math.min(deficit, storageRate);
                        toUse = math.min(toUse, fatStorage.ValueRO.Current);
                        
                        if (toUse > 0.01f)
                        {
                            // Convert fat to energy (90% efficient)
                            energy.ValueRW.Current += toUse * 0.9f;
                            fatStorage.ValueRW.Current -= toUse;
                        }
                    }
                }
                // In the deadband range: do nothing (hysteresis prevents oscillation)
                
                // Clamp values
                fatStorage.ValueRW.Current = math.clamp(fatStorage.ValueRO.Current, 0f, maxFat);
                energy.ValueRW.Current = math.clamp(energy.ValueRO.Current, 0f, energy.ValueRO.Maximum);
            }
        }
    }
    
    /// <summary>
    /// System to add FatStorage component to bibites that don't have it.
    /// This ensures backwards compatibility with existing bibites.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct FatStorageInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (organs, entity) 
                in SystemAPI.Query<RefRO<Organs>>()
                .WithAll<BibiteTag>()
                .WithNone<FatStorage>()
                .WithEntityAccess())
            {
                // Initialize fat storage based on FatReserve organ
                ecb.AddComponent(entity, new FatStorage
                {
                    Current = 0f,
                    Threshold = 0.7f, // Start storing at 70% energy
                    Deadband = 0.1f   // 10% deadband (store above 80%, use below 60%)
                });
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// System to add StomachContents component to bibites that don't have it.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct StomachInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (organs, entity) 
                in SystemAPI.Query<RefRO<Organs>>()
                .WithAll<BibiteTag>()
                .WithNone<StomachContents>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new StomachContents
                {
                    PlantMatter = 0f,
                    MeatMatter = 0f,
                    DigestProgress = 0f
                });
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
