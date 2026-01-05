using Unity.Entities;
using Unity.Mathematics;
using BiomeBibites.BIOME;
using System.Collections.Generic;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles sexual reproduction between two bibites.
    /// When two compatible bibites meet and both want to reproduce,
    /// they can create offspring with genetic crossover.
    /// </summary>
    public partial class SexualReproductionSystem : SystemBase
    {
        private Random _random;
        
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
            _random = new Random((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            // Get world settings
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float worldSize = worldSettings.SimulationSize;
            
            // Build list of potential mates (bibites wanting to reproduce)
            var potentialMates = new List<MateCandidate>();
            
            foreach (var (reproState, energy, position, age, color, entity) 
                in SystemAPI.Query<
                    RefRO<ReproductionState>,
                    RefRO<Energy>,
                    RefRO<Position>,
                    RefRO<Age>,
                    RefRO<BibiteColor>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                // Must be mature
                if (age.ValueRO.Maturity < 0.9f) continue;
                
                // Must have enough energy
                float energyRatio = energy.ValueRO.Current / energy.ValueRO.Maximum;
                if (energyRatio < 0.6f) continue;
                
                // Must have eggs ready
                if (!reproState.ValueRO.ReadyToLay) continue;
                
                // Get brain state
                if (!EntityManager.HasComponent<BrainState>(entity)) continue;
                var brain = EntityManager.GetComponentData<BrainState>(entity);
                
                // Must want to reproduce
                if (brain.WantToLayOutput < 0.4f) continue;
                
                potentialMates.Add(new MateCandidate
                {
                    Entity = entity,
                    Position = position.ValueRO.Value,
                    Color = new float3(color.ValueRO.R, color.ValueRO.G, color.ValueRO.B)
                });
            }
            
            // No mates available
            if (potentialMates.Count < 2) return;
            
            // Find compatible pairs (nearby bibites)
            float matingRange = 20f;
            var matedEntities = new HashSet<Entity>();
            
            for (int i = 0; i < potentialMates.Count; i++)
            {
                if (matedEntities.Contains(potentialMates[i].Entity)) continue;
                
                for (int j = i + 1; j < potentialMates.Count; j++)
                {
                    if (matedEntities.Contains(potentialMates[j].Entity)) continue;
                    
                    // Check distance
                    float2 delta = potentialMates[j].Position - potentialMates[i].Position;
                    WrapDelta(ref delta, worldSize);
                    float dist = math.length(delta);
                    
                    if (dist > matingRange) continue;
                    
                    // Check color similarity (prefer similar colors - same species)
                    float colorDist = math.length(potentialMates[i].Color - potentialMates[j].Color);
                    float colorCompatibility = 1f - math.saturate(colorDist);
                    
                    // Random chance based on compatibility
                    if (_random.NextFloat() > colorCompatibility * 0.1f) continue;
                    
                    // MATE!
                    PerformCrossover(potentialMates[i].Entity, potentialMates[j].Entity);
                    
                    matedEntities.Add(potentialMates[i].Entity);
                    matedEntities.Add(potentialMates[j].Entity);
                    break;
                }
            }
        }
        
        private void PerformCrossover(Entity parent1, Entity parent2)
        {
            // Get parent brains
            if (!EntityManager.HasComponent<BiomeBrainComponent>(parent1)) return;
            if (!EntityManager.HasComponent<BiomeBrainComponent>(parent2)) return;
            
            var brain1Comp = EntityManager.GetComponentData<BiomeBrainComponent>(parent1);
            var brain2Comp = EntityManager.GetComponentData<BiomeBrainComponent>(parent2);
            
            if (brain1Comp.Brain == null || brain2Comp.Brain == null) return;
            
            // Get fitness (use energy as proxy for now)
            var energy1 = EntityManager.GetComponentData<Energy>(parent1);
            var energy2 = EntityManager.GetComponentData<Energy>(parent2);
            
            brain1Comp.Brain.Fitness = energy1.Current;
            brain2Comp.Brain.Fitness = energy2.Current;
            
            // Perform NEAT-style crossover
            var offspringBrain = BiomeGenome.Crossover(brain1Comp.Brain, brain2Comp.Brain, ref _random);
            
            // Apply mutation
            var mutRandom = new Random(_random.NextUInt());
            EnhancedMutation.MutateComprehensive(offspringBrain, ref mutRandom, MutationConfig.Default);
            
            // Deduct energy from both parents
            var reproState1 = EntityManager.GetComponentData<ReproductionState>(parent1);
            var reproState2 = EntityManager.GetComponentData<ReproductionState>(parent2);
            
            reproState1.EggsStored = math.max(0, reproState1.EggsStored - 1);
            reproState2.EggsStored = math.max(0, reproState2.EggsStored - 1);
            
            if (reproState1.EggsStored <= 0) reproState1.ReadyToLay = false;
            if (reproState2.EggsStored <= 0) reproState2.ReadyToLay = false;
            
            EntityManager.SetComponentData(parent1, reproState1);
            EntityManager.SetComponentData(parent2, reproState2);
            
            // Deduct energy
            energy1.Current -= 20f;
            energy2.Current -= 20f;
            EntityManager.SetComponentData(parent1, energy1);
            EntityManager.SetComponentData(parent2, energy2);
            
            // Create egg with crossover brain
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            // Get positions and average them
            var pos1 = EntityManager.GetComponentData<Position>(parent1);
            var pos2 = EntityManager.GetComponentData<Position>(parent2);
            float2 eggPos = (pos1.Value + pos2.Value) * 0.5f;
            
            // Get traits from both parents
            var color1 = EntityManager.GetComponentData<BibiteColor>(parent1);
            var color2 = EntityManager.GetComponentData<BibiteColor>(parent2);
            var diet1 = EntityManager.GetComponentData<Diet>(parent1);
            var diet2 = EntityManager.GetComponentData<Diet>(parent2);
            var gen1 = EntityManager.GetComponentData<Generation>(parent1);
            var gen2 = EntityManager.GetComponentData<Generation>(parent2);
            var organs1 = EntityManager.GetComponentData<Organs>(parent1);
            var organs2 = EntityManager.GetComponentData<Organs>(parent2);
            var clock1 = EntityManager.GetComponentData<InternalClock>(parent1);
            var clock2 = EntityManager.GetComponentData<InternalClock>(parent2);
            
            var eggEntity = ecb.CreateEntity();
            
            // Egg component
            ecb.AddComponent(eggEntity, new Egg
            {
                Energy = 40f,
                HatchProgress = 0f,
                HatchTime = 8f,
                Parent = parent1
            });
            
            ecb.AddComponent(eggEntity, new Position { Value = eggPos });
            ecb.AddComponent(eggEntity, new Radius { Value = 3f });
            ecb.AddSharedComponent(eggEntity, new EntityType { Value = EntityTypeEnum.Egg });
            
            // Inherited traits (blend of both parents)
            ecb.AddComponent(eggEntity, new InheritedTraits
            {
                ColorR = math.lerp(color1.R, color2.R, _random.NextFloat()) + _random.NextFloat(-0.05f, 0.05f),
                ColorG = math.lerp(color1.G, color2.G, _random.NextFloat()) + _random.NextFloat(-0.05f, 0.05f),
                ColorB = math.lerp(color1.B, color2.B, _random.NextFloat()) + _random.NextFloat(-0.05f, 0.05f),
                Diet = math.lerp(diet1.Value, diet2.Value, _random.NextFloat()) + _random.NextFloat(-0.02f, 0.02f),
                ClockFrequency = math.lerp(clock1.Frequency, clock2.Frequency, _random.NextFloat()),
                Generation = math.max(gen1.Value, gen2.Value) + 1,
                StartingMaturity = 0.2f,
                BrainSeed = _random.NextUInt()
            });
            
            // Blend organs
            float blend = _random.NextFloat();
            ecb.AddComponent(eggEntity, new Organs
            {
                Stomach = math.lerp(organs1.Stomach, organs2.Stomach, blend),
                MoveMuscle = math.lerp(organs1.MoveMuscle, organs2.MoveMuscle, blend),
                JawMuscle = math.lerp(organs1.JawMuscle, organs2.JawMuscle, blend),
                Armor = math.lerp(organs1.Armor, organs2.Armor, blend),
                EggOrgan = math.lerp(organs1.EggOrgan, organs2.EggOrgan, blend),
                Throat = math.lerp(organs1.Throat, organs2.Throat, blend),
                FatReserve = math.lerp(organs1.FatReserve, organs2.FatReserve, blend)
            });
            
            // Store crossover brain directly (it's already created)
            ecb.AddComponent(eggEntity, new CrossoverBrainData
            {
                Brain = offspringBrain
            });
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private void WrapDelta(ref float2 delta, float worldSize)
        {
            float halfSize = worldSize / 2f;
            if (delta.x > halfSize) delta.x -= worldSize;
            else if (delta.x < -halfSize) delta.x += worldSize;
            if (delta.y > halfSize) delta.y -= worldSize;
            else if (delta.y < -halfSize) delta.y += worldSize;
        }
        
        private struct MateCandidate
        {
            public Entity Entity;
            public float2 Position;
            public float3 Color;
        }
    }
    
    /// <summary>
    /// Component to store a pre-created crossover brain in an egg
    /// </summary>
    public class CrossoverBrainData : IComponentData
    {
        public BiomeBrain Brain;
    }
    
    /// <summary>
    /// Updated hatching system that handles crossover brains
    /// </summary>
    public partial class CrossoverHatchingSystem : SystemBase
    {
        private Random _random;
        
        protected override void OnCreate()
        {
            _random = new Random((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Handle eggs with crossover brains
            foreach (var (egg, position, traits, organs, brainData, entity) 
                in SystemAPI.Query<
                    RefRW<Egg>,
                    RefRO<Position>,
                    RefRO<InheritedTraits>,
                    RefRO<Organs>,
                    CrossoverBrainData>()
                .WithEntityAccess())
            {
                // Progress incubation
                float hatchRate = 1f / egg.ValueRO.HatchTime;
                egg.ValueRW.HatchProgress += hatchRate * deltaTime;
                
                if (egg.ValueRO.HatchProgress < 1f) continue;
                
                // Hatch with the crossover brain
                var offspring = ecb.CreateEntity();
                
                ecb.AddComponent(offspring, new BibiteTag { });
                ecb.AddComponent(offspring, new Generation { Value = traits.ValueRO.Generation });
                
                float2 birthPos = position.ValueRO.Value + new float2(
                    _random.NextFloat(-2f, 2f),
                    _random.NextFloat(-2f, 2f)
                );
                
                ecb.AddComponent(offspring, new Position { Value = birthPos });
                ecb.AddComponent(offspring, new Velocity { Value = float2.zero });
                ecb.AddComponent(offspring, new Rotation { Value = _random.NextFloat(0f, math.PI * 2f) });
                
                float startSize = 0.3f + traits.ValueRO.StartingMaturity * 0.7f;
                ecb.AddComponent(offspring, new Size { Ratio = startSize });
                ecb.AddComponent(offspring, new Radius { Value = 5f * startSize });
                
                ecb.AddComponent(offspring, new BibiteColor
                {
                    R = math.clamp(traits.ValueRO.ColorR, 0f, 1f),
                    G = math.clamp(traits.ValueRO.ColorG, 0f, 1f),
                    B = math.clamp(traits.ValueRO.ColorB, 0f, 1f)
                });
                
                float maxEnergy = 100f * (0.5f + startSize * 0.5f);
                ecb.AddComponent(offspring, new Energy
                {
                    Current = egg.ValueRO.Energy,
                    Maximum = maxEnergy,
                    Metabolism = 0.8f + _random.NextFloat(0f, 0.4f)
                });
                
                ecb.AddComponent(offspring, new Health { Current = 100f, Maximum = 100f });
                ecb.AddComponent(offspring, new Age { TimeAlive = 0f, Maturity = traits.ValueRO.StartingMaturity });
                ecb.AddComponent(offspring, new Diet { Value = math.clamp(traits.ValueRO.Diet, 0f, 1f) });
                ecb.AddComponent(offspring, organs.ValueRO);
                ecb.AddComponent(offspring, new BrainState { });
                ecb.AddComponent(offspring, new SensoryInputs { });
                ecb.AddComponent(offspring, new ReproductionState { });
                ecb.AddComponent(offspring, new InternalClock
                {
                    Phase = _random.NextFloat(0f, math.PI * 2f),
                    Frequency = traits.ValueRO.ClockFrequency,
                    MinuteCounter = 0f
                });
                
                // Use the pre-made crossover brain
                var brainComponent = new BiomeBrainComponent
                {
                    Brain = brainData.Brain,
                    InputBuffer = new float[InputNeurons.COUNT],
                    OutputBuffer = new float[OutputNeurons.COUNT]
                };
                ecb.AddComponent(offspring, brainComponent);
                
                ecb.AddSharedComponent(offspring, new EntityType { Value = EntityTypeEnum.Bibite });
                
                // Destroy egg
                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
