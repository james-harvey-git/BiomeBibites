using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    // ============================================================================
    // MODULE TYPES
    // ============================================================================
    
    /// <summary>
    /// The four types of modules in BIOME (from creator's description)
    /// </summary>
    public enum ModuleType : byte
    {
        /// <summary>
        /// Input modules only have OUTPUT nodes.
        /// They read from environment/bibite state and provide values.
        /// Example: Health Sense Module reads Agent.Health/Agent.MaxHealth
        /// </summary>
        Input = 0,
        
        /// <summary>
        /// Output modules only have INPUT nodes.
        /// They read node values and execute actions in the world.
        /// Example: Movement Module applies velocity based on Move/Rotation nodes
        /// </summary>
        Output = 1,
        
        /// <summary>
        /// Functional modules have both input AND output nodes.
        /// They contain internal logic too complex for simple activation functions.
        /// Example: Internal Clock Module, Value Memory Module
        /// </summary>
        Functional = 2,
        
        /// <summary>
        /// Meta modules have I/O nodes AND an internal network.
        /// Created through the Modularization Mutation.
        /// The internal network can continue to evolve.
        /// </summary>
        Meta = 3
    }
    
    /// <summary>
    /// Categories for organizing modules
    /// </summary>
    public enum ModuleCategoryType : byte
    {
        Internal = 0,    // Energy, Health, Maturity
        Sensing = 1,     // Vision, Touch, Pheromone sensing
        Processing = 2,  // Clock, Memory, evolved Meta-modules
        Motor = 3,       // Movement, Mouth
        Social = 4       // Pheromone emission
    }
    
    // ============================================================================
    // BIOME MODULE (REVISED)
    // ============================================================================
    
    /// <summary>
    /// A module in the BIOME network.
    /// Modules are self-contained functional units that can have internal logic.
    /// </summary>
    public class BiomeModuleInstance
    {
        // Identity
        public int InstanceId;              // Unique ID for this instance
        public int DefinitionId;            // Which module definition this is an instance of
        public string Name;
        
        // Type info
        public ModuleType Type;
        public ModuleCategoryType Category;
        public int Tier;                    // 1-4 for tiered modules
        
        // Interface - node IDs that this module exposes
        public List<int> InputNodeIds = new List<int>();   // Nodes this module READS
        public List<int> OutputNodeIds = new List<int>();  // Nodes this module PROVIDES
        
        // For Functional modules: internal state
        public Dictionary<string, float> InternalState = new Dictionary<string, float>();
        
        // For Meta modules: reference to template
        public int? TemplateId;
        
        // Runtime state
        public bool Enabled = true;
        public int LastProcessedFrame = -1;
    }
    
    // ============================================================================
    // MODULE DEFINITIONS (Templates for Tier-1 modules)
    // ============================================================================
    
    /// <summary>
    /// Static definition of a module type.
    /// Instances are created from these definitions.
    /// </summary>
    public class BiomeModuleDefinition
    {
        public int DefinitionId;
        public string Name;
        public ModuleType Type;
        public ModuleCategoryType Category;
        public int MaxTier;
        
        // Node definitions (relative IDs, mapped to actual IDs on instantiation)
        public List<ModuleNodeDef> InputNodes = new List<ModuleNodeDef>();
        public List<ModuleNodeDef> OutputNodes = new List<ModuleNodeDef>();
        
        // For Functional modules
        public List<string> InternalStateKeys = new List<string>();
        
        // Processing delegate
        public Action<BiomeModuleInstance, BiomeBrain, float> ProcessLogic;
    }
    
    /// <summary>
    /// Definition of a node within a module
    /// </summary>
    public struct ModuleNodeDef
    {
        public string Name;
        public float DefaultBias;
        public ActivationFunction Activation;
        public float GeneticAffinity;
        public float HormonalAffinity;
        public float BehavioralAffinity;
        
        public ModuleNodeDef(string name, float bias = 0f, 
            ActivationFunction act = ActivationFunction.Identity,
            float genetic = 0f, float hormonal = 0f, float behavioral = 1f)
        {
            Name = name;
            DefaultBias = bias;
            Activation = act;
            GeneticAffinity = genetic;
            HormonalAffinity = hormonal;
            BehavioralAffinity = behavioral;
        }
    }
    
    // ============================================================================
    // TIER-1 MODULE REGISTRY
    // ============================================================================
    
    /// <summary>
    /// Registry of all Tier-1 (bootstrap) module definitions.
    /// These are the "atoms" of the BIOME world - every bibite starts with these.
    /// </summary>
    public static class Tier1Modules
    {
        // Definition IDs
        public const int ENERGY_MODULE = 1;
        public const int HEALTH_MODULE = 2;
        public const int MATURITY_MODULE = 3;
        public const int STOMACH_MODULE = 4;
        public const int CLOCK_MODULE = 5;
        public const int VISION_PLANT_MODULE = 6;
        public const int VISION_MEAT_MODULE = 7;
        public const int VISION_BIBITE_MODULE = 8;
        public const int MOTOR_MODULE = 9;
        public const int MOUTH_MODULE = 10;
        public const int REPRODUCTION_MODULE = 11;
        public const int GROWTH_MODULE = 12;
        
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
            // INPUT MODULES (Sensors - only have output nodes)
            // ================================================================
            
            // ENERGY MODULE - reads energy state
            _definitions[ENERGY_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = ENERGY_MODULE,
                Name = "Energy Sense",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("EnergyRatio", 0f, ActivationFunction.Identity, 0f, 0.2f, 0.8f)
                },
                ProcessLogic = ProcessEnergyModule
            };
            
            // HEALTH MODULE - reads health state
            _definitions[HEALTH_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = HEALTH_MODULE,
                Name = "Health Sense",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("HealthRatio", 0f, ActivationFunction.Identity, 0f, 0.2f, 0.8f),
                    new ModuleNodeDef("AttackedDamage", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = ProcessHealthModule
            };
            
            // MATURITY MODULE - reads age/growth state
            _definitions[MATURITY_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = MATURITY_MODULE,
                Name = "Maturity Sense",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("Maturity", 0f, ActivationFunction.Identity, 0f, 0.5f, 0.5f),
                    new ModuleNodeDef("TimeAlive", 0f, ActivationFunction.Identity, 0f, 0.8f, 0.2f)
                },
                ProcessLogic = ProcessMaturityModule
            };
            
            // STOMACH MODULE - reads digestion state
            _definitions[STOMACH_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = STOMACH_MODULE,
                Name = "Stomach Sense",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("Fullness", 0f, ActivationFunction.Identity, 0f, 0.3f, 0.7f)
                },
                ProcessLogic = ProcessStomachModule
            };
            
            // VISION MODULES (one per target type for clarity)
            _definitions[VISION_PLANT_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = VISION_PLANT_MODULE,
                Name = "Plant Vision",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 4,  // Tier 1=presence, 2=direction, 3=distance, 4=details
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("PlantCloseness", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("PlantAngle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("PlantCount", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = ProcessVisionPlantModule
            };
            
            _definitions[VISION_MEAT_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = VISION_MEAT_MODULE,
                Name = "Meat Vision",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 4,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("MeatCloseness", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("MeatAngle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("MeatCount", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = ProcessVisionMeatModule
            };
            
            _definitions[VISION_BIBITE_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = VISION_BIBITE_MODULE,
                Name = "Bibite Vision",
                Type = ModuleType.Input,
                Category = ModuleCategoryType.Sensing,
                MaxTier = 4,
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("BibiteCloseness", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("BibiteAngle", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("BibiteCount", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("BibiteColorR", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("BibiteColorG", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("BibiteColorB", 0f, ActivationFunction.Identity, 0f, 0f, 1f)
                },
                ProcessLogic = ProcessVisionBibiteModule
            };
            
            // ================================================================
            // FUNCTIONAL MODULES (Have both I/O and internal logic)
            // ================================================================
            
            // CLOCK MODULE - internal timing
            _definitions[CLOCK_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = CLOCK_MODULE,
                Name = "Internal Clock",
                Type = ModuleType.Functional,
                Category = ModuleCategoryType.Processing,
                MaxTier = 4,
                InputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("ClockEnable", 1f, ActivationFunction.Identity, 0.5f, 0.3f, 0.2f),
                    new ModuleNodeDef("ClockPeriod", 1f, ActivationFunction.Identity, 0.8f, 0.2f, 0f)
                },
                OutputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("Tic", 0f, ActivationFunction.Identity, 0f, 0f, 1f),
                    new ModuleNodeDef("Counter", 0f, ActivationFunction.Identity, 0f, 0.5f, 0.5f)
                },
                InternalStateKeys = new List<string> { "accumulator" },
                ProcessLogic = ProcessClockModule
            };
            
            // ================================================================
            // OUTPUT MODULES (Actuators - only have input nodes)
            // ================================================================
            
            // MOTOR MODULE - movement control
            _definitions[MOTOR_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = MOTOR_MODULE,
                Name = "Motor Control",
                Type = ModuleType.Output,
                Category = ModuleCategoryType.Motor,
                MaxTier = 4,
                InputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("Accelerate", 0f, ActivationFunction.Tanh, 0f, 0f, 1f),
                    new ModuleNodeDef("Rotate", 0f, ActivationFunction.Tanh, 0f, 0f, 1f),
                    new ModuleNodeDef("Speed", 0f, ActivationFunction.Identity, 0f, 0f, 1f)  // Output for sensing
                },
                ProcessLogic = null  // Handled by MovementSystem
            };
            
            // MOUTH MODULE - eating and grabbing
            _definitions[MOUTH_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = MOUTH_MODULE,
                Name = "Mouth Control",
                Type = ModuleType.Output,
                Category = ModuleCategoryType.Motor,
                MaxTier = 2,
                InputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("WantToEat", 1.5f, ActivationFunction.Sigmoid, 0f, 0.1f, 0.9f),
                    new ModuleNodeDef("Digestion", 0.5f, ActivationFunction.Sigmoid, 0f, 0.2f, 0.8f),
                    new ModuleNodeDef("WantToAttack", 0f, ActivationFunction.Sigmoid, 0f, 0.1f, 0.9f),
                    new ModuleNodeDef("Grab", 0f, ActivationFunction.Tanh, 0f, 0f, 1f),
                    new ModuleNodeDef("IsGrabbing", 0f, ActivationFunction.Identity, 0f, 0f, 1f)  // Output for sensing
                },
                ProcessLogic = null  // Handled by EatingSystem, CombatSystem, GrabSystem
            };
            
            // REPRODUCTION MODULE
            _definitions[REPRODUCTION_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = REPRODUCTION_MODULE,
                Name = "Reproduction Control",
                Type = ModuleType.Output,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                InputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("EggProduction", 0.2f, ActivationFunction.Tanh, 0f, 0.3f, 0.7f),
                    new ModuleNodeDef("WantToLay", -1f, ActivationFunction.Sigmoid, 0f, 0.2f, 0.8f),
                    new ModuleNodeDef("EggStored", 0f, ActivationFunction.Identity, 0f, 0f, 1f)  // Output for sensing
                },
                ProcessLogic = null  // Handled by EggProductionSystem
            };
            
            // GROWTH MODULE
            _definitions[GROWTH_MODULE] = new BiomeModuleDefinition
            {
                DefinitionId = GROWTH_MODULE,
                Name = "Growth Control",
                Type = ModuleType.Output,
                Category = ModuleCategoryType.Internal,
                MaxTier = 1,
                InputNodes = new List<ModuleNodeDef>
                {
                    new ModuleNodeDef("WantToGrow", 0f, ActivationFunction.Sigmoid, 0f, 0.4f, 0.6f),
                    new ModuleNodeDef("WantToHeal", 0f, ActivationFunction.Sigmoid, 0f, 0.3f, 0.7f)
                },
                ProcessLogic = null  // Handled by GrowthSystem
            };
        }
        
        // ================================================================
        // MODULE PROCESSING LOGIC
        // ================================================================
        
        private static void ProcessEnergyModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // This would read from the bibite's Energy component
            // For now, just ensure the output node exists
            // Actual values are set by BiomeBrainSystem from ECS components
        }
        
        private static void ProcessHealthModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from Health component
        }
        
        private static void ProcessMaturityModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from Age component
        }
        
        private static void ProcessStomachModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from StomachContents component
        }
        
        private static void ProcessVisionPlantModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from SensoryInputs - calculated by sensing system
        }
        
        private static void ProcessVisionMeatModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from SensoryInputs
        }
        
        private static void ProcessVisionBibiteModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Reads from SensoryInputs
        }
        
        private static void ProcessClockModule(BiomeModuleInstance instance, BiomeBrain brain, float deltaTime)
        {
            // Get internal state
            if (!instance.InternalState.ContainsKey("accumulator"))
                instance.InternalState["accumulator"] = 0f;
            
            float accumulator = instance.InternalState["accumulator"];
            
            // Get input values (Enable and Period)
            float enable = 1f;
            float period = 1f;
            
            if (instance.InputNodeIds.Count >= 2)
            {
                if (brain.Nodes.TryGetValue(instance.InputNodeIds[0], out var enableNode))
                    enable = enableNode.Output;
                if (brain.Nodes.TryGetValue(instance.InputNodeIds[1], out var periodNode))
                    period = math.max(0.1f, periodNode.Output);
            }
            
            // Internal logic (from diagram):
            // if (En > 0): val += deltaTime
            // Clk = val > Period
            // if (val > Period): Clk = 1, Counter += 1, val -= Period
            // else: Clk = 0
            
            float tic = 0f;
            float counter = 0f;
            
            if (enable > 0)
            {
                accumulator += deltaTime;
                
                if (accumulator > period)
                {
                    tic = 1f;
                    counter = 1f;  // Increment signal
                    accumulator -= period;
                }
                else
                {
                    tic = 0f;
                }
            }
            
            instance.InternalState["accumulator"] = accumulator;
            
            // Set output node values
            if (instance.OutputNodeIds.Count >= 2)
            {
                if (brain.Nodes.TryGetValue(instance.OutputNodeIds[0], out var ticNode))
                {
                    ticNode.Output = tic;
                    brain.Nodes[instance.OutputNodeIds[0]] = ticNode;
                }
                if (brain.Nodes.TryGetValue(instance.OutputNodeIds[1], out var counterNode))
                {
                    counterNode.Output = counterNode.Output + counter;  // Accumulate counter
                    brain.Nodes[instance.OutputNodeIds[1]] = counterNode;
                }
            }
        }
        
        /// <summary>
        /// Create instances of all Tier-1 modules for a new bibite
        /// </summary>
        public static List<BiomeModuleInstance> CreateTier1Modules(BiomeBrain brain, ref int nextNodeId, ref int nextModuleId)
        {
            var modules = new List<BiomeModuleInstance>();
            
            foreach (var def in Definitions.Values)
            {
                var instance = InstantiateModule(def, brain, ref nextNodeId, nextModuleId++);
                modules.Add(instance);
            }
            
            return modules;
        }
        
        /// <summary>
        /// Instantiate a module from a definition, creating its nodes
        /// </summary>
        public static BiomeModuleInstance InstantiateModule(BiomeModuleDefinition def, BiomeBrain brain, 
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
            
            // Create input nodes (nodes this module READS from)
            foreach (var nodeDef in def.InputNodes)
            {
                int nodeId = nextNodeId++;
                var node = new BiomeNode
                {
                    Id = (ushort)nodeId,
                    Type = NodeType.Hidden,  // Module interface nodes are "hidden" in the network
                    Affinity = DominantAffinity(nodeDef),
                    Activation = nodeDef.Activation,
                    Bias = nodeDef.DefaultBias,
                    Output = 0f,
                    Accumulator = 0f,
                    FrameCounter = 0,
                    UpdateInterval = CalculateUpdateInterval(nodeDef),
                    ModuleId = instanceId,
                    ModuleTier = 1
                };
                brain.Nodes[nodeId] = node;
                instance.InputNodeIds.Add(nodeId);
            }
            
            // Create output nodes (nodes this module PROVIDES)
            foreach (var nodeDef in def.OutputNodes)
            {
                int nodeId = nextNodeId++;
                var node = new BiomeNode
                {
                    Id = (ushort)nodeId,
                    Type = NodeType.Hidden,
                    Affinity = DominantAffinity(nodeDef),
                    Activation = nodeDef.Activation,
                    Bias = nodeDef.DefaultBias,
                    Output = 0f,
                    Accumulator = 0f,
                    FrameCounter = 0,
                    UpdateInterval = CalculateUpdateInterval(nodeDef),
                    ModuleId = instanceId,
                    ModuleTier = 1
                };
                brain.Nodes[nodeId] = node;
                instance.OutputNodeIds.Add(nodeId);
            }
            
            // Initialize internal state
            foreach (var key in def.InternalStateKeys)
            {
                instance.InternalState[key] = 0f;
            }
            
            return instance;
        }
        
        private static NodeAffinity DominantAffinity(ModuleNodeDef def)
        {
            if (def.GeneticAffinity >= def.HormonalAffinity && def.GeneticAffinity >= def.BehavioralAffinity)
                return NodeAffinity.Genetic;
            if (def.HormonalAffinity >= def.BehavioralAffinity)
                return NodeAffinity.Hormonal;
            return NodeAffinity.Behavioral;
        }
        
        private static int CalculateUpdateInterval(ModuleNodeDef def)
        {
            // Genetic = never (essentially infinite)
            if (def.GeneticAffinity > 0.9f) return int.MaxValue;
            
            // Weighted average of hormonal (60 frames) and behavioral (1 frame)
            float total = def.HormonalAffinity + def.BehavioralAffinity;
            if (total < 0.01f) return int.MaxValue;
            
            float avgInterval = (def.HormonalAffinity * 60f + def.BehavioralAffinity * 1f) / total;
            return math.max(1, (int)math.round(avgInterval));
        }
    }
    
    // ============================================================================
    // META-MODULE TEMPLATES (For Modularization Mutation)
    // ============================================================================
    
    /// <summary>
    /// A template for Meta-Modules created by the Modularization Mutation.
    /// All instances of this template share the same internal structure.
    /// When the template mutates, all instances update together.
    /// </summary>
    public class MetaModuleTemplate
    {
        public int TemplateId;
        public string Name;
        
        // The internal network structure (shared by all instances)
        public List<BiomeNode> InternalNodes = new List<BiomeNode>();
        public List<BiomeConnection> InternalConnections = new List<BiomeConnection>();
        
        // Which internal nodes serve as the interface
        public List<int> InputNodeLocalIndices = new List<int>();   // Indices into InternalNodes
        public List<int> OutputNodeLocalIndices = new List<int>();  // Indices into InternalNodes
        
        // Mutation tracking
        public int Generation = 0;
        public int UsageCount = 0;  // How many bibites use this template
    }
}
