using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Enhanced energy system with organ-based metabolism scaling.
    /// Handles base metabolic costs, activity costs, and organ maintenance.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct MetabolismSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float totalBiomassReturn = 0f;

            foreach (var (energy, velocity, organs, brain, size, age, entity) 
                in SystemAPI.Query<
                    RefRW<Energy>,
                    RefRO<Velocity>,
                    RefRO<Organs>,
                    RefRO<BrainState>,
                    RefRO<Size>,
                    RefRO<Age>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                float metabolism = energy.ValueRO.Metabolism;
                float sizeRatio = size.ValueRO.Ratio;
                float maturity = age.ValueRO.Maturity;
                
                // === BASE METABOLIC COST ===
                // Scales with size^0.75 (Kleiber's law - larger animals are more efficient per unit mass)
                float effectiveSize = math.pow(sizeRatio, 0.75f);
                float baseCost = 0.3f * metabolism * effectiveSize * deltaTime;
                
                // Growing bibites have higher metabolism
                if (maturity < 1f)
                {
                    baseCost *= 1f + (1f - maturity) * 0.5f; // Up to 50% higher when young
                }
                
                // === MOVEMENT COST ===
                float speed = math.length(velocity.ValueRO.Value);
                // MoveMuscle efficiency: higher muscle = lower cost per speed unit
                float moveEfficiency = 0.5f + organs.ValueRO.MoveMuscle * 0.5f;
                float movementCost = speed * 0.015f * metabolism * sizeRatio / moveEfficiency * deltaTime;
                
                // === BRAIN COST ===
                // Brain activity based on output magnitudes
                float brainActivity = math.abs(brain.ValueRO.AccelerateOutput) + 
                                     math.abs(brain.ValueRO.RotateOutput) + 
                                     brain.ValueRO.WantToEatOutput +
                                     brain.ValueRO.WantToAttackOutput;
                float brainCost = (0.05f + brainActivity * 0.02f) * metabolism * deltaTime;
                
                // === ORGAN MAINTENANCE COSTS ===
                float organCost = 0f;
                
                // Muscles require maintenance proportional to their size
                organCost += organs.ValueRO.MoveMuscle * 0.08f * metabolism * deltaTime;
                organCost += organs.ValueRO.JawMuscle * 0.06f * metabolism * deltaTime;
                
                // Armor is heavy and costly to maintain
                organCost += organs.ValueRO.Armor * 0.1f * metabolism * deltaTime;
                
                // Stomach has a small maintenance cost
                organCost += organs.ValueRO.Stomach * 0.02f * metabolism * deltaTime;
                
                // Reproductive organs
                organCost += organs.ValueRO.EggOrgan * 0.04f * metabolism * deltaTime;
                
                // Fat reserve is efficient (low cost)
                organCost += organs.ValueRO.FatReserve * 0.01f * metabolism * deltaTime;
                
                // === TOTAL COST ===
                float totalCost = baseCost + movementCost + brainCost + organCost;
                
                // Apply energy consumption
                energy.ValueRW.Current -= totalCost;
                
                // Energy lost as heat returns to biomass (entropy tax)
                float heatLoss = totalCost * 0.15f;
                totalBiomassReturn += heatLoss;
                
                // Clamp energy to valid range
                energy.ValueRW.Current = math.clamp(energy.ValueRO.Current, 0f, energy.ValueRO.Maximum);
            }
            
            // Return heat loss to biomass pool
            var newSettings = worldSettings;
            newSettings.FreeBiomass += totalBiomassReturn;
            SystemAPI.SetSingleton(newSettings);
        }
    }
    
    /// <summary>
    /// Handles starvation damage when energy is depleted
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MetabolismSystem))]
    public partial struct StarvationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (energy, health, fatStorage) 
                in SystemAPI.Query<
                    RefRW<Energy>,
                    RefRW<Health>,
                    RefRW<FatStorage>>()
                .WithAll<BibiteTag>())
            {
                // If energy is very low, start using fat reserves
                if (energy.ValueRO.Current < energy.ValueRO.Maximum * 0.2f)
                {
                    if (fatStorage.ValueRO.Current > 0f)
                    {
                        // Convert fat to energy
                        float fatToUse = math.min(fatStorage.ValueRO.Current, 2f * deltaTime);
                        fatStorage.ValueRW.Current -= fatToUse;
                        energy.ValueRW.Current += fatToUse * 0.9f; // 90% efficient conversion
                    }
                }
                
                // If energy depleted and no fat, take starvation damage
                if (energy.ValueRO.Current <= 0f && fatStorage.ValueRO.Current <= 0f)
                {
                    health.ValueRW.Current -= 10f * deltaTime;
                }
            }
            
            // Also handle bibites without fat storage component
            foreach (var (energy, health) 
                in SystemAPI.Query<RefRO<Energy>, RefRW<Health>>()
                .WithAll<BibiteTag>()
                .WithNone<FatStorage>())
            {
                if (energy.ValueRO.Current <= 0f)
                {
                    health.ValueRW.Current -= 10f * deltaTime;
                }
            }
        }
    }
}
