using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using BiomeBibites.BIOME;

namespace BiomeBibites.Systems
{
    // ============================================================================
    // BIOME BRAIN COMPONENT
    // 
    // This is now much simpler - it just holds a reference to the unified BiomeBrain.
    // All the module/node management is inside BiomeBrain itself.
    // ============================================================================
    
    /// <summary>
    /// Component that holds a bibite's BIOME brain.
    /// The brain contains all modules, nodes, and connections in a single unified structure.
    /// </summary>
    public class BiomeBrainComponent : IComponentData
    {
        public BiomeBrain Brain;
        
        public BiomeBrainComponent()
        {
            Brain = null;
        }
    }
    
    // ============================================================================
    // SENSORY INPUT SYSTEM
    // 
    // Populates all Input module output nodes with data from the environment.
    // This is the ONLY place where sensor data enters the brain.
    // ============================================================================
    
    /// <summary>
    /// System that populates sensory inputs for all bibites.
    /// Reads from ECS components and writes to Input module output nodes.
    /// </summary>
    [UpdateBefore(typeof(BiomeBrainProcessingSystem))]
    public partial class SensoryInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
        }
        
        protected override void OnUpdate()
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            float visionRange = 100f;
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Get all plant positions for vision
            var plantQuery = EntityManager.CreateEntityQuery(typeof(PlantPellet), typeof(Position));
            var plantPositions = plantQuery.ToComponentDataArray<Position>(Allocator.Temp);
            
            // Get all meat positions
            var meatQuery = EntityManager.CreateEntityQuery(typeof(MeatPellet), typeof(Position));
            var meatPositions = meatQuery.ToComponentDataArray<Position>(Allocator.Temp);
            
            // Get all bibite positions (for bibite vision)
            var bibiteQuery = EntityManager.CreateEntityQuery(typeof(BibiteTag), typeof(Position), typeof(BibiteColor));
            var bibitePositions = bibiteQuery.ToComponentDataArray<Position>(Allocator.Temp);
            var bibiteColors = bibiteQuery.ToComponentDataArray<BibiteColor>(Allocator.Temp);
            
            // Process each bibite with a brain
            foreach (var (brainComp, position, rotation, energy, health, clock, velocity, entity) 
                in SystemAPI.Query<
                    BiomeBrainComponent,
                    RefRO<Position>,
                    RefRO<Rotation>,
                    RefRO<Energy>,
                    RefRO<Health>,
                    RefRO<InternalClock>,
                    RefRO<Velocity>>()
                .WithAll<BibiteTag>()
                .WithEntityAccess())
            {
                if (brainComp.Brain == null) continue;
                var brain = brainComp.Brain;
                
                float2 pos = position.ValueRO.Value;
                float rot = rotation.ValueRO.Value;
                
                // ================================================================
                // INTERNAL STATE MODULES
                // ================================================================
                
                // Energy Module
                float energyRatio = energy.ValueRO.Current / energy.ValueRO.Maximum;
                brain.SetModuleOutput(Tier1Modules.ENERGY_MODULE, 0, energyRatio);
                
                // Health Module
                float healthRatio = health.ValueRO.Current / health.ValueRO.Maximum;
                brain.SetModuleOutput(Tier1Modules.HEALTH_MODULE, 0, healthRatio);
                
                // Maturity Module
                float maturity = 1f;
                if (EntityManager.HasComponent<Age>(entity))
                {
                    var age = EntityManager.GetComponentData<Age>(entity);
                    maturity = age.Maturity;
                    brain.SetModuleOutput(Tier1Modules.MATURITY_MODULE, 1, math.min(age.TimeAlive / 600f, 1f));
                }
                brain.SetModuleOutput(Tier1Modules.MATURITY_MODULE, 0, maturity);
                
                // Stomach Module
                float fullness = 0f;
                if (EntityManager.HasComponent<StomachContents>(entity))
                {
                    var stomach = EntityManager.GetComponentData<StomachContents>(entity);
                    fullness = (stomach.PlantMatter + stomach.MeatMatter) / 100f;
                }
                brain.SetModuleOutput(Tier1Modules.STOMACH_MODULE, 0, fullness);
                
                // ================================================================
                // VISION - PLANTS
                // ================================================================
                
                float plantCloseness = 0f;
                float plantAngle = 0f;
                int plantCount = 0;
                float nearestPlantDist = float.MaxValue;
                
                for (int i = 0; i < plantPositions.Length; i++)
                {
                    float2 delta = plantPositions[i].Value - pos;
                    WrapDelta(ref delta, worldSettings.SimulationSize);
                    float dist = math.length(delta);
                    
                    if (dist < visionRange)
                    {
                        plantCount++;
                        if (dist < nearestPlantDist)
                        {
                            nearestPlantDist = dist;
                            plantCloseness = 1f - (dist / visionRange);
                            plantAngle = GetRelativeAngle(delta, rot);
                        }
                    }
                }
                
                brain.SetModuleOutput(Tier1Modules.VISION_PLANT_MODULE, 0, plantCloseness);
                brain.SetModuleOutput(Tier1Modules.VISION_PLANT_MODULE, 1, plantAngle);
                brain.SetModuleOutput(Tier1Modules.VISION_PLANT_MODULE, 2, math.min(plantCount / 10f, 1f));
                
                // ================================================================
                // VISION - MEAT
                // ================================================================
                
                float meatCloseness = 0f;
                float meatAngle = 0f;
                int meatCount = 0;
                float nearestMeatDist = float.MaxValue;
                
                for (int i = 0; i < meatPositions.Length; i++)
                {
                    float2 delta = meatPositions[i].Value - pos;
                    WrapDelta(ref delta, worldSettings.SimulationSize);
                    float dist = math.length(delta);
                    
                    if (dist < visionRange)
                    {
                        meatCount++;
                        if (dist < nearestMeatDist)
                        {
                            nearestMeatDist = dist;
                            meatCloseness = 1f - (dist / visionRange);
                            meatAngle = GetRelativeAngle(delta, rot);
                        }
                    }
                }
                
                brain.SetModuleOutput(Tier1Modules.VISION_MEAT_MODULE, 0, meatCloseness);
                brain.SetModuleOutput(Tier1Modules.VISION_MEAT_MODULE, 1, meatAngle);
                brain.SetModuleOutput(Tier1Modules.VISION_MEAT_MODULE, 2, math.min(meatCount / 10f, 1f));
                
                // ================================================================
                // VISION - BIBITES
                // ================================================================
                
                float bibiteCloseness = 0f;
                float bibiteAngle = 0f;
                int bibiteCount = 0;
                float nearestBibiteDist = float.MaxValue;
                
                for (int i = 0; i < bibitePositions.Length; i++)
                {
                    float2 delta = bibitePositions[i].Value - pos;
                    WrapDelta(ref delta, worldSettings.SimulationSize);
                    float dist = math.length(delta);
                    
                    if (dist > 0.1f && dist < visionRange)
                    {
                        bibiteCount++;
                        if (dist < nearestBibiteDist)
                        {
                            nearestBibiteDist = dist;
                            bibiteCloseness = 1f - (dist / visionRange);
                            bibiteAngle = GetRelativeAngle(delta, rot);
                        }
                    }
                }
                
                brain.SetModuleOutput(Tier1Modules.VISION_BIBITE_MODULE, 0, bibiteCloseness);
                brain.SetModuleOutput(Tier1Modules.VISION_BIBITE_MODULE, 1, bibiteAngle);
                brain.SetModuleOutput(Tier1Modules.VISION_BIBITE_MODULE, 2, math.min(bibiteCount / 10f, 1f));
                
                // ================================================================
                // PHEROMONE SENSING
                // ================================================================
                
                if (EntityManager.HasComponent<PheromoneSensor>(entity))
                {
                    var sensor = EntityManager.GetComponentData<PheromoneSensor>(entity);
                    
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_1_MODULE, 0, sensor.Intensity1);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_1_MODULE, 1, sensor.Angle1);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_1_MODULE, 2, sensor.Heading1 / math.PI);
                    
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_2_MODULE, 0, sensor.Intensity2);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_2_MODULE, 1, sensor.Angle2);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_2_MODULE, 2, sensor.Heading2 / math.PI);
                    
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_3_MODULE, 0, sensor.Intensity3);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_3_MODULE, 1, sensor.Angle3);
                    brain.SetModuleOutput(PheromoneModules.PHEROMONE_SENSE_3_MODULE, 2, sensor.Heading3 / math.PI);
                }
                
                // ================================================================
                // FUNCTIONAL MODULES (Clock, etc.)
                // ================================================================
                
                ProcessFunctionalModules(brain, deltaTime);
            }
            
            plantPositions.Dispose();
            meatPositions.Dispose();
            bibitePositions.Dispose();
            bibiteColors.Dispose();
        }
        
        private void ProcessFunctionalModules(BiomeBrain brain, float deltaTime)
        {
            foreach (var module in brain.Modules)
            {
                if (module.Type != ModuleType.Functional) continue;
                if (!module.Enabled) continue;
                
                // Get the definition and call its process logic
                if (Tier1Modules.Definitions.TryGetValue(module.DefinitionId, out var def))
                {
                    def.ProcessLogic?.Invoke(module, brain, deltaTime);
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
        
        private float GetRelativeAngle(float2 direction, float rotation)
        {
            float absAngle = math.atan2(direction.y, direction.x);
            float relAngle = absAngle - rotation;
            
            while (relAngle > math.PI) relAngle -= 2f * math.PI;
            while (relAngle < -math.PI) relAngle += 2f * math.PI;
            
            return relAngle / math.PI;  // Normalize to -1 to 1
        }
    }
    
    // ============================================================================
    // BIOME BRAIN PROCESSING SYSTEM
    // 
    // Processes the brain network and reads Output module input nodes
    // to update the BrainState component (which drives movement, eating, etc.)
    // ============================================================================
    
    /// <summary>
    /// System that processes BIOME brains and extracts outputs.
    /// </summary>
    [UpdateAfter(typeof(SensoryInputSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class BiomeBrainProcessingSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<WorldSettings>();
        }
        
        protected override void OnUpdate()
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            int frameCount = worldSettings.FrameCount;
            
            foreach (var (brainComp, brainState) 
                in SystemAPI.Query<BiomeBrainComponent, RefRW<BrainState>>()
                .WithAll<BibiteTag>())
            {
                if (brainComp.Brain == null) continue;
                var brain = brainComp.Brain;
                
                // Process the brain network (propagate signals, apply activations)
                brain.Process(frameCount);
                
                // ================================================================
                // READ FROM OUTPUT MODULES → BrainState
                // ================================================================
                
                // Motor Module
                brainState.ValueRW.AccelerateOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.MOTOR_MODULE, 0), -1f, 1f);
                brainState.ValueRW.RotateOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.MOTOR_MODULE, 1), -1f, 1f);
                
                // Mouth Module
                brainState.ValueRW.WantToEatOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.MOUTH_MODULE, 0), 0f, 1f);
                brainState.ValueRW.WantToAttackOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.MOUTH_MODULE, 2), 0f, 1f);
                brainState.ValueRW.WantToGrabOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.MOUTH_MODULE, 3), -1f, 1f);
                
                // Reproduction Module
                brainState.ValueRW.WantToLayOutput = math.clamp(
                    brain.GetModuleInput(Tier1Modules.REPRODUCTION_MODULE, 1), 0f, 1f);
                
                // Pheromone Emission Module
                brainState.ValueRW.PheromoneOut1 = math.clamp(
                    brain.GetModuleInput(PheromoneModules.PHEROMONE_EMIT_MODULE, 0), 0f, 1f);
                brainState.ValueRW.PheromoneOut2 = math.clamp(
                    brain.GetModuleInput(PheromoneModules.PHEROMONE_EMIT_MODULE, 1), 0f, 1f);
                brainState.ValueRW.PheromoneOut3 = math.clamp(
                    brain.GetModuleInput(PheromoneModules.PHEROMONE_EMIT_MODULE, 2), 0f, 1f);
            }
        }
    }
}
