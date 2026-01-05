using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using BiomeBibites.BIOME;
using BiomeBibites.Systems;

namespace BiomeBibites.Core
{
    /// <summary>
    /// Bootstrap class that initializes the BIOME simulation world.
    /// 
    /// This version uses the UNIFIED BIOME brain system where:
    /// - Everything goes through modules (no old InputNeurons/OutputNeurons)
    /// - Connections link module nodes directly
    /// - Brain presets create connections between modules
    /// </summary>
    public class SimulationBootstrap : MonoBehaviour
    {
        [Header("World Settings")]
        public float SimulationSize = 500f;
        public float BiomassDensity = 0.5f;
        
        [Header("Initial Population")]
        public int InitialBibites = 20;
        public int InitialPlantPellets = 100;
        
        [Header("Performance")]
        public int TargetFrameRate = 60;
        
        [Header("Brain Settings")]
        [Tooltip("Brain preset to use for initial bibites")]
        public BrainPreset InitialBrainPreset = BrainPreset.FoodSeeker;
        
        public enum BrainPreset
        {
            FoodSeeker,
            Carnivore,
            Omnivore,
            Random
        }
        
        private EntityManager _entityManager;
        private Entity _worldSettingsEntity;
        
        void Start()
        {
            Application.targetFrameRate = TargetFrameRate;
            
            // Get the default world's entity manager
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Create world settings singleton
            InitializeWorldSettings();
            
            // Spawn initial entities
            SpawnInitialPellets();
            SpawnInitialBibites();
            
            Debug.Log($"[BIOME] Simulation initialized: {SimulationSize}x{SimulationSize} world");
            Debug.Log($"[BIOME] {InitialBibites} bibites ({InitialBrainPreset}), {InitialPlantPellets} pellets");
        }
        
        private void InitializeWorldSettings()
        {
            _worldSettingsEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(_worldSettingsEntity, new WorldSettings
            {
                SimulationSize = SimulationSize,
                BiomassDensity = BiomassDensity,
                TotalEnergy = BiomassDensity * SimulationSize * SimulationSize,
                FreeBiomass = BiomassDensity * SimulationSize * SimulationSize * 0.5f,
                SimulationTime = 0f,
                FrameCount = 0
            });
            _entityManager.SetName(_worldSettingsEntity, "WorldSettings");
        }
        
        private void SpawnInitialPellets()
        {
            var random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            
            for (int i = 0; i < InitialPlantPellets; i++)
            {
                var entity = _entityManager.CreateEntity();
                
                float2 position = new float2(
                    random.NextFloat(-SimulationSize / 2f, SimulationSize / 2f),
                    random.NextFloat(-SimulationSize / 2f, SimulationSize / 2f)
                );
                
                _entityManager.AddComponentData(entity, new PlantPellet { Energy = 30f });
                _entityManager.AddComponentData(entity, new Position { Value = position });
                _entityManager.AddComponentData(entity, new Radius { Value = 2f });
                _entityManager.AddSharedComponent(entity, new EntityType { Value = EntityTypeEnum.PlantPellet });
            }
        }
        
        private void SpawnInitialBibites()
        {
            var random = new Unity.Mathematics.Random((uint)(System.DateTime.Now.Ticks + 12345));
            
            for (int i = 0; i < InitialBibites; i++)
            {
                SpawnBibite(
                    new float2(
                        random.NextFloat(-SimulationSize / 2f, SimulationSize / 2f),
                        random.NextFloat(-SimulationSize / 2f, SimulationSize / 2f)
                    ),
                    random.NextFloat(0f, math.PI * 2f),
                    ref random,
                    null  // No parent brain
                );
            }
        }
        
