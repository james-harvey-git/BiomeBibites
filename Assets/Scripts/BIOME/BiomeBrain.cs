using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace BiomeBibites.BIOME
{
    // ============================================================================
    // UNIFIED BIOME BRAIN
    // 
    // This is the SINGLE network that describes a bibite. There is no separate
    // "gene list" and "neural network" - everything is unified.
    //
    // Key principles:
    // 1. Modules expose nodes - they don't connect directly to each other
    // 2. Connections link nodes between modules
    // 3. The old InputNeurons/OutputNeurons system is GONE
    // 4. All I/O goes through modules
    // ============================================================================
    
    /// <summary>
    /// The unified BIOME brain. Contains nodes, connections, and module instances.
    /// This single structure replaces both the old gene system AND neural network.
    /// </summary>
    public class BiomeBrain
    {
        // ================================================================
        // CORE DATA STRUCTURES
        // ================================================================
        
        /// <summary>
        /// All nodes in the network, keyed by their unique ID.
        /// Nodes can be: module interface nodes, hidden nodes, or meta-module internal nodes.
        /// </summary>
        public Dictionary<int, BiomeNode> Nodes = new Dictionary<int, BiomeNode>();
        
        /// <summary>
        /// All connections in the network.
        /// Connections link nodes together, carrying signals between them.
        /// </summary>
        public List<BiomeConnection> Connections = new List<BiomeConnection>();
        
        /// <summary>
        /// All module instances in this brain.
        /// Modules provide the interface to the environment (sensors/actuators).
        /// </summary>
        public List<BiomeModuleInstance> Modules = new List<BiomeModuleInstance>();
        
        /// <summary>
        /// Meta-module templates (for evolved modular structures).
        /// Shared across instances - when template mutates, all instances update.
        /// </summary>
        public List<MetaModuleTemplate> MetaModuleTemplates = new List<MetaModuleTemplate>();
        
        // ================================================================
        // ID MANAGEMENT
        // ================================================================
        
        /// <summary>Next available node ID</summary>
        public int NextNodeId = 1;
        
        /// <summary>Next available module instance ID</summary>
        public int NextModuleId = 1;
        
        /// <summary>Seed for reproducible randomness</summary>
        public uint RandomSeed;
        
        // ================================================================
        // NODE LOOKUP CACHE (for fast access by module)
        // ================================================================
        
        private Dictionary<string, int> _nodeNameCache = new Dictionary<string, int>();
        
        /// <summary>
        /// Get a node ID by module name and node name.
        /// Example: GetNodeId("Plant Vision", "PlantCloseness")
        /// </summary>
        public int GetNodeId(string moduleName, string nodeName)
        {
            string key = $"{moduleName}.{nodeName}";
            if (_nodeNameCache.TryGetValue(key, out int id))
                return id;
            return -1;
        }
        
        /// <summary>
        /// Get a node ID by module definition ID and output index.
        /// </summary>
        public int GetModuleOutputNodeId(int moduleDefId, int outputIndex)
        {
            var module = Modules.FirstOrDefault(m => m.DefinitionId == moduleDefId);
            if (module != null && outputIndex < module.OutputNodeIds.Count)
                return module.OutputNodeIds[outputIndex];
            return -1;
        }
        
        /// <summary>
        /// Get a node ID by module definition ID and input index.
        /// </summary>
        public int GetModuleInputNodeId(int moduleDefId, int inputIndex)
        {
            var module = Modules.FirstOrDefault(m => m.DefinitionId == moduleDefId);
            if (module != null && inputIndex < module.InputNodeIds.Count)
                return module.InputNodeIds[inputIndex];
            return -1;
        }
        
        /// <summary>
        /// Rebuild the node name cache after modules change
        /// </summary>
        public void RebuildNodeCache()
        {
            _nodeNameCache.Clear();
            foreach (var module in Modules)
            {
                var def = GetModuleDefinition(module.DefinitionId);
                if (def == null) continue;
                
                // Map output nodes
                for (int i = 0; i < def.OutputNodes.Count && i < module.OutputNodeIds.Count; i++)
                {
                    string key = $"{module.Name}.{def.OutputNodes[i].Name}";
                    _nodeNameCache[key] = module.OutputNodeIds[i];
                }
                
                // Map input nodes
                for (int i = 0; i < def.InputNodes.Count && i < module.InputNodeIds.Count; i++)
                {
                    string key = $"{module.Name}.{def.InputNodes[i].Name}";
                    _nodeNameCache[key] = module.InputNodeIds[i];
                }
            }
        }
        
        private BiomeModuleDefinition GetModuleDefinition(int defId)
        {
            if (Tier1Modules.Definitions.TryGetValue(defId, out var def))
                return def;
            if (PheromoneModules.Definitions.TryGetValue(defId, out var phDef))
                return phDef;
            return null;
        }
        
        // ================================================================
        // MODULE ACCESS HELPERS
        // ================================================================
        
        /// <summary>
        /// Get a module instance by definition ID
        /// </summary>
        public BiomeModuleInstance GetModule(int definitionId)
        {
            return Modules.FirstOrDefault(m => m.DefinitionId == definitionId);
        }
        
        /// <summary>
        /// Set a module's output node value (for Input modules - sensors)
        /// </summary>
        public void SetModuleOutput(int moduleDefId, int outputIndex, float value)
        {
            int nodeId = GetModuleOutputNodeId(moduleDefId, outputIndex);
            if (nodeId >= 0 && Nodes.TryGetValue(nodeId, out var node))
            {
                node.Output = value;
                Nodes[nodeId] = node;
            }
        }
        
        /// <summary>
        /// Get a module's input node value (for Output modules - actuators)
        /// </summary>
        public float GetModuleInput(int moduleDefId, int inputIndex)
        {
            int nodeId = GetModuleInputNodeId(moduleDefId, inputIndex);
            if (nodeId >= 0 && Nodes.TryGetValue(nodeId, out var node))
            {
                return node.Output;
            }
            return 0f;
        }
        
        // ================================================================
        // NETWORK CONSTRUCTION
        // ================================================================
        
        /// <summary>
        /// Create an empty brain with no modules or connections
        /// </summary>
        public static BiomeBrain CreateEmpty(uint seed = 0)
        {
            return new BiomeBrain
            {
                RandomSeed = seed == 0 ? (uint)DateTime.Now.Ticks : seed
            };
        }
        
        /// <summary>
        /// Initialize all Tier-1 modules (the bootstrap modules every bibite has)
        /// </summary>
        public void InitializeTier1Modules()
        {
            // Create all Tier-1 modules
            var tier1Modules = Tier1Modules.CreateTier1Modules(this, ref NextNodeId, ref NextModuleId);
            Modules.AddRange(tier1Modules);
            
            // Create pheromone modules
            var pheromoneModules = PheromoneModules.CreatePheromoneModules(this, ref NextNodeId, ref NextModuleId);
            Modules.AddRange(pheromoneModules);
            
            // Rebuild the node cache
            RebuildNodeCache();
        }
        
        /// <summary>
        /// Add a connection between two nodes (by node ID)
        /// </summary>
        public void AddConnection(int fromNodeId, int toNodeId, float weight)
        {
            // Validate nodes exist
            if (!Nodes.ContainsKey(fromNodeId) || !Nodes.ContainsKey(toNodeId))
            {
                UnityEngine.Debug.LogWarning($"Cannot add connection: node {fromNodeId} or {toNodeId} doesn't exist");
                return;
            }
            
            // Check for duplicate
            foreach (var conn in Connections)
            {
                if (conn.FromNode == fromNodeId && conn.ToNode == toNodeId)
                    return;
            }
            
            var fromNode = Nodes[fromNodeId];
            var toNode = Nodes[toNodeId];
            
            var connection = new BiomeConnection
            {
                FromNode = fromNodeId,
                ToNode = toNodeId,
                Weight = weight,
                Type = DetermineConnectionType(fromNode.Affinity, toNode.Affinity),
                Enabled = true
            };
            
            Connections.Add(connection);
        }
        
        /// <summary>
        /// Add a connection between module nodes (by module def ID and node index)
        /// </summary>
        public void AddModuleConnection(int fromModuleDefId, int fromOutputIndex,
                                         int toModuleDefId, int toInputIndex, float weight)
        {
            int fromNodeId = GetModuleOutputNodeId(fromModuleDefId, fromOutputIndex);
            int toNodeId = GetModuleInputNodeId(toModuleDefId, toInputIndex);
            
            if (fromNodeId < 0 || toNodeId < 0)
            {
                UnityEngine.Debug.LogWarning($"Cannot add module connection: module or node not found");
                return;
            }
            
            AddConnection(fromNodeId, toNodeId, weight);
        }
        
        private ConnectionType DetermineConnectionType(NodeAffinity from, NodeAffinity to)
        {
            // For now, all connections are standard
            // Future: could have modulatory/gating based on affinities
            return ConnectionType.Standard;
        }
        
        // ================================================================
        // NETWORK PROCESSING
        // ================================================================
        
        /// <summary>
        /// Process the brain network for one frame.
        /// 
        /// Processing order:
        /// 1. Input modules have already set their output node values (done externally)
        /// 2. Propagate signals through connections
        /// 3. Apply activation functions to nodes
        /// 4. Process functional modules
        /// 5. Output module input nodes now have their final values (read externally)
        /// </summary>
        public void Process(int frameCount)
        {
            // Reset accumulators for all nodes
            foreach (var nodeId in Nodes.Keys.ToList())
            {
                var node = Nodes[nodeId];
                node.Accumulator = node.Bias;  // Start with bias
                Nodes[nodeId] = node;
            }
            
            // Propagate through connections
            foreach (var conn in Connections)
            {
                if (!conn.Enabled) continue;
                
                // Check if this connection should propagate this frame
                if (!ShouldPropagate(conn, frameCount)) continue;
                
                if (Nodes.TryGetValue(conn.FromNode, out var fromNode) &&
                    Nodes.TryGetValue(conn.ToNode, out var toNode))
                {
                    toNode.Accumulator += fromNode.Output * conn.Weight;
                    Nodes[conn.ToNode] = toNode;
                }
            }
            
            // Apply activation functions to hidden/output nodes
            // (Input module output nodes are set directly, don't process them)
            foreach (var nodeId in Nodes.Keys.ToList())
            {
                var node = Nodes[nodeId];
                
                // Skip nodes that belong to Input modules (their values are set externally)
                if (IsInputModuleNode(nodeId)) continue;
                
                // Check if this node should update this frame (affinity-based timing)
                if (!ShouldUpdateNode(node, frameCount)) continue;
                
                node.Output = ApplyActivation(node.Accumulator, node.Activation);
                node.FrameCounter = frameCount;
                Nodes[nodeId] = node;
            }
        }
        
        private bool IsInputModuleNode(int nodeId)
        {
            foreach (var module in Modules)
            {
                if (module.Type == ModuleType.Input && module.OutputNodeIds.Contains(nodeId))
                    return true;
            }
            return false;
        }
        
        private bool ShouldPropagate(BiomeConnection conn, int frameCount)
        {
            // Behavioral connections: every frame
            // Hormonal connections: every ~60 frames
            // Genetic connections: never (fixed at birth)
            
            var fromNode = Nodes[conn.FromNode];
            switch (fromNode.Affinity)
            {
                case NodeAffinity.Genetic:
                    return false;  // Genetic connections don't propagate
                case NodeAffinity.Hormonal:
                    return (frameCount % 60) == 0;  // ~1/second at 60fps
                case NodeAffinity.Behavioral:
                default:
                    return true;  // Every frame
            }
        }
        
        private bool ShouldUpdateNode(BiomeNode node, int frameCount)
        {
            if (node.UpdateInterval <= 1) return true;
            if (node.UpdateInterval == int.MaxValue) return false;
            return (frameCount - node.FrameCounter) >= node.UpdateInterval;
        }
        
        private float ApplyActivation(float value, ActivationFunction activation)
        {
            switch (activation)
            {
                case ActivationFunction.Sigmoid:
                    return 1f / (1f + math.exp(-value));
                case ActivationFunction.Tanh:
                    return math.tanh(value);
                case ActivationFunction.ReLU:
                    return math.max(0, value);
                case ActivationFunction.LeakyReLU:
                    return value > 0 ? value : value * 0.01f;
                case ActivationFunction.Gaussian:
                    return math.exp(-value * value);
                case ActivationFunction.Sin:
                    return math.sin(value);
                case ActivationFunction.Abs:
                    return math.abs(value);
                case ActivationFunction.Step:
                    return value > 0 ? 1f : 0f;
                case ActivationFunction.Identity:
                default:
                    return value;
            }
        }
        
        // ================================================================
        // BRAIN PRESETS (replacing the old BrainPresets class)
        // ================================================================
        
        /// <summary>
        /// Create a Food Seeker brain - basic herbivore that seeks plants.
        /// 
        /// Connections:
        /// - PlantCloseness → Accelerate (move toward plants)
        /// - PlantAngle → Rotate (turn toward plants)
        /// - Fullness → Digestion (digest when full)
        /// </summary>
        public static BiomeBrain CreateFoodSeeker(uint seed = 0)
        {
            var brain = CreateEmpty(seed);
            brain.InitializeTier1Modules();
            
            // PlantCloseness → Accelerate
            brain.AddModuleConnection(
                Tier1Modules.VISION_PLANT_MODULE, 0,  // PlantCloseness output
                Tier1Modules.MOTOR_MODULE, 0,         // Accelerate input
                1.0f
            );
            
            // PlantAngle → Rotate
            brain.AddModuleConnection(
                Tier1Modules.VISION_PLANT_MODULE, 1,  // PlantAngle output
                Tier1Modules.MOTOR_MODULE, 1,         // Rotate input
                1.16f
            );
            
            // Fullness → Digestion
            brain.AddModuleConnection(
                Tier1Modules.STOMACH_MODULE, 0,       // Fullness output
                Tier1Modules.MOUTH_MODULE, 1,         // Digestion input
                1.0f
            );
            
            // Energy → WantToLay (reproduce when high energy)
            brain.AddModuleConnection(
                Tier1Modules.ENERGY_MODULE, 0,        // EnergyRatio output
                Tier1Modules.REPRODUCTION_MODULE, 1,  // WantToLay input
                1.5f
            );
            
            return brain;
        }
        
        /// <summary>
        /// Create a Carnivore brain - hunts other bibites
        /// </summary>
        public static BiomeBrain CreateCarnivore(uint seed = 0)
        {
            var brain = CreateEmpty(seed);
            brain.InitializeTier1Modules();
            
            // BibiteCloseness → Accelerate (move toward prey)
            brain.AddModuleConnection(
                Tier1Modules.VISION_BIBITE_MODULE, 0,
                Tier1Modules.MOTOR_MODULE, 0,
                0.8f
            );
            
            // BibiteAngle → Rotate (turn toward prey)
            brain.AddModuleConnection(
                Tier1Modules.VISION_BIBITE_MODULE, 1,
                Tier1Modules.MOTOR_MODULE, 1,
                2.5f
            );
            
            // BibiteCloseness → WantToAttack
            brain.AddModuleConnection(
                Tier1Modules.VISION_BIBITE_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 2,  // WantToAttack
                3.0f
            );
            
            // MeatCloseness → WantToEat (eat the kills)
            brain.AddModuleConnection(
                Tier1Modules.VISION_MEAT_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 0,  // WantToEat
                2.0f
            );
            
            // MeatAngle → Rotate (turn toward meat)
            brain.AddModuleConnection(
                Tier1Modules.VISION_MEAT_MODULE, 1,
                Tier1Modules.MOTOR_MODULE, 1,
                1.5f
            );
            
            // Fullness → Digestion
            brain.AddModuleConnection(
                Tier1Modules.STOMACH_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 1,
                1.0f
            );
            
            // Energy → WantToLay
            brain.AddModuleConnection(
                Tier1Modules.ENERGY_MODULE, 0,
                Tier1Modules.REPRODUCTION_MODULE, 1,
                1.2f
            );
            
            return brain;
        }
        
        /// <summary>
        /// Create an Omnivore brain - eats both plants and meat
        /// </summary>
        public static BiomeBrain CreateOmnivore(uint seed = 0)
        {
            var brain = CreateEmpty(seed);
            brain.InitializeTier1Modules();
            
            // PlantCloseness → Accelerate
            brain.AddModuleConnection(
                Tier1Modules.VISION_PLANT_MODULE, 0,
                Tier1Modules.MOTOR_MODULE, 0,
                0.4f
            );
            
            // MeatCloseness → Accelerate
            brain.AddModuleConnection(
                Tier1Modules.VISION_MEAT_MODULE, 0,
                Tier1Modules.MOTOR_MODULE, 0,
                0.5f
            );
            
            // PlantAngle → Rotate
            brain.AddModuleConnection(
                Tier1Modules.VISION_PLANT_MODULE, 1,
                Tier1Modules.MOTOR_MODULE, 1,
                1.5f
            );
            
            // MeatAngle → Rotate
            brain.AddModuleConnection(
                Tier1Modules.VISION_MEAT_MODULE, 1,
                Tier1Modules.MOTOR_MODULE, 1,
                1.8f
            );
            
            // PlantCloseness → WantToEat
            brain.AddModuleConnection(
                Tier1Modules.VISION_PLANT_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 0,
                2.0f
            );
            
            // MeatCloseness → WantToEat
            brain.AddModuleConnection(
                Tier1Modules.VISION_MEAT_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 0,
                2.5f
            );
            
            // Fullness → Digestion
            brain.AddModuleConnection(
                Tier1Modules.STOMACH_MODULE, 0,
                Tier1Modules.MOUTH_MODULE, 1,
                1.0f
            );
            
            // Energy → WantToLay
            brain.AddModuleConnection(
                Tier1Modules.ENERGY_MODULE, 0,
                Tier1Modules.REPRODUCTION_MODULE, 1,
                1.0f
            );
            
            return brain;
        }
        
        /// <summary>
        /// Create a random brain with random connections between modules
        /// </summary>
        public static BiomeBrain CreateRandom(uint seed = 0)
        {
            var brain = CreateEmpty(seed);
            brain.InitializeTier1Modules();
            
            var random = new Random(seed == 0 ? (uint)DateTime.Now.Ticks : seed);
            
            // Collect all input module output nodes (sources)
            var sourceNodes = new List<int>();
            foreach (var module in brain.Modules)
            {
                if (module.Type == ModuleType.Input || module.Type == ModuleType.Functional)
                {
                    sourceNodes.AddRange(module.OutputNodeIds);
                }
            }
            
            // Collect all output module input nodes (sinks)
            var sinkNodes = new List<int>();
            foreach (var module in brain.Modules)
            {
                if (module.Type == ModuleType.Output || module.Type == ModuleType.Functional)
                {
                    sinkNodes.AddRange(module.InputNodeIds);
                }
            }
            
            // Add random connections
            int numConnections = random.NextInt(5, 15);
            for (int i = 0; i < numConnections; i++)
            {
                int fromNode = sourceNodes[random.NextInt(0, sourceNodes.Count)];
                int toNode = sinkNodes[random.NextInt(0, sinkNodes.Count)];
                float weight = random.NextFloat(-2f, 2f);
                brain.AddConnection(fromNode, toNode, weight);
            }
            
            return brain;
        }
        
        // ================================================================
        // CLONING (for reproduction)
        // ================================================================
        
        /// <summary>
        /// Create a deep copy of this brain
        /// </summary>
        public BiomeBrain Clone()
        {
            var clone = new BiomeBrain
            {
                RandomSeed = RandomSeed,
                NextNodeId = NextNodeId,
                NextModuleId = NextModuleId
            };
            
            // Clone nodes
            foreach (var kvp in Nodes)
            {
                clone.Nodes[kvp.Key] = kvp.Value;  // BiomeNode is a struct, so this copies
            }
            
            // Clone connections
            foreach (var conn in Connections)
            {
                clone.Connections.Add(new BiomeConnection
                {
                    FromNode = conn.FromNode,
                    ToNode = conn.ToNode,
                    Weight = conn.Weight,
                    Type = conn.Type,
                    Enabled = conn.Enabled
                });
            }
            
            // Clone modules
            foreach (var module in Modules)
            {
                var clonedModule = new BiomeModuleInstance
                {
                    InstanceId = module.InstanceId,
                    DefinitionId = module.DefinitionId,
                    Name = module.Name,
                    Type = module.Type,
                    Category = module.Category,
                    Tier = module.Tier,
                    InputNodeIds = new List<int>(module.InputNodeIds),
                    OutputNodeIds = new List<int>(module.OutputNodeIds),
                    InternalState = new Dictionary<string, float>(module.InternalState),
                    TemplateId = module.TemplateId,
                    Enabled = module.Enabled
                };
                clone.Modules.Add(clonedModule);
            }
            
            // Clone meta-module templates (these are shared references for now)
            clone.MetaModuleTemplates = new List<MetaModuleTemplate>(MetaModuleTemplates);
            
            clone.RebuildNodeCache();
            return clone;
        }
    }
    
    // ============================================================================
    // SUPPORTING TYPES
    // ============================================================================
    
    public enum ActivationFunction : byte
    {
        Identity = 0,
        Sigmoid = 1,
        Tanh = 2,
        ReLU = 3,
        LeakyReLU = 4,
        Gaussian = 5,
        Sin = 6,
        Abs = 7,
        Step = 8
    }
    
    public enum ConnectionType : byte
    {
        Standard = 0,     // Normal weighted sum
        Modulatory = 1,   // Scales node sensitivity
        Gating = 2        // Multiplicative gating
    }
    
    public enum NodeType : byte
    {
        Hidden = 0,
        Input = 1,   // Not used in unified system (modules provide input)
        Output = 2   // Not used in unified system (modules read output)
    }
    
    public enum NodeAffinity : byte
    {
        Behavioral = 0,  // Updates every frame
        Hormonal = 1,    // Updates slowly (~1/second)
        Genetic = 2      // Fixed at birth
    }
    
    /// <summary>
    /// A node in the BIOME network.
    /// Nodes are the fundamental computing units - they replace both genes AND neurons.
    /// </summary>
    public struct BiomeNode
    {
        public ushort Id;
        public NodeType Type;
        public NodeAffinity Affinity;
        public ActivationFunction Activation;
        
        public float Bias;        // Default/baseline value (like a "gene value")
        public float Accumulator; // Accumulated input this frame
        public float Output;      // Result after activation function
        
        // Timing
        public int FrameCounter;    // Last frame this node was updated
        public int UpdateInterval;  // How often to update (1=every frame, 60=~1/sec)
        
        // Module association
        public int ModuleId;      // Which module instance this node belongs to
        public int ModuleTier;    // Tier of the module
    }
    
    /// <summary>
    /// A connection between two nodes in the BIOME network.
    /// </summary>
    public struct BiomeConnection
    {
        public int FromNode;
        public int ToNode;
        public float Weight;
        public ConnectionType Type;
        public bool Enabled;
    }
}
