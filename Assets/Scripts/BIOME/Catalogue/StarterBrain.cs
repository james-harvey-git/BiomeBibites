using Unity.Mathematics;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Creates the default starter brain configuration for new bibites.
    ///
    /// The starter brain includes:
    /// - All essential gene nodes (with default values)
    /// - A sparse subset of sensor nodes (PlantCloseness, PlantAngle, Fullness, EnergyRatio)
    /// - All output nodes (most unconnected, using their default bias)
    /// - Seed connections for basic food-seeking behavior
    ///
    /// Mutations can add more nodes from the catalogue and create new connections.
    /// </summary>
    public static class StarterBrain
    {
        /// <summary>
        /// Creates a default BIOME network for a new bibite.
        /// </summary>
        public static BiomeNetwork CreateDefault()
        {
            var network = new BiomeNetwork();

            // Add all essential genes with default values
            AddEssentialGenes(network);

            // Add sparse sensor subset (not all sensors - sparse instantiation)
            AddStarterSensors(network);

            // Add all output nodes (they use default bias when unconnected)
            AddAllOutputs(network);

            // Add seed connections for basic food-seeking
            AddSeedConnections(network);

            return network;
        }

        /// <summary>
        /// Creates a BIOME network with randomized gene values.
        /// </summary>
        public static BiomeNetwork CreateRandomized(ref Random random, float variance = 0.2f)
        {
            var network = new BiomeNetwork();

            // Add genes with randomized values
            AddEssentialGenesRandomized(network, ref random, variance);

            // Add sparse sensor subset
            AddStarterSensors(network);

            // Add all output nodes
            AddAllOutputs(network);

            // Add seed connections
            AddSeedConnections(network);

            return network;
        }

        private static void AddEssentialGenes(BiomeNetwork network)
        {
            // Appearance Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ColorRed);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ColorGreen);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ColorBlue);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_EyeHueOffset);

            // Size & Metabolism Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_SizeRatio);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_MetabolismSpeed);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_Diet);

            // Mutation Rate Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_AvgGeneMutations);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_GeneMutationVariance);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_AvgBrainMutations);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_BrainMutationVariance);

            // Reproduction Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_LayTime);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_BroodTime);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HatchTime);

            // Vision Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ViewRadius);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ViewAngle);

            // Clock Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_ClockPeriod);

            // Pheromone Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_PheromoneRadius);

            // Herding Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HerdSeparationWeight);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HerdAlignmentWeight);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HerdCohesionWeight);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HerdVelocityWeight);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_HerdSeparationDistance);

            // Growth Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_GrowthScaleFactor);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_GrowthMaturityFactor);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_GrowthMaturityExponent);

            // Organ WAG Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_ArmMuscles);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_Stomach);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_EggOrgan);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_FatOrgan);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_Armor);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_Throat);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_WAG_JawMuscles);

            // Fat Metabolism Genes
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_FatStorageThreshold);
            network.AddNodeFromCatalogue(NodeCatalogue.Gene_FatStorageDeadband);

            // Constants for module enables
            network.AddNodeFromCatalogue(NodeCatalogue.Constant_1);
            network.AddNodeFromCatalogue(NodeCatalogue.Constant_0);
        }

        private static void AddEssentialGenesRandomized(BiomeNetwork network, ref Random random, float variance)
        {
            // Helper to add gene with randomized value around default
            void AddGeneRandomized(int catalogueId)
            {
                var def = NodeCatalogue.GetDefinition(catalogueId);
                float randomizedValue = def.DefaultBias + random.NextFloat(-variance, variance) * math.abs(def.DefaultBias + 0.1f);

                // Clamp certain genes to valid ranges
                switch (catalogueId)
                {
                    case NodeCatalogue.Gene_ColorRed:
                    case NodeCatalogue.Gene_ColorGreen:
                    case NodeCatalogue.Gene_ColorBlue:
                        randomizedValue = math.clamp(randomizedValue, 0f, 1f);
                        break;
                    case NodeCatalogue.Gene_Diet:
                        randomizedValue = math.clamp(randomizedValue, 0f, 1f);
                        break;
                    case NodeCatalogue.Gene_SizeRatio:
                    case NodeCatalogue.Gene_MetabolismSpeed:
                        randomizedValue = math.max(0.1f, randomizedValue);
                        break;
                    case NodeCatalogue.Gene_ViewRadius:
                    case NodeCatalogue.Gene_ViewAngle:
                    case NodeCatalogue.Gene_ClockPeriod:
                    case NodeCatalogue.Gene_PheromoneRadius:
                        randomizedValue = math.max(0.1f, randomizedValue);
                        break;
                }

                network.AddNodeFromCatalogue(catalogueId, randomizedValue);
            }

            // Appearance Genes
            AddGeneRandomized(NodeCatalogue.Gene_ColorRed);
            AddGeneRandomized(NodeCatalogue.Gene_ColorGreen);
            AddGeneRandomized(NodeCatalogue.Gene_ColorBlue);
            AddGeneRandomized(NodeCatalogue.Gene_EyeHueOffset);

            // Size & Metabolism Genes
            AddGeneRandomized(NodeCatalogue.Gene_SizeRatio);
            AddGeneRandomized(NodeCatalogue.Gene_MetabolismSpeed);
            AddGeneRandomized(NodeCatalogue.Gene_Diet);

            // Mutation Rate Genes
            AddGeneRandomized(NodeCatalogue.Gene_AvgGeneMutations);
            AddGeneRandomized(NodeCatalogue.Gene_GeneMutationVariance);
            AddGeneRandomized(NodeCatalogue.Gene_AvgBrainMutations);
            AddGeneRandomized(NodeCatalogue.Gene_BrainMutationVariance);

            // Reproduction Genes
            AddGeneRandomized(NodeCatalogue.Gene_LayTime);
            AddGeneRandomized(NodeCatalogue.Gene_BroodTime);
            AddGeneRandomized(NodeCatalogue.Gene_HatchTime);

            // Vision Genes
            AddGeneRandomized(NodeCatalogue.Gene_ViewRadius);
            AddGeneRandomized(NodeCatalogue.Gene_ViewAngle);

            // Clock Genes
            AddGeneRandomized(NodeCatalogue.Gene_ClockPeriod);

            // Pheromone Genes
            AddGeneRandomized(NodeCatalogue.Gene_PheromoneRadius);

            // Herding Genes
            AddGeneRandomized(NodeCatalogue.Gene_HerdSeparationWeight);
            AddGeneRandomized(NodeCatalogue.Gene_HerdAlignmentWeight);
            AddGeneRandomized(NodeCatalogue.Gene_HerdCohesionWeight);
            AddGeneRandomized(NodeCatalogue.Gene_HerdVelocityWeight);
            AddGeneRandomized(NodeCatalogue.Gene_HerdSeparationDistance);

            // Growth Genes
            AddGeneRandomized(NodeCatalogue.Gene_GrowthScaleFactor);
            AddGeneRandomized(NodeCatalogue.Gene_GrowthMaturityFactor);
            AddGeneRandomized(NodeCatalogue.Gene_GrowthMaturityExponent);

            // Organ WAG Genes (keep these more constrained)
            AddGeneRandomized(NodeCatalogue.Gene_WAG_ArmMuscles);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_Stomach);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_EggOrgan);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_FatOrgan);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_Armor);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_Throat);
            AddGeneRandomized(NodeCatalogue.Gene_WAG_JawMuscles);

            // Fat Metabolism Genes
            AddGeneRandomized(NodeCatalogue.Gene_FatStorageThreshold);
            AddGeneRandomized(NodeCatalogue.Gene_FatStorageDeadband);

            // Constants (not randomized)
            network.AddNodeFromCatalogue(NodeCatalogue.Constant_1);
            network.AddNodeFromCatalogue(NodeCatalogue.Constant_0);
        }

        private static void AddStarterSensors(BiomeNetwork network)
        {
            // Sparse instantiation - only add essential sensors for basic survival
            // Mutations can add more sensors from the catalogue

            // Internal state (BIOLOGICAL affinity)
            network.AddNodeFromCatalogue(NodeCatalogue.Sense_EnergyRatio);
            network.AddNodeFromCatalogue(NodeCatalogue.Sense_Fullness);

            // Vision - Plants (BEHAVIOURAL affinity) - essential for food finding
            network.AddNodeFromCatalogue(NodeCatalogue.Sense_PlantCloseness);
            network.AddNodeFromCatalogue(NodeCatalogue.Sense_PlantAngle);

            // Note: Other sensors like MeatCloseness, BibiteCloseness, Pheromones, Clock
            // are NOT added by default - mutations can add them for more complex behaviors
        }

        private static void AddAllOutputs(BiomeNetwork network)
        {
            // All outputs are added but most will be unconnected
            // Unconnected outputs use ActFunc(bias) as their default value

            // Movement
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Accelerate);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Rotate);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Herding);

            // Reproduction
            network.AddNodeFromCatalogue(NodeCatalogue.Output_EggProduction);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Want2Lay);

            // Feeding
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Want2Eat);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Digestion);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Grab);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Want2Attack);

            // Growth/Health
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Want2Grow);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_Want2Heal);

            // Clock
            network.AddNodeFromCatalogue(NodeCatalogue.Output_ClkReset);

            // Pheromones
            network.AddNodeFromCatalogue(NodeCatalogue.Output_PhereOut1);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_PhereOut2);
            network.AddNodeFromCatalogue(NodeCatalogue.Output_PhereOut3);
        }

        private static void AddSeedConnections(BiomeNetwork network)
        {
            // Seed connections create basic "food seeker" behavior
            // These are Bibites-faithful starter connections

            // PlantAngle → Rotate (weight: +1.0)
            // Turn toward plants
            network.AddConnectionByCatalogue(
                NodeCatalogue.Sense_PlantAngle,
                NodeCatalogue.Output_Rotate,
                1.0f);

            // PlantCloseness → Accelerate (weight: -1.0)
            // Move faster when plants are far (low closeness = more speed)
            network.AddConnectionByCatalogue(
                NodeCatalogue.Sense_PlantCloseness,
                NodeCatalogue.Output_Accelerate,
                -1.0f);

            // Fullness → Digestion (weight: +1.0)
            // Digest faster when stomach is full
            network.AddConnectionByCatalogue(
                NodeCatalogue.Sense_Fullness,
                NodeCatalogue.Output_Digestion,
                1.0f);
        }

        /// <summary>
        /// Validates that a network has all required components.
        /// </summary>
        public static bool Validate(BiomeNetwork network, out string error)
        {
            // Check essential genes
            if (!network.HasNodeFromCatalogue(NodeCatalogue.Gene_SizeRatio))
            {
                error = "Missing Gene_SizeRatio";
                return false;
            }
            if (!network.HasNodeFromCatalogue(NodeCatalogue.Gene_MetabolismSpeed))
            {
                error = "Missing Gene_MetabolismSpeed";
                return false;
            }
            if (!network.HasNodeFromCatalogue(NodeCatalogue.Gene_Diet))
            {
                error = "Missing Gene_Diet";
                return false;
            }

            // Check at least one movement output
            if (!network.HasNodeFromCatalogue(NodeCatalogue.Output_Accelerate))
            {
                error = "Missing Output_Accelerate";
                return false;
            }

            error = null;
            return true;
        }
    }
}
