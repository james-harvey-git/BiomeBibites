using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Module-specific mutations for the BIOME system.
    /// These work with BiomeModuleInstance and Tier1Modules.
    /// </summary>
    public static class ModuleMutations
    {
        /// <summary>
        /// Apply all module mutations to a bibite's brain and modules
        /// </summary>
        public static void MutateModules(BiomeBrain brain, List<BiomeModuleInstance> modules, 
            ref Random random, ModuleMutationConfig config = null)
        {
            config ??= ModuleMutationConfig.Default;
            
            // Module duplication
            if (random.NextFloat() < config.DuplicationRate)
            {
                TryDuplicateModule(brain, modules, ref random);
            }
            
            // Module tier upgrade
            if (random.NextFloat() < config.TierUpgradeRate)
            {
                TryUpgradeModuleTier(brain, modules, ref random);
            }
            
            // Inter-module connection mutation
            if (random.NextFloat() < config.InterModuleConnectionRate)
            {
                TryAddInterModuleConnection(brain, modules, ref random);
            }
            
            // Module internal connection mutation
            if (random.NextFloat() < config.InternalConnectionRate)
            {
                TryMutateModuleInternalConnections(brain, modules, ref random);
            }
            
            // Module parameter drift (for modules with genetic-affinity nodes)
            if (random.NextFloat() < config.ParameterDriftRate)
            {
                TryMutateModuleParameters(brain, modules, ref random);
            }
        }
        
        /// <summary>
        /// Duplicate an existing module, creating a new instance with mutated connections
        /// </summary>
        public static void TryDuplicateModule(BiomeBrain brain, List<BiomeModuleInstance> modules, ref Random random)
        {
            if (modules.Count == 0) return;
            if (modules.Count >= 20) return; // Limit total modules
            
            // Pick a random module to duplicate
            int sourceIdx = random.NextInt(0, modules.Count);
            var source = modules[sourceIdx];
            
            // Some modules shouldn't be duplicated (e.g., Energy, Health - only one makes sense)
            if (!CanDuplicate(source.DefinitionId)) return;
            
            // Find next available IDs
            int newInstanceId = modules.Max(m => m.InstanceId) + 1;
            int newNodeIdBase = brain.Nodes.Keys.Max() + 1;
            
            // Create new module instance
            var newModule = new BiomeModuleInstance
            {
                InstanceId = newInstanceId,
                DefinitionId = source.DefinitionId,
                Name = $"{source.Name}_{newInstanceId}",
                Type = source.Type,
                Category = source.Category,
                Tier = source.Tier,
                Enabled = true
            };
            
            // Create node mapping for connections
            var nodeMapping = new Dictionary<int, int>();
            
            // Clone input nodes
            int nodeId = newNodeIdBase;
            foreach (var oldNodeId in source.InputNodeIds)
            {
                if (!brain.Nodes.ContainsKey(oldNodeId)) continue;
                
                var oldNode = brain.Nodes[oldNodeId];
                var newNode = oldNode;
                newNode.Id = (ushort)nodeId;
                newNode.ModuleId = newInstanceId;
                // Add small mutation to bias
                newNode.Bias += random.NextFloat(-0.1f, 0.1f);
                
                brain.Nodes[nodeId] = newNode;
                newModule.InputNodeIds.Add(nodeId);
                nodeMapping[oldNodeId] = nodeId;
                nodeId++;
            }
            
            // Clone output nodes
            foreach (var oldNodeId in source.OutputNodeIds)
            {
                if (!brain.Nodes.ContainsKey(oldNodeId)) continue;
                
                var oldNode = brain.Nodes[oldNodeId];
                var newNode = oldNode;
                newNode.Id = (ushort)nodeId;
                newNode.ModuleId = newInstanceId;
                newNode.Bias += random.NextFloat(-0.1f, 0.1f);
                
                brain.Nodes[nodeId] = newNode;
                newModule.OutputNodeIds.Add(nodeId);
                nodeMapping[oldNodeId] = nodeId;
                nodeId++;
            }
            
            // Clone internal state
            foreach (var kvp in source.InternalState)
            {
                newModule.InternalState[kvp.Key] = 0f; // Reset for new instance
            }
            
            // Clone connections that were internal to the original module
            foreach (var conn in brain.Connections)
            {
                bool fromInSource = source.InputNodeIds.Contains(conn.FromNode) || 
                                   source.OutputNodeIds.Contains(conn.FromNode);
                bool toInSource = source.InputNodeIds.Contains(conn.ToNode) || 
                                 source.OutputNodeIds.Contains(conn.ToNode);
                
                if (fromInSource && toInSource && 
                    nodeMapping.ContainsKey(conn.FromNode) && 
                    nodeMapping.ContainsKey(conn.ToNode))
                {
                    var newConn = new BiomeConnection(
                        (ushort)nodeMapping[conn.FromNode],
                        (ushort)nodeMapping[conn.ToNode],
                        conn.Weight + random.NextFloat(-0.2f, 0.2f)
                    );
                    newConn.Type = conn.Type;
                    newConn.Innovation = brain.InnovationCounter++;
                    brain.Connections.Add(newConn);
                }
            }
            
            modules.Add(newModule);
            
            UnityEngine.Debug.Log($"[BIOME] Duplicated module {source.Name} -> {newModule.Name}");
        }
        
        /// <summary>
        /// Check if a module type can be duplicated
        /// </summary>
        private static bool CanDuplicate(int definitionId)
        {
            // Single-instance modules (doesn't make sense to have multiples)
            switch (definitionId)
            {
                case Tier1Modules.ENERGY_MODULE:
                case Tier1Modules.HEALTH_MODULE:
                case Tier1Modules.MATURITY_MODULE:
                case Tier1Modules.STOMACH_MODULE:
                    return false;
                    
                // Can have multiple of these
                case Tier1Modules.CLOCK_MODULE:
                case Tier1Modules.VISION_PLANT_MODULE:
                case Tier1Modules.VISION_MEAT_MODULE:
                case Tier1Modules.VISION_BIBITE_MODULE:
                case Tier1Modules.MOTOR_MODULE:
                case Tier1Modules.MOUTH_MODULE:
                case Tier1Modules.REPRODUCTION_MODULE:
                case Tier1Modules.GROWTH_MODULE:
                default:
                    return true;
            }
        }
        
        /// <summary>
        /// Upgrade a module to a higher tier, adding new capabilities
        /// </summary>
        public static void TryUpgradeModuleTier(BiomeBrain brain, List<BiomeModuleInstance> modules, ref Random random)
        {
            if (modules.Count == 0) return;
            
            // Find modules that can be upgraded
            var upgradeable = modules.Where(m => CanUpgradeTier(m)).ToList();
            if (upgradeable.Count == 0) return;
            
            var module = upgradeable[random.NextInt(0, upgradeable.Count)];
            int oldTier = module.Tier;
            module.Tier++;
            
            // Add new nodes/capabilities for higher tier
            AddTierUpgradeNodes(brain, module, ref random);
            
            UnityEngine.Debug.Log($"[BIOME] Upgraded {module.Name} from Tier {oldTier} to Tier {module.Tier}");
        }
        
        private static bool CanUpgradeTier(BiomeModuleInstance module)
        {
            if (!Tier1Modules.Definitions.TryGetValue(module.DefinitionId, out var def))
                return false;
            return module.Tier < def.MaxTier;
        }
        
        private static void AddTierUpgradeNodes(BiomeBrain brain, BiomeModuleInstance module, ref Random random)
        {
            int newNodeId = brain.Nodes.Keys.Max() + 1;
            
            // Add tier-specific capabilities
            switch (module.DefinitionId)
            {
                case Tier1Modules.VISION_PLANT_MODULE:
                case Tier1Modules.VISION_MEAT_MODULE:
                case Tier1Modules.VISION_BIBITE_MODULE:
                    // Tier 2: Add size perception
                    // Tier 3: Add distance perception
                    // Tier 4: Add movement direction perception
                    if (module.Tier == 2)
                    {
                        var sizeNode = new BiomeNode
                        {
                            Id = (ushort)newNodeId,
                            Type = NodeType.Hidden,
                            Affinity = NodeAffinity.Behavioral,
                            Activation = ActivationFunction.Identity,
                            Bias = 0f,
                            ModuleId = module.InstanceId,
                            ModuleTier = 2
                        };
                        brain.Nodes[newNodeId] = sizeNode;
                        module.OutputNodeIds.Add(newNodeId);
                    }
                    break;
                    
                case Tier1Modules.CLOCK_MODULE:
                    // Tier 2: Add phase output
                    // Tier 3: Add frequency modulation input
                    // Tier 4: Add multiple frequencies
                    if (module.Tier == 2)
                    {
                        var phaseNode = new BiomeNode
                        {
                            Id = (ushort)newNodeId,
                            Type = NodeType.Hidden,
                            Affinity = NodeAffinity.Behavioral,
                            Activation = ActivationFunction.Sin,
                            Bias = 0f,
                            ModuleId = module.InstanceId,
                            ModuleTier = 2
                        };
                        brain.Nodes[newNodeId] = phaseNode;
                        module.OutputNodeIds.Add(newNodeId);
                        module.InternalState["phase"] = 0f;
                    }
                    break;
                    
                case Tier1Modules.MOTOR_MODULE:
                    // Tier 2: Add strafe
                    // Tier 3: Add boost
                    // Tier 4: Add fine control
                    if (module.Tier == 2)
                    {
                        var strafeNode = new BiomeNode
                        {
                            Id = (ushort)newNodeId,
                            Type = NodeType.Hidden,
                            Affinity = NodeAffinity.Behavioral,
                            Activation = ActivationFunction.Tanh,
                            Bias = 0f,
                            ModuleId = module.InstanceId,
                            ModuleTier = 2
                        };
                        brain.Nodes[newNodeId] = strafeNode;
                        module.InputNodeIds.Add(newNodeId);
                    }
                    break;
            }
        }
        
        /// <summary>
        /// Add a connection between two different modules
        /// </summary>
        public static void TryAddInterModuleConnection(BiomeBrain brain, List<BiomeModuleInstance> modules, ref Random random)
        {
            if (modules.Count < 2) return;
            
            // Pick two different modules
            int fromModuleIdx = random.NextInt(0, modules.Count);
            int toModuleIdx = random.NextInt(0, modules.Count);
            if (fromModuleIdx == toModuleIdx) return;
            
            var fromModule = modules[fromModuleIdx];
            var toModule = modules[toModuleIdx];
            
            // Get output nodes from source module
            var fromNodes = fromModule.OutputNodeIds;
            if (fromNodes.Count == 0) fromNodes = fromModule.InputNodeIds; // Some modules only have inputs
            if (fromNodes.Count == 0) return;
            
            // Get input nodes from target module
            var toNodes = toModule.InputNodeIds;
            if (toNodes.Count == 0) toNodes = toModule.OutputNodeIds;
            if (toNodes.Count == 0) return;
            
            // Pick random nodes
            int fromNodeId = fromNodes[random.NextInt(0, fromNodes.Count)];
            int toNodeId = toNodes[random.NextInt(0, toNodes.Count)];
            
            // Check if connection already exists
            foreach (var conn in brain.Connections)
            {
                if (conn.FromNode == fromNodeId && conn.ToNode == toNodeId)
                    return;
            }
            
            // Check affinity-based connection likelihood
            if (brain.Nodes.TryGetValue(fromNodeId, out var fromNode) &&
                brain.Nodes.TryGetValue(toNodeId, out var toNode))
            {
                if (!ConnectionLikelihood.ShouldConnect(fromNode.Affinity, toNode.Affinity, ref random))
                    return;
            }
            
            // Add the connection
            var newConn = new BiomeConnection((ushort)fromNodeId, (ushort)toNodeId, random.NextFloat(-1f, 1f));
            newConn.Innovation = brain.InnovationCounter++;
            newConn.Type = ConnectionTypeLikelihood.GetRandom(ref random);
            brain.Connections.Add(newConn);
            
            UnityEngine.Debug.Log($"[BIOME] Added inter-module connection: {fromModule.Name} -> {toModule.Name}");
        }
        
        /// <summary>
        /// Mutate connections within a module
        /// </summary>
        public static void TryMutateModuleInternalConnections(BiomeBrain brain, List<BiomeModuleInstance> modules, ref Random random)
        {
            if (modules.Count == 0) return;
            
            var module = modules[random.NextInt(0, modules.Count)];
            
            // Get all nodes in this module
            var moduleNodes = new HashSet<int>();
            foreach (var id in module.InputNodeIds) moduleNodes.Add(id);
            foreach (var id in module.OutputNodeIds) moduleNodes.Add(id);
            
            if (moduleNodes.Count < 2) return;
            
            // Find existing internal connections
            var internalConns = new List<int>();
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                var conn = brain.Connections[i];
                if (moduleNodes.Contains(conn.FromNode) && moduleNodes.Contains(conn.ToNode))
                {
                    internalConns.Add(i);
                }
            }
            
            // Either add new connection or mutate existing
            if (internalConns.Count == 0 || random.NextFloat() < 0.5f)
            {
                // Add new internal connection
                var nodeList = moduleNodes.ToList();
                int fromIdx = random.NextInt(0, nodeList.Count);
                int toIdx = random.NextInt(0, nodeList.Count);
                if (fromIdx == toIdx) return;
                
                int fromId = nodeList[fromIdx];
                int toId = nodeList[toIdx];
                
                // Check if already exists
                foreach (var conn in brain.Connections)
                {
                    if (conn.FromNode == fromId && conn.ToNode == toId) return;
                }
                
                var newConn = new BiomeConnection((ushort)fromId, (ushort)toId, random.NextFloat(-1f, 1f));
                newConn.Innovation = brain.InnovationCounter++;
                brain.Connections.Add(newConn);
            }
            else
            {
                // Mutate existing connection
                int connIdx = internalConns[random.NextInt(0, internalConns.Count)];
                var conn = brain.Connections[connIdx];
                conn.Weight += random.NextFloat(-0.5f, 0.5f);
                conn.Weight = math.clamp(conn.Weight, -5f, 5f);
                brain.Connections[connIdx] = conn;
            }
        }
        
        /// <summary>
        /// Drift module parameters (bias values of high-genetic-affinity nodes)
        /// </summary>
        public static void TryMutateModuleParameters(BiomeBrain brain, List<BiomeModuleInstance> modules, ref Random random)
        {
            if (modules.Count == 0) return;
            
            var module = modules[random.NextInt(0, modules.Count)];
            
            // Get all nodes with high genetic affinity (these act as parameters)
            var parameterNodes = new List<int>();
            foreach (var nId in module.InputNodeIds.Concat(module.OutputNodeIds))
            {
                if (brain.Nodes.TryGetValue(nId, out var node))
                {
                    if (node.Affinity == NodeAffinity.Genetic || 
                        node.Affinity == NodeAffinity.Hormonal)
                    {
                        parameterNodes.Add(nId);
                    }
                }
            }
            
            if (parameterNodes.Count == 0) return;
            
            // Mutate a random parameter
            int nodeId = parameterNodes[random.NextInt(0, parameterNodes.Count)];
            var paramNode = brain.Nodes[nodeId];
            paramNode.Bias += random.NextFloat(-0.2f, 0.2f);
            paramNode.Bias = math.clamp(paramNode.Bias, -3f, 3f);
            brain.Nodes[nodeId] = paramNode;
        }
    }
    
    /// <summary>
    /// Configuration for module mutation rates
    /// </summary>
    public class ModuleMutationConfig
    {
        public float DuplicationRate = 0.005f;
        public float TierUpgradeRate = 0.01f;
        public float InterModuleConnectionRate = 0.1f;
        public float InternalConnectionRate = 0.05f;
        public float ParameterDriftRate = 0.1f;
        
        public static ModuleMutationConfig Default => new ModuleMutationConfig();
        
        public static ModuleMutationConfig HighEvolution => new ModuleMutationConfig
        {
            DuplicationRate = 0.02f,
            TierUpgradeRate = 0.05f,
            InterModuleConnectionRate = 0.2f,
            InternalConnectionRate = 0.1f,
            ParameterDriftRate = 0.2f
        };
    }
}
