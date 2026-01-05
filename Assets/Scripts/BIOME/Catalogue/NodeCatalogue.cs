using System.Collections.Generic;

namespace BiomeBibites.BIOME
{
    /// <summary>
    /// Category of node in the catalogue.
    /// </summary>
    public enum NodeCategory
    {
        Gene,           // GENETIC affinity nodes (gene values)
        SensorInternal, // BIOLOGICAL affinity internal state sensors
        SensorExternal, // BEHAVIOURAL affinity external sensors
        Output,         // BEHAVIOURAL affinity output commands
        Hidden          // BEHAVIOURAL affinity hidden neurons (not in catalogue, created by mutation)
    }

    /// <summary>
    /// Definition of a node type in the catalogue.
    /// The catalogue defines all possible node types that can be instantiated.
    /// Bibites start with a sparse subset; mutations can add more from the catalogue.
    /// </summary>
    public struct NodeDefinition
    {
        public int CatalogueId;
        public string Name;
        public NodeCategory Category;
        public NodeAffinity Affinity;
        public ActivationFunctionType ActivationFunction;
        public float DefaultBias;
        public string Description;

        public NodeDefinition(int id, string name, NodeCategory category, NodeAffinity affinity,
            ActivationFunctionType actFunc, float defaultBias, string description)
        {
            CatalogueId = id;
            Name = name;
            Category = category;
            Affinity = affinity;
            ActivationFunction = actFunc;
            DefaultBias = defaultBias;
            Description = description;
        }
    }

    /// <summary>
    /// The complete catalogue of all possible node types in BIOME.
    /// This includes all genes from the original Bibites, all sensor inputs, and all outputs.
    /// Mutations can add nodes from this catalogue that are not yet instantiated.
    /// </summary>
    public static class NodeCatalogue
    {
        // ============================================================
        // CATALOGUE IDs - Use these constants to reference nodes
        // ============================================================

        // Gene Nodes: 0-99
        public const int Gene_ColorRed = 0;
        public const int Gene_ColorGreen = 1;
        public const int Gene_ColorBlue = 2;
        public const int Gene_EyeHueOffset = 3;
        public const int Gene_SizeRatio = 10;
        public const int Gene_MetabolismSpeed = 11;
        public const int Gene_Diet = 12;
        public const int Gene_AvgGeneMutations = 20;
        public const int Gene_GeneMutationVariance = 21;
        public const int Gene_AvgBrainMutations = 22;
        public const int Gene_BrainMutationVariance = 23;
        public const int Gene_LayTime = 30;
        public const int Gene_BroodTime = 31;
        public const int Gene_HatchTime = 32;
        public const int Gene_ViewRadius = 40;
        public const int Gene_ViewAngle = 41;
        public const int Gene_ClockPeriod = 50;
        public const int Gene_PheromoneRadius = 51;
        public const int Gene_HerdSeparationWeight = 60;
        public const int Gene_HerdAlignmentWeight = 61;
        public const int Gene_HerdCohesionWeight = 62;
        public const int Gene_HerdVelocityWeight = 63;
        public const int Gene_HerdSeparationDistance = 64;
        public const int Gene_GrowthScaleFactor = 70;
        public const int Gene_GrowthMaturityFactor = 71;
        public const int Gene_GrowthMaturityExponent = 72;
        public const int Gene_WAG_ArmMuscles = 80;
        public const int Gene_WAG_Stomach = 81;
        public const int Gene_WAG_EggOrgan = 82;
        public const int Gene_WAG_FatOrgan = 83;
        public const int Gene_WAG_Armor = 84;
        public const int Gene_WAG_Throat = 85;
        public const int Gene_WAG_JawMuscles = 86;
        public const int Gene_FatStorageThreshold = 90;
        public const int Gene_FatStorageDeadband = 91;

        // Internal Sensor Nodes (BIOLOGICAL): 100-149
        public const int Sense_EnergyRatio = 100;
        public const int Sense_LifeRatio = 101;
        public const int Sense_Fullness = 102;
        public const int Sense_Maturity = 103;
        public const int Sense_EggStored = 104;
        public const int Sense_FatRatio = 105;
        public const int Sense_TimeAlive = 106;

