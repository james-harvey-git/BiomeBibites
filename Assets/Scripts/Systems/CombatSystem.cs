using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles combat between bibites - continuous biting and damage dealing.
    /// 
    /// Based on original Bibites mechanics:
    /// - WantToAttack > 0.15 triggers attack mode
    /// - While attacking and in contact, continuously drains target (like a mosquito)
    /// - Damage rate based on JawMuscle organ
    /// - Damage reduced by target's Armor organ
    /// - Attacker gains energy from successful bites (carnivore feeding)
    /// 
    /// This is CONTINUOUS damage while in contact, not discrete attacks.
    /// </summary>
    public partial class CombatSystem : SystemBase
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
            
            // Build list of potential targets
            var targetList = new NativeList<TargetData>(Allocator.Temp);
            
            foreach (var (position, radius, health, organs, energy, entity) 
                in SystemAPI.Query<
                    RefRO<Position>,
                    RefRO<Radius>,
                    RefRO<Health>,
                    RefRO<Organs>,
                    RefRO<Energy>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                targetList.Add(new TargetData
                {
                    Entity = entity,
                    Position = position.ValueRO.Value,
                    Radius = radius.ValueRO.Value,
                    Health = health.ValueRO.Current,
                    Armor = organs.ValueRO.Armor,
                    Energy = energy.ValueRO.Current
                });
            }
            
            // Process attackers - split into two queries due to component limit
            foreach (var (position, rotation, radius, brain, organs, diet, entity) 
                in SystemAPI.Query<
                    RefRO<Position>,
                    RefRO<Rotation>,
                    RefRO<Radius>,
                    RefRO<BrainState>,
                    RefRO<Organs>,
                    RefRO<Diet>>()
                .WithAll<BibiteTag, CombatState, Energy>()
                .WithEntityAccess())
            {
                // Get mutable components separately
                var combat = EntityManager.GetComponentData<CombatState>(entity);
                var energy = EntityManager.GetComponentData<Energy>(entity);
                
                // Lower threshold for attacking (matches original Bibites ~0.15)
                if (brain.ValueRO.WantToAttackOutput < 0.15f)
                {
                    combat.IsAttacking = false;
                    EntityManager.SetComponentData(entity, combat);
                    continue;
                }
                
                // Calculate bite range based on Throat organ (contact + small reach)
                float biteRange = radius.ValueRO.Value + 2f + organs.ValueRO.Throat * 8f;
                
                // Calculate damage RATE based on JawMuscle (damage per second)
                float baseDamageRate = 3f + organs.ValueRO.JawMuscle * 12f; // 3-15 damage/second
                
                // Scale by attack intensity
                baseDamageRate *= brain.ValueRO.WantToAttackOutput;
                
                float2 attackerPos = position.ValueRO.Value;
                float2 forward = new float2(
                    math.cos(rotation.ValueRO.Value),
                    math.sin(rotation.ValueRO.Value)
                );
                
                // Find best target in front
                Entity bestTarget = Entity.Null;
                float bestDist = float.MaxValue;
                TargetData bestTargetData = default;
                
                for (int i = 0; i < targetList.Length; i++)
                {
                    var target = targetList[i];
                    if (target.Entity == entity) continue; // Can't attack self
                    
                    float2 delta = target.Position - attackerPos;
                    WrapDelta(ref delta, worldSize);
                    float dist = math.length(delta);
                    
                    // Check if in bite range (including target's radius)
                    if (dist > biteRange + target.Radius) continue;
                    
                    // Check if target is roughly in front (wider cone for biting)
                    if (dist > 0.1f)
                    {
                        float2 toTarget = delta / dist;
                        float dot = math.dot(forward, toTarget);
                        if (dot < 0.0f) continue; // Must be in front hemisphere
                    }
                    
                    // Prefer closest target
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = target.Entity;
                        bestTargetData = target;
                    }
                }
                
                // Attack if target found and in range
                if (bestTarget != Entity.Null && EntityManager.Exists(bestTarget))
                {
                    // Calculate actual damage rate (reduced by armor)
                    float armorReduction = bestTargetData.Armor * 0.7f; // Up to 70% reduction
                    float actualDamageRate = baseDamageRate * (1f - armorReduction);
                    
                    // Apply damage over time
                    float damageThisFrame = actualDamageRate * deltaTime;
                    
                    var targetHealth = EntityManager.GetComponentData<Health>(bestTarget);
                    targetHealth.Current -= damageThisFrame;
                    EntityManager.SetComponentData(bestTarget, targetHealth);
                    
                    // Mark that we're attacking (for visual feedback)
                    combat.IsAttacking = true;
                    combat.LastTargetPosition = bestTargetData.Position;
                    
                    // Mark damage on target
                    if (EntityManager.HasComponent<CombatState>(bestTarget))
                    {
                        var targetCombat = EntityManager.GetComponentData<CombatState>(bestTarget);
                        targetCombat.DamageTakenThisFrame += damageThisFrame;
                        EntityManager.SetComponentData(bestTarget, targetCombat);
                    }
                    
                    // Attacker gains energy from bite (carnivore feeding)
                    // Carnivores (diet=1) get full benefit, herbivores (diet=0) get little
                    float energyGainRate = actualDamageRate * 0.4f * diet.ValueRO.Value;
                    energy.Current += energyGainRate * deltaTime;
                    energy.Current = math.min(energy.Current, energy.Maximum);
                    
                    // Attack costs energy (smaller continuous cost)
                    float attackCostRate = 1f + organs.ValueRO.JawMuscle * 2f;
                    energy.Current -= attackCostRate * deltaTime;
                }
                else
                {
                    combat.IsAttacking = false;
                }
                
                // Write back mutable components
                EntityManager.SetComponentData(entity, combat);
                EntityManager.SetComponentData(entity, energy);
            }
            
            targetList.Dispose();
        }
        
        private void WrapDelta(ref float2 delta, float worldSize)
        {
            float halfSize = worldSize / 2f;
            if (delta.x > halfSize) delta.x -= worldSize;
            else if (delta.x < -halfSize) delta.x += worldSize;
            if (delta.y > halfSize) delta.y -= worldSize;
            else if (delta.y < -halfSize) delta.y += worldSize;
        }
        
        private struct TargetData
        {
            public Entity Entity;
            public float2 Position;
            public float Radius;
            public float Health;
            public float Armor;
            public float Energy;
        }
    }
    
    /// <summary>
    /// Initializes CombatState on bibites that don't have it
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CombatInitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (tag, entity) 
                in SystemAPI.Query<RefRO<BibiteTag>>()
                .WithNone<CombatState>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new CombatState
                {
                    DamageTakenThisFrame = 0f,
                    AttackCooldown = 0f,
                    IsAttacking = false,
                    LastTargetPosition = float2.zero
                });
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Resets per-frame combat state
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct CombatResetSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var combat in SystemAPI.Query<RefRW<CombatState>>())
            {
                combat.ValueRW.DamageTakenThisFrame = 0f;
                // Don't reset IsAttacking - CombatSystem will set it
            }
        }
    }
}
