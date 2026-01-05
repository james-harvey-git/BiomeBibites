using System;
using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Node affinity determines update rate and connection effectiveness.
    /// In BIOME, genes and neurons are unified - a "gene" is just a node with GENETIC affinity.
    /// </summary>
    public enum NodeAffinity
    {
        /// <summary>Fixed at birth, never updates. Output = Bias. Used for gene values.</summary>
        Genetic = 0,
        /// <summary>Updates slowly (~1-5 Hz). Used for internal state like fullness, health.</summary>
        Biological = 1,
        /// <summary>Updates every frame (~60 Hz). Used for sensors, motors, hidden neurons.</summary>
        Behavioural = 2
    }

    /// <summary>
    /// Activation function types for nodes.
    /// Output nodes have FIXED activation functions. Hidden nodes can mutate their function.
    /// </summary>
    public enum ActivationFunctionType
    {
        Identity = 0,    // y = x (used for Gene nodes)
        Sigmoid = 1,     // y = 1/(1+e^-x), range [0,1]
        Linear = 2,      // y = x, range (-inf, inf)
        TanH = 3,        // y = tanh(x), range [-1,1]
        Sine = 4,        // y = sin(x), range [-1,1]
        ReLU = 5,        // y = max(0,x), range [0,inf)
        Gaussian = 6,    // y = 1/(x^2+1), range (0,1]
        Latch = 7,       // Binary memory, range [0,1]
        Differential = 8,// Rate of change, range (-inf,inf)
        Abs = 9,         // y = |x|, range [0,inf)
        Mult = 10,       // Product of inputs, range [0,1]
        Integrator = 11, // Accumulator, range (-inf,inf)
        Inhibitory = 12, // Self-decaying, range (-inf,inf)
        SoftLatch = 13   // Hysteresis, range [0,1]
    }

    /// <summary>
    /// The fundamental computing unit in BIOME.
    /// Nodes replace BOTH genes AND neurons - they exist in the same network.
    /// A "gene" is a node with GENETIC affinity where Output = Bias (fixed at birth).
    /// A "neuron" is a node with BEHAVIOURAL affinity that computes Output = ActFunc(Activation + Bias).
    /// </summary>
    [Serializable]
    public struct BiomeNode
    {
        /// <summary>Unique identifier for this node instance</summary>
        public int Id;

        /// <summary>Node type from catalogue (e.g., "Gene_Diet", "Sense_PlantAngle", "Output_Accelerate")</summary>
        public int CatalogueId;

        /// <summary>Determines update rate: GENETIC (never), BIOLOGICAL (slow), BEHAVIOURAL (every frame)</summary>
        public NodeAffinity Affinity;

        /// <summary>How to process the activation. Fixed for output nodes, mutable for hidden nodes.</summary>
        public ActivationFunctionType ActivationFunction;

        /// <summary>
        /// Default/baseline value. For gene nodes: this IS the gene value.
        /// For other nodes: added to activation before applying ActFunc.
        /// </summary>
        public float Bias;

        /// <summary>Accumulated input stimulation this tick (sum of weighted incoming signals)</summary>
        public float Activation;

        /// <summary>The result: ActFunc(Activation + Bias). For genes: Output = Bias.</summary>
        public float Output;

        /// <summary>Previous frame's output. Used for stateful functions (Latch, Differential, Integrator).</summary>
        public float PreviousOutput;

        /// <summary>Frame number when this node was last updated. Used for affinity-based timing.</summary>
        public int LastUpdateFrame;

        /// <summary>
        /// Creates a new node with the given properties.
        /// </summary>
        public static BiomeNode Create(int id, int catalogueId, NodeAffinity affinity,
            ActivationFunctionType actFunc, float bias)
        {
            return new BiomeNode
            {
                Id = id,
                CatalogueId = catalogueId,
                Affinity = affinity,
                ActivationFunction = actFunc,
                Bias = bias,
                Activation = 0f,
                Output = affinity == NodeAffinity.Genetic ? bias : 0f,
                PreviousOutput = 0f,
                LastUpdateFrame = 0
            };
        }

        /// <summary>
        /// Creates a Gene node (GENETIC affinity, Identity activation, Output = Bias).
        /// </summary>
        public static BiomeNode CreateGene(int id, int catalogueId, float geneValue)
        {
            return new BiomeNode
            {
                Id = id,
                CatalogueId = catalogueId,
                Affinity = NodeAffinity.Genetic,
                ActivationFunction = ActivationFunctionType.Identity,
                Bias = geneValue,
                Activation = 0f,
                Output = geneValue, // Gene nodes: Output = Bias always
                PreviousOutput = geneValue,
                LastUpdateFrame = 0
            };
        }

        /// <summary>
        /// Creates a hidden neuron node (BEHAVIOURAL affinity, random activation function).
        /// </summary>
        public static BiomeNode CreateHidden(int id, ActivationFunctionType actFunc, float bias = 0f)
        {
            return new BiomeNode
            {
                Id = id,
                CatalogueId = -1, // Hidden nodes are not in catalogue
                Affinity = NodeAffinity.Behavioural,
                ActivationFunction = actFunc,
                Bias = bias,
                Activation = 0f,
                Output = 0f,
                PreviousOutput = 0f,
                LastUpdateFrame = 0
            };
        }
    }
}
