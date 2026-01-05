using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    // NOTE: The eating system has been replaced by EnhancedEatingSystem in DigestionSystem.cs
    // NOTE: MeatDecaySystem has been moved to DeathSystem.cs
    
    // This file is kept empty - all eating logic is now handled by:
    // - EnhancedEatingSystem (in DigestionSystem.cs) - handles eating into stomach
    // - DigestionSystem (in DigestionSystem.cs) - handles converting stomach contents to energy
}
