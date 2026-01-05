using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites
{
    // ============================================================================
    // WORLD SETTINGS (Singleton)
    // ============================================================================
    
    public struct WorldSettings : IComponentData
    {
        public float SimulationSize;
        public float BiomassDensity;
        public float TotalEnergy;
        public float FreeBiomass;
        public float SimulationTime;
        public int FrameCount;
    }
    
    // ============================================================================
    // ENTITY TYPE IDENTIFICATION
    // ============================================================================
    
    public enum EntityTypeEnum : byte
    {
        Bibite = 0,
        PlantPellet = 1,
        MeatPellet = 2,
        Egg = 3,
        Pheromone = 4
    }
    
    public struct EntityType : ISharedComponentData
    {
        public EntityTypeEnum Value;
    }
    
    // ============================================================================
    // SPATIAL COMPONENTS (shared by all entities)
    // ============================================================================
    
    public struct Position : IComponentData
    {
        public float2 Value;
    }
    
    public struct Velocity : IComponentData
    {
        public float2 Value;
    }
    
    public struct Rotation : IComponentData
    {
        public float Value; // Radians
    }
    
    public struct Radius : IComponentData
    {
        public float Value;
    }
    
    // ============================================================================
    // BIBITE CORE COMPONENTS
    // ============================================================================
    
    /// <summary>
    /// Tag component to identify bibite entities
    /// </summary>
    public struct BibiteTag : IComponentData { }
    
    /// <summary>
    /// Generation counter for evolutionary tracking
    /// </summary>
    public struct Generation : IComponentData
    {
        public int Value;
    }
    
    /// <summary>
    /// Visual appearance
    /// </summary>
    public struct BibiteColor : IComponentData
    {
        public float R;
        public float G;
        public float B;
    }
    
    /// <summary>
    /// Body size ratio (1.0 = standard adult size of ~10 units)
    /// </summary>
    public struct Size : IComponentData
    {
        public float Ratio;
    }
    
    // ============================================================================
    // ENERGY & METABOLISM
    // ============================================================================
    
    public struct Energy : IComponentData
    {
        public float Current;
        public float Maximum;
        public float Metabolism; // Speed multiplier for all processes
    }
    
    public struct Health : IComponentData
    {
        public float Current;
        public float Maximum;
    }
    
    /// <summary>
    /// Diet preference: 0 = pure herbivore, 1 = pure carnivore
    /// </summary>
    public struct Diet : IComponentData
    {
        public float Value;
    }
    
    // ============================================================================
    // ORGANS (WAGG Genes equivalent)
    // ============================================================================
    
    /// <summary>
    /// Internal organ sizes that determine capabilities.
    /// Values should roughly sum to 1.0 (proportional allocation)
    /// </summary>
    public struct Organs : IComponentData
    {
        public float Armor;       // Damage resistance, adds weight
        public float Stomach;     // Digestion capacity
        public float EggOrgan;    // Clutch size, offspring maturity
        public float Throat;      // Bite/swallow size
        public float MoveMuscle;  // Movement power
        public float JawMuscle;   // Attack power
        public float FatReserve;  // Fat storage capacity
    }
    
    // ============================================================================
    // DIGESTION & FAT
    // ============================================================================
    
    public struct StomachContents : IComponentData
    {
        public float PlantMatter;
        public float MeatMatter;
        public float DigestProgress; // 0-1 progress through digestion
    }
    
    public struct FatStorage : IComponentData
    {
        public float Current;
        public float Threshold;  // Energy level to start storing fat
        public float Deadband;   // Hysteresis for fat storage/consumption
    }
    
    // ============================================================================
    // AGE & MATURITY
    // ============================================================================
    
    public struct Age : IComponentData
    {
        public float TimeAlive;  // Seconds alive
        public float Maturity;   // 0 = hatchling, 1 = full adult
    }
    
    // ============================================================================
    // REPRODUCTION
    // ============================================================================
    
    public struct ReproductionState : IComponentData
    {
        public float EggProgress;  // 0-1 egg development
        public int EggsStored;     // Number of eggs ready
        public bool ReadyToLay;
    }
    
    // ============================================================================
    // BRAIN STATE
    // ============================================================================
    
    /// <summary>
    /// Brain outputs that control behavior.
    /// These values come from the BIOME neural network.
    /// </summary>
    public struct BrainState : IComponentData
    {
        // Movement outputs (-1 to 1)
        public float AccelerateOutput;
        public float RotateOutput;
        
        // Feeding outputs (0 to 1)
        public float WantToEatOutput;
        
        // Reproduction outputs (0 to 1)
        public float WantToLayOutput;
        
        // Combat outputs (0 to 1)
        public float WantToAttackOutput;
        // Grab output (-1 to 1): positive = grab, negative = throw
        public float WantToGrabOutput;
        
        // Pheromone outputs (0 to 1)
        public float PheromoneOut1;
        public float PheromoneOut2;
        public float PheromoneOut3;
    }
    
    /// <summary>
    /// Sensory inputs that feed into the brain
    /// </summary>
    public struct SensoryInputs : IComponentData
    {
        // Internal state
        public float EnergyRatio;
        public float HealthRatio;
        public float Maturity;
        public float Fullness;
        public float Speed;
        
        // Nearest plant
        public float PlantCloseness;
        public float PlantAngle;
        public int PlantCount;
        
        // Nearest meat
        public float MeatCloseness;
        public float MeatAngle;
        public int MeatCount;
        
        // Nearest bibite
        public float BibiteCloseness;
        public float BibiteAngle;
        public int BibiteCount;
        
        // Clocks
        public float Tic;
        public float Minute;
    }
    
    // ============================================================================
    // INTERNAL CLOCKS
    // ============================================================================
    
    public struct InternalClock : IComponentData
    {
        public float Phase;      // Current phase
        public float Frequency;  // Oscillation frequency
        public float MinuteCounter; // 0-60 counter
    }
    
    // ============================================================================
    // COMBAT & INTERACTION
    // ============================================================================
    
    public struct GrabState : IComponentData
    {
        public Entity HeldEntity;
        public bool IsGrabbing;
    }
    
    public struct CombatState : IComponentData
    {
        public float DamageTakenThisFrame;
        public float AttackCooldown;
        public bool IsAttacking;
        public float2 LastTargetPosition;
    }
    
    // ============================================================================
    // PELLET COMPONENTS
    // ============================================================================
    
    public struct PlantPellet : IComponentData
    {
        public float Energy;
    }
    
    public struct MeatPellet : IComponentData
    {
        public float Energy;
        public float DecayTimer; // Time until it rots away
    }
    
    // ============================================================================
    // EGG COMPONENTS
    // ============================================================================
    
    public struct Egg : IComponentData
    {
        public float Energy;
        public float HatchProgress; // 0-1
        public float HatchTime;     // Total time to hatch
        public Entity Parent;
    }
    
    // ============================================================================
    // PHEROMONE COMPONENTS
    // ============================================================================
    
    public struct PheromoneSource : IComponentData
    {
        public float3 RGB;          // Pheromone color channels
        public float Intensity;
        public float DecayRate;
    }
    
    // ============================================================================
    // TAGS FOR SYSTEM FILTERING
    // ============================================================================
    
    public struct DeadTag : IComponentData { }
    public struct NewbornTag : IComponentData { }
}
