using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BiomeBibites.Core
{
    /// <summary>
    /// Renders all entities using GL immediate mode rendering.
    /// This approach works reliably with both Built-in and URP pipelines.
    /// </summary>
    public class SimulationRenderer : MonoBehaviour
    {
        [Header("Rendering Settings")]
        public bool ShowBibites = true;
        public bool ShowPlantPellets = true;
        public bool ShowMeatPellets = true;
        public bool ShowEggs = true;
        public bool ShowWorldBounds = true;
        public bool ShowVelocityVectors = false;
        
        [Header("Camera")]
        public Camera MainCamera;
        public float ZoomSpeed = 20f;
        public float PanSpeed = 200f;
        public float MinZoom = 20f;
        public float MaxZoom = 500f;
        
        private EntityManager _entityManager;
        private float _currentZoom = 250f;
        
        // Material for GL rendering
        private Material _glMaterial;
        
        // Circle vertices (precomputed for pellets)
        private Vector2[] _circleVerts;
        private const int CIRCLE_SEGMENTS = 12;
        
        void Start()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            if (MainCamera == null)
                MainCamera = Camera.main;
            
            // Setup camera for 2D top-down view
            if (MainCamera != null)
            {
                MainCamera.orthographic = true;
                MainCamera.orthographicSize = _currentZoom;
                MainCamera.transform.position = new Vector3(0, 0, -10);
                MainCamera.transform.rotation = Quaternion.identity;
                MainCamera.clearFlags = CameraClearFlags.SolidColor;
                MainCamera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
                RenderSettings.skybox = null;
            }
            
            // Create GL material - this shader works everywhere
            _glMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            _glMaterial.hideFlags = HideFlags.HideAndDontSave;
            _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _glMaterial.SetInt("_ZWrite", 0);
            
            // Precompute circle vertices
            _circleVerts = new Vector2[CIRCLE_SEGMENTS];
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                float angle = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
                _circleVerts[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            
            Debug.Log("[BIOME] GL Renderer initialized");
        }
        
        void Update()
        {
            HandleCameraInput();
        }
        
        void HandleCameraInput()
        {
            if (MainCamera == null) return;
            
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            
            if (keyboard == null) return;
            
            // Pan with WASD
            Vector3 pan = Vector3.zero;
            if (keyboard.wKey.isPressed) pan.y += PanSpeed * Time.deltaTime;
            if (keyboard.sKey.isPressed) pan.y -= PanSpeed * Time.deltaTime;
            if (keyboard.aKey.isPressed) pan.x -= PanSpeed * Time.deltaTime;
            if (keyboard.dKey.isPressed) pan.x += PanSpeed * Time.deltaTime;
            
            pan *= (_currentZoom / 100f);
            MainCamera.transform.position += pan;
            
            // Scroll wheel zoom - FIXED: much more responsive now
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0)
                {
                    // Zoom proportionally to current zoom level for smooth feel
                    float zoomDelta = scroll * ZoomSpeed * (_currentZoom / 100f);
                    _currentZoom -= zoomDelta;
                    _currentZoom = Mathf.Clamp(_currentZoom, MinZoom, MaxZoom);
                    MainCamera.orthographicSize = _currentZoom;
                }
            }
            
            // Q/E keys for zoom
            if (keyboard.qKey.isPressed)
            {
                _currentZoom += ZoomSpeed * Time.deltaTime * 2f;
                _currentZoom = Mathf.Clamp(_currentZoom, MinZoom, MaxZoom);
                MainCamera.orthographicSize = _currentZoom;
            }
            if (keyboard.eKey.isPressed)
            {
                _currentZoom -= ZoomSpeed * Time.deltaTime * 2f;
                _currentZoom = Mathf.Clamp(_currentZoom, MinZoom, MaxZoom);
                MainCamera.orthographicSize = _currentZoom;
            }
        }
        
        // OnRenderObject is called after the camera has rendered the scene
        void OnRenderObject()
        {
            // Safety check - get the default world
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            
            _entityManager = world.EntityManager;
            if (_glMaterial == null) return;
            
            // Set up GL rendering
            _glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(MainCamera.projectionMatrix);
            GL.modelview = MainCamera.worldToCameraMatrix;
            
            // Draw world bounds
            if (ShowWorldBounds)
            {
                DrawWorldBounds();
            }
            
            // Draw plant pellets
            if (ShowPlantPellets)
            {
                DrawPlantPellets();
            }
            
            // Draw meat pellets
            if (ShowMeatPellets)
            {
                DrawMeatPellets();
            }
            
            // Draw eggs
            if (ShowEggs)
            {
                DrawEggs();
            }
            
            // Draw bibites
            if (ShowBibites)
            {
                DrawBibites();
            }
            
            GL.PopMatrix();
        }
        
        void DrawWorldBounds()
        {
            var query = _entityManager.CreateEntityQuery(typeof(WorldSettings));
            if (query.CalculateEntityCount() == 0) return;
            
            var settings = _entityManager.GetComponentData<WorldSettings>(query.GetSingletonEntity());
            float halfSize = settings.SimulationSize / 2f;
            
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.5f, 0.5f, 0.5f, 1f));
            
            GL.Vertex3(-halfSize, -halfSize, 0);
            GL.Vertex3(halfSize, -halfSize, 0);
            GL.Vertex3(halfSize, -halfSize, 0);
            GL.Vertex3(halfSize, halfSize, 0);
            GL.Vertex3(halfSize, halfSize, 0);
            GL.Vertex3(-halfSize, halfSize, 0);
            GL.Vertex3(-halfSize, halfSize, 0);
            GL.Vertex3(-halfSize, -halfSize, 0);
            
            GL.End();
        }
        
        void DrawPlantPellets()
        {
            var query = _entityManager.CreateEntityQuery(typeof(PlantPellet), typeof(Position), typeof(Radius));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0.2f, 0.85f, 0.2f, 1f)); // Green
            
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<Position>(entity);
                var radius = _entityManager.GetComponentData<Radius>(entity);
                
                DrawFilledCircle(pos.Value.x, pos.Value.y, radius.Value);
            }
            
            GL.End();
            entities.Dispose();
        }
        
        void DrawMeatPellets()
        {
            var query = _entityManager.CreateEntityQuery(typeof(MeatPellet), typeof(Position), typeof(Radius));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0.85f, 0.2f, 0.2f, 1f)); // Red
            
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<Position>(entity);
                var radius = _entityManager.GetComponentData<Radius>(entity);
                
                DrawFilledCircle(pos.Value.x, pos.Value.y, radius.Value);
            }
            
            GL.End();
            entities.Dispose();
        }
        
        void DrawEggs()
        {
            var query = _entityManager.CreateEntityQuery(typeof(Egg), typeof(Position), typeof(Radius));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            GL.Begin(GL.TRIANGLES);
            
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<Position>(entity);
                var radius = _entityManager.GetComponentData<Radius>(entity);
                var egg = _entityManager.GetComponentData<Egg>(entity);
                
                // Color based on hatch progress (yellow -> white as it hatches)
                float progress = egg.HatchProgress;
                GL.Color(new Color(1f, 1f - progress * 0.3f, 0.5f + progress * 0.5f, 1f));
                
                DrawFilledCircle(pos.Value.x, pos.Value.y, radius.Value);
            }
            
            GL.End();
            entities.Dispose();
        }
        
        void DrawBibites()
        {
            var query = _entityManager.CreateEntityQuery(
                typeof(BibiteTag), typeof(Position), typeof(Rotation), 
                typeof(Radius), typeof(BibiteColor));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            GL.Begin(GL.TRIANGLES);
            
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<Position>(entity);
                var rot = _entityManager.GetComponentData<Rotation>(entity);
                var radius = _entityManager.GetComponentData<Radius>(entity);
                var color = _entityManager.GetComponentData<BibiteColor>(entity);
                
                GL.Color(new Color(color.R, color.G, color.B, 1f));
                DrawBibiteTriangle(pos.Value.x, pos.Value.y, rot.Value, radius.Value);
            }
            
            GL.End();
            
            // Draw velocity vectors if enabled
            if (ShowVelocityVectors)
            {
                GL.Begin(GL.LINES);
                GL.Color(Color.yellow);
                
                foreach (var entity in entities)
                {
                    if (!_entityManager.HasComponent<Velocity>(entity)) continue;
                    
                    var pos = _entityManager.GetComponentData<Position>(entity);
                    var velocity = _entityManager.GetComponentData<Velocity>(entity);
                    
                    GL.Vertex3(pos.Value.x, pos.Value.y, 0);
                    GL.Vertex3(pos.Value.x + velocity.Value.x, pos.Value.y + velocity.Value.y, 0);
                }
                
                GL.End();
            }
            
            entities.Dispose();
        }
        
        // Draw a filled circle using triangles (fan pattern)
        void DrawFilledCircle(float x, float y, float radius)
        {
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                int next = (i + 1) % CIRCLE_SEGMENTS;
                
                GL.Vertex3(x, y, 0);
                GL.Vertex3(x + _circleVerts[i].x * radius, y + _circleVerts[i].y * radius, 0);
                GL.Vertex3(x + _circleVerts[next].x * radius, y + _circleVerts[next].y * radius, 0);
            }
        }
        
        // Draw a bibite as a triangle pointing in its rotation direction
        void DrawBibiteTriangle(float x, float y, float rotation, float radius)
        {
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            
            float tipX = radius;
            float tipY = 0;
            float blX = -radius * 0.5f;
            float blY = radius * 0.4f;
            float brX = -radius * 0.5f;
            float brY = -radius * 0.4f;
            
            GL.Vertex3(x + tipX * cos - tipY * sin, y + tipX * sin + tipY * cos, 0);
            GL.Vertex3(x + blX * cos - blY * sin, y + blX * sin + blY * cos, 0);
            GL.Vertex3(x + brX * cos - brY * sin, y + brX * sin + brY * cos, 0);
        }
        
        void OnDestroy()
        {
            if (_glMaterial != null)
            {
                DestroyImmediate(_glMaterial);
            }
        }
    }
}
