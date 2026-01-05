using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Enhanced mutation system with all BIOME mutation types.
    /// Updated for sparse Dictionary-based BiomeBrain.
    /// </summary>
    public static class EnhancedMutation
    {
        /// <summary>
        /// Apply all mutation types to a brain with configurable rates
        /// </summary>
        public static void MutateComprehensive(BiomeBrain brain, ref Random random, MutationConfig config = null)
        {
            config ??= MutationConfig.Default;
            
            // === WEIGHT MUTATIONS (most common) ===
            MutateWeights(brain, ref random, config);
            
            // === BIAS MUTATIONS ===
            MutateBiases(brain, ref random, config);
            
            // === CONNECTION MUTATIONS ===
            MutateConnections(brain, ref random, config);
            
            // === NODE MUTATIONS ===
            MutateNodes(brain, ref random, config);
            
            // === STRUCTURAL MUTATIONS ===
            MutateStructure(brain, ref random, config);
            
            // === MODULE MUTATIONS (rare but powerful) ===
            MutateModules(brain, ref random, config);
        }
        
        private static void MutateWeights(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                var conn = brain.Connections[i];
                
                // Weight shift (small adjustment)
                if (random.NextFloat() < config.WeightShiftRate)
                {
                    float shift = random.NextFloat(-1f, 1f) * config.WeightShiftMagnitude;
                    conn.Weight += shift;
                    conn.Weight = math.clamp(conn.Weight, -5f, 5f);
                }
                // Weight randomize (complete replacement)
                else if (random.NextFloat() < config.WeightRandomizeRate)
                {
                    conn.Weight = random.NextFloat(-2f, 2f);
                }
                
                brain.Connections[i] = conn;
            }
        }
        
        private static void MutateBiases(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            // Get all non-input node IDs (outputs and hidden)
            var nodeIds = brain.Nodes.Keys.Where(id => 
                id >= OutputNeurons.OFFSET || id >= HiddenNeurons.OFFSET).ToList();
            
            foreach (var nodeId in nodeIds)
            {
                var node = brain.Nodes[nodeId];
                
                if (random.NextFloat() < config.BiasShiftRate)
                {
                    float shift = random.NextFloat(-1f, 1f) * config.BiasShiftMagnitude;
                    node.Bias += shift;
                    node.Bias = math.clamp(node.Bias, -3f, 3f);
                }
                
                brain.Nodes[nodeId] = node;
            }
        }
        
        private static void MutateConnections(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            // Toggle connection enabled state
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                if (random.NextFloat() < config.ConnectionToggleRate)
                {
                    var conn = brain.Connections[i];
                    conn.Enabled = !conn.Enabled;
                    brain.Connections[i] = conn;
                }
            }
            
            // Change connection type
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                if (random.NextFloat() < config.ConnectionTypeChangeRate)
                {
                    var conn = brain.Connections[i];
                    conn.Type = (ConnectionType)random.NextInt(0, 3);
                    brain.Connections[i] = conn;
                }
            }
        }
        
        private static void MutateNodes(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            // Only mutate hidden nodes
            var hiddenNodeIds = brain.Nodes.Keys.Where(id => id >= HiddenNeurons.OFFSET).ToList();
            
            foreach (var nodeId in hiddenNodeIds)
            {
                var node = brain.Nodes[nodeId];
                
                // Affinity shift
                if (random.NextFloat() < config.AffinityShiftRate)
                {
                    int currentAffinity = (int)node.Affinity;
                    int shift = random.NextFloat() < 0.5f ? -1 : 1;
                    int newAffinity = math.clamp(currentAffinity + shift, 0, 2);
                    node.Affinity = (NodeAffinity)newAffinity;
                    
                    // Update interval for hormonal nodes
                    if (node.Affinity == NodeAffinity.Hormonal)
                    {
                        node.UpdateInterval = random.NextInt(30, 120);
                    }
                }
                
                // Activation function change
                if (random.NextFloat() < config.ActivationChangeRate)
                {
                    node.Activation = (ActivationFunction)random.NextInt(0, 8);
                }
                
                brain.Nodes[nodeId] = node;
            }
        }
        
        private static void MutateStructure(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            // Add connection
            if (random.NextFloat() < config.AddConnectionRate)
            {
                TryAddConnection(brain, ref random);
            }
            
            // Add node (split connection)
            if (random.NextFloat() < config.AddNodeRate)
            {
                TryAddNode(brain, ref random);
            }
            
            // Remove node
            if (random.NextFloat() < config.RemoveNodeRate)
            {
                TryRemoveNode(brain, ref random);
            }
        }
        
        private static void MutateModules(BiomeBrain brain, ref Random random, MutationConfig config)
        {
            // Module tier upgrade
            if (random.NextFloat() < config.ModuleTierUpgradeRate)
            {
                TryUpgradeModuleTier(brain, ref random);
            }
            
            // Modularization (package sub-network into template)
            if (random.NextFloat() < config.ModularizationRate)
            {
                TryModularize(brain, ref random);
            }
            
            // Module duplication
            if (random.NextFloat() < config.ModuleDuplicationRate)
            {
                TryDuplicateModule(brain, ref random);
            }
        }
        
        private static void TryAddConnection(BiomeBrain brain, ref Random random)
        {
            if (brain.Nodes.Count == 0) return;
            
            // Get all node IDs
            var allNodeIds = brain.Nodes.Keys.ToList();
            
            // Get target node IDs (outputs and hidden)
            var targetNodeIds = allNodeIds.Where(id => id >= OutputNeurons.OFFSET).ToList();
            
            if (targetNodeIds.Count == 0) return;
            
            // Pick random source (any node)
            int fromNode = allNodeIds[random.NextInt(0, allNodeIds.Count)];
            
            // Pick random target (not input nodes)
            int toNode = targetNodeIds[random.NextInt(0, targetNodeIds.Count)];
            
            // No self-connections
            if (fromNode == toNode) return;
            
            // Check if exists
            foreach (var conn in brain.Connections)
            {
                if (conn.FromNode == fromNode && conn.ToNode == toNode)
                    return;
            }
            
            // Verify nodes exist
            if (!brain.Nodes.ContainsKey(fromNode) || !brain.Nodes.ContainsKey(toNode))
                return;
            
            // Check affinity likelihood
            var fromAffinity = brain.Nodes[fromNode].Affinity;
            var toAffinity = brain.Nodes[toNode].Affinity;
            
            if (!ConnectionLikelihood.ShouldConnect(fromAffinity, toAffinity, ref random))
                return;
            
            // Add connection
            var newConn = new BiomeConnection((ushort)fromNode, (ushort)toNode, random.NextFloat(-2f, 2f));
            newConn.Innovation = brain.InnovationCounter++;
            newConn.Type = ConnectionTypeLikelihood.SelectType(
                brain.Nodes[fromNode].Type,
                brain.Nodes[toNode].Type,
                ref random
            );
            brain.Connections.Add(newConn);
        }
        
        private static void TryAddNode(BiomeBrain brain, ref Random random)
        {
            if (brain.Connections.Count == 0) return;
            
            // Find an enabled connection to split
            int attempts = 10;
            int connIndex = -1;
            while (attempts-- > 0)
            {
                int idx = random.NextInt(0, brain.Connections.Count);
                if (brain.Connections[idx].Enabled)
                {
                    connIndex = idx;
                    break;
                }
            }
            
            if (connIndex < 0) return;
            
            var oldConn = brain.Connections[connIndex];
            
            // Verify both nodes exist
            if (!brain.Nodes.ContainsKey(oldConn.FromNode) || !brain.Nodes.ContainsKey(oldConn.ToNode))
                return;
            
            // Disable old connection
            oldConn.Enabled = false;
            brain.Connections[connIndex] = oldConn;
            
            // Determine new node affinity based on connected nodes
            var fromAffinity = brain.Nodes[oldConn.FromNode].Affinity;
            var toAffinity = brain.Nodes[oldConn.ToNode].Affinity;
            
            NodeAffinity newAffinity;
            if (fromAffinity == NodeAffinity.Genetic && toAffinity == NodeAffinity.Genetic)
                newAffinity = NodeAffinity.Genetic;
            else if (fromAffinity == NodeAffinity.Hormonal || toAffinity == NodeAffinity.Hormonal)
                newAffinity = random.NextFloat() < 0.3f ? NodeAffinity.Hormonal : NodeAffinity.Behavioral;
            else
                newAffinity = NodeAffinity.Behavioral;
            
            // Create new hidden node using brain's method (returns new ID)
            int newNodeId = brain.AddHiddenNode(newAffinity);
            
            // Create two new connections
            var conn1 = new BiomeConnection(oldConn.FromNode, (ushort)newNodeId, 1.0f);
            conn1.Innovation = brain.InnovationCounter++;
            brain.Connections.Add(conn1);
            
            var conn2 = new BiomeConnection((ushort)newNodeId, oldConn.ToNode, oldConn.Weight);
            conn2.Innovation = brain.InnovationCounter++;
            brain.Connections.Add(conn2);
        }
        
        private static void TryRemoveNode(BiomeBrain brain, ref Random random)
        {
            // Get hidden node IDs
            var hiddenNodeIds = brain.Nodes.Keys.Where(id => id >= HiddenNeurons.OFFSET).ToList();
            
            if (hiddenNodeIds.Count == 0) return;
            
            // Pick random hidden node
            int nodeId = hiddenNodeIds[random.NextInt(0, hiddenNodeIds.Count)];
            
            // Don't remove module nodes
            if (brain.Nodes[nodeId].ModuleId > 0) return;
            
            // Remove connections involving this node
            brain.Connections.RemoveAll(c => c.FromNode == nodeId || c.ToNode == nodeId);
            
            // Remove the node
            brain.Nodes.Remove(nodeId);
            
            // Note: With dictionary-based storage, no need to update other node IDs
        }
        
        private static void TryUpgradeModuleTier(BiomeBrain brain, ref Random random)
        {
            if (brain.Modules.Count == 0) return;
            
            int moduleIdx = random.NextInt(0, brain.Modules.Count);
            var module = brain.Modules[moduleIdx];
            
            if (module.Tier < 4)
            {
                module.Tier++;
                // Could add new nodes/connections for higher tier here
            }
        }
        
        private static void TryModularize(BiomeBrain brain, ref Random random)
        {
            // This is the BIOME-specific modularization mutation
            // It finds a useful sub-network and packages it as a template module
            
            // Get hidden nodes
            var hiddenNodeIds = brain.Nodes.Keys.Where(id => id >= HiddenNeurons.OFFSET).ToList();
            
            if (hiddenNodeIds.Count < 3) return;
            
            // Find a cluster of connected hidden nodes
            int clusterSize = random.NextInt(2, 5);
            var clusterNodes = new List<int>();
            
            // Start from a random hidden node
            int startNodeId = hiddenNodeIds[random.NextInt(0, hiddenNodeIds.Count)];
            if (brain.Nodes[startNodeId].ModuleId > 0) return; // Already in a module
            
            clusterNodes.Add(startNodeId);
            
            // Find connected nodes
            for (int i = 0; i < brain.Connections.Count && clusterNodes.Count < clusterSize; i++)
            {
                var conn = brain.Connections[i];
                if (!conn.Enabled) continue;
                
                if (clusterNodes.Contains(conn.FromNode) && 
                    conn.ToNode >= HiddenNeurons.OFFSET &&
                    brain.Nodes.ContainsKey(conn.ToNode) &&
                    brain.Nodes[conn.ToNode].ModuleId == 0)
                {
                    if (!clusterNodes.Contains(conn.ToNode))
                        clusterNodes.Add(conn.ToNode);
                }
                else if (clusterNodes.Contains(conn.ToNode) && 
                         conn.FromNode >= HiddenNeurons.OFFSET &&
                         brain.Nodes.ContainsKey(conn.FromNode) &&
                         brain.Nodes[conn.FromNode].ModuleId == 0)
                {
                    if (!clusterNodes.Contains(conn.FromNode))
                        clusterNodes.Add(conn.FromNode);
                }
            }
            
            if (clusterNodes.Count < 2) return;
            
            // Create a new module from this cluster
            int newModuleId = brain.Modules.Count + 1;
            var newModule = new BiomeModule(newModuleId, $"Evolved_{newModuleId}", 1, ModuleCategory.Processing);
            
            foreach (var nodeId in clusterNodes)
            {
                var node = brain.Nodes[nodeId];
                node.ModuleId = newModuleId;
                node.ModuleTier = 1;
                brain.Nodes[nodeId] = node;
                newModule.ContainedNodes.Add(nodeId);
            }
            
            brain.Modules.Add(newModule);
        }
        
        private static void TryDuplicateModule(BiomeBrain brain, ref Random random)
        {
            if (brain.Modules.Count == 0) return;
            if (brain.Modules.Count >= 10) return; // Limit modules
            
            int moduleIdx = random.NextInt(0, brain.Modules.Count);
            var sourceModule = brain.Modules[moduleIdx];
            
            // Create copies of nodes
            int newModuleId = brain.Modules.Count + 1;
            var newModule = new BiomeModule(newModuleId, $"{sourceModule.Name}_copy", 
                                            sourceModule.Tier, sourceModule.Category);
            
            var nodeMapping = new Dictionary<int, int>();
            
            foreach (var oldNodeId in sourceModule.ContainedNodes)
            {
                if (!brain.Nodes.ContainsKey(oldNodeId)) continue;
                
                var oldNode = brain.Nodes[oldNodeId];
                
                // Create new node with new ID
                int newNodeId = brain.AddHiddenNode(oldNode.Affinity, oldNode.Activation);
                
                // Copy properties
                var newNode = brain.Nodes[newNodeId];
                newNode.Bias = oldNode.Bias + random.NextFloat(-0.2f, 0.2f);
                newNode.ModuleId = newModuleId;
                newNode.ModuleTier = oldNode.ModuleTier;
                brain.Nodes[newNodeId] = newNode;
                
                newModule.ContainedNodes.Add(newNodeId);
                nodeMapping[oldNodeId] = newNodeId;
            }
            
            // Copy internal connections
            foreach (var connIdx in sourceModule.ContainedConnections)
            {
                if (connIdx >= brain.Connections.Count) continue;
                var oldConn = brain.Connections[connIdx];
                
                if (nodeMapping.ContainsKey(oldConn.FromNode) && nodeMapping.ContainsKey(oldConn.ToNode))
                {
                    var newConn = new BiomeConnection(
                        (ushort)nodeMapping[oldConn.FromNode],
                        (ushort)nodeMapping[oldConn.ToNode],
                        oldConn.Weight + random.NextFloat(-0.3f, 0.3f)
                    );
                    newConn.Innovation = brain.InnovationCounter++;
                    newConn.Type = oldConn.Type;
                    
                    int newConnIdx = brain.Connections.Count;
                    brain.Connections.Add(newConn);
                    newModule.ContainedConnections.Add(newConnIdx);
                }
            }
            
            brain.Modules.Add(newModule);
        }
    }
    
    /// <summary>
    /// Configuration for mutation rates
    /// </summary>
    public class MutationConfig
    {
        // Weight mutations
        public float WeightShiftRate = 0.8f;
        public float WeightShiftMagnitude = 0.5f;
        public float WeightRandomizeRate = 0.1f;
        
        // Bias mutations
        public float BiasShiftRate = 0.3f;
        public float BiasShiftMagnitude = 0.3f;
        
        // Connection mutations
        public float ConnectionToggleRate = 0.05f;
        public float ConnectionTypeChangeRate = 0.02f;
        
        // Node mutations
        public float AffinityShiftRate = 0.1f;
        public float ActivationChangeRate = 0.05f;
        
        // Structural mutations
        public float AddConnectionRate = 0.15f;
        public float AddNodeRate = 0.03f;
        public float RemoveNodeRate = 0.01f;
        
        // Module mutations
        public float ModuleTierUpgradeRate = 0.01f;
        public float ModularizationRate = 0.001f;
        public float ModuleDuplicationRate = 0.005f;
        
        public static MutationConfig Default => new MutationConfig();
        
        public static MutationConfig HighMutation => new MutationConfig
        {
            WeightShiftRate = 0.9f,
            WeightShiftMagnitude = 0.8f,
            AddConnectionRate = 0.25f,
            AddNodeRate = 0.08f,
            ModularizationRate = 0.005f
        };
        
        public static MutationConfig LowMutation => new MutationConfig
        {
            WeightShiftRate = 0.5f,
            WeightShiftMagnitude = 0.3f,
            AddConnectionRate = 0.05f,
            AddNodeRate = 0.01f,
            ModularizationRate = 0.0001f
        };
    }
}
