using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles grabbing and throwing mechanics matching original Bibites.
    /// 
    /// Based on original Bibites "Grab" neuron:
    /// - Output range: -1 to 1 (TanH)
    /// - Positive activation = grab objects entering mouth
    /// - Negative activation = throw grabbed objects
    /// - |activation| less than 0.15 = no grab/throw action
    /// - Stronger activation = tighter hold OR more violent throw
    /// - Force distributed across all held objects
    /// 
    /// This uses a SINGLE output neuron (WantToGrab) with range -1 to 1,
    /// NOT separate grab and throw outputs.
    /// </summary>
    public partial class GrabSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f) return; // Paused
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float worldSize = worldSettings.SimulationSize;
            
            // Process grabbers
            foreach (var (position, rotation, radius, brain, organs, grab, energy, entity) 
                in SystemAPI.Query<
                    RefRO<Position>,
                    RefRO<Rotation>,
                    RefRO<Radius>,
                    RefRO<BrainState>,
                    RefRO<Organs>,
                    RefRW<GrabState>,
                    RefRW<Energy>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                float2 grabberPos = position.ValueRO.Value;
                float grabberRot = rotation.ValueRO.Value;
                float grabberRadius = radius.ValueRO.Value;
                
                // Get grab activation (-1 to 1)
                // Positive = grab, Negative = throw
                float grabActivation = brain.ValueRO.WantToGrabOutput;
                float absActivation = math.abs(grabActivation);
                
                // Threshold: |activation| < 0.15 = no action
                bool wantsToGrab = grabActivation >= 0.15f;
                bool wantsToThrow = grabActivation <= -0.15f;
                bool noAction = absActivation < 0.15f;
                
                // If currently holding something
                if (grab.ValueRO.IsGrabbing && EntityManager.Exists(grab.ValueRO.HeldEntity))
                {
                    Entity heldEntity = grab.ValueRO.HeldEntity;
                    
                    // THROW: Negative activation throws
                    if (wantsToThrow)
                    {
                        // Throw force scales with activation strength and MoveMuscle
                        float throwStrength = math.abs(grabActivation); // 0.15 to 1.0
                        float throwForce = (15f + organs.ValueRO.MoveMuscle * 35f) * throwStrength; // 15-50 * strength
                        
                        float2 throwDir = new float2(
                            math.cos(grabberRot),
                            math.sin(grabberRot)
                        );
                        
                        // Apply velocity to thrown entity
                        if (EntityManager.HasComponent<Velocity>(heldEntity))
                        {
                            var vel = EntityManager.GetComponentData<Velocity>(heldEntity);
                            vel.Value = throwDir * throwForce;
                            EntityManager.SetComponentData(heldEntity, vel);
                        }
                        
                        // Release grab
                        grab.ValueRW.IsGrabbing = false;
                        grab.ValueRW.HeldEntity = Entity.Null;
                        
                        // Throwing costs energy (scales with force)
                        energy.ValueRW.Current -= (2f + organs.ValueRO.MoveMuscle * 2f) * throwStrength;
                        
                        continue;
                    }
                    
                    // RELEASE: No action (below threshold) releases gently
                    if (noAction)
                    {
                        grab.ValueRW.IsGrabbing = false;
                        grab.ValueRW.HeldEntity = Entity.Null;
                        continue;
                    }
                    
                    // HOLD: Positive activation keeps holding
                    // Stronger activation = tighter hold (entity held closer)
                    float holdTightness = grabActivation; // 0.15 to 1.0
                    float holdDistance = grabberRadius + 2f + organs.ValueRO.Throat * 4f * (1f - holdTightness * 0.5f);
                    
                    float2 holdOffset = new float2(
                        math.cos(grabberRot),
                        math.sin(grabberRot)
                    ) * holdDistance;
                    
                    float2 holdPos = grabberPos + holdOffset;
                    WrapPosition(ref holdPos, worldSize);
                    
                    if (EntityManager.HasComponent<Position>(heldEntity))
                    {
                        EntityManager.SetComponentData(heldEntity, new Position { Value = holdPos });
                    }
                    
                    // Zero velocity of held entity
                    if (EntityManager.HasComponent<Velocity>(heldEntity))
                    {
                        EntityManager.SetComponentData(heldEntity, new Velocity { Value = float2.zero });
                    }
                    
                    // Holding costs energy (small continuous drain, scales with tightness)
                    energy.ValueRW.Current -= 0.3f * holdTightness * deltaTime;
                }
                else
                {
                    // NOT HOLDING - check if we want to grab something new
                    if (!wantsToGrab) continue;
                    
                    // Grab range based on Throat (objects entering mouth)
                    float grabRange = grabberRadius + 2f + organs.ValueRO.Throat * 6f;
                    
                    float2 forward = new float2(
                        math.cos(grabberRot),
                        math.sin(grabberRot)
                    );
                    
                    // Find nearest entity in mouth area (front cone)
                    Entity bestTarget = Entity.Null;
                    float bestDist = grabRange;
                    
                    // Check bibites
                    foreach (var (targetPos, targetRadius, targetEntity) 
                        in SystemAPI.Query<RefRO<Position>, RefRO<Radius>>()
                        .WithAll<BibiteTag>()
                        .WithEntityAccess())
                    {
                        if (targetEntity == entity) continue;
                        
                        float2 delta = targetPos.ValueRO.Value - grabberPos;
                        WrapDelta(ref delta, worldSize);
                        float dist = math.length(delta);
                        
                        // Contact distance
                        float contactDist = grabberRadius + targetRadius.ValueRO.Value;
                        if (dist > grabRange + targetRadius.ValueRO.Value) continue;
                        if (dist < 0.1f) continue;
                        
                        // Check if in front (in mouth area)
                        float dot = math.dot(forward, delta / dist);
                        if (dot < 0.3f) continue; // Must be in front
                        
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = targetEntity;
                        }
                    }
                    
                    // Check pellets
                    foreach (var (targetPos, targetRadius, targetEntity) 
                        in SystemAPI.Query<RefRO<Position>, RefRO<Radius>>()
                        .WithAny<PlantPellet, MeatPellet>()
                        .WithEntityAccess())
                    {
                        float2 delta = targetPos.ValueRO.Value - grabberPos;
                        WrapDelta(ref delta, worldSize);
                        float dist = math.length(delta);
                        
                        if (dist > grabRange + targetRadius.ValueRO.Value) continue;
                        if (dist < 0.1f) continue;
                        
                        float dot = math.dot(forward, delta / dist);
                        if (dot < 0.3f) continue;
                        
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = targetEntity;
                        }
                    }
                    
                    // Check eggs
                    foreach (var (targetPos, targetRadius, targetEntity) 
                        in SystemAPI.Query<RefRO<Position>, RefRO<Radius>>()
                        .WithAll<Egg>()
                        .WithEntityAccess())
                    {
                        float2 delta = targetPos.ValueRO.Value - grabberPos;
                        WrapDelta(ref delta, worldSize);
                        float dist = math.length(delta);
                        
                        if (dist > grabRange + targetRadius.ValueRO.Value) continue;
                        if (dist < 0.1f) continue;
                        
                        float dot = math.dot(forward, delta / dist);
                        if (dot < 0.3f) continue;
                        
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = targetEntity;
                        }
                    }
                    
                    // Grab if target found
                    if (bestTarget != Entity.Null)
                    {
                        grab.ValueRW.IsGrabbing = true;
                        grab.ValueRW.HeldEntity = bestTarget;
                        
                        // Initial grab costs energy
                        energy.ValueRW.Current -= 1f;
                    }
                }
            }
        }
        
        private void WrapDelta(ref float2 delta, float worldSize)
        {
            float halfSize = worldSize / 2f;
            if (delta.x > halfSize) delta.x -= worldSize;
            else if (delta.x < -halfSize) delta.x += worldSize;
            if (delta.y > halfSize) delta.y -= worldSize;
            else if (delta.y < -halfSize) delta.y += worldSize;
        }
        
        private void WrapPosition(ref float2 pos, float worldSize)
        {
            float halfSize = worldSize / 2f;
            if (pos.x > halfSize) pos.x -= worldSize;
            else if (pos.x < -halfSize) pos.x += worldSize;
            if (pos.y > halfSize) pos.y -= worldSize;
            else if (pos.y < -halfSize) pos.y += worldSize;
        }
    }
    
    /// <summary>
    /// Initializes GrabState on bibites that don't have it
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GrabInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (tag, entity) 
                in SystemAPI.Query<RefRO<BibiteTag>>()
                .WithNone<GrabState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new GrabState
                {
                    HeldEntity = Entity.Null,
                    IsGrabbing = false
                });
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Cleans up grab states when held entities are destroyed
    /// </summary>
    public partial class GrabCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var grab in SystemAPI.Query<RefRW<GrabState>>().WithAll<BibiteTag>())
            {
                if (grab.ValueRO.IsGrabbing && !EntityManager.Exists(grab.ValueRO.HeldEntity))
                {
                    grab.ValueRW.IsGrabbing = false;
                    grab.ValueRW.HeldEntity = Entity.Null;
                }
            }
        }
    }
}
