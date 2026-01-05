using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// The BIOME network - a unified structure containing all genes, sensors, outputs, and hidden neurons.
    /// In BIOME, there is NO separate genome - genes ARE nodes with GENETIC affinity.
    ///
    /// Key principles:
    /// 1. All genes are nodes (GENETIC affinity, Output = Bias)
    /// 2. Module configuration comes through input nodes, not parameters
    /// 3. Sparse instantiation - bibites only have the nodes they need
    /// 4. Affinity system controls update rates and connection effectiveness
    /// </summary>
    [Serializable]
    public class BiomeNetwork
    {
        // ============================================================
        // NODE AND CONNECTION STORAGE
        // ============================================================

        /// <summary>All nodes in this network, indexed by node ID.</summary>
        private Dictionary<int, BiomeNode> _nodes = new Dictionary<int, BiomeNode>();

        /// <summary>All connections in this network.</summary>
        private List<BiomeConnection> _connections = new List<BiomeConnection>();

        /// <summary>Next available node ID for creating new nodes.</summary>
        private int _nextNodeId = 0;

        /// <summary>Next available connection ID.</summary>
        private int _nextConnectionId = 0;

        /// <summary>Current frame number for update timing.</summary>
        private int _currentFrame = 0;

        // ============================================================
        // QUICK LOOKUP CACHES
        // ============================================================

        /// <summary>Maps catalogue ID to instantiated node ID (for quick lookup).</summary>
        private Dictionary<int, int> _catalogueToNodeId = new Dictionary<int, int>();

        /// <summary>Connections grouped by destination node (for activation propagation).</summary>
        private Dictionary<int, List<int>> _connectionsByDestination = new Dictionary<int, List<int>>();

        /// <summary>List of gene node IDs for quick iteration.</summary>
        private List<int> _geneNodeIds = new List<int>();

        /// <summary>List of sensor node IDs (both BIOLOGICAL and BEHAVIOURAL).</summary>
        private List<int> _sensorNodeIds = new List<int>();

        /// <summary>List of output node IDs.</summary>
        private List<int> _outputNodeIds = new List<int>();

        /// <summary>List of hidden node IDs.</summary>
        private List<int> _hiddenNodeIds = new List<int>();

        // ============================================================
        // CONSTRUCTION
        // ============================================================

        /// <summary>
        /// Creates an empty BIOME network.
        /// Use StarterBrain.CreateDefault() to create a functional starter brain.
        /// </summary>
        public BiomeNetwork()
        {
            NodeCatalogue.EnsureInitialized();
        }

        /// <summary>
        /// Deep clones this network for offspring (before applying mutations).
        /// </summary>
        public BiomeNetwork Clone()
        {
            var clone = new BiomeNetwork();
            clone._nextNodeId = _nextNodeId;
            clone._nextConnectionId = _nextConnectionId;
            clone._currentFrame = 0;

            // Clone nodes
            foreach (var kvp in _nodes)
            {
                clone._nodes[kvp.Key] = kvp.Value;
            }

            // Clone connections
            foreach (var conn in _connections)
            {
                clone._connections.Add(conn);
            }

            // Rebuild caches
            clone.RebuildCaches();

            return clone;
        }

        /// <summary>
        /// Rebuilds all lookup caches. Call after bulk modifications.
        /// </summary>
        public void RebuildCaches()
        {
            _catalogueToNodeId.Clear();
            _connectionsByDestination.Clear();
            _geneNodeIds.Clear();
            _sensorNodeIds.Clear();
            _outputNodeIds.Clear();
            _hiddenNodeIds.Clear();

            // Rebuild node caches
            foreach (var kvp in _nodes)
            {
                var node = kvp.Value;
                if (node.CatalogueId >= 0)
                {
                    _catalogueToNodeId[node.CatalogueId] = node.Id;
                }

                switch (node.Affinity)
                {
                    case NodeAffinity.Genetic:
                        _geneNodeIds.Add(node.Id);
                        break;
                    case NodeAffinity.Biological:
                        _sensorNodeIds.Add(node.Id);
                        break;
                    case NodeAffinity.Behavioural:
                        if (node.CatalogueId >= 0)
                        {
                            var def = NodeCatalogue.GetDefinition(node.CatalogueId);
                            if (def.Category == NodeCategory.Output)
                                _outputNodeIds.Add(node.Id);
                            else if (def.Category == NodeCategory.SensorExternal)
                                _sensorNodeIds.Add(node.Id);
                            else
                                _hiddenNodeIds.Add(node.Id);
                        }
                        else
                        {
                            _hiddenNodeIds.Add(node.Id);
                        }
                        break;
                }
            }

            // Rebuild connection cache
            for (int i = 0; i < _connections.Count; i++)
            {
                var conn = _connections[i];
                if (!_connectionsByDestination.TryGetValue(conn.ToNodeId, out var list))
                {
                    list = new List<int>();
                    _connectionsByDestination[conn.ToNodeId] = list;
                }
                list.Add(i);
            }
        }

        // ============================================================
        // NODE MANAGEMENT
        // ============================================================

        /// <summary>
        /// Adds a node from the catalogue with its default bias value.
        /// Returns the new node's ID.
        /// </summary>
        public int AddNodeFromCatalogue(int catalogueId)
        {
            var def = NodeCatalogue.GetDefinition(catalogueId);
            return AddNodeFromCatalogue(catalogueId, def.DefaultBias);
        }

        /// <summary>
        /// Adds a node from the catalogue with a specific bias value.
        /// For genes, the bias IS the gene value.
        /// Returns the new node's ID.
        /// </summary>
        public int AddNodeFromCatalogue(int catalogueId, float bias)
        {
            if (_catalogueToNodeId.ContainsKey(catalogueId))
            {
                throw new InvalidOperationException($"Node from catalogue {catalogueId} already exists in this network");
            }

            var def = NodeCatalogue.GetDefinition(catalogueId);
            int nodeId = _nextNodeId++;

            BiomeNode node;
            if (def.Category == NodeCategory.Gene)
            {
                node = BiomeNode.CreateGene(nodeId, catalogueId, bias);
                _geneNodeIds.Add(nodeId);
            }
            else
            {
                node = BiomeNode.Create(nodeId, catalogueId, def.Affinity, def.ActivationFunction, bias);
                if (def.Category == NodeCategory.Output)
                    _outputNodeIds.Add(nodeId);
                else
                    _sensorNodeIds.Add(nodeId);
            }

            _nodes[nodeId] = node;
            _catalogueToNodeId[catalogueId] = nodeId;

            return nodeId;
        }

        /// <summary>
        /// Adds a hidden neuron node (not from catalogue).
        /// Returns the new node's ID.
        /// </summary>
        public int AddHiddenNode(ActivationFunctionType actFunc, float bias = 0f)
        {
            int nodeId = _nextNodeId++;
            var node = BiomeNode.CreateHidden(nodeId, actFunc, bias);
            _nodes[nodeId] = node;
            _hiddenNodeIds.Add(nodeId);
            return nodeId;
        }

        /// <summary>
        /// Checks if a node from the catalogue is instantiated in this network.
        /// </summary>
        public bool HasNodeFromCatalogue(int catalogueId)
        {
            return _catalogueToNodeId.ContainsKey(catalogueId);
        }

        /// <summary>
        /// Gets the node ID for a catalogue entry, or -1 if not instantiated.
        /// </summary>
        public int GetNodeIdFromCatalogue(int catalogueId)
        {
            return _catalogueToNodeId.TryGetValue(catalogueId, out int nodeId) ? nodeId : -1;
        }

        /// <summary>
        /// Gets a node by its ID.
        /// </summary>
        public BiomeNode GetNode(int nodeId)
        {
            return _nodes[nodeId];
        }

        /// <summary>
        /// Tries to get a node by its ID.
        /// </summary>
        public bool TryGetNode(int nodeId, out BiomeNode node)
        {
            return _nodes.TryGetValue(nodeId, out node);
        }

        /// <summary>
        /// Updates a node's values (for sensor population, output reading, etc.).
        /// </summary>
        public void SetNode(int nodeId, BiomeNode node)
        {
            _nodes[nodeId] = node;
        }

        /// <summary>
        /// Gets a gene value by catalogue ID. Returns default if not present.
        /// </summary>
        public float GetGeneValue(int geneCatalogueId)
        {
            if (_catalogueToNodeId.TryGetValue(geneCatalogueId, out int nodeId))
            {
                return _nodes[nodeId].Output; // For genes, Output = Bias = gene value
            }

            // Return default from catalogue
            var def = NodeCatalogue.GetDefinition(geneCatalogueId);
            return def.DefaultBias;
        }

        /// <summary>
        /// Sets a gene value by catalogue ID. Only works for GENETIC nodes.
        /// </summary>
        public void SetGeneValue(int geneCatalogueId, float value)
        {
            if (_catalogueToNodeId.TryGetValue(geneCatalogueId, out int nodeId))
            {
                var node = _nodes[nodeId];
                if (node.Affinity != NodeAffinity.Genetic)
                    throw new InvalidOperationException($"Node {geneCatalogueId} is not a gene node");

                node.Bias = value;
                node.Output = value;
                _nodes[nodeId] = node;
            }
        }

        /// <summary>
        /// Gets an output value by catalogue ID. Returns ActFunc(Bias) if not connected.
        /// </summary>
        public float GetOutputValue(int outputCatalogueId)
        {
            if (_catalogueToNodeId.TryGetValue(outputCatalogueId, out int nodeId))
            {
                return _nodes[nodeId].Output;
            }

            // Return default (ActFunc applied to default bias)
            var def = NodeCatalogue.GetDefinition(outputCatalogueId);
            return ActivationFunctions.Apply(def.ActivationFunction, 0f, def.DefaultBias);
        }

        /// <summary>
        /// Sets a sensor value (for modules to populate sensors).
        /// </summary>
        public void SetSensorValue(int sensorCatalogueId, float value)
        {
            if (_catalogueToNodeId.TryGetValue(sensorCatalogueId, out int nodeId))
            {
                var node = _nodes[nodeId];
                node.Output = value;
                _nodes[nodeId] = node;
            }
        }

        // ============================================================
        // CONNECTION MANAGEMENT
        // ============================================================

        /// <summary>
        /// Adds a connection between two nodes.
        /// Returns the connection ID.
        /// </summary>
        public int AddConnection(int fromNodeId, int toNodeId, float weight, int innovationNumber = -1)
        {
            if (!_nodes.ContainsKey(fromNodeId))
                throw new ArgumentException($"From node {fromNodeId} does not exist");
            if (!_nodes.ContainsKey(toNodeId))
                throw new ArgumentException($"To node {toNodeId} does not exist");

            int connId = _nextConnectionId++;
            var conn = BiomeConnection.Create(connId, fromNodeId, toNodeId, weight, true, innovationNumber);
            _connections.Add(conn);

            // Update destination cache
            if (!_connectionsByDestination.TryGetValue(toNodeId, out var list))
            {
                list = new List<int>();
                _connectionsByDestination[toNodeId] = list;
            }
            list.Add(_connections.Count - 1);

            return connId;
        }

        /// <summary>
        /// Adds a connection using catalogue IDs (must already be instantiated).
        /// </summary>
        public int AddConnectionByCatalogue(int fromCatalogueId, int toCatalogueId, float weight)
        {
            int fromNodeId = GetNodeIdFromCatalogue(fromCatalogueId);
            int toNodeId = GetNodeIdFromCatalogue(toCatalogueId);

            if (fromNodeId < 0)
                throw new ArgumentException($"From catalogue node {fromCatalogueId} not instantiated");
            if (toNodeId < 0)
                throw new ArgumentException($"To catalogue node {toCatalogueId} not instantiated");

            return AddConnection(fromNodeId, toNodeId, weight);
        }

        /// <summary>
        /// Gets a connection by index.
        /// </summary>
        public BiomeConnection GetConnection(int index)
        {
            return _connections[index];
        }

        /// <summary>
        /// Updates a connection.
        /// </summary>
        public void SetConnection(int index, BiomeConnection connection)
        {
            _connections[index] = connection;
        }

        /// <summary>
        /// Gets the number of connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        // ============================================================
        // NETWORK PROPERTIES
        // ============================================================

        /// <summary>Total number of nodes.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>Number of gene nodes.</summary>
        public int GeneCount => _geneNodeIds.Count;

        /// <summary>Number of hidden nodes.</summary>
        public int HiddenCount => _hiddenNodeIds.Count;

        /// <summary>All node IDs.</summary>
        public IEnumerable<int> AllNodeIds => _nodes.Keys;

        /// <summary>All gene node IDs.</summary>
        public IReadOnlyList<int> GeneNodeIds => _geneNodeIds;

        /// <summary>All sensor node IDs.</summary>
        public IReadOnlyList<int> SensorNodeIds => _sensorNodeIds;

        /// <summary>All output node IDs.</summary>
        public IReadOnlyList<int> OutputNodeIds => _outputNodeIds;

        /// <summary>All hidden node IDs.</summary>
        public IReadOnlyList<int> HiddenNodeIds => _hiddenNodeIds;

        // ============================================================
        // NETWORK PROCESSING
        // ============================================================

        /// <summary>
        /// Advances the frame counter and processes the network.
        /// Call this once per frame after populating sensors.
        /// </summary>
        public void Process(float deltaTime)
        {
            _currentFrame++;

            // 1. Reset activation accumulators for nodes that will update
            ResetActivations();

            // 2. Propagate signals through connections
            PropagateConnections();

            // 3. Apply activation functions and update outputs
            UpdateNodes(deltaTime);
        }

        private void ResetActivations()
        {
            // Reset behavioural nodes (update every frame)
            foreach (int nodeId in _outputNodeIds)
            {
                var node = _nodes[nodeId];
                node.Activation = 0f;
                _nodes[nodeId] = node;
            }
            foreach (int nodeId in _hiddenNodeIds)
            {
                var node = _nodes[nodeId];
                node.Activation = 0f;
                _nodes[nodeId] = node;
            }

            // Reset biological nodes only on their update tick
            foreach (int nodeId in _sensorNodeIds)
            {
                var node = _nodes[nodeId];
                if (node.Affinity == NodeAffinity.Biological &&
                    AffinitySystem.ShouldUpdateNode(node.Affinity, node.LastUpdateFrame, _currentFrame))
                {
                    node.Activation = 0f;
                    _nodes[nodeId] = node;
                }
            }
        }

        private void PropagateConnections()
        {
            foreach (var conn in _connections)
            {
                if (!conn.Enabled) continue;

                if (!_nodes.TryGetValue(conn.FromNodeId, out var fromNode)) continue;
                if (!_nodes.TryGetValue(conn.ToNodeId, out var toNode)) continue;

                // Check if destination should update this frame
                if (!AffinitySystem.ShouldUpdateNode(toNode.Affinity, toNode.LastUpdateFrame, _currentFrame))
                    continue;

                // Calculate effective weight with affinity scaling
                float effectiveWeight = conn.GetEffectiveWeight(fromNode.Affinity, toNode.Affinity);

                // Accumulate weighted signal
                toNode.Activation += fromNode.Output * effectiveWeight;
                _nodes[conn.ToNodeId] = toNode;
            }
        }

        private void UpdateNodes(float deltaTime)
        {
            // Update behavioural nodes (every frame)
            UpdateNodeList(_outputNodeIds, deltaTime);
            UpdateNodeList(_hiddenNodeIds, deltaTime);

            // Update biological nodes (only on their tick)
            foreach (int nodeId in _sensorNodeIds)
            {
                var node = _nodes[nodeId];
                if (node.Affinity == NodeAffinity.Biological &&
                    AffinitySystem.ShouldUpdateNode(node.Affinity, node.LastUpdateFrame, _currentFrame))
                {
                    node.PreviousOutput = node.Output;
                    node.Output = ActivationFunctions.Apply(
                        node.ActivationFunction, node.Activation, node.Bias,
                        node.PreviousOutput, deltaTime);
                    node.LastUpdateFrame = _currentFrame;
                    _nodes[nodeId] = node;
                }
            }

            // Gene nodes never update - their output is always their bias
        }

        private void UpdateNodeList(List<int> nodeIds, float deltaTime)
        {
            foreach (int nodeId in nodeIds)
            {
                var node = _nodes[nodeId];
                node.PreviousOutput = node.Output;
                node.Output = ActivationFunctions.Apply(
                    node.ActivationFunction, node.Activation, node.Bias,
                    node.PreviousOutput, deltaTime);
                node.LastUpdateFrame = _currentFrame;
                _nodes[nodeId] = node;
            }
        }

        // ============================================================
        // WAG CALCULATIONS
        // ============================================================

        /// <summary>
        /// Calculates the total WAG weight for organ size distribution.
        /// </summary>
        public float GetTotalWAG()
        {
            float total = 0f;
            total += GetGeneValue(NodeCatalogue.Gene_WAG_ArmMuscles);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_Stomach);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_EggOrgan);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_FatOrgan);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_Armor);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_Throat);
            total += GetGeneValue(NodeCatalogue.Gene_WAG_JawMuscles);
            return math.max(total, 0.001f); // Prevent division by zero
        }

        /// <summary>
        /// Gets the normalized organ size (0-1) for a WAG gene.
        /// </summary>
        public float GetOrganSize(int wagGeneCatalogueId)
        {
            float total = GetTotalWAG();
            return GetGeneValue(wagGeneCatalogueId) / total;
        }

        /// <summary>
        /// Gets all organ sizes as a struct for efficient access.
        /// </summary>
        public OrganSizes GetOrganSizes()
        {
            float total = GetTotalWAG();
            return new OrganSizes
            {
                ArmMuscles = GetGeneValue(NodeCatalogue.Gene_WAG_ArmMuscles) / total,
                Stomach = GetGeneValue(NodeCatalogue.Gene_WAG_Stomach) / total,
                EggOrgan = GetGeneValue(NodeCatalogue.Gene_WAG_EggOrgan) / total,
                FatOrgan = GetGeneValue(NodeCatalogue.Gene_WAG_FatOrgan) / total,
                Armor = GetGeneValue(NodeCatalogue.Gene_WAG_Armor) / total,
                Throat = GetGeneValue(NodeCatalogue.Gene_WAG_Throat) / total,
                JawMuscles = GetGeneValue(NodeCatalogue.Gene_WAG_JawMuscles) / total
            };
        }
    }

    /// <summary>
    /// Normalized organ sizes (each value 0-1, all sum to 1).
    /// </summary>
    public struct OrganSizes
    {
        public float ArmMuscles;
        public float Stomach;
        public float EggOrgan;
        public float FatOrgan;
        public float Armor;
        public float Throat;
        public float JawMuscles;
    }
}
