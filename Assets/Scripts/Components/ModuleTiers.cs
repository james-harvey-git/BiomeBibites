using Unity.Entities;

namespace BiomeBibites
{
    /// <summary>
    /// Tracks the tier level of each module for a bibite.
    /// Module tiers are evolved over generations through the ModuleTierUpgrade mutation.
    /// Higher tiers provide more sophisticated capabilities but cost more energy.
    /// </summary>
    public struct ModuleTiers : IComponentData
    {
        /// <summary>Vision: 1=presence, 2=direction, 3=distance, 4=color</summary>
        public byte VisionTier;
        
        /// <summary>Clock: 1=tic, 2=normalized, 3=multi-freq, 4=phase</summary>
        public byte ClockTier;
        
        /// <summary>Motor: 1=accel, 2=rotate, 3=fine, 4=efficient</summary>
        public byte MotorTier;
        
        /// <summary>Digestion: 1=hunger, 2=preference, 3=timing, 4=predictive</summary>
        public byte DigestTier;
        
        /// <summary>Combat: 1=bite, 2=jaw, 3=defense, 4=specialized</summary>
        public byte CombatTier;
        
        /// <summary>Pheromone: 1=detect, 2=emit, 3=gradient, 4=complex</summary>
        public byte PheromoneTier;
        
        /// <summary>
        /// Create default tier 1 modules
        /// </summary>
        public static ModuleTiers Default => new ModuleTiers
        {
            VisionTier = 1,
            ClockTier = 1,
            MotorTier = 1,
            DigestTier = 1,
            CombatTier = 0,     // Combat not unlocked by default
            PheromoneTier = 0   // Pheromone not unlocked by default
        };
        
        /// <summary>
        /// Calculate total energy cost of maintaining all modules
        /// </summary>
        public float GetTotalEnergyCost()
        {
            float cost = 0f;
            
            // Vision cost
            cost += VisionTier switch
            {
                1 => 0.01f,
                2 => 0.02f,
                3 => 0.04f,
                4 => 0.08f,
                _ => 0f
            };
            
            // Clock cost (cheap)
            cost += ClockTier switch
            {
                1 => 0.001f,
                2 => 0.002f,
                3 => 0.003f,
                4 => 0.005f,
                _ => 0f
            };
            
            // Motor cost
            cost += MotorTier switch
            {
                1 => 0.02f,
                2 => 0.03f,
                3 => 0.04f,
                4 => 0.03f, // Tier 4 is more efficient
                _ => 0f
            };
            
            // Digest cost
            cost += DigestTier switch
            {
                1 => 0.005f,
                2 => 0.008f,
                3 => 0.01f,
                4 => 0.015f,
                _ => 0f
            };
            
            // Combat cost (expensive)
            cost += CombatTier switch
            {
                1 => 0.03f,
                2 => 0.05f,
                3 => 0.07f,
                4 => 0.1f,
                _ => 0f
            };
            
            // Pheromone cost
            cost += PheromoneTier switch
            {
                1 => 0.01f,
                2 => 0.02f,
                3 => 0.03f,
                4 => 0.05f,
                _ => 0f
            };
            
            return cost;
        }
        
        /// <summary>
        /// Get total complexity score (for speciation/statistics)
        /// </summary>
        public int GetComplexity()
        {
            return VisionTier + ClockTier + MotorTier + DigestTier + CombatTier + PheromoneTier;
        }
        
        /// <summary>
        /// Try to upgrade a random module tier (mutation)
        /// </summary>
        public bool TryUpgradeRandom(ref Unity.Mathematics.Random random)
        {
            // Pick a random module
            int module = random.NextInt(0, 6);
            
            switch (module)
            {
                case 0:
                    if (VisionTier < 4) { VisionTier++; return true; }
                    break;
                case 1:
                    if (ClockTier < 4) { ClockTier++; return true; }
                    break;
                case 2:
                    if (MotorTier < 4) { MotorTier++; return true; }
                    break;
                case 3:
                    if (DigestTier < 4) { DigestTier++; return true; }
                    break;
                case 4:
                    if (CombatTier < 4) { CombatTier++; return true; }
                    break;
                case 5:
                    if (PheromoneTier < 4) { PheromoneTier++; return true; }
                    break;
            }
            
            return false;
        }
    }
}