        // External Sensor Nodes (BEHAVIOURAL): 150-249
        public const int Sense_Speed = 150;
        public const int Sense_RotationSpeed = 151;
        public const int Sense_IsGrabbing = 152;
        public const int Sense_AttackedDamage = 153;
        public const int Sense_PlantCloseness = 160;
        public const int Sense_PlantAngle = 161;
        public const int Sense_NPlants = 162;
        public const int Sense_MeatCloseness = 170;
        public const int Sense_MeatAngle = 171;
        public const int Sense_NMeats = 172;
        public const int Sense_BibiteCloseness = 180;
        public const int Sense_BibiteAngle = 181;
        public const int Sense_NBibites = 182;
        public const int Sense_BibiteRed = 183;
        public const int Sense_BibiteGreen = 184;
        public const int Sense_BibiteBlue = 185;
        public const int Sense_Tic = 190;
        public const int Sense_Minute = 191;
        public const int Sense_Phero1Intensity = 200;
        public const int Sense_Phero1Angle = 201;
        public const int Sense_Phero1Heading = 202;
        public const int Sense_Phero2Intensity = 203;
        public const int Sense_Phero2Angle = 204;
        public const int Sense_Phero2Heading = 205;
        public const int Sense_Phero3Intensity = 206;
        public const int Sense_Phero3Angle = 207;
        public const int Sense_Phero3Heading = 208;

        // Output Nodes (BEHAVIOURAL): 300-399
        public const int Output_Accelerate = 300;
        public const int Output_Rotate = 301;
        public const int Output_Herding = 302;
        public const int Output_EggProduction = 310;
        public const int Output_Want2Lay = 311;
        public const int Output_Want2Eat = 320;
        public const int Output_Digestion = 321;
        public const int Output_Grab = 322;
        public const int Output_Want2Attack = 323;
        public const int Output_Want2Grow = 330;
        public const int Output_Want2Heal = 331;
        public const int Output_ClkReset = 340;
        public const int Output_PhereOut1 = 350;
        public const int Output_PhereOut2 = 351;
        public const int Output_PhereOut3 = 352;

        // Constant nodes for module enable signals: 400-409
        public const int Constant_1 = 400;
        public const int Constant_0 = 401;

        // ============================================================
        // NODE DEFINITIONS
        // ============================================================

        private static readonly Dictionary<int, NodeDefinition> _catalogue = new Dictionary<int, NodeDefinition>();
        private static readonly List<int> _geneNodeIds = new List<int>();
        private static readonly List<int> _sensorNodeIds = new List<int>();
        private static readonly List<int> _outputNodeIds = new List<int>();
        private static bool _initialized = false;

        /// <summary>
        /// Ensures the catalogue is initialized.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            InitializeCatalogue();
            _initialized = true;
        }

