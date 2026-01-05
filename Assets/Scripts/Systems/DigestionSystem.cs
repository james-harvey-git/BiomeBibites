using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Digestion System - Converts stomach contents to energy over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DigestionSystem : ISystem
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

            foreach (var (stomach, organs, diet, energy) 
                in SystemAPI.Query<
                    RefRW<StomachContents>,
                    RefRO<Organs>,
                    RefRO<Diet>,
                    RefRW<Energy>>()
                .WithAll<BibiteTag>())
            {
                // Base digestion rate modified by stomach organ size
                float stomachEfficiency = 1f + organs.ValueRO.Stomach * 2f;
                float baseDigestRate = 2f * stomachEfficiency * deltaTime;
                
                // Diet affects efficiency
                float dietValue = diet.ValueRO.Value;
                float plantEfficiency = math.lerp(0.9f, 0.4f, dietValue);
                float meatEfficiency = math.lerp(0.4f, 0.9f, dietValue);
                
                plantEfficiency += organs.ValueRO.Stomach * 0.1f;
                meatEfficiency += organs.ValueRO.Stomach * 0.1f;
                
                // Digest plant matter
                if (stomach.ValueRO.PlantMatter > 0)
                {
                    float toDigest = math.min(stomach.ValueRO.PlantMatter, baseDigestRate);
                    stomach.ValueRW.PlantMatter -= toDigest;
                    float energyGain = toDigest * plantEfficiency;
                    energy.ValueRW.Current = math.min(energy.ValueRO.Current + energyGain, energy.ValueRO.Maximum);
                }
                
                // Digest meat matter
                if (stomach.ValueRO.MeatMatter > 0)
                {
                    float toDigest = math.min(stomach.ValueRO.MeatMatter, baseDigestRate);
                    stomach.ValueRW.MeatMatter -= toDigest;
                    float energyGain = toDigest * meatEfficiency;
                    energy.ValueRW.Current = math.min(energy.ValueRO.Current + energyGain, energy.ValueRO.Maximum);
                }
            }
        }
    }
    
    /// <summary>
    /// Continuous Eating System - Bibites eat while in contact with food.
    /// 
    /// Uses EndSimulationEntityCommandBufferSystem to avoid conflicts with
    /// other systems that may destroy entities during BeginSimulation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class EnhancedEatingSystem : SystemBase
    {
        private EndSimulationEntityCommandBufferSystem _ecbSystem;
        
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
            _ecbSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            var ecb = _ecbSystem.CreateCommandBuffer();
            
            // Get all pellet data
            var plantQuery = GetEntityQuery(typeof(PlantPellet), typeof(Position), typeof(Radius));
            var meatQuery = GetEntityQuery(typeof(MeatPellet), typeof(Position), typeof(Radius));
            
            var plantEntities = plantQuery.ToEntityArray(Allocator.Temp);
            var plantPositions = plantQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var plantPellets = plantQuery.ToComponentDataArray<PlantPellet>(Allocator.Temp);
            var plantRadii = plantQuery.ToComponentDataArray<Radius>(Allocator.Temp);
            
            var meatEntities = meatQuery.ToEntityArray(Allocator.Temp);
            var meatPositions = meatQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var meatPellets = meatQuery.ToComponentDataArray<MeatPellet>(Allocator.Temp);
            var meatRadii = meatQuery.ToComponentDataArray<Radius>(Allocator.Temp);

            // Track remaining energy in each pellet
            var plantRemainingEnergy = new NativeArray<float>(plantEntities.Length, Allocator.Temp);
            var meatRemainingEnergy = new NativeArray<float>(meatEntities.Length, Allocator.Temp);
            
            // Track which pellets still exist (might have been destroyed by other systems)
            var plantExists = new NativeArray<bool>(plantEntities.Length, Allocator.Temp);
            var meatExists = new NativeArray<bool>(meatEntities.Length, Allocator.Temp);
            
            for (int i = 0; i < plantPellets.Length; i++)
            {
                plantRemainingEnergy[i] = plantPellets[i].Energy;
                plantExists[i] = EntityManager.Exists(plantEntities[i]);
            }
            for (int i = 0; i < meatPellets.Length; i++)
            {
                meatRemainingEnergy[i] = meatPellets[i].Energy;
                meatExists[i] = EntityManager.Exists(meatEntities[i]);
            }

            // Process each bibite
            foreach (var (position, brain, organs, diet, stomach, radius) 
                in SystemAPI.Query<
                    RefRO<Position>,
                    RefRO<BrainState>,
                    RefRO<Organs>,
                    RefRO<Diet>,
                    RefRW<StomachContents>,
                    RefRO<Radius>>()
                .WithAll<BibiteTag>())
            {
                if (brain.ValueRO.WantToEatOutput < 0.2f) continue;
                
                float stomachCapacity = 20f + organs.ValueRO.Stomach * 30f;
                float currentFood = stomach.ValueRO.PlantMatter + stomach.ValueRO.MeatMatter;
                if (currentFood >= stomachCapacity * 0.98f) continue;
                
                float throatSize = organs.ValueRO.Throat;
                float eatRadius = radius.ValueRO.Value + throatSize * 8f;
                float eatRate = (3f + throatSize * 12f) * deltaTime;
                eatRate *= brain.ValueRO.WantToEatOutput;
                
                float2 pos = position.ValueRO.Value;
                float spaceInStomach = stomachCapacity - currentFood;
                float dietValue = diet.ValueRO.Value;
                
                // Eat plants
                for (int i = 0; i < plantPositions.Length; i++)
                {
                    if (spaceInStomach <= 0.01f) break;
                    if (!plantExists[i]) continue; // Entity was destroyed
                    if (plantRemainingEnergy[i] <= 0) continue;
                    
                    float dist = math.distance(pos, plantPositions[i].Value);
                    float contactDist = radius.ValueRO.Value + plantRadii[i].Value;
                    
                    if (dist > contactDist + 2f) continue;
                    
                    float toBite = math.min(eatRate, math.min(plantRemainingEnergy[i], spaceInStomach));
                    if (dietValue < 0.5f)
                        toBite *= 1f + (0.5f - dietValue);
                    
                    stomach.ValueRW.PlantMatter += toBite;
                    plantRemainingEnergy[i] -= toBite;
                    spaceInStomach -= toBite;
                }
                
                // Eat meat
                for (int i = 0; i < meatPositions.Length; i++)
                {
                    if (spaceInStomach <= 0.01f) break;
                    if (!meatExists[i]) continue;
                    if (meatRemainingEnergy[i] <= 0) continue;
                    
                    float dist = math.distance(pos, meatPositions[i].Value);
                    float contactDist = radius.ValueRO.Value + meatRadii[i].Value;
                    
                    if (dist > contactDist + 2f) continue;
                    
                    float toBite = math.min(eatRate, math.min(meatRemainingEnergy[i], spaceInStomach));
                    if (dietValue > 0.5f)
                        toBite *= 1f + (dietValue - 0.5f);
                    
                    stomach.ValueRW.MeatMatter += toBite;
                    meatRemainingEnergy[i] -= toBite;
                    spaceInStomach -= toBite;
                }
            }
            
            // Apply changes - check existence before each operation
            for (int i = 0; i < plantEntities.Length; i++)
            {
                if (!plantExists[i]) continue;
                if (!EntityManager.Exists(plantEntities[i])) continue; // Double-check
                
                float originalEnergy = plantPellets[i].Energy;
                float newEnergy = plantRemainingEnergy[i];
                
                if (math.abs(newEnergy - originalEnergy) > 0.001f)
                {
                    if (newEnergy <= 0.1f)
                    {
                        ecb.DestroyEntity(plantEntities[i]);
                    }
                    else
                    {
                        ecb.SetComponent(plantEntities[i], new PlantPellet { Energy = newEnergy });
                        float scale = math.sqrt(newEnergy / 30f);
                        float newRadius = math.max(1f, 2f * scale);
                        ecb.SetComponent(plantEntities[i], new Radius { Value = newRadius });
                    }
                }
            }
            
            for (int i = 0; i < meatEntities.Length; i++)
            {
                if (!meatExists[i]) continue;
                if (!EntityManager.Exists(meatEntities[i])) continue;
                
                float originalEnergy = meatPellets[i].Energy;
                float newEnergy = meatRemainingEnergy[i];
                
                if (math.abs(newEnergy - originalEnergy) > 0.001f)
                {
                    if (newEnergy <= 0.1f)
                    {
                        ecb.DestroyEntity(meatEntities[i]);
                    }
                    else
                    {
                        ecb.SetComponent(meatEntities[i], new MeatPellet 
                        { 
                            Energy = newEnergy,
                            DecayTimer = meatPellets[i].DecayTimer 
                        });
                        float scale = math.sqrt(newEnergy / 50f);
                        float newRadius = math.max(1f, 3f * scale);
                        ecb.SetComponent(meatEntities[i], new Radius { Value = newRadius });
                    }
                }
            }
            
            // Cleanup
            plantEntities.Dispose();
            plantPositions.Dispose();
            plantPellets.Dispose();
            plantRadii.Dispose();
            meatEntities.Dispose();
            meatPositions.Dispose();
            meatPellets.Dispose();
            meatRadii.Dispose();
            plantRemainingEnergy.Dispose();
            meatRemainingEnergy.Dispose();
            plantExists.Dispose();
            meatExists.Dispose();
        }
    }
    
    // NOTE: StomachInitSystem is defined in FatStorageSystem.cs
}
