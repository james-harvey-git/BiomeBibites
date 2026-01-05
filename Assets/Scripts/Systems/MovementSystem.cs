using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles movement of all bibites based on their brain outputs.
    /// 
    /// Physics model:
    /// - Bibites can only accelerate forward/backward (along facing direction)
    /// - Sideways drag is very high (prevents drifting sideways)
    /// - Forward drag is lower (allows coasting)
    /// - This creates realistic movement where bibites must turn to change direction
    /// 
    /// IMPORTANT: Moving backward has a significant efficiency penalty!
    /// - Forward movement: normal energy cost
    /// - Backward movement: 2.5x energy cost (inefficient)
    /// </summary>
    [BurstCompile]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            float halfSize = worldSettings.SimulationSize / 2f;

            // Process all bibites
            foreach (var (position, velocity, rotation, brain, organs, energy, size) 
                in SystemAPI.Query<
                    RefRW<Position>,
                    RefRW<Velocity>,
                    RefRW<Rotation>,
                    RefRO<BrainState>,
                    RefRO<Organs>,
                    RefRW<Energy>,
                    RefRO<Size>>()
                .WithAll<BibiteTag>())
            {
                // Get movement parameters
                float movePower = organs.ValueRO.MoveMuscle * 50f; // Base movement force
                float metabolism = energy.ValueRO.Metabolism;
                float sizeMultiplier = size.ValueRO.Ratio;
                
                // Rotation based on brain output
                float rotationSpeed = 3f * metabolism;
                rotation.ValueRW.Value += brain.ValueRO.RotateOutput * rotationSpeed * deltaTime;
                
                // Keep rotation in 0-2π range
                rotation.ValueRW.Value = math.fmod(rotation.ValueRW.Value + math.PI * 2f, math.PI * 2f);
                
                // Calculate facing direction vectors
                float2 forward = new float2(
                    math.cos(rotation.ValueRO.Value),
                    math.sin(rotation.ValueRO.Value)
                );
                float2 right = new float2(-forward.y, forward.x); // Perpendicular to forward
                
                // Get acceleration input
                float accelInput = brain.ValueRO.AccelerateOutput; // -1 to 1
                
                // === BACKWARD MOVEMENT PENALTY ===
                float efficiencyMultiplier = 1f;
                float speedMultiplier = 1f;
                
                if (accelInput < 0f)
                {
                    // Backward movement:
                    // - 2.5x energy cost (very inefficient)
                    // - 0.5x speed (harder to move backward)
                    efficiencyMultiplier = 2.5f;
                    speedMultiplier = 0.5f;
                }
                
                // Apply speed multiplier to backward movement
                float effectiveAccel = accelInput;
                if (accelInput < 0f)
                {
                    effectiveAccel *= speedMultiplier;
                }
                
                // Calculate acceleration (only in forward direction!)
                float accelerationMagnitude = effectiveAccel * movePower * metabolism / sizeMultiplier;
                float2 acceleration = forward * accelerationMagnitude;
                
                // Apply acceleration to velocity
                velocity.ValueRW.Value += acceleration * deltaTime;
                
                // === DIRECTIONAL DRAG ===
                // Decompose velocity into forward and sideways components
                float forwardSpeed = math.dot(velocity.ValueRO.Value, forward);
                float sidewaysSpeed = math.dot(velocity.ValueRO.Value, right);
                
                // Apply different drag to each component
                float forwardDrag = 0.95f;   // Low drag - can coast forward
                float sidewaysDrag = 0.7f;   // High drag - quickly stops sideways movement
                
                forwardSpeed *= forwardDrag;
                sidewaysSpeed *= sidewaysDrag;
                
                // Reconstruct velocity from components
                velocity.ValueRW.Value = forward * forwardSpeed + right * sidewaysSpeed;
                
                // Clamp max speed
                float maxSpeed = 30f * metabolism;
                float speed = math.length(velocity.ValueRO.Value);
                if (speed > maxSpeed)
                {
                    velocity.ValueRW.Value = math.normalize(velocity.ValueRO.Value) * maxSpeed;
                }
                
                // === MOVEMENT ENERGY COST ===
                float movementEnergyCost = math.abs(accelInput) * 0.05f * efficiencyMultiplier * metabolism * deltaTime;
                energy.ValueRW.Current -= movementEnergyCost;
                
                // Update position
                position.ValueRW.Value += velocity.ValueRO.Value * deltaTime;
                
                // World boundary wrapping (toroidal world)
                if (position.ValueRO.Value.x > halfSize)
                    position.ValueRW.Value.x -= worldSettings.SimulationSize;
                else if (position.ValueRO.Value.x < -halfSize)
                    position.ValueRW.Value.x += worldSettings.SimulationSize;
                    
                if (position.ValueRO.Value.y > halfSize)
                    position.ValueRW.Value.y -= worldSettings.SimulationSize;
                else if (position.ValueRO.Value.y < -halfSize)
                    position.ValueRW.Value.y += worldSettings.SimulationSize;
            }
        }
    }
}