        private static void InitializeCatalogue()
        {
            // ============================================================
            // GENE NODES (GENETIC affinity, Identity activation)
            // ============================================================

            // Appearance Genes
            AddGene(Gene_ColorRed, "Gene_ColorRed", 0.5f, "Red pigment (0-1)");
            AddGene(Gene_ColorGreen, "Gene_ColorGreen", 0.5f, "Green pigment (0-1)");
            AddGene(Gene_ColorBlue, "Gene_ColorBlue", 0.5f, "Blue pigment (0-1)");
            AddGene(Gene_EyeHueOffset, "Gene_EyeHueOffset", 0f, "Eye hue shift");

            // Size & Metabolism Genes
            AddGene(Gene_SizeRatio, "Gene_SizeRatio", 1f, "Adult size multiplier");
            AddGene(Gene_MetabolismSpeed, "Gene_MetabolismSpeed", 1f, "Process speed multiplier");
            AddGene(Gene_Diet, "Gene_Diet", 0f, "0=herbivore, 1=carnivore");

            // Mutation Rate Genes
            AddGene(Gene_AvgGeneMutations, "Gene_AvgGeneMutations", 1f, "Expected gene mutations per reproduction");
            AddGene(Gene_GeneMutationVariance, "Gene_GeneMutationVariance", 0.1f, "Std dev of gene mutation magnitude");
            AddGene(Gene_AvgBrainMutations, "Gene_AvgBrainMutations", 3f, "Expected brain mutations per reproduction");
            AddGene(Gene_BrainMutationVariance, "Gene_BrainMutationVariance", 0.5f, "Std dev of brain mutation magnitude");

            // Reproduction Genes
            AddGene(Gene_LayTime, "Gene_LayTime", 10f, "Seconds to produce an egg at full activation");
            AddGene(Gene_BroodTime, "Gene_BroodTime", 5f, "Affects maturity at birth");
            AddGene(Gene_HatchTime, "Gene_HatchTime", 15f, "Seconds for egg to hatch");

            // Vision Genes
            AddGene(Gene_ViewRadius, "Gene_ViewRadius", 100f, "Maximum vision distance");
            AddGene(Gene_ViewAngle, "Gene_ViewAngle", 2f, "Field of view in radians");

            // Clock Genes
            AddGene(Gene_ClockPeriod, "Gene_ClockPeriod", 1f, "Period of internal clock");

            // Pheromone Genes
            AddGene(Gene_PheromoneRadius, "Gene_PheromoneRadius", 50f, "Pheromone sensing radius");

            // Herding Genes
            AddGene(Gene_HerdSeparationWeight, "Gene_HerdSeparationWeight", 1f, "Separation force weight");
            AddGene(Gene_HerdAlignmentWeight, "Gene_HerdAlignmentWeight", 1f, "Alignment force weight");
            AddGene(Gene_HerdCohesionWeight, "Gene_HerdCohesionWeight", 1f, "Cohesion force weight");
            AddGene(Gene_HerdVelocityWeight, "Gene_HerdVelocityWeight", 1f, "Velocity matching weight");
            AddGene(Gene_HerdSeparationDistance, "Gene_HerdSeparationDistance", 15f, "Target separation distance");

            // Growth Genes
            AddGene(Gene_GrowthScaleFactor, "Gene_GrowthScaleFactor", 1f, "Growth curve scale");
            AddGene(Gene_GrowthMaturityFactor, "Gene_GrowthMaturityFactor", 1f, "Maturity influence on growth");
            AddGene(Gene_GrowthMaturityExponent, "Gene_GrowthMaturityExponent", 2f, "Growth curve shape");

            // Organ WAG Genes
            AddGene(Gene_WAG_ArmMuscles, "Gene_WAG_ArmMuscles", 1f, "Movement muscle share");
            AddGene(Gene_WAG_Stomach, "Gene_WAG_Stomach", 1f, "Stomach share");
            AddGene(Gene_WAG_EggOrgan, "Gene_WAG_EggOrgan", 1f, "Reproductive organ share");
            AddGene(Gene_WAG_FatOrgan, "Gene_WAG_FatOrgan", 1f, "Fat storage share");
            AddGene(Gene_WAG_Armor, "Gene_WAG_Armor", 1f, "Armor share");
            AddGene(Gene_WAG_Throat, "Gene_WAG_Throat", 1f, "Throat share");
            AddGene(Gene_WAG_JawMuscles, "Gene_WAG_JawMuscles", 1f, "Jaw muscle share");

            // Fat Metabolism Genes
            AddGene(Gene_FatStorageThreshold, "Gene_FatStorageThreshold", 0.7f, "Energy ratio to start storing fat");
            AddGene(Gene_FatStorageDeadband, "Gene_FatStorageDeadband", 0.1f, "Neutral band width");

            // ============================================================
            // INTERNAL SENSOR NODES (BIOLOGICAL affinity)
            // ============================================================

            AddSensor(Sense_EnergyRatio, "Sense_EnergyRatio", NodeAffinity.Biological, "Current/Max energy");
            AddSensor(Sense_LifeRatio, "Sense_LifeRatio", NodeAffinity.Biological, "Current/Max health");
            AddSensor(Sense_Fullness, "Sense_Fullness", NodeAffinity.Biological, "Stomach fullness");
            AddSensor(Sense_Maturity, "Sense_Maturity", NodeAffinity.Biological, "Growth maturity (0-1)");
            AddSensor(Sense_EggStored, "Sense_EggStored", NodeAffinity.Biological, "Number of eggs stored");
            AddSensor(Sense_FatRatio, "Sense_FatRatio", NodeAffinity.Biological, "Fat storage level");
            AddSensor(Sense_TimeAlive, "Sense_TimeAlive", NodeAffinity.Biological, "Normalized age");

            // ============================================================
            // EXTERNAL SENSOR NODES (BEHAVIOURAL affinity)
            // ============================================================

            // Movement Sensors
            AddSensor(Sense_Speed, "Sense_Speed", NodeAffinity.Behavioural, "Current forward speed");
            AddSensor(Sense_RotationSpeed, "Sense_RotationSpeed", NodeAffinity.Behavioural, "Current angular velocity");
            AddSensor(Sense_IsGrabbing, "Sense_IsGrabbing", NodeAffinity.Behavioural, "1 if grabbing, else 0");
            AddSensor(Sense_AttackedDamage, "Sense_AttackedDamage", NodeAffinity.Behavioural, "Damage taken this frame");

            // Vision - Plants
            AddSensor(Sense_PlantCloseness, "Sense_PlantCloseness", NodeAffinity.Behavioural, "Proximity to nearest plant (0-1)");
            AddSensor(Sense_PlantAngle, "Sense_PlantAngle", NodeAffinity.Behavioural, "Angle to nearest plant (-1 to 1)");
            AddSensor(Sense_NPlants, "Sense_NPlants", NodeAffinity.Behavioural, "Number of visible plants");

            // Vision - Meat
            AddSensor(Sense_MeatCloseness, "Sense_MeatCloseness", NodeAffinity.Behavioural, "Proximity to nearest meat");
            AddSensor(Sense_MeatAngle, "Sense_MeatAngle", NodeAffinity.Behavioural, "Angle to nearest meat");
            AddSensor(Sense_NMeats, "Sense_NMeats", NodeAffinity.Behavioural, "Number of visible meats");

            // Vision - Bibites
            AddSensor(Sense_BibiteCloseness, "Sense_BibiteCloseness", NodeAffinity.Behavioural, "Proximity to nearest bibite");
            AddSensor(Sense_BibiteAngle, "Sense_BibiteAngle", NodeAffinity.Behavioural, "Angle to nearest bibite");
            AddSensor(Sense_NBibites, "Sense_NBibites", NodeAffinity.Behavioural, "Number of visible bibites");
            AddSensor(Sense_BibiteRed, "Sense_BibiteRed", NodeAffinity.Behavioural, "Red color of nearest bibite");
            AddSensor(Sense_BibiteGreen, "Sense_BibiteGreen", NodeAffinity.Behavioural, "Green color of nearest bibite");
            AddSensor(Sense_BibiteBlue, "Sense_BibiteBlue", NodeAffinity.Behavioural, "Blue color of nearest bibite");

            // Clock Sensors
            AddSensor(Sense_Tic, "Sense_Tic", NodeAffinity.Behavioural, "Rapid clock pulse (0 or 1)");
            AddSensor(Sense_Minute, "Sense_Minute", NodeAffinity.Behavioural, "Slow counter (0-1 over 60s)");

            // Pheromone Sensors
            AddSensor(Sense_Phero1Intensity, "Sense_Phero1Intensity", NodeAffinity.Behavioural, "Red pheromone strength");
            AddSensor(Sense_Phero1Angle, "Sense_Phero1Angle", NodeAffinity.Behavioural, "Red pheromone direction");
            AddSensor(Sense_Phero1Heading, "Sense_Phero1Heading", NodeAffinity.Behavioural, "Red pheromone trail heading");
            AddSensor(Sense_Phero2Intensity, "Sense_Phero2Intensity", NodeAffinity.Behavioural, "Green pheromone strength");
            AddSensor(Sense_Phero2Angle, "Sense_Phero2Angle", NodeAffinity.Behavioural, "Green pheromone direction");
            AddSensor(Sense_Phero2Heading, "Sense_Phero2Heading", NodeAffinity.Behavioural, "Green pheromone trail heading");
            AddSensor(Sense_Phero3Intensity, "Sense_Phero3Intensity", NodeAffinity.Behavioural, "Blue pheromone strength");
            AddSensor(Sense_Phero3Angle, "Sense_Phero3Angle", NodeAffinity.Behavioural, "Blue pheromone direction");
            AddSensor(Sense_Phero3Heading, "Sense_Phero3Heading", NodeAffinity.Behavioural, "Blue pheromone trail heading");

            // ============================================================
            // OUTPUT NODES (BEHAVIOURAL affinity, FIXED activation functions)
            // ============================================================

            // Movement Outputs
            AddOutput(Output_Accelerate, "Output_Accelerate", ActivationFunctionType.TanH, 0.45f, "Forward/backward thrust");
            AddOutput(Output_Rotate, "Output_Rotate", ActivationFunctionType.TanH, 0f, "Turn left/right");
            AddOutput(Output_Herding, "Output_Herding", ActivationFunctionType.TanH, 0f, "Herding behaviour blend");

            // Reproduction Outputs
            AddOutput(Output_EggProduction, "Output_EggProduction", ActivationFunctionType.TanH, 0.2f, "Egg production rate");
            AddOutput(Output_Want2Lay, "Output_Want2Lay", ActivationFunctionType.Sigmoid, 0f, "Lay eggs trigger");

            // Feeding Outputs
            AddOutput(Output_Want2Eat, "Output_Want2Eat", ActivationFunctionType.TanH, 1.23f, "Swallow/vomit");
            AddOutput(Output_Digestion, "Output_Digestion", ActivationFunctionType.Sigmoid, -2.07f, "Digestion speed");
            AddOutput(Output_Grab, "Output_Grab", ActivationFunctionType.TanH, 0f, "Grab/throw");
            AddOutput(Output_Want2Attack, "Output_Want2Attack", ActivationFunctionType.Sigmoid, 0f, "Bite strength");

            // Growth/Health Outputs
            AddOutput(Output_Want2Grow, "Output_Want2Grow", ActivationFunctionType.Sigmoid, 0f, "Growth rate multiplier");
            AddOutput(Output_Want2Heal, "Output_Want2Heal", ActivationFunctionType.Sigmoid, 0f, "Healing rate");

            // Clock Output
            AddOutput(Output_ClkReset, "Output_ClkReset", ActivationFunctionType.Sigmoid, 0f, "Reset clock trigger");

            // Pheromone Outputs
            AddOutput(Output_PhereOut1, "Output_PhereOut1", ActivationFunctionType.ReLU, 0f, "Red pheromone emission");
            AddOutput(Output_PhereOut2, "Output_PhereOut2", ActivationFunctionType.ReLU, 0f, "Green pheromone emission");
            AddOutput(Output_PhereOut3, "Output_PhereOut3", ActivationFunctionType.ReLU, 0f, "Blue pheromone emission");

            // ============================================================
            // CONSTANT NODES (for module enable signals)
            // ============================================================

            AddGene(Constant_1, "Constant_1", 1f, "Always 1 - used for enabling modules");
            AddGene(Constant_0, "Constant_0", 0f, "Always 0");
        }

