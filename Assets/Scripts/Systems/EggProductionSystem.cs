using Unity.Entities;
using Unity.Mathematics;
using BiomeBibites.BIOME;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles egg production within bibites.
    /// Eggs develop internally before being laid.
    /// EggOrgan size affects clutch size and offspring maturity.
    /// </summary>
    public partial class EggProductionSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;

            foreach (var (reproState, energy, organs, age, brain) 
                in SystemAPI.Query<
                    RefRW<ReproductionState>,
                    RefRO<Energy>,
                    RefRO<Organs>,
                    RefRO<Age>,
                    RefRO<BrainState>>()
                .WithAll<BibiteTag>())
            {
                // Must be mature to reproduce
                if (age.ValueRO.Maturity < 0.9f) continue;
                
                // Must have enough energy (at least 50%)
                float energyRatio = energy.ValueRO.Current / energy.ValueRO.Maximum;
                if (energyRatio < 0.5f) continue;
                
                // Must want to reproduce (brain output)
                if (brain.ValueRO.WantToLayOutput < 0.3f && reproState.ValueRO.EggProgress < 0.5f)
                {
                    // Not actively trying to reproduce and no egg in progress
                    continue;
                }
                
                // Egg development rate based on EggOrgan and energy availability
                float baseRate = 0.02f; // Base: ~50 seconds to develop egg
                float eggOrganBonus = organs.ValueRO.EggOrgan * 2f; // Up to 3x faster
                float energyBonus = (energyRatio - 0.5f) * 2f; // Faster when well-fed
                float developRate = baseRate * (1f + eggOrganBonus) * (1f + math.max(0f, energyBonus));
                
                // Progress egg development
                reproState.ValueRW.EggProgress += developRate * deltaTime;
                
                // Egg is ready when progress reaches 1.0
                if (reproState.ValueRO.EggProgress >= 1f)
                {
                    reproState.ValueRW.EggProgress = 0f;
                    reproState.ValueRW.EggsStored++;
                    reproState.ValueRW.ReadyToLay = true;
                    
                    // Max eggs that can be stored based on EggOrgan
                    int maxEggs = 1 + (int)(organs.ValueRO.EggOrgan * 3f); // 1-4 eggs
                    reproState.ValueRW.EggsStored = math.min(reproState.ValueRO.EggsStored, maxEggs);
                }
            }
        }
    }
    
    /// <summary>
    /// Handles egg laying - creating egg entities in the world.
    /// Eggs contain the genetic information that will become a new bibite.
    /// </summary>
    public partial class EggLayingSystem : SystemBase
    {
        private Random _random;
        
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
            _random = new Random((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Query with max 7 components (6 + entity access), fetch others manually
            foreach (var (reproState, energy, organs, position, rotation, color, entity) 
                in SystemAPI.Query<
                    RefRW<ReproductionState>,
                    RefRW<Energy>,
                    RefRO<Organs>,
                    RefRO<Position>,
                    RefRO<Rotation>,
                    RefRO<BibiteColor>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                // Check if ready to lay
                if (!reproState.ValueRO.ReadyToLay) continue;
                if (reproState.ValueRO.EggsStored <= 0) continue;
                
                // Get brain output for laying desire
                var brain = EntityManager.GetComponentData<BrainState>(entity);
                if (brain.WantToLayOutput < 0.5f) continue;
                
                // Fetch additional components manually
                var diet = EntityManager.GetComponentData<Diet>(entity);
                var generation = EntityManager.GetComponentData<Generation>(entity);
                var clock = EntityManager.GetComponentData<InternalClock>(entity);
                
                // Calculate egg cost based on EggOrgan
                float baseEggCost = 30f;
                float eggOrganBonus = organs.ValueRO.EggOrgan * 20f;
                float eggCost = baseEggCost + eggOrganBonus;
                
                // Must have enough energy
                if (energy.ValueRO.Current < eggCost * 1.2f) continue;
                
                // Lay the egg!
                energy.ValueRW.Current -= eggCost;
                reproState.ValueRW.EggsStored--;
                if (reproState.ValueRO.EggsStored <= 0)
                {
                    reproState.ValueRW.ReadyToLay = false;
                }
                
                // Calculate egg position (behind parent)
                float2 offsetDir = new float2(
                    math.cos(rotation.ValueRO.Value + math.PI),
                    math.sin(rotation.ValueRO.Value + math.PI)
                );
                float2 eggPos = position.ValueRO.Value + offsetDir * 10f;
                
                // Create egg entity
                var eggEntity = ecb.CreateEntity();
                
                // Egg properties
                float hatchTime = 10f - organs.ValueRO.EggOrgan * 5f;
                hatchTime = math.max(3f, hatchTime);
                
                ecb.AddComponent(eggEntity, new Egg
                {
                    Energy = eggCost * 0.9f,
                    HatchProgress = 0f,
                    HatchTime = hatchTime,
                    Parent = entity
                });
                
                ecb.AddComponent(eggEntity, new Position { Value = eggPos });
                ecb.AddComponent(eggEntity, new Radius { Value = 3f });
                ecb.AddSharedComponent(eggEntity, new EntityType { Value = EntityTypeEnum.Egg });
                
                // Store genetic information in egg
                var inheritedTraits = new InheritedTraits
                {
                    ColorR = math.clamp(color.ValueRO.R + _random.NextFloat(-0.1f, 0.1f), 0f, 1f),
                    ColorG = math.clamp(color.ValueRO.G + _random.NextFloat(-0.1f, 0.1f), 0f, 1f),
                    ColorB = math.clamp(color.ValueRO.B + _random.NextFloat(-0.1f, 0.1f), 0f, 1f),
                    Diet = math.clamp(diet.Value + _random.NextFloat(-0.05f, 0.05f), 0f, 1f),
                    ClockFrequency = math.clamp(clock.Frequency + _random.NextFloat(-0.2f, 0.2f), 0.1f, 3f),
                    Generation = generation.Value + 1,
                    StartingMaturity = 0.1f + organs.ValueRO.EggOrgan * 0.4f,
                    BrainSeed = _random.NextUInt()
                };
                ecb.AddComponent(eggEntity, inheritedTraits);
                
                // Store mutated organs
                float organMutation = 0.03f;
                var inheritedOrgans = MutateOrgans(organs.ValueRO, organMutation, ref _random);
                ecb.AddComponent(eggEntity, inheritedOrgans);
                
                // Clone parent brain reference
                if (EntityManager.HasComponent<BiomeBrainComponent>(entity))
                {
                    var parentBrainComp = EntityManager.GetComponentData<BiomeBrainComponent>(entity);
                    if (parentBrainComp.Brain != null)
                    {
                        ecb.AddComponent(eggEntity, new EggBrainData
                        {
                            ParentEntity = entity
                        });
                    }
                }
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private Organs MutateOrgans(Organs parent, float mutationRate, ref Random random)
        {
            var newOrgans = new Organs
            {
                Stomach = math.max(0.05f, parent.Stomach + random.NextFloat(-mutationRate, mutationRate)),
                MoveMuscle = math.max(0.05f, parent.MoveMuscle + random.NextFloat(-mutationRate, mutationRate)),
                JawMuscle = math.max(0.05f, parent.JawMuscle + random.NextFloat(-mutationRate, mutationRate)),
                Armor = math.max(0.05f, parent.Armor + random.NextFloat(-mutationRate, mutationRate)),
                EggOrgan = math.max(0.05f, parent.EggOrgan + random.NextFloat(-mutationRate, mutationRate)),
                Throat = math.max(0.05f, parent.Throat + random.NextFloat(-mutationRate, mutationRate)),
                FatReserve = math.max(0.05f, parent.FatReserve + random.NextFloat(-mutationRate, mutationRate))
            };
            
            // Normalize to sum to 1
            float sum = newOrgans.Stomach + newOrgans.MoveMuscle + newOrgans.JawMuscle + 
                        newOrgans.Armor + newOrgans.EggOrgan + newOrgans.Throat + newOrgans.FatReserve;
            newOrgans.Stomach /= sum;
            newOrgans.MoveMuscle /= sum;
            newOrgans.JawMuscle /= sum;
            newOrgans.Armor /= sum;
            newOrgans.EggOrgan /= sum;
            newOrgans.Throat /= sum;
            newOrgans.FatReserve /= sum;
            
            return newOrgans;
        }
    }
    
    /// <summary>
    /// Component to store inherited traits in an egg
    /// </summary>
    public struct InheritedTraits : IComponentData
    {
        public float ColorR, ColorG, ColorB;
        public float Diet;
        public float ClockFrequency;
        public int Generation;
        public float StartingMaturity;
        public uint BrainSeed;
    }
    
    /// <summary>
    /// Component to store brain inheritance data
    /// </summary>
    public struct EggBrainData : IComponentData
    {
        public Entity ParentEntity;
    }
}
