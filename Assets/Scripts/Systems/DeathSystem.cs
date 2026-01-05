using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles death and conversion to meat pellets.
    /// Properly accounts for all stored energy (current + fat + stomach contents).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct DeathSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float biomassToReturn = 0f;

            foreach (var (health, energy, position, radius, size, entity) 
                in SystemAPI.Query<
                    RefRO<Health>,
                    RefRO<Energy>,
                    RefRO<Position>,
                    RefRO<Radius>,
                    RefRO<Size>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                // Check if dead
                if (health.ValueRO.Current > 0f) continue;
                
                // Calculate total energy stored in the bibite
                float totalEnergy = energy.ValueRO.Current;
                
                // Add fat storage if present
                if (state.EntityManager.HasComponent<FatStorage>(entity))
                {
                    var fat = state.EntityManager.GetComponentData<FatStorage>(entity);
                    totalEnergy += fat.Current;
                }
                
                // Add stomach contents if present
                if (state.EntityManager.HasComponent<StomachContents>(entity))
                {
                    var stomach = state.EntityManager.GetComponentData<StomachContents>(entity);
                    totalEnergy += stomach.PlantMatter + stomach.MeatMatter;
                }
                
                // Add "body mass" energy (proportional to size and max energy)
                float bodyMassEnergy = energy.ValueRO.Maximum * 0.3f * size.ValueRO.Ratio;
                totalEnergy += bodyMassEnergy;
                
                // Create meat pellet(s) at death location
                // Large bibites may create multiple smaller pellets
                float meatEnergy = totalEnergy * 0.7f; // 70% becomes meat, 30% lost
                float lostEnergy = totalEnergy * 0.3f;
                biomassToReturn += lostEnergy;
                
                // Create meat pellet
                if (meatEnergy > 5f)
                {
                    var meatEntity = ecb.CreateEntity();
                    ecb.AddComponent(meatEntity, new MeatPellet 
                    { 
                        Energy = meatEnergy,
                        DecayTimer = 90f // 90 seconds to decay
                    });
                    ecb.AddComponent(meatEntity, new Position { Value = position.ValueRO.Value });
                    
                    // Meat pellet size based on energy content
                    float meatRadius = math.clamp(2f + meatEnergy * 0.05f, 2f, 8f);
                    ecb.AddComponent(meatEntity, new Radius { Value = meatRadius });
                    ecb.AddSharedComponent(meatEntity, new EntityType { Value = EntityTypeEnum.MeatPellet });
                }
                else
                {
                    // Too small for meat - return all to biomass
                    biomassToReturn += meatEnergy;
                }
                
                // Destroy the bibite
                ecb.DestroyEntity(entity);
            }
            
            // Return lost energy to biomass
            if (biomassToReturn > 0f)
            {
                var newSettings = worldSettings;
                newSettings.FreeBiomass += biomassToReturn;
                SystemAPI.SetSingleton(newSettings);
            }
        }
    }
    
    /// <summary>
    /// Handles meat pellet decay over time
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MeatDecaySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float biomassToReturn = 0f;

            foreach (var (meat, entity) 
                in SystemAPI.Query<RefRW<MeatPellet>>()
                .WithEntityAccess())
            {
                meat.ValueRW.DecayTimer -= deltaTime;
                
                // Gradual energy loss during decay
                float decayLoss = meat.ValueRO.Energy * 0.005f * deltaTime; // 0.5% per second
                meat.ValueRW.Energy -= decayLoss;
                biomassToReturn += decayLoss;
                
                // Destroy if fully decayed
                if (meat.ValueRO.DecayTimer <= 0f || meat.ValueRO.Energy <= 1f)
                {
                    biomassToReturn += meat.ValueRO.Energy;
                    ecb.DestroyEntity(entity);
                }
            }
            
            // Return decayed energy to biomass
            if (biomassToReturn > 0f)
            {
                var newSettings = worldSettings;
                newSettings.FreeBiomass += biomassToReturn;
                SystemAPI.SetSingleton(newSettings);
            }
        }
    }
    
    /// <summary>
    /// Handles healing when health is damaged but bibite has enough energy
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HealingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (health, energy) 
                in SystemAPI.Query<RefRW<Health>, RefRW<Energy>>()
                .WithAll<BibiteTag>())
            {
                // Only heal if damaged and has energy
                if (health.ValueRO.Current >= health.ValueRO.Maximum) continue;
                if (energy.ValueRO.Current < energy.ValueRO.Maximum * 0.5f) continue;
                
                // Heal slowly
                float healRate = 2f * deltaTime; // 2 HP per second
                float healAmount = math.min(healRate, health.ValueRO.Maximum - health.ValueRO.Current);
                
                // Healing costs energy
                float healCost = healAmount * 0.5f; // 0.5 energy per HP
                if (energy.ValueRO.Current >= healCost)
                {
                    health.ValueRW.Current += healAmount;
                    energy.ValueRW.Current -= healCost;
                }
            }
        }
    }
}
