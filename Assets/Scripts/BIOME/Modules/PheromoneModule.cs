using System.Collections.Generic;
using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Pheromone Module definitions for the BIOME system.
    /// Adds pheromone sensing and emission capabilities.
    /// </summary>
    public static class PheromoneModules
    {
        // Definition IDs (continuing from Tier1Modules)
        public const int PHEROMONE_SENSE_1_MODULE = 101;
        public const int PHEROMONE_SENSE_2_MODULE = 102;
        public const int PHEROMONE_SENSE_3_MODULE = 103;
        public const int PHEROMONE_EMIT_MODULE = 104;
        
        private static Dictionary<int, BiomeModuleDefinition> _definitions;
        
        public static Dictionary<int, BiomeModuleDefinition> Definitions
        {
            get
            {
                if (_definitions == null) InitializeDefinitions();
                return _definitions;
            }
        }
        
        private static void InitializeDefinitions()
        {
            _definitions = new Dictionary<int, BiomeModuleDefinition>();
            
            // ================================================================
            // PHEROMONE SENSING MODULES (Input - one per channel)
            // ================================================================
            
            // Channel 1 (Red) Pheromone Sensor
            _definitions[PHEROMONE_SENSE_1_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = PHEROMONE_SENSE_1_MODULE,
                Name = "Pheromone Sense 1",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 2,
                OutputNodes = new List<ModuleNodeDef>
                {
                    // Intensity of pheromone at current location
                    new ModuleNodeDef("P1_Intensity", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    // Angle to gradient peak relative to heading (-1 to 1)
                    new ModuleNodeDef("P1_Angle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    // Absolute heading of gradient (Tier 2)
                    new ModuleNodeDef("P1_Heading", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = null // Populated by PheromoneSensingSystem
            };
            
            // Channel 2 (Green) Pheromone Sensor
            _definitions[PHEROMONE_SENSE_2_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = PHEROMONE_SENSE_2_MODULE,
                Name = "Pheromone Sense 2",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 2,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("P2_Intensity", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("P2_Angle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("P2_Heading", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = null
            };
            
            // Channel 3 (Blue) Pheromone Sensor
            _definitions[PHEROMONE_SENSE_3_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = PHEROMONE_SENSE_3_MODULE,
                Name = "Pheromone Sense 3",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 2,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("P3_Intensity", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("P3_Angle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("P3_Heading", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = null
            };
            
            // ================================================================
            // PHEROMONE EMISSION MODULE (Output)
            // ================================================================
            
            _definitions[PHEROMONE_EMIT_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = PHEROMONE_EMIT_MODULE,
                Name = "Pheromone Emission",
                Type = ModuleType.Output,
                Category = ModuleCategoryType.Social,
                MaxTier = 3,
                InputNodes = new List<ModuleNodeDef>
                {
                    // Emission strength for each channel (0-1)
                    new ModuleNodeDef("EmitP1", 0f, ActivationFunction.Sigmoid, 0f, 0.2f, 0.8f),
                    new ModuleNodeDef("EmitP2", 0f, ActivationFunction.Sigmoid, 0f, 0.2f, 0.8f),
                    new ModuleNodeDef("EmitP3", 0f, ActivationFunction.Sigmoid, 0f, 0.2f, 0.8f),
                    // Tier 2: Emission intensity multiplier
                    new ModuleNodeDef("EmitStrength", 0.5f, ActivationFunction.Sigmoid, 0.3f, 0.3f, 0.4f)
                },
                ProcessLogic = null // Handled by PheromoneEmissionSystem
            };
        }
        
        /// <summary>
        /// Register pheromone modules in the global registry
        /// </summary>
        public static void RegisterModules()
        {
            foreach (var kvp in Definitions)
            {
                if (!Tier1Modules.Definitions.ContainsKey(kvp.Key))
                {
                    // Add to tier1 definitions for unified access
                    // Note: In production, might want a separate ModuleRegistry
                }
            }
        }
        
        /// <summary>
        /// Create pheromone module instances for a bibite
        /// </summary>
        public static List<BiomeModuleInstance> CreatePheromoneModules(BiomeBrain brain, ref int nextNodeId, ref int nextModuleId)
        {
            var modules = new List<BiomeModuleInstance>();
            
            foreach (var def in Definitions.Values)
            {
                var instance = InstantiateModule(def, brain, ref nextNodeId, nextModuleId++);
                modules.Add(instance);
            }
            
            return modules;
        }
        
        private static BiomeModuleInstance InstantiateModule(BiomeModuleDefinition def, BiomeBrain brain,
            ref int nextNodeId, int instanceId)
        {
            var instance = new BiomeModuleInstance
            {
                InstanceId = instanceId,
                DefinitionId = def.DefinitionId,
                Name = def.Name,
                Type = def.Type,
                Category = def.Category,
                Tier = 1
            };
            
            // Create input nodes
            foreach (var nodeDef in def.InputNodes)
            {
                int nodeId = nextNodeId++;
                var node = new BiomeNode
                {
                    Id = (ushort)nodeId,
                    Type = NodeType.Hidden,
                    Affinity = GetDominantAffinity(nodeDef),
                    Activation = nodeDef.Activation,
                    Bias = nodeDef.DefaultBias,
                    Output = 0f,
                    Accumulator = 0f,
                    ModuleId = instanceId,
                    ModuleTier = 1
                };
                brain.Nodes[nodeId] = node;
                instance.InputNodeIds.Add(nodeId);
            }
            
            // Create output nodes
            foreach (var nodeDef in def.OutputNodes)
            {
                int nodeId = nextNodeId++;
                var node = new BiomeNode
                {
                    Id = (ushort)nodeId,
                    Type = NodeType.Hidden,
                    Affinity = GetDominantAffinity(nodeDef),
                    Activation = nodeDef.Activation,
                    Bias = nodeDef.DefaultBias,
                    Output = 0f,
                    Accumulator = 0f,
                    ModuleId = instanceId,
                    ModuleTier = 1
                };
                brain.Nodes[nodeId] = node;
                instance.OutputNodeIds.Add(nodeId);
            }
            
            return instance;
        }
        
        private static NodeAffinity GetDominantAffinity(ModuleNodeDef def)
        {
            if (def.GeneticAffinity >= def.HormonalAffinity && def.GeneticAffinity >= def.BehavioralAffinity)
                return NodeAffinity.Genetic;
            if (def.HormonalAffinity >= def.BehavioralAffinity)
                return NodeAffinity.Hormonal;
            return NodeAffinity.Behavioral;
        }
    }
    
    /// <summary>
    /// Extension methods for integrating pheromone modules with BiomeBrainComponent
    /// </summary>
    public static class PheromoneModuleExtensions
    {
        /// <summary>
        /// Initialize pheromone modules for a bibite
        /// </summary>
        public static void InitializePheromoneModules(this Systems.BiomeBrainComponent brainComp)
        {
            if (brainComp.Brain == null || brainComp.ModuleInstances == null) return;
            
            int nextNodeId = 4000; // Pheromone module nodes start at 4000
            if (brainComp.Brain.Nodes.Count > 0)
            {
                nextNodeId = System.Linq.Enumerable.Max(brainComp.Brain.Nodes.Keys) + 1;
            }
            
            int nextModuleId = brainComp.ModuleInstances.Count + 1;
            
            var pheromoneModules = PheromoneModules.CreatePheromoneModules(
                brainComp.Brain, ref nextNodeId, ref nextModuleId);
            
            brainComp.ModuleInstances.AddRange(pheromoneModules);
        }
        
        /// <summary>
        /// Set pheromone sensor values from ECS component
        /// </summary>
        public static void SetPheromoneSensorValues(this Systems.BiomeBrainComponent brainComp, 
            Systems.PheromoneSensor sensor)
        {
            if (brainComp.Brain == null || brainComp.ModuleInstances == null) return;
            
            // Find pheromone sensing modules
            foreach (var module in brainComp.ModuleInstances)
            {
                switch (module.DefinitionId)
                {
                    case PheromoneModules.PHEROMONE_SENSE_1_MODULE:
                        SetModuleOutputs(brainComp.Brain, module, 
                            sensor.Intensity1, sensor.Angle1, sensor.Heading1 / math.PI);
                        break;
                        
                    case PheromoneModules.PHEROMONE_SENSE_2_MODULE:
                        SetModuleOutputs(brainComp.Brain, module,
                            sensor.Intensity2, sensor.Angle2, sensor.Heading2 / math.PI);
                        break;
                        
                    case PheromoneModules.PHEROMONE_SENSE_3_MODULE:
                        SetModuleOutputs(brainComp.Brain, module,
                            sensor.Intensity3, sensor.Angle3, sensor.Heading3 / math.PI);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Get pheromone emission values for ECS component
        /// </summary>
        public static void GetPheromoneEmissionValues(this Systems.BiomeBrainComponent brainComp,
            out float emit1, out float emit2, out float emit3)
        {
            emit1 = 0f;
            emit2 = 0f;
            emit3 = 0f;
            
            if (brainComp.Brain == null || brainComp.ModuleInstances == null) return;
            
            // Find emission module
            foreach (var module in brainComp.ModuleInstances)
            {
                if (module.DefinitionId == PheromoneModules.PHEROMONE_EMIT_MODULE)
                {
                    if (module.InputNodeIds.Count >= 3)
                    {
                        emit1 = GetNodeOutput(brainComp.Brain, module.InputNodeIds[0]);
                        emit2 = GetNodeOutput(brainComp.Brain, module.InputNodeIds[1]);
                        emit3 = GetNodeOutput(brainComp.Brain, module.InputNodeIds[2]);
                    }
                    break;
                }
            }
        }
        
        private static void SetModuleOutputs(BiomeBrain brain, BiomeModuleInstance module,
            float val0, float val1, float val2)
        {
            if (module.OutputNodeIds.Count > 0)
                SetNodeOutput(brain, module.OutputNodeIds[0], val0);
            if (module.OutputNodeIds.Count > 1)
                SetNodeOutput(brain, module.OutputNodeIds[1], val1);
            if (module.OutputNodeIds.Count > 2)
                SetNodeOutput(brain, module.OutputNodeIds[2], val2);
        }
        
        private static void SetNodeOutput(BiomeBrain brain, int nodeId, float value)
        {
            if (brain.Nodes.TryGetValue(nodeId, out var node))
            {
                node.Output = value;
                brain.Nodes[nodeId] = node;
            }
        }
        
        private static float GetNodeOutput(BiomeBrain brain, int nodeId)
        {
            if (brain.Nodes.TryGetValue(nodeId, out var node))
            {
                return node.Output;
            }
            return 0f;
        }
    }
}
