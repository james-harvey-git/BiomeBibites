using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;

namespace BiomeBibites.Systems
{
    /// <summary>
    /// Pheromone grid component - stores the 3-channel pheromone field.
    /// Each cell has RGB values representing 3 distinct pheromone types.
    /// </summary>
    public struct PheromoneGrid : IComponentData
    {
        public int Resolution;           // Grid cells per axis
        public float CellSize;           // World units per cell
        public float WorldSize;          // Total world size
        public float DiffusionRate;      // How fast pheromones spread (0-1)
        public float DecayRate;          // How fast pheromones fade (per second)
        public float EmissionStrength;   // Multiplier for emission
        public BlobAssetReference<PheromoneGridData> GridData;
    }
    
    /// <summary>
    /// Blob asset for pheromone data - allows Burst compilation
    /// </summary>
    public struct PheromoneGridData
    {
        // Flat arrays for each channel: index = y * resolution + x
        public BlobArray<float> Channel1; // Red pheromone
        public BlobArray<float> Channel2; // Green pheromone
        public BlobArray<float> Channel3; // Blue pheromone
    }
    
    /// <summary>
    /// Component for entities that emit pheromones
    /// </summary>
    public struct PheromoneEmitter : IComponentData
    {
        public float Emission1; // Output from PhereOut1 neuron (0-1)
        public float Emission2; // Output from PhereOut2 neuron (0-1)
        public float Emission3; // Output from PhereOut3 neuron (0-1)
    }
    
    /// <summary>
    /// Component for entities that sense pheromones
    /// </summary>
    public struct PheromoneSensor : IComponentData
    {
        // Sensed values for each channel
        public float Intensity1;
        public float Intensity2;
        public float Intensity3;
        
        // Gradient direction (angle relative to bibite heading)
        public float Angle1;
        public float Angle2;
        public float Angle3;
        
        // Gradient heading (which way pheromone is increasing)
        public float Heading1;
        public float Heading2;
        public float Heading3;
    }
    
    /// <summary>
    /// System that initializes the pheromone grid
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PheromoneGridInitSystem : SystemBase
    {
        private bool _initialized = false;
        
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
        }
        
        protected override void OnUpdate()
        {
            if (_initialized) return;
            
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            
            // Create pheromone grid entity
            int resolution = 64; // 64x64 grid
            float cellSize = worldSettings.SimulationSize / resolution;
            
            var gridEntity = EntityManager.CreateEntity();
            EntityManager.SetName(gridEntity, "PheromoneGrid");
            
            // Create blob asset for grid data
            using (var builder = new BlobBuilder(Allocator.Temp))
            {
                ref var root = ref builder.ConstructRoot<PheromoneGridData>();
                
                int cellCount = resolution * resolution;
                var channel1 = builder.Allocate(ref root.Channel1, cellCount);
                var channel2 = builder.Allocate(ref root.Channel2, cellCount);
                var channel3 = builder.Allocate(ref root.Channel3, cellCount);
                
                // Initialize to zero
                for (int i = 0; i < cellCount; i++)
                {
                    channel1[i] = 0f;
                    channel2[i] = 0f;
                    channel3[i] = 0f;
                }
                
                var blobAsset = builder.CreateBlobAssetReference<PheromoneGridData>(Allocator.Persistent);
                
                EntityManager.AddComponentData(gridEntity, new PheromoneGrid
                {
                    Resolution = resolution,
                    CellSize = cellSize,
                    WorldSize = worldSettings.SimulationSize,
                    DiffusionRate = 0.1f,
                    DecayRate = 0.05f,
                    EmissionStrength = 1f,
                    GridData = blobAsset
                });
            }
            
            _initialized = true;
            UnityEngine.Debug.Log($"[BIOME] Pheromone grid initialized: {resolution}x{resolution}, cell size: {cellSize:F1}");
        }
        