        private static void AddGene(int id, string name, float defaultBias, string description)
        {
            var def = new NodeDefinition(id, name, NodeCategory.Gene, NodeAffinity.Genetic,
                ActivationFunctionType.Identity, defaultBias, description);
            _catalogue[id] = def;
            _geneNodeIds.Add(id);
        }

        private static void AddSensor(int id, string name, NodeAffinity affinity, string description)
        {
            var category = affinity == NodeAffinity.Biological ? NodeCategory.SensorInternal : NodeCategory.SensorExternal;
            var def = new NodeDefinition(id, name, category, affinity,
                ActivationFunctionType.Identity, 0f, description);
            _catalogue[id] = def;
            _sensorNodeIds.Add(id);
        }

        private static void AddOutput(int id, string name, ActivationFunctionType actFunc, float defaultBias, string description)
        {
            var def = new NodeDefinition(id, name, NodeCategory.Output, NodeAffinity.Behavioural,
                actFunc, defaultBias, description);
            _catalogue[id] = def;
            _outputNodeIds.Add(id);
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Gets a node definition by its catalogue ID.
        /// </summary>
        public static NodeDefinition GetDefinition(int catalogueId)
        {
            EnsureInitialized();
            if (_catalogue.TryGetValue(catalogueId, out var def))
                return def;
            throw new System.ArgumentException($"Unknown catalogue ID: {catalogueId}");
        }

        /// <summary>
        /// Tries to get a node definition by its catalogue ID.
        /// </summary>
        public static bool TryGetDefinition(int catalogueId, out NodeDefinition definition)
        {
            EnsureInitialized();
            return _catalogue.TryGetValue(catalogueId, out definition);
        }

        /// <summary>
        /// Gets all gene node catalogue IDs.
        /// </summary>
        public static IReadOnlyList<int> GetGeneNodeIds()
        {
            EnsureInitialized();
            return _geneNodeIds;
        }

        /// <summary>
        /// Gets all sensor node catalogue IDs.
        /// </summary>
        public static IReadOnlyList<int> GetSensorNodeIds()
        {
            EnsureInitialized();
            return _sensorNodeIds;
        }

        /// <summary>
        /// Gets all output node catalogue IDs.
        /// </summary>
        public static IReadOnlyList<int> GetOutputNodeIds()
        {
            EnsureInitialized();
            return _outputNodeIds;
        }

        /// <summary>
        /// Creates a BiomeNode from a catalogue definition with default values.
        /// </summary>
        public static BiomeNode CreateNodeFromCatalogue(int catalogueId, int nodeId)
        {
            var def = GetDefinition(catalogueId);
            return BiomeNode.Create(nodeId, catalogueId, def.Affinity, def.ActivationFunction, def.DefaultBias);
        }

        /// <summary>
        /// Creates a Gene node from catalogue with a specific gene value.
        /// </summary>
        public static BiomeNode CreateGeneNode(int catalogueId, int nodeId, float geneValue)
        {
            var def = GetDefinition(catalogueId);
            if (def.Category != NodeCategory.Gene)
                throw new System.ArgumentException($"Catalogue ID {catalogueId} is not a gene node");

            return BiomeNode.CreateGene(nodeId, catalogueId, geneValue);
        }

        /// <summary>
        /// Gets the total number of nodes in the catalogue.
        /// </summary>
        public static int Count
        {
            get
            {
                EnsureInitialized();
                return _catalogue.Count;
            }
        }
    }
}
