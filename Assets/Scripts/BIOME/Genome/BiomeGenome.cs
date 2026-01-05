using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Mutation types in BIOME
    /// </summary>
    public enum MutationType
    {
        // Connection mutations
        WeightShift,            // Small change to existing weight
        WeightRandomize,        // Completely new random weight
        ConnectionToggle,       // Enable/disable connection
        AddConnection,          // New connection between existing nodes
        
        // Node mutations
        AddNode,                // Split connection with new node
        RemoveNode,             // Remove a hidden node
        NodeAffinityShift,      // Change node affinity (Genetic/Hormonal/Behavioral)
        NodeActivationChange,   // Change activation function
        BiasShift,              // Change node bias
        
        // Module mutations
        ModuleTierUpgrade,      // Upgrade module to next tier
        Modularization,         // Package sub-network into module
        ModuleDuplication,      // Copy a module
        
        // Structural mutations
        ConnectionTypeChange    // Change connection type (Standard/Modulatory/Gating)
    }
    
    /// <summary>
    /// Mutation probabilities for BIOME
    /// </summary>
    public static class MutationProbabilities
    {
        // Connection mutations (most common)
        public const float WeightShift = 0.80f;
        public const float WeightRandomize = 0.10f;
        public const float ConnectionToggle = 0.05f;
        public const float AddConnection = 0.15f;
        
        // Node mutations (less common)
        public const float AddNode = 0.03f;
        public const float RemoveNode = 0.01f;
        public const float NodeAffinityShift = 0.10f;
        public const float NodeActivationChange = 0.05f;
        public const float BiasShift = 0.30f;
        
        // Module mutations (rare)
        public const float ModuleTierUpgrade = 0.01f;
        public const float Modularization = 0.001f;
        public const float ModuleDuplication = 0.005f;
        
        // Connection type mutations
        public const float ConnectionTypeChange = 0.02f;
        
        // Mutation magnitude
        public const float WeightShiftMagnitude = 0.5f;
        public const float BiasShiftMagnitude = 0.3f;
    }
    
    /// <summary>
    /// The BIOME Genome handles mutation and crossover operations.
    /// Updated for sparse Dictionary-based BiomeBrain.
    /// </summary>
    public static class BiomeGenome
    {
        /// <summary>
        /// Apply mutations to a brain (sparse dictionary version)
        /// </summary>
        public static void Mutate(BiomeBrain brain, ref Random random)
        {
            // Weight mutations (applied to each connection)
            for (int i = 0; i < brain.Connections.Count; i++)
            {
                var conn = brain.Connections[i];
                
                if (random.NextFloat() < MutationProbabilities.WeightShift)
                {
                    conn.Weight += random.NextFloat(-1f, 1f) * MutationProbabilities.WeightShiftMagnitude;
                    conn.Weight = math.clamp(conn.Weight, -5f, 5f);
                }
                else if (random.NextFloat() < MutationProbabilities.WeightRandomize)
                {
                    conn.Weight = random.NextFloat(-2f, 2f);
                }
                
                if (random.NextFloat() < MutationProbabilities.ConnectionToggle)
                {
                    conn.Enabled = !conn.Enabled;
                }
                
                if (random.NextFloat() < MutationProbabilities.ConnectionTypeChange)
                {
                    conn.Type = (ConnectionType)random.NextInt(0, 3);
                }
                
                brain.Connections[i] = conn;
            }
            
            // Bias mutations (applied to output and hidden nodes)
            // Get list of node IDs (keys) that are not inputs
            var nodeIds = brain.Nodes.Keys.Where(id => 
                id >= OutputNeurons.OFFSET || id >= HiddenNeurons.OFFSET).ToList();
            
            foreach (var nodeId in nodeIds)
            {
                var node = brain.Nodes[nodeId];
                
                if (random.NextFloat() < MutationProbabilities.BiasShift)
                {
                    node.Bias += random.NextFloat(-1f, 1f) * MutationProbabilities.BiasShiftMagnitude;
                    node.Bias = math.clamp(node.Bias, -3f, 3f);
                }
                
                brain.Nodes[nodeId] = node;
            }
            
            // Node affinity shift (for hidden nodes only)
            var hiddenNodeIds = brain.Nodes.Keys.Where(id => id >= HiddenNeurons.OFFSET).ToList();
            
            foreach (var nodeId in hiddenNodeIds)
            {
                var node = brain.Nodes[nodeId];
                
                if (random.NextFloat() < MutationProbabilities.NodeAffinityShift)
                {
                    int currentAffinity = (int)node.Affinity;
                    int shift = random.NextFloat() < 0.5f ? -1 : 1;
                    int newAffinity = math.clamp(currentAffinity + shift, 0, 2);
                    node.Affinity = (NodeAffinity)newAffinity;
                    
                    if (node.Affinity == NodeAffinity.Hormonal)
                    {
                        node.UpdateInterval = random.NextInt(30, 120);
                    }
                }
                
                if (random.NextFloat() < MutationProbabilities.NodeActivationChange)
                {
                    node.Activation = (ActivationFunction)random.NextInt(0, 8);
                }
                
                brain.Nodes[nodeId] = node;
            }
            
            // Structural mutations
            if (random.NextFloat() < MutationProbabilities.AddConnection)
            {
                TryAddConnection(brain, ref random);
            }
            
            if (random.NextFloat() < MutationProbabilities.AddNode)
            {
                TryAddNode(brain, ref random);
            }
            
            if (random.NextFloat() < MutationProbabilities.RemoveNode)
            {
                TryRemoveNode(brain, ref random);
            }
        }
        
        /// <summary>
        /// Try to add a new connection between existing nodes
        /// </summary>
        private static void TryAddConnection(BiomeBrain brain, ref Random random)
        {
            if (brain.Nodes.Count == 0) return;
            
            // Get list of all node IDs
            var allNodeIds = brain.Nodes.Keys.ToList();
            
            // Get node IDs that can be targets (outputs and hidden nodes)
            var targetNodeIds = allNodeIds.Where(id => 
                id >= OutputNeurons.OFFSET).ToList();
            
            if (targetNodeIds.Count == 0) return;
            
            // Pick random source node (any node)
            int fromNode = allNodeIds[random.NextInt(0, allNodeIds.Count)];
            
            // Pick random target node (output or hidden)
            int toNode = targetNodeIds[random.NextInt(0, targetNodeIds.Count)];
            
            // Don't connect node to itself
            if (fromNode == toNode) return;
            
            // Check if connection exists
            foreach (var conn in brain.Connections)
            {
                if (conn.FromNode == fromNode && conn.ToNode == toNode)
                    return;
            }
            
            // Check nodes exist and get affinities
            if (!brain.Nodes.ContainsKey(fromNode) || !brain.Nodes.ContainsKey(toNode))
                return;
            
            var fromAffinity = brain.Nodes[fromNode].Affinity;
            var toAffinity = brain.Nodes[toNode].Affinity;
            
            if (!ConnectionLikelihood.ShouldConnect(fromAffinity, toAffinity, ref random))
                return;
            
            // Add the connection
            float weight = random.NextFloat(-2f, 2f);
            var connection = new BiomeConnection((ushort)fromNode, (ushort)toNode, weight);
            connection.Innovation = brain.InnovationCounter++;
            connection.Type = ConnectionTypeLikelihood.SelectType(
                brain.Nodes[fromNode].Type,
                brain.Nodes[toNode].Type,
                ref random
            );
            brain.Connections.Add(connection);
        }
        
        /// <summary>
        /// Try to add a new node by splitting an existing connection
        /// </summary>
        private static void TryAddNode(BiomeBrain brain, ref Random random)
        {
            if (brain.Connections.Count == 0) return;
            
            // Pick a random enabled connection
            int attempts = 10;
            int connIndex = -1;
            while (attempts > 0)
            {
                int idx = random.NextInt(0, brain.Connections.Count);
                if (brain.Connections[idx].Enabled)
                {
                    connIndex = idx;
                    break;
                }
                attempts--;
            }
            
            if (connIndex < 0) return;
            
            var oldConn = brain.Connections[connIndex];
            
            // Verify both nodes exist
            if (!brain.Nodes.ContainsKey(oldConn.FromNode) || !brain.Nodes.ContainsKey(oldConn.ToNode))
                return;
            
            // Disable old connection
            oldConn.Enabled = false;
            brain.Connections[connIndex] = oldConn;
            
            // Create new hidden node using brain's method (auto-assigns ID)
            var fromAffinity = brain.Nodes[oldConn.FromNode].Affinity;
            var toAffinity = brain.Nodes[oldConn.ToNode].Affinity;
            NodeAffinity newAffinity;
            
            if (fromAffinity == NodeAffinity.Genetic && toAffinity == NodeAffinity.Genetic)
                newAffinity = NodeAffinity.Genetic;
            else if (fromAffinity == NodeAffinity.Hormonal || toAffinity == NodeAffinity.Hormonal)
                newAffinity = random.NextFloat() < 0.3f ? NodeAffinity.Hormonal : NodeAffinity.Behavioral;
            else
                newAffinity = NodeAffinity.Behavioral;
            
            int newNodeId = brain.AddHiddenNode(newAffinity);
            
            // Create two new connections
            // From -> NewNode (weight 1.0 to preserve signal)
            var conn1 = new BiomeConnection(oldConn.FromNode, (ushort)newNodeId, 1.0f);
            conn1.Innovation = brain.InnovationCounter++;
            brain.Connections.Add(conn1);
            
            // NewNode -> To (old weight)
            var conn2 = new BiomeConnection((ushort)newNodeId, oldConn.ToNode, oldConn.Weight);
            conn2.Innovation = brain.InnovationCounter++;
            brain.Connections.Add(conn2);
        }
        
        /// <summary>
        /// Try to remove a hidden node
        /// </summary>
        private static void TryRemoveNode(BiomeBrain brain, ref Random random)
        {
            // Get hidden node IDs
            var hiddenNodeIds = brain.Nodes.Keys.Where(id => id >= HiddenNeurons.OFFSET).ToList();
            
            if (hiddenNodeIds.Count == 0) return;
            
            // Pick random hidden node
            int nodeId = hiddenNodeIds[random.NextInt(0, hiddenNodeIds.Count)];
            
            // Remove all connections involving this node
            brain.Connections.RemoveAll(c => c.FromNode == nodeId || c.ToNode == nodeId);
            
            // Remove the node from dictionary
            brain.Nodes.Remove(nodeId);
            
            // Note: With dictionary-based sparse storage, we don't need to update other node IDs
            // since they're keyed by their actual ID, not index position
        }
        
        /// <summary>
        /// Crossover two brains to create offspring (NEAT-style)
        /// Updated for sparse dictionary storage
        /// </summary>
        public static BiomeBrain Crossover(BiomeBrain parent1, BiomeBrain parent2, ref Random random)
        {
            // Determine more fit parent
            BiomeBrain moreFit = parent1.Fitness >= parent2.Fitness ? parent1 : parent2;
            BiomeBrain lessFit = parent1.Fitness >= parent2.Fitness ? parent2 : parent1;
            
            var offspring = new BiomeBrain();
            offspring.RandomSeed = (uint)random.NextUInt();
            offspring.Generation = math.max(parent1.Generation, parent2.Generation) + 1;
            
            // Copy nodes from more fit parent (dictionary copy)
            foreach (var kvp in moreFit.Nodes)
            {
                offspring.Nodes[kvp.Key] = kvp.Value;
            }
            
            // Build innovation lookup for less fit parent
            var lessFitInnovations = new Dictionary<int, BiomeConnection>();
            foreach (var conn in lessFit.Connections)
            {
                lessFitInnovations[conn.Innovation] = conn;
            }
            
            // Crossover connections
            foreach (var conn in moreFit.Connections)
            {
                BiomeConnection newConn;
                
                if (lessFitInnovations.TryGetValue(conn.Innovation, out var matchingConn))
                {
                    // Matching gene - randomly pick from either parent
                    newConn = random.NextFloat() < 0.5f ? conn : matchingConn;
                }
                else
                {
                    // Disjoint/excess gene - inherit from more fit parent
                    newConn = conn;
                }
                
                offspring.Connections.Add(newConn);
            }
            
            offspring.InnovationCounter = math.max(parent1.InnovationCounter, parent2.InnovationCounter);
            
            return offspring;
        }
        
        /// <summary>
        /// Calculate genetic distance between two brains (for speciation)
        /// </summary>
        public static float GeneticDistance(BiomeBrain brain1, BiomeBrain brain2)
        {
            const float c1 = 1.0f; // Excess coefficient
            const float c2 = 1.0f; // Disjoint coefficient
            const float c3 = 0.4f; // Weight difference coefficient
            
            // Build innovation sets
            var innovations1 = new HashSet<int>();
            var weights1 = new Dictionary<int, float>();
            foreach (var conn in brain1.Connections)
            {
                innovations1.Add(conn.Innovation);
                weights1[conn.Innovation] = conn.Weight;
            }
            
            var innovations2 = new HashSet<int>();
            var weights2 = new Dictionary<int, float>();
            foreach (var conn in brain2.Connections)
            {
                innovations2.Add(conn.Innovation);
                weights2[conn.Innovation] = conn.Weight;
            }
            
            // Find matching, disjoint, excess genes
            int matching = 0;
            float weightDiff = 0f;
            
            foreach (int innov in innovations1)
            {
                if (innovations2.Contains(innov))
                {
                    matching++;
                    weightDiff += math.abs(weights1[innov] - weights2[innov]);
                }
            }
            
            int disjoint = innovations1.Count + innovations2.Count - 2 * matching;
            
            // Normalize
            int n = math.max(brain1.Connections.Count, brain2.Connections.Count);
            if (n < 20) n = 1;
            
            float avgWeightDiff = matching > 0 ? weightDiff / matching : 0f;
            
            return (c1 * disjoint / n) + (c3 * avgWeightDiff);
        }
    }
}