        /// <summary>
        /// Spawn a new bibite entity
        /// </summary>
        public Entity SpawnBibite(float2 position, float rotation, ref Unity.Mathematics.Random random, BiomeBrain parentBrain)
        {
            var entity = _entityManager.CreateEntity();
            
            // Core identity
            _entityManager.AddComponentData(entity, new BibiteTag { });
            
            // Physical state
            _entityManager.AddComponentData(entity, new Position { Value = position });
            _entityManager.AddComponentData(entity, new Velocity { Value = float2.zero });
            _entityManager.AddComponentData(entity, new Rotation { Value = rotation });
            _entityManager.AddComponentData(entity, new Radius { Value = 5f });
            
            // Size and color
            _entityManager.AddComponentData(entity, new Size { Ratio = 1f });
            _entityManager.AddComponentData(entity, new BibiteColor
            {
                R = random.NextFloat(0.2f, 1f),
                G = random.NextFloat(0.2f, 1f),
                B = random.NextFloat(0.2f, 1f)
            });
            
            // Energy and health
            _entityManager.AddComponentData(entity, new Energy
            {
                Current = 100f,
                Maximum = 100f,
                Metabolism = 1f
            });
            _entityManager.AddComponentData(entity, new Health
            {
                Current = 100f,
                Maximum = 100f
            });
            
            // Age - start as adults
            _entityManager.AddComponentData(entity, new Age
            {
                TimeAlive = 0f,
                Maturity = 1f
            });
            
            // Diet
            _entityManager.AddComponentData(entity, new Diet { Value = random.NextFloat(0f, 0.3f) });
            
            // Organs
            _entityManager.AddComponentData(entity, new Organs
            {
                Stomach = 0.2f,
                MoveMuscle = 0.25f,
                JawMuscle = 0.1f,
                Armor = 0.1f,
                EggOrgan = 0.15f,
                Throat = 0.1f,
                FatReserve = 0.1f
            });
            
            // Brain state (output buffer read by movement/eating systems)
            _entityManager.AddComponentData(entity, new BrainState
            {
                AccelerateOutput = 0f,
                RotateOutput = 0f,
                WantToEatOutput = 0f,
                WantToLayOutput = 0f,
                WantToAttackOutput = 0f
            });
            
            // Sensory inputs (legacy - may be removed later)
            _entityManager.AddComponentData(entity, new SensoryInputs { });
            
            // Reproduction state
            _entityManager.AddComponentData(entity, new ReproductionState
            {
                EggProgress = 0f,
                EggsStored = 0,
                ReadyToLay = false
            });
            
            // Internal clock
            _entityManager.AddComponentData(entity, new InternalClock
            {
                Phase = random.NextFloat(0f, math.PI * 2f),
                Frequency = 0.5f + random.NextFloat(0f, 1f)
            });
            
            // Stomach contents
            _entityManager.AddComponentData(entity, new StomachContents
            {
                PlantMatter = 0f,
                MeatMatter = 0f,
                DigestProgress = 0f
            });
            
            // Pheromone components
            _entityManager.AddComponentData(entity, new PheromoneEmitter());
            _entityManager.AddComponentData(entity, new PheromoneSensor());
            
            // ================================================================
            // CREATE UNIFIED BIOME BRAIN
            // ================================================================
            
            BiomeBrain brain;
            uint seed = random.NextUInt();
            
            if (parentBrain != null)
            {
                // Clone and mutate parent brain
                brain = parentBrain.Clone();
                var mutationRandom = new Unity.Mathematics.Random(seed);
                MutateBrain(brain, ref mutationRandom);
            }
            else
            {
                // Create new brain based on preset
                brain = CreateBrainFromPreset(InitialBrainPreset, seed);
            }
            
            // Add brain component
            var brainComponent = new BiomeBrainComponent { Brain = brain };
            _entityManager.AddComponentData(entity, brainComponent);
            
            _entityManager.AddSharedComponent(entity, new EntityType { Value = EntityTypeEnum.Bibite });
            
            return entity;
        }
        
        /// <summary>
        /// Create a brain from a preset type
        /// </summary>
        private BiomeBrain CreateBrainFromPreset(BrainPreset preset, uint seed)
        {
            switch (preset)
            {
                case BrainPreset.Carnivore:
                    return BiomeBrain.CreateCarnivore(seed);
                case BrainPreset.Omnivore:
                    return BiomeBrain.CreateOmnivore(seed);
                case BrainPreset.Random:
                    return BiomeBrain.CreateRandom(seed);
                case BrainPreset.FoodSeeker:
                default:
                    return BiomeBrain.CreateFoodSeeker(seed);
            }
        }
        
        /// <summary>
        /// Apply mutations to a brain
        /// </summary>
        private void MutateBrain(BiomeBrain brain, ref Unity.Mathematics.Random random)
        {
            // Connection weight mutation
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                if (random.NextFloat() < 0.1f)
                {
                    var conn = brain.Connections[i];
                    conn.Weight += random.NextFloat(-0.5f, 0.5f);
                    conn.Weight = math.clamp(conn.Weight, -5f, 5f);
                    brain.Connections[i] = conn;
                }
            }
            
            // Node bias mutation
            var nodeIds = new System.Collections.Generic.List<int>(brain.Nodes.Keys);
            foreach (var nodeId in nodeIds)
            {
                if (random.NextFloat() < 0.05f)
                {
                    var node = brain.Nodes[nodeId];
                    node.Bias += random.NextFloat(-0.2f, 0.2f);
                    node.Bias = math.clamp(node.Bias, -3f, 3f);
                    brain.Nodes[nodeId] = node;
                }
            }
            
            // Add new connection mutation
            if (random.NextFloat() < 0.05f && brain.Modules.Count > 0)
            {
                // Find source (output) nodes
                var sourceNodes = new System.Collections.Generic.List<int>();
                foreach (var module in brain.Modules)
                {
                    if (module.Type == ModuleType.Input || module.Type == ModuleType.Functional)
                        sourceNodes.AddRange(module.OutputNodeIds);
                }
                
                // Find sink (input) nodes
                var sinkNodes = new System.Collections.Generic.List<int>();
                foreach (var module in brain.Modules)
                {
                    if (module.Type == ModuleType.Output || module.Type == ModuleType.Functional)
                        sinkNodes.AddRange(module.InputNodeIds);
                }
                
                if (sourceNodes.Count > 0 && sinkNodes.Count > 0)
                {
                    int from = sourceNodes[random.NextInt(0, sourceNodes.Count)];
                    int to = sinkNodes[random.NextInt(0, sinkNodes.Count)];
                    float weight = random.NextFloat(-2f, 2f);
                    brain.AddConnection(from, to, weight);
                }
            }
        }
        
        /// <summary>
        /// Spawn a bibite from an egg (called by HatchingSystem)
        /// </summary>
        public Entity SpawnFromEgg(float2 position, float rotation, BiomeBrain parentBrain, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            return SpawnBibite(position, rotation, ref random, parentBrain);
        }
    }
}
