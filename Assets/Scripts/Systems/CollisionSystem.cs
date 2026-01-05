using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Handles collision detection and response between all entities.
    /// 
    /// Uses gentle position-based collision resolution:
    /// - No velocity impulses (which cause glitchy behavior)
    /// - Simple separation based on overlap
    /// - Respects simulation pause state
    /// 
    /// Collision types:
    /// - Bibite vs Bibite: Soft push apart, mass-weighted
    /// - Bibite vs Pellet: Bibites push pellets
    /// - Bibite vs Egg: Gentle push
    /// </summary>
    [UpdateAfter(typeof(EnhancedEatingSystem))]
    [BurstCompile]
    public partial struct CollisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Skip collision when paused (deltaTime = 0)
            float deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f) return;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float worldSize = worldSettings.SimulationSize;
            
            // === BIBITE VS BIBITE COLLISIONS ===
            // Collect bibite data
            var bibiteQuery = SystemAPI.QueryBuilder()
                .WithAll<BibiteTag, Position, Radius, Size>()
                .Build();
            
            int bibiteCount = bibiteQuery.CalculateEntityCount();
            if (bibiteCount < 2) return; // Need at least 2 for collision
            
            var bibiteEntities = bibiteQuery.ToEntityArray(Allocator.Temp);
            var bibitePositions = bibiteQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var bibiteRadii = bibiteQuery.ToComponentDataArray<Radius>(Allocator.Temp);
            var bibiteSizes = bibiteQuery.ToComponentDataArray<Size>(Allocator.Temp);
            
            // Accumulate position corrections
            var positionCorrections = new NativeArray<float2>(bibiteCount, Allocator.Temp);
            
            // Check all bibite pairs
            for (int i = 0; i < bibiteCount; i++)
            {
                for (int j = i + 1; j < bibiteCount; j++)
                {
                    float2 posA = bibitePositions[i].Value;
                    float2 posB = bibitePositions[j].Value;
                    
                    float2 delta = posB - posA;
                    WrapDelta(ref delta, worldSize);
                    
                    float dist = math.length(delta);
                    float minDist = bibiteRadii[i].Value + bibiteRadii[j].Value;
                    
                    // Only process if overlapping
                    if (dist >= minDist || dist < 0.001f) continue;
                    
                    // Calculate overlap
                    float overlap = minDist - dist;
                    float2 normal = delta / dist;
                    
                    // Mass-weighted separation (heavier moves less)
                    float massA = bibiteSizes[i].Ratio * bibiteSizes[i].Ratio;
                    float massB = bibiteSizes[j].Ratio * bibiteSizes[j].Ratio;
                    float totalMass = massA + massB;
                    
                    float ratioA = massB / totalMass; // A moves based on B's mass
                    float ratioB = massA / totalMass; // B moves based on A's mass
                    
                    // Gentle separation - only resolve 30% of overlap per frame
                    // This prevents glitchy bouncing
                    float separationStrength = 0.3f;
                    float2 separation = normal * overlap * separationStrength;
                    
                    positionCorrections[i] -= separation * ratioA;
                    positionCorrections[j] += separation * ratioB;
                }
            }
            
            // Apply bibite corrections
            for (int i = 0; i < bibiteCount; i++)
            {
                if (math.lengthsq(positionCorrections[i]) > 0.0001f)
                {
                    var pos = state.EntityManager.GetComponentData<Position>(bibiteEntities[i]);
                    pos.Value += positionCorrections[i];
                    WrapPosition(ref pos.Value, worldSize);
                    state.EntityManager.SetComponentData(bibiteEntities[i], pos);
                }
            }
            
            // === BIBITE VS PELLET COLLISIONS ===
            var pelletQuery = SystemAPI.QueryBuilder()
                .WithAll<Position, Radius>()
                .WithAny<PlantPellet, MeatPellet>()
                .Build();
            
            var pelletEntities = pelletQuery.ToEntityArray(Allocator.Temp);
            var pelletPositions = pelletQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var pelletRadii = pelletQuery.ToComponentDataArray<Radius>(Allocator.Temp);
            
            for (int p = 0; p < pelletEntities.Length; p++)
            {
                float2 pelletPos = pelletPositions[p].Value;
                float pelletRadius = pelletRadii[p].Value;
                float2 totalPush = float2.zero;
                
                for (int b = 0; b < bibiteCount; b++)
                {
                    float2 bibitePos = bibitePositions[b].Value;
                    float bibiteRadius = bibiteRadii[b].Value;
                    
                    float2 delta = pelletPos - bibitePos;
                    WrapDelta(ref delta, worldSize);
                    
                    float dist = math.length(delta);
                    float minDist = bibiteRadius + pelletRadius;
                    
                    if (dist >= minDist || dist < 0.001f) continue;
                    
                    float overlap = minDist - dist;
                    float2 normal = delta / dist;
                    
                    // Pellets get pushed fully by bibites (they're light)
                    totalPush += normal * overlap * 0.5f;
                }
                
                if (math.lengthsq(totalPush) > 0.0001f)
                {
                    var pos = pelletPositions[p];
                    pos.Value += totalPush;
                    WrapPosition(ref pos.Value, worldSize);
                    state.EntityManager.SetComponentData(pelletEntities[p], pos);
                }
            }
            
            // === BIBITE VS EGG COLLISIONS ===
            var eggQuery = SystemAPI.QueryBuilder()
                .WithAll<Egg, Position, Radius>()
                .Build();
            
            var eggEntities = eggQuery.ToEntityArray(Allocator.Temp);
            var eggPositions = eggQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var eggRadii = eggQuery.ToComponentDataArray<Radius>(Allocator.Temp);
            
            for (int e = 0; e < eggEntities.Length; e++)
            {
                float2 eggPos = eggPositions[e].Value;
                float eggRadius = eggRadii[e].Value;
                float2 totalPush = float2.zero;
                
                for (int b = 0; b < bibiteCount; b++)
                {
                    float2 bibitePos = bibitePositions[b].Value;
                    float bibiteRadius = bibiteRadii[b].Value;
                    
                    float2 delta = eggPos - bibitePos;
                    WrapDelta(ref delta, worldSize);
                    
                    float dist = math.length(delta);
                    float minDist = bibiteRadius + eggRadius;
                    
                    if (dist >= minDist || dist < 0.001f) continue;
                    
                    float overlap = minDist - dist;
                    float2 normal = delta / dist;
                    
                    // Eggs get pushed gently
                    totalPush += normal * overlap * 0.4f;
                }
                
                if (math.lengthsq(totalPush) > 0.0001f)
                {
                    var pos = eggPositions[e];
                    pos.Value += totalPush;
                    WrapPosition(ref pos.Value, worldSize);
                    state.EntityManager.SetComponentData(eggEntities[e], pos);
                }
            }
            
            // Cleanup
            bibiteEntities.Dispose();
            bibitePositions.Dispose();
            bibiteRadii.Dispose();
            bibiteSizes.Dispose();
            positionCorrections.Dispose();
            pelletEntities.Dispose();
            pelletPositions.Dispose();
            pelletRadii.Dispose();
            eggEntities.Dispose();
            eggPositions.Dispose();
            eggRadii.Dispose();
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
    /// Handles pellet-pellet collisions to prevent stacking.
    /// Runs infrequently for performance.
    /// </summary>
    [UpdateAfter(typeof(CollisionSystem))]
    public partial struct PelletCollisionSystem : ISystem
    {
        private int _frameSkip;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
            _frameSkip = 0;
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Only run every 8 frames
            _frameSkip++;
            if (_frameSkip % 8 != 0) return;
            
            // Skip when paused
            float deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f) return;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float worldSize = worldSettings.SimulationSize;
            
            var pelletQuery = SystemAPI.QueryBuilder()
                .WithAll<Position, Radius>()
                .WithAny<PlantPellet, MeatPellet>()
                .Build();
            
            var pelletEntities = pelletQuery.ToEntityArray(Allocator.Temp);
            var pelletPositions = pelletQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var pelletRadii = pelletQuery.ToComponentDataArray<Radius>(Allocator.Temp);
            
            var corrections = new NativeArray<float2>(pelletEntities.Length, Allocator.Temp);
            
            for (int i = 0; i < pelletEntities.Length; i++)
            {
                for (int j = i + 1; j < pelletEntities.Length; j++)
                {
                    float2 delta = pelletPositions[j].Value - pelletPositions[i].Value;
                    WrapDelta(ref delta, worldSize);
                    
                    float dist = math.length(delta);
                    float minDist = pelletRadii[i].Value + pelletRadii[j].Value;
                    
                    if (dist >= minDist || dist < 0.001f) continue;
                    
                    float overlap = minDist - dist;
                    float2 normal = delta / dist;
                    float2 separation = normal * overlap * 0.25f;
                    
                    corrections[i] -= separation;
                    corrections[j] += separation;
                }
            }
            
            for (int i = 0; i < pelletEntities.Length; i++)
            {
                if (math.lengthsq(corrections[i]) > 0.0001f)
                {
                    if (state.EntityManager.Exists(pelletEntities[i]))
                    {
                        var pos = pelletPositions[i];
                        pos.Value += corrections[i];
                        WrapPosition(ref pos.Value, worldSize);
                        state.EntityManager.SetComponentData(pelletEntities[i], pos);
                    }
                }
            }
            
            pelletEntities.Dispose();
            pelletPositions.Dispose();
            pelletRadii.Dispose();
            corrections.Dispose();
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
}
