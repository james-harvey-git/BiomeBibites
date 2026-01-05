using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles bibite growth from birth to adulthood.
    /// 
    /// Bibites start at ~30% adult size and grow to full size over time.
    /// Growth requires energy investment - faster growth costs more.
    /// Maturity affects:
    /// - Physical size (and thus collision radius)
    /// - Energy capacity
    /// - Reproduction capability (must be mature to reproduce)
    /// - Movement speed (young are slower)
    /// - Combat strength (young are weaker)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GrowthSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float simTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (age, size, energy, radius, organs) 
                in SystemAPI.Query<
                    RefRW<Age>,
                    RefRW<Size>,
                    RefRW<Energy>,
                    RefRW<Radius>,
                    RefRO<Organs>>()
                .WithAll<BibiteTag>())
            {
                // Update time alive
                age.ValueRW.TimeAlive += deltaTime;
                
                // Skip if already fully mature
                if (age.ValueRO.Maturity >= 1f) continue;
                
                // Growth rate based on energy availability and EggOrgan
                // Higher EggOrgan = offspring start more mature but also grow faster
                float baseGrowthRate = 0.01f; // Takes ~100 seconds to fully mature at base rate
                float eggOrganBonus = organs.ValueRO.EggOrgan * 0.5f; // Up to 50% faster
                float growthRate = baseGrowthRate * (1f + eggOrganBonus);
                
                // Growth requires energy - scale growth by energy availability
                float energyRatio = energy.ValueRO.Current / energy.ValueRO.Maximum;
                if (energyRatio < 0.3f)
                {
                    // Too hungry to grow - stunted growth
                    growthRate *= 0.1f;
                }
                else if (energyRatio > 0.7f)
                {
                    // Well fed - accelerated growth
                    growthRate *= 1.5f;
                }
                
                // Apply growth
                float previousMaturity = age.ValueRO.Maturity;
                float newMaturity = math.min(1f, previousMaturity + growthRate * deltaTime);
                age.ValueRW.Maturity = newMaturity;
                
                // Growth costs energy (proportional to growth amount)
                float growthAmount = newMaturity - previousMaturity;
                float growthEnergyCost = growthAmount * 20f; // 20 energy to grow from 0 to 1
                energy.ValueRW.Current = math.max(0f, energy.ValueRO.Current - growthEnergyCost);
                
                // Update physical size based on maturity
                // Starts at 30% size, grows to 100%
                float newSize = 0.3f + newMaturity * 0.7f;
                size.ValueRW.Ratio = newSize;
                
                // Update collision radius (base 5, scales with size)
                radius.ValueRW.Value = 5f * newSize;
                
                // Update energy maximum (scales with size)
                // Bigger bibites can store more energy
                float baseMaxEnergy = 100f;
                float newMaxEnergy = baseMaxEnergy * (0.5f + newSize * 0.5f); // 50-100% of base
                energy.ValueRW.Maximum = newMaxEnergy;
            }
        }
    }
    
    /// <summary>
    /// System to update the Age.TimeAlive for all bibites
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct AgeUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var age in SystemAPI.Query<RefRW<Age>>().WithAll<BibiteTag>())
            {
                age.ValueRW.TimeAlive += deltaTime;
            }
        }
    }
    
    /// <summary>
    /// Applies maturity-based modifiers to various attributes
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct MaturityModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system could apply temporary modifiers based on maturity
            // For now, the growth system handles size changes directly
            // Future: could add MaturityModifiers component for speed/strength penalties
        }
    }
    
    /// <summary>
    /// Handles old age effects (optional - bibites can die of old age)
    /// Currently disabled - bibites only die from starvation or combat
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct AgingSystem : ISystem
    {
        // Configuration
        private const float MAX_AGE = 600f; // 10 minutes max lifespan (set to 0 to disable)
        private const float OLD_AGE_START = 480f; // Start aging effects at 8 minutes
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Skip if old age is disabled
            if (MAX_AGE <= 0f) return;
            
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (age, health, energy) 
                in SystemAPI.Query<
                    RefRO<Age>,
                    RefRW<Health>,
                    RefRW<Energy>>()
                .WithAll<BibiteTag>())
            {
                float timeAlive = age.ValueRO.TimeAlive;
                
                // Old age effects
                if (timeAlive > OLD_AGE_START)
                {
                    // Gradually reduce max health and energy as bibite ages
                    float ageProgress = (timeAlive - OLD_AGE_START) / (MAX_AGE - OLD_AGE_START);
                    ageProgress = math.saturate(ageProgress);
                    
                    // Reduced effectiveness
                    float ageDecay = 1f - ageProgress * 0.5f; // Down to 50% at max age
                    
                    // Old age damage (very slow)
                    if (timeAlive > MAX_AGE * 0.9f)
                    {
                        health.ValueRW.Current -= 2f * deltaTime;
                    }
                }
            }
        }
    }
}
