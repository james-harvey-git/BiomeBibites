using Unity.Entities;
using Unity.Mathematics;
using BiomeBibites.BIOME;
using System.Collections.Generic;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles egg incubation and hatching.
    /// When an egg's hatch progress reaches 100%, a new bibite is born.
    /// Now also handles module inheritance.
    /// </summary>
    public partial class HatchingSystem : SystemBase
    {
        private Random _random;
        
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
            _random = new Random((uint)System.DateTime.Now.Ticks);
        }

        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (egg, position, traits, organs, entity) 
                in SystemAPI.Query<
                    RefRW<Egg>,
                    RefRO<Position>,
                    RefRO<InheritedTraits>,
                    RefRO<Organs>>()
                .WithEntityAccess())
            {
                // Progress incubation
                float hatchRate = 1f / egg.ValueRO.HatchTime;
                egg.ValueRW.HatchProgress += hatchRate * deltaTime;
                
                // Check if ready to hatch
                if (egg.ValueRO.HatchProgress < 1f) continue;
                
                // === HATCH THE EGG ===
                var offspring = ecb.CreateEntity();
                
                // Core identity
                ecb.AddComponent(offspring, new BibiteTag { });
                ecb.AddComponent(offspring, new Generation { Value = traits.ValueRO.Generation });
                
                // Position (at egg location with slight offset)
                float2 offsetDir = new float2(
                    math.cos(_random.NextFloat(0f, math.PI * 2f)),
                    math.sin(_random.NextFloat(0f, math.PI * 2f))
                );
                float2 birthPos = position.ValueRO.Value + offsetDir * 2f;
                
                ecb.AddComponent(offspring, new Position { Value = birthPos });
                ecb.AddComponent(offspring, new Velocity { Value = float2.zero });
                ecb.AddComponent(offspring, new Rotation { Value = _random.NextFloat(0f, math.PI * 2f) });
                
                // Size based on starting maturity
                float startSize = 0.3f + traits.ValueRO.StartingMaturity * 0.7f;
                ecb.AddComponent(offspring, new Size { Ratio = startSize });
                ecb.AddComponent(offspring, new Radius { Value = 5f * startSize });
                
                // Color
                ecb.AddComponent(offspring, new BibiteColor
                {
                    R = traits.ValueRO.ColorR,
                    G = traits.ValueRO.ColorG,
                    B = traits.ValueRO.ColorB
                });
                
                // Energy from egg
                float maxEnergy = 100f * (0.5f + startSize * 0.5f);
                ecb.AddComponent(offspring, new Energy
                {
                    Current = egg.ValueRO.Energy,
                    Maximum = maxEnergy,
                    Metabolism = 0.8f + _random.NextFloat(0f, 0.4f)
                });
                
                ecb.AddComponent(offspring, new Health
                {
                    Current = 100f,
                    Maximum = 100f
                });
                
                // Age and maturity
                ecb.AddComponent(offspring, new Age
                {
                    TimeAlive = 0f,
                    Maturity = traits.ValueRO.StartingMaturity
                });
                
                // Diet
                ecb.AddComponent(offspring, new Diet { Value = traits.ValueRO.Diet });
                
                // Inherited organs
                ecb.AddComponent(offspring, organs.ValueRO);
                
                // Brain state
                ecb.AddComponent(offspring, new BrainState { });
                ecb.AddComponent(offspring, new SensoryInputs { });
                
                // Reproduction state (empty for newborn)
                ecb.AddComponent(offspring, new ReproductionState
                {
                    EggProgress = 0f,
                    EggsStored = 0,
                    ReadyToLay = false
                });
                
                // Internal clock
                ecb.AddComponent(offspring, new InternalClock
                {
                    Phase = _random.NextFloat(0f, math.PI * 2f),
                    Frequency = traits.ValueRO.ClockFrequency,
                    MinuteCounter = 0f
                });
                
                // === BRAIN AND MODULE INHERITANCE ===
                BiomeBrain offspringBrain = null;
                List<BiomeModuleInstance> offspringModules = null;
                
                // Try to get parent brain and modules
                if (EntityManager.HasComponent<EggBrainData>(entity))
                {
                    var brainData = EntityManager.GetComponentData<EggBrainData>(entity);
                    
                    if (EntityManager.Exists(brainData.ParentEntity) && 
                        EntityManager.HasComponent<BiomeBrainComponent>(brainData.ParentEntity))
                    {
                        var parentBrainComp = EntityManager.GetComponentData<BiomeBrainComponent>(brainData.ParentEntity);
                        if (parentBrainComp.Brain != null)
                        {
                            // Clone and mutate parent brain
                            offspringBrain = CloneBrain(parentBrainComp.Brain);
                            var mutRandom = new Random(traits.ValueRO.BrainSeed);
                            BiomeGenome.Mutate(offspringBrain, ref mutRandom);
                            offspringBrain.Generation = traits.ValueRO.Generation;
                            offspringBrain.RandomSeed = traits.ValueRO.BrainSeed;
                            
                            // Clone parent modules
                            if (parentBrainComp.ModuleInstances != null && parentBrainComp.ModuleInstances.Count > 0)
                            {
                                offspringModules = CloneModuleInstances(parentBrainComp.ModuleInstances);
                            }
                        }
                    }
                }
                
                // Fallback: create new brain if no parent brain available
                if (offspringBrain == null)
                {
                    offspringBrain = BiomeBrain.CreateFoodSeeker(traits.ValueRO.BrainSeed);
                    offspringBrain.Generation = traits.ValueRO.Generation;
                }
                
                var brainComponent = new BiomeBrainComponent
                {
                    Brain = offspringBrain,
                    InputBuffer = new float[InputNeurons.COUNT],
                    OutputBuffer = new float[OutputNeurons.COUNT]
                };
                
                // Initialize or inherit modules
                if (offspringModules != null && offspringModules.Count > 0)
                {
                    brainComponent.ModuleInstances = offspringModules;
                }
                else
                {
                    // Initialize Tier-1 modules for new bibite
                    brainComponent.InitializeModules();
                }
                
                ecb.AddComponent(offspring, brainComponent);
                
                // Entity type
                ecb.AddSharedComponent(offspring, new EntityType { Value = EntityTypeEnum.Bibite });
                
                // Destroy the egg
                ecb.DestroyEntity(entity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        private BiomeBrain CloneBrain(BiomeBrain source)
        {
            var clone = new BiomeBrain();
            clone.RandomSeed = source.RandomSeed;
            clone.Generation = source.Generation;
            clone.SpeciesId = source.SpeciesId;
            clone.InnovationCounter = source.InnovationCounter;
            clone.Fitness = 0f;
            
            // Clone nodes (Dictionary: key-value pairs)
            foreach (var kvp in source.Nodes)
            {
                clone.Nodes[kvp.Key] = kvp.Value;
            }
            
            // Clone connections
            foreach (var conn in source.Connections)
            {
                clone.Connections.Add(conn);
            }
            
            // Clone modules (constructor order: id, name, tier, category)
            foreach (var module in source.Modules)
            {
                var clonedModule = new BiomeModule(module.ModuleId, module.Name, module.Tier, module.Category);
                clonedModule.ContainedNodes.AddRange(module.ContainedNodes);
                clonedModule.ContainedConnections.AddRange(module.ContainedConnections);
                clonedModule.InputNodeIndices.AddRange(module.InputNodeIndices);
                clonedModule.OutputNodeIndices.AddRange(module.OutputNodeIndices);
                clone.Modules.Add(clonedModule);
            }
            
            return clone;
        }
        
        /// <summary>
        /// Clone module instances for inheritance
        /// </summary>
        private List<BiomeModuleInstance> CloneModuleInstances(List<BiomeModuleInstance> sourceModules)
        {
            var clonedModules = new List<BiomeModuleInstance>();
            
            foreach (var source in sourceModules)
            {
                var clone = new BiomeModuleInstance
                {
                    InstanceId = source.InstanceId,
                    DefinitionId = source.DefinitionId,
                    Name = source.Name,
                    Type = source.Type,
                    Category = source.Category,
                    Tier = source.Tier,
                    TemplateId = source.TemplateId,
                    Enabled = source.Enabled
                };
                
                // Clone node ID lists
                clone.InputNodeIds.AddRange(source.InputNodeIds);
                clone.OutputNodeIds.AddRange(source.OutputNodeIds);
                
                // Clone internal state (reset to defaults for offspring)
                foreach (var kvp in source.InternalState)
                {
                    clone.InternalState[kvp.Key] = 0f; // Reset state for new bibite
                }
                
                clonedModules.Add(clone);
            }
            
            return clonedModules;
        }
    }
    
    /// <summary>
    /// System to handle eggs that lose their parent (parent died)
    /// These eggs can still hatch but won't have brain inheritance
    /// </summary>
    public partial class OrphanEggSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (brainData, entity) 
                in SystemAPI.Query<RefRO<EggBrainData>>()
                .WithEntityAccess())
            {
                // Check if parent still exists
                if (!EntityManager.Exists(brainData.ValueRO.ParentEntity))
                {
                    // Remove the brain data component - egg will hatch with new brain
                    ecb.RemoveComponent<EggBrainData>(entity);
                }
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
