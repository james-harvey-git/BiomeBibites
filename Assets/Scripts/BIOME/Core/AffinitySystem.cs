using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Manages the affinity system which controls:
    /// 1. Node update rates (GENETIC=never, BIOLOGICAL=slow, BEHAVIOURAL=every frame)
    /// 2. Connection effectiveness between nodes of different affinities
    /// 3. Mutation priors for new connections
    /// </summary>
    public static class AffinitySystem
    {
        /// <summary>
        /// Update interval in frames for BIOLOGICAL nodes.
        /// At 60 FPS, 12 frames = 5 Hz update rate.
        /// </summary>
        public const int BiologicalUpdateInterval = 12;

        /// <summary>
        /// Connection effectiveness matrix.
        /// Indexed by [fromAffinity, toAffinity].
        /// This enforces biological realism:
        /// - Genes strongly influence biology, weakly influence behavior directly
        /// - Biology cannot easily change genes (blocks Lamarckism)
        /// - Behavior cannot change genes (thoughts don't rewrite DNA)
        /// </summary>
        private static readonly float[,] EffectivenessMatrix = new float[3, 3]
        {
            // To:      Genetic  Biological  Behavioural
            /* From Genetic */     { 1.0f,    0.8f,       0.3f },
            /* From Biological */  { 0.05f,   1.0f,       1.0f },
            /* From Behavioural */ { 0.01f,   0.3f,       1.0f }
        };

        /// <summary>
        /// Mutation prior weights for adding new connections.
        /// Higher values = more likely to be selected.
        /// Indexed by [fromAffinity, toAffinity].
        /// </summary>
        private static readonly float[,] MutationPriorMatrix = new float[3, 3]
        {
            // To:      Genetic  Biological  Behavioural
            /* From Genetic */     { 0.5f,    1.0f,       0.8f },
            /* From Biological */  { 0.05f,   0.7f,       1.0f },
            /* From Behavioural */ { 0.01f,   0.2f,       1.0f }
        };

        /// <summary>
        /// Gets the connection effectiveness multiplier for a connection from one affinity to another.
        /// </summary>
        public static float GetConnectionEffectiveness(NodeAffinity from, NodeAffinity to)
        {
            return EffectivenessMatrix[(int)from, (int)to];
        }

        /// <summary>
        /// Gets the mutation prior weight for adding a connection from one affinity to another.
        /// Used when randomly selecting node pairs for new connections.
        /// </summary>
        public static float GetMutationPrior(NodeAffinity from, NodeAffinity to)
        {
            return MutationPriorMatrix[(int)from, (int)to];
        }

        /// <summary>
        /// Determines if a node should update this frame based on its affinity and last update time.
        /// - GENETIC: Never updates (returns false always)
        /// - BIOLOGICAL: Updates every BiologicalUpdateInterval frames
        /// - BEHAVIOURAL: Updates every frame
        /// </summary>
        public static bool ShouldUpdateNode(NodeAffinity affinity, int lastUpdateFrame, int currentFrame)
        {
            switch (affinity)
            {
                case NodeAffinity.Genetic:
                    // Gene nodes never update - their output is fixed at birth
                    return false;

                case NodeAffinity.Biological:
                    // Biological nodes update at a slow rate
                    return (currentFrame - lastUpdateFrame) >= BiologicalUpdateInterval;

                case NodeAffinity.Behavioural:
                    // Behavioural nodes update every frame
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns a description of the update rate for a given affinity.
        /// </summary>
        public static string GetUpdateRateDescription(NodeAffinity affinity)
        {
            switch (affinity)
            {
                case NodeAffinity.Genetic:
                    return "Never (fixed at birth)";
                case NodeAffinity.Biological:
                    return $"~{60 / BiologicalUpdateInterval} Hz (every {BiologicalUpdateInterval} frames)";
                case NodeAffinity.Behavioural:
                    return "Every frame (~60 Hz)";
                default:
                    return "Unknown";
            }
        }
    }
}
