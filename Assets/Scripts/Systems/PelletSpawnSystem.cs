using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Spawns plant pellets from free biomass
    /// </summary>
    public partial struct PelletSpawnSystem : ISystem
    {
        private Unity.Mathematics.Random _random;
        private float _spawnTimer;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
            _random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            _spawnTimer = 0f;
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            _spawnTimer += deltaTime;
            
            // Only spawn every 0.5 seconds
            if (_spawnTimer < 0.5f) return;
            _spawnTimer = 0f;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            
            // Count existing plant pellets
            int pelletCount = 0;
            float totalPelletEnergy = 0f;
            foreach (var pellet in SystemAPI.Query<RefRO<PlantPellet>>())
            {
                pelletCount++;
                totalPelletEnergy += pellet.ValueRO.Energy;
            }
            
            // Target pellet count based on world size
            int targetPellets = (int)(worldSettings.SimulationSize * worldSettings.SimulationSize * 0.001f);
            targetPellets = math.max(50, targetPellets);
            
            // Energy per pellet
            float pelletEnergy = 30f;
            
            // Spawn pellets if we have biomass and need more pellets
            float halfSize = worldSettings.SimulationSize / 2f;
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            
            int pelletsToSpawn = math.min(
                targetPellets - pelletCount, 
                (int)(worldSettings.FreeBiomass / pelletEnergy)
            );
            pelletsToSpawn = math.max(0, math.min(pelletsToSpawn, 10)); // Max 10 per tick
            
            float biomassUsed = 0f;
            
            for (int i = 0; i < pelletsToSpawn; i++)
            {
                if (worldSettings.FreeBiomass - biomassUsed < pelletEnergy) break;
                
                var entity = ecb.CreateEntity();
                
                float2 position = new float2(
                    _random.NextFloat(-halfSize, halfSize),
                    _random.NextFloat(-halfSize, halfSize)
                );
                
                ecb.AddComponent(entity, new PlantPellet { Energy = pelletEnergy });
                ecb.AddComponent(entity, new Position { Value = position });
                ecb.AddComponent(entity, new Radius { Value = 2f });
                ecb.AddSharedComponent(entity, new EntityType { Value = EntityTypeEnum.PlantPellet });
                
                biomassUsed += pelletEnergy;
            }
            
            // Update biomass
            if (biomassUsed > 0f)
            {
                var newSettings = worldSettings;
                newSettings.FreeBiomass -= biomassUsed;
                SystemAPI.SetSingleton(newSettings);
            }
        }
    }
    
    /// <summary>
    /// Updates world time and frame count
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldTimeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            var newSettings = worldSettings;
            newSettings.SimulationTime += deltaTime;
            newSettings.FrameCount++;
            SystemAPI.SetSingleton(newSettings);
        }
    }
}
