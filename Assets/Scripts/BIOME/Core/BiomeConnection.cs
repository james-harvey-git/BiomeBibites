using System;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// A connection links two nodes together, allowing one node's output to influence another's activation.
    /// Connection effectiveness is scaled by the affinity compatibility matrix.
    /// </summary>
    [Serializable]
    public struct BiomeConnection
    {
        /// <summary>Unique identifier for this connection</summary>
        public int Id;

        /// <summary>The source node ID (signal comes FROM this node)</summary>
        public int FromNodeId;

        /// <summary>The destination node ID (signal goes TO this node)</summary>
        public int ToNodeId;

        /// <summary>How strongly the signal is scaled. Can be positive or negative.</summary>
        public float Weight;

        /// <summary>Whether this connection is active. Disabled connections don't propagate signals.</summary>
        public bool Enabled;

        /// <summary>
        /// Innovation number for NEAT-style crossover (optional).
        /// Connections with the same innovation number are considered homologous.
        /// </summary>
        public int InnovationNumber;

        /// <summary>
        /// Creates a new connection between two nodes.
        /// </summary>
        public static BiomeConnection Create(int id, int fromNodeId, int toNodeId, float weight,
            bool enabled = true, int innovationNumber = -1)
        {
            return new BiomeConnection
            {
                Id = id,
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Weight = weight,
                Enabled = enabled,
                InnovationNumber = innovationNumber
            };
        }

        /// <summary>
        /// Calculates the effective weight after applying the affinity effectiveness matrix.
        /// Connections between nodes of different affinities have reduced effectiveness.
        /// </summary>
        public float GetEffectiveWeight(NodeAffinity fromAffinity, NodeAffinity toAffinity)
        {
            return Weight * AffinitySystem.GetConnectionEffectiveness(fromAffinity, toAffinity);
        }
    }
}