        protected override void OnDestroy()
        {
            // Clean up blob asset
            foreach (var grid in SystemAPI.Query<RefRO<PheromoneGrid>>())
            {
                if (grid.ValueRO.GridData.IsCreated)
                {
                    grid.ValueRO.GridData.Dispose();
                }
            }
        }
    }
    
    // ========================================================================
    // SYSTEM ORDER:
    // 1. PheromoneSensingSystem - Reads current grid state for bibites
    // 2. SensoryInputSystem - Uses pheromone data to populate brain inputs
    // 3. BiomeBrainProcessingSystem - Processes brain, outputs pheromone emission
    // 4. PheromoneEmissionSystem - Adds emissions to grid
    // 5. PheromoneDiffusionSystem - Spreads and decays pheromones
    // ========================================================================
    
    /// <summary>
    /// System that senses pheromones for each bibite.
    /// Runs FIRST in the pheromone cycle - reads the grid state from last frame.
    /// </summary>
    [UpdateBefore(typeof(SensoryInputSystem))]
    public partial class PheromoneSensingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<PheromoneGrid>(out var grid))
                return;
            
            ref var gridData = ref grid.GridData.Value;
            int resolution = grid.Resolution;
            float halfWorld = grid.WorldSize / 2f;
            float cellSize = grid.CellSize;
            
            // Update pheromone sensors for each bibite
            foreach (var (sensor, position, rotation) 
                in SystemAPI.Query<RefRW<PheromoneSensor>, RefRO<Position>, RefRO<Rotation>>()
                .WithAll<BibiteTag>())
            {
                float2 pos = position.ValueRO.Value;
                float heading = rotation.ValueRO.Value;
                
                // Get current cell
                int cellX = (int)((pos.x + halfWorld) / cellSize);
                int cellY = (int)((pos.y + halfWorld) / cellSize);
                cellX = math.clamp(cellX, 0, resolution - 1);
                cellY = math.clamp(cellY, 0, resolution - 1);
                int cellIdx = cellY * resolution + cellX;
                
                // Current intensity
                sensor.ValueRW.Intensity1 = gridData.Channel1[cellIdx];
                sensor.ValueRW.Intensity2 = gridData.Channel2[cellIdx];
                sensor.ValueRW.Intensity3 = gridData.Channel3[cellIdx];
                
                // Calculate gradient for each channel
                CalculateGradient(ref gridData.Channel1, cellX, cellY, resolution, heading,
                    out sensor.ValueRW.Angle1, out sensor.ValueRW.Heading1);
                CalculateGradient(ref gridData.Channel2, cellX, cellY, resolution, heading,
                    out sensor.ValueRW.Angle2, out sensor.ValueRW.Heading2);
                CalculateGradient(ref gridData.Channel3, cellX, cellY, resolution, heading,
                    out sensor.ValueRW.Angle3, out sensor.ValueRW.Heading3);
            }
        }
        
        private void CalculateGradient(ref BlobArray<float> channel, int cellX, int cellY, 
            int resolution, float heading, out float angle, out float gradHeading)
        {
            // Sample in cardinal directions
            int left = cellY * resolution + ((cellX - 1 + resolution) % resolution);
            int right = cellY * resolution + ((cellX + 1) % resolution);
            int up = ((cellY + 1) % resolution) * resolution + cellX;
            int down = ((cellY - 1 + resolution) % resolution) * resolution + cellX;
            
            // Calculate gradient vector
            float gradX = channel[right] - channel[left];
            float gradY = channel[up] - channel[down];
            
            // Gradient heading (world space)
            gradHeading = math.atan2(gradY, gradX);
            
            // Angle relative to bibite heading
            float relAngle = gradHeading - heading;
            
            // Normalize to -π to π
            while (relAngle > math.PI) relAngle -= 2f * math.PI;
            while (relAngle < -math.PI) relAngle += 2f * math.PI;
            
            // Normalize to -1 to 1
            angle = relAngle / math.PI;
        }
    }
    
    /// <summary>
    /// System that handles pheromone emission from bibites.
    /// Runs AFTER brain processing to use the emission outputs.
    /// </summary>
    [UpdateAfter(typeof(BiomeBrainProcessingSystem))]
    [UpdateBefore(typeof(PheromoneDiffusionSystem))]
    public partial class PheromoneEmissionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Get pheromone grid
            if (!SystemAPI.TryGetSingletonRW<PheromoneGrid>(out var gridRW))
                return;
            
            ref var grid = ref gridRW.ValueRW;
            ref var gridData = ref grid.GridData.Value;
            
            float halfWorld = grid.WorldSize / 2f;
            float emissionStr = grid.EmissionStrength;
            
            // Process each emitting bibite
            foreach (var (emitter, position, brainState) 
                in SystemAPI.Query<RefRO<PheromoneEmitter>, RefRO<Position>, RefRO<BrainState>>()
                .WithAll<BibiteTag>())
            {
                // Get grid cell
                float2 pos = position.ValueRO.Value;
                int cellX = (int)((pos.x + halfWorld) / grid.CellSize);
                int cellY = (int)((pos.y + halfWorld) / grid.CellSize);
                
                // Clamp to grid bounds
                cellX = math.clamp(cellX, 0, grid.Resolution - 1);
                cellY = math.clamp(cellY, 0, grid.Resolution - 1);
                
                int cellIdx = cellY * grid.Resolution + cellX;
                
                // Emit pheromones based on brain outputs
                float emit1 = brainState.ValueRO.PheromoneOut1 * emissionStr;
                float emit2 = brainState.ValueRO.PheromoneOut2 * emissionStr;
                float emit3 = brainState.ValueRO.PheromoneOut3 * emissionStr;
                
                // Add to grid (clamped to max value)
                gridData.Channel1[cellIdx] = math.min(1f, gridData.Channel1[cellIdx] + emit1 * 0.1f);
                gridData.Channel2[cellIdx] = math.min(1f, gridData.Channel2[cellIdx] + emit2 * 0.1f);
                gridData.Channel3[cellIdx] = math.min(1f, gridData.Channel3[cellIdx] + emit3 * 0.1f);
            }
        }
    }
    
    /// <summary>
    /// System that handles pheromone diffusion and decay.
    /// Runs LAST - after emission, spreads pheromones for next frame.
    /// </summary>
    [UpdateAfter(typeof(PheromoneEmissionSystem))]
    public partial class PheromoneDiffusionSystem : SystemBase
    {
        private NativeArray<float> _tempChannel1;
        private NativeArray<float> _tempChannel2;
        private NativeArray<float> _tempChannel3;
        
        protected override void OnDestroy()
        {
            if (_tempChannel1.IsCreated) _tempChannel1.Dispose();
            if (_tempChannel2.IsCreated) _tempChannel2.Dispose();
            if (_tempChannel3.IsCreated) _tempChannel3.Dispose();
        }
        
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonRW<PheromoneGrid>(out var gridRW))
                return;
            
            ref var grid = ref gridRW.ValueRW;
            ref var gridData = ref grid.GridData.Value;
            
            int resolution = grid.Resolution;
            int cellCount = resolution * resolution;
            float dt = SystemAPI.Time.DeltaTime;
            float diffusion = grid.DiffusionRate;
            float decay = grid.DecayRate * dt;
            
            // Allocate temp arrays if needed
            if (!_tempChannel1.IsCreated || _tempChannel1.Length != cellCount)
            {
                if (_tempChannel1.IsCreated) _tempChannel1.Dispose();
                if (_tempChannel2.IsCreated) _tempChannel2.Dispose();
                if (_tempChannel3.IsCreated) _tempChannel3.Dispose();
                
                _tempChannel1 = new NativeArray<float>(cellCount, Allocator.Persistent);
                _tempChannel2 = new NativeArray<float>(cellCount, Allocator.Persistent);
                _tempChannel3 = new NativeArray<float>(cellCount, Allocator.Persistent);
            }
            
            // Copy current state
            for (int i = 0; i < cellCount; i++)
            {
                _tempChannel1[i] = gridData.Channel1[i];
                _tempChannel2[i] = gridData.Channel2[i];
                _tempChannel3[i] = gridData.Channel3[i];
            }
            
            // Apply diffusion and decay
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int idx = y * resolution + x;
                    
                    // Get neighbors (with wrapping)
                    int left = y * resolution + ((x - 1 + resolution) % resolution);
                    int right = y * resolution + ((x + 1) % resolution);
                    int up = ((y + 1) % resolution) * resolution + x;
                    int down = ((y - 1 + resolution) % resolution) * resolution + x;
                    
                    // Diffusion: average of neighbors
                    float neighborAvg1 = (_tempChannel1[left] + _tempChannel1[right] + 
                                          _tempChannel1[up] + _tempChannel1[down]) * 0.25f;
                    float neighborAvg2 = (_tempChannel2[left] + _tempChannel2[right] + 
                                          _tempChannel2[up] + _tempChannel2[down]) * 0.25f;
                    float neighborAvg3 = (_tempChannel3[left] + _tempChannel3[right] + 
                                          _tempChannel3[up] + _tempChannel3[down]) * 0.25f;
                    
                    // Blend with neighbors and apply decay
                    float val1 = math.lerp(_tempChannel1[idx], neighborAvg1, diffusion);
                    float val2 = math.lerp(_tempChannel2[idx], neighborAvg2, diffusion);
                    float val3 = math.lerp(_tempChannel3[idx], neighborAvg3, diffusion);
                    
                    // Decay
                    gridData.Channel1[idx] = math.max(0, val1 - decay);
                    gridData.Channel2[idx] = math.max(0, val2 - decay);
                    gridData.Channel3[idx] = math.max(0, val3 - decay);
                }
            }
        }
    }
    
    /// <summary>
    /// Add pheromone components to bibites that don't have them
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PheromoneComponentInitSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (tag, entity) in SystemAPI.Query<RefRO<BibiteTag>>()
                .WithNone<PheromoneEmitter>()
                .WithEntityAccess())
            {
                ecb.AddComponent(entity, new PheromoneEmitter());
                ecb.AddComponent(entity, new PheromoneSensor());
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
