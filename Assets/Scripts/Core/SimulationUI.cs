using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using BiomeBibites.BIOME;
using BiomeBibites.Systems;
using System.Collections.Generic;

namespace BiomeBibites.Core
{
    /// <summary>
    /// Enhanced UI for the BIOME simulation with selection, statistics, and brain visualization
    /// </summary>
    public class SimulationUI : MonoBehaviour
    {
        [Header("UI Settings")]
        public bool ShowMainPanel = true;
        public bool ShowBrainPanel = false;
        public bool ShowGraphs = false;
        
        [Header("Selection")]
        public Entity SelectedEntity;
        public bool HasSelection = false;
        
        // Statistics tracking
        private List<float> _populationHistory = new List<float>();
        private List<float> _generationHistory = new List<float>();
        private List<float> _biomassHistory = new List<float>();
        private const int MAX_HISTORY = 300;
        private float _historyTimer = 0f;
        private const float HISTORY_INTERVAL = 0.5f;
        
        // Simulation speed
        private float _timeScale = 1f;
        private bool _isPaused = false;
        
        // Cached data
        private EntityManager _entityManager;
        private bool _initialized = false;
        private int _bibiteCount;
        private int _plantCount;
        private int _meatCount;
        private float _freeBiomass;
        private float _simTime;
        private int _maxGeneration;
        private int _totalConnections;
        private int _totalNodes;
        private float _avgEnergy;
        
        // Brain visualization
        private BiomeBrain _selectedBrain;
        private Rect _brainPanelRect = new Rect(10, 200, 300, 400);
        
        void Start()
        {
            TryInitialize();
        }
        
        void TryInitialize()
        {
            if (_initialized) return;
            
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                _entityManager = world.EntityManager;
                _initialized = true;
            }
        }
        
        void Update()
        {
            // Try to initialize if not yet done
            if (!_initialized)
            {
                TryInitialize();
                if (!_initialized) return;
            }
            
            // Handle input
            HandleInput();
            
            // Update statistics
            UpdateStatistics();
            
            // Record history
            _historyTimer += Time.unscaledDeltaTime;
            if (_historyTimer >= HISTORY_INTERVAL)
            {
                RecordHistory();
                _historyTimer = 0f;
            }
            
            // Handle mouse selection
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsMouseOverUI())
            {
                TrySelectBibite();
            }
        }
        
        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            
            // Toggle panels
            if (keyboard.bKey.wasPressedThisFrame)
            {
                ShowBrainPanel = !ShowBrainPanel;
            }
            if (keyboard.gKey.wasPressedThisFrame)
            {
                ShowGraphs = !ShowGraphs;
            }
            
            // Speed controls
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _isPaused = !_isPaused;
                Time.timeScale = _isPaused ? 0f : _timeScale;
            }
            if (keyboard.periodKey.wasPressedThisFrame && !_isPaused)
            {
                _timeScale = Mathf.Min(_timeScale * 2f, 8f);
                Time.timeScale = _timeScale;
            }
            if (keyboard.commaKey.wasPressedThisFrame && !_isPaused)
            {
                _timeScale = Mathf.Max(_timeScale * 0.5f, 0.25f);
                Time.timeScale = _timeScale;
            }
            
            // Escape to deselect
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HasSelection = false;
                SelectedEntity = Entity.Null;
                _selectedBrain = null;
            }
        }
        
        void UpdateStatistics()
        {
            // Safety check - ensure world is still valid
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _initialized = false;
                return;
            }
            _entityManager = world.EntityManager;
            
            // Count entities
            var bibiteQuery = _entityManager.CreateEntityQuery(typeof(BibiteTag));
            var plantQuery = _entityManager.CreateEntityQuery(typeof(PlantPellet));
            var meatQuery = _entityManager.CreateEntityQuery(typeof(MeatPellet));
            
            _bibiteCount = bibiteQuery.CalculateEntityCount();
            _plantCount = plantQuery.CalculateEntityCount();
            _meatCount = meatQuery.CalculateEntityCount();
            
            // Get world settings
            var settingsQuery = _entityManager.CreateEntityQuery(typeof(WorldSettings));
            if (settingsQuery.CalculateEntityCount() > 0)
            {
                var settings = _entityManager.GetComponentData<WorldSettings>(settingsQuery.GetSingletonEntity());
                _freeBiomass = settings.FreeBiomass;
                _simTime = settings.SimulationTime;
            }
            
            // Calculate advanced stats
            _maxGeneration = 0;
            _totalConnections = 0;
            _totalNodes = 0;
            _avgEnergy = 0f;
            int brainCount = 0;
            
            var entities = bibiteQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                if (_entityManager.HasComponent<Generation>(entity))
                {
                    var gen = _entityManager.GetComponentData<Generation>(entity);
                    _maxGeneration = math.max(_maxGeneration, gen.Value);
                }
                
                if (_entityManager.HasComponent<Energy>(entity))
                {
                    var energy = _entityManager.GetComponentData<Energy>(entity);
                    _avgEnergy += energy.Current / energy.Maximum;
                }
                
                if (_entityManager.HasComponent<BiomeBrainComponent>(entity))
                {
                    var brainComp = _entityManager.GetComponentData<BiomeBrainComponent>(entity);
                    if (brainComp.Brain != null)
                    {
                        _totalConnections += brainComp.Brain.Connections.Count;
                        _totalNodes += brainComp.Brain.Nodes.Count;
                        brainCount++;
                    }
                }
            }
            entities.Dispose();
            
            if (_bibiteCount > 0)
                _avgEnergy /= _bibiteCount;
            
            // Update selected entity brain
            if (HasSelection && _entityManager.Exists(SelectedEntity))
            {
                if (_entityManager.HasComponent<BiomeBrainComponent>(SelectedEntity))
                {
                    var brainComp = _entityManager.GetComponentData<BiomeBrainComponent>(SelectedEntity);
                    _selectedBrain = brainComp.Brain;
                }
            }
            else if (HasSelection)
            {
                // Entity no longer exists
                HasSelection = false;
                SelectedEntity = Entity.Null;
                _selectedBrain = null;
            }
        }
        
        void RecordHistory()
        {
            _populationHistory.Add(_bibiteCount);
            _generationHistory.Add(_maxGeneration);
            _biomassHistory.Add(_freeBiomass);
            
            // Trim history
            while (_populationHistory.Count > MAX_HISTORY)
            {
                _populationHistory.RemoveAt(0);
                _generationHistory.RemoveAt(0);
                _biomassHistory.RemoveAt(0);
            }
        }
        
        void TrySelectBibite()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            
            var mouse = Mouse.current;
            if (mouse == null) return;
            
            Vector3 mousePos = new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), 0);
            Vector3 mouseWorld = cam.ScreenToWorldPoint(mousePos);
            float2 clickPos = new float2(mouseWorld.x, mouseWorld.y);
            
            var query = _entityManager.CreateEntityQuery(typeof(BibiteTag), typeof(Position));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            float closestDist = 15f; // Selection radius
            Entity closest = Entity.Null;
            
            foreach (var entity in entities)
            {
                var pos = _entityManager.GetComponentData<Position>(entity);
                float dist = math.distance(clickPos, pos.Value);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entity;
                }
            }
            entities.Dispose();
            
            if (closest != Entity.Null)
            {
                SelectedEntity = closest;
                HasSelection = true;
                ShowBrainPanel = true;
            }
        }
        
        bool IsMouseOverUI()
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            
            Vector2 mousePos = new Vector2(mouse.position.x.ReadValue(), Screen.height - mouse.position.y.ReadValue());
            Rect mainPanel = new Rect(10, 10, 220, 240);
            if (mainPanel.Contains(mousePos)) return true;
            if (ShowBrainPanel && _brainPanelRect.Contains(mousePos)) return true;
            return false;
        }
        
        void OnGUI()
        {
            if (!_initialized) return;
            
            // Main stats panel
            if (ShowMainPanel)
            {
                DrawMainPanel();
            }
            
            // Brain visualization panel
            if (ShowBrainPanel && HasSelection && _selectedBrain != null)
            {
                DrawBrainPanel();
            }
            
            // Population graphs
            if (ShowGraphs)
            {
                DrawGraphs();
            }
            
            // Speed indicator
            DrawSpeedIndicator();
            
            // Controls help
            DrawControlsHelp();
        }
        
        void DrawMainPanel()
        {
            GUI.Box(new Rect(10, 10, 220, 240), "");
            
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            
            int y = 15;
            GUI.Label(new Rect(15, y, 200, 25), "BIOME Bibites Simulation", titleStyle);
            y += 25;
            
            int minutes = (int)(_simTime / 60f);
            int seconds = (int)(_simTime % 60f);
            GUI.Label(new Rect(15, y, 200, 20), $"Time: {minutes:00}:{seconds:00}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Bibites: {_bibiteCount}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Plant Pellets: {_plantCount}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Meat Pellets: {_meatCount}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Free Biomass: {_freeBiomass:F0}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Max Generation: {_maxGeneration}", labelStyle);
            y += 18;
            
            GUI.Label(new Rect(15, y, 200, 20), $"Avg Energy: {_avgEnergy * 100:F0}%", labelStyle);
            y += 18;
            
            if (_bibiteCount > 0)
            {
                float avgConn = (float)_totalConnections / _bibiteCount;
                float avgNodes = (float)_totalNodes / _bibiteCount;
                GUI.Label(new Rect(15, y, 200, 20), $"Avg Connections: {avgConn:F1}", labelStyle);
                y += 18;
                GUI.Label(new Rect(15, y, 200, 20), $"Avg Nodes: {avgNodes:F1}", labelStyle);
            }
        }
        
        void DrawBrainPanel()
        {
            _brainPanelRect = GUI.Window(1, _brainPanelRect, DrawBrainWindow, "Selected Bibite Brain");
        }
        
        void DrawBrainWindow(int windowID)
        {
            if (_selectedBrain == null) return;
            
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 11;
            
            int y = 20;
            
            // Brain stats
            GUI.Label(new Rect(10, y, 280, 20), $"Generation: {_selectedBrain.Generation}", labelStyle);
            y += 16;
            GUI.Label(new Rect(10, y, 280, 20), $"Nodes: {_selectedBrain.Nodes.Count}", labelStyle);
            y += 16;
            GUI.Label(new Rect(10, y, 280, 20), $"Connections: {_selectedBrain.Connections.Count} ({_selectedBrain.GetEnabledConnectionCount()} enabled)", labelStyle);
            y += 16;
            GUI.Label(new Rect(10, y, 280, 20), $"Complexity: {_selectedBrain.GetComplexity():F1}", labelStyle);
            y += 20;
            
            // Entity stats
            if (_entityManager.HasComponent<Energy>(SelectedEntity))
            {
                var energy = _entityManager.GetComponentData<Energy>(SelectedEntity);
                GUI.Label(new Rect(10, y, 280, 20), $"Energy: {energy.Current:F0}/{energy.Maximum:F0}", labelStyle);
                y += 16;
            }
            
            if (_entityManager.HasComponent<BrainState>(SelectedEntity))
            {
                var state = _entityManager.GetComponentData<BrainState>(SelectedEntity);
                GUI.Label(new Rect(10, y, 280, 20), $"Accelerate: {state.AccelerateOutput:F2}", labelStyle);
                y += 16;
                GUI.Label(new Rect(10, y, 280, 20), $"Rotate: {state.RotateOutput:F2}", labelStyle);
                y += 16;
                GUI.Label(new Rect(10, y, 280, 20), $"Want to Eat: {state.WantToEatOutput:F2}", labelStyle);
                y += 20;
            }
            
            // Connection list (scrollable)
            GUI.Label(new Rect(10, y, 280, 20), "Connections:", labelStyle);
            y += 18;
            
            int connY = 0;
            int maxVisible = 12;
            int shown = 0;
            
            foreach (var conn in _selectedBrain.Connections)
            {
                if (!conn.Enabled) continue;
                if (shown >= maxVisible) break;
                
                string fromName = GetNodeName(conn.FromNode);
                string toName = GetNodeName(conn.ToNode);
                string connStr = $"{fromName} → {toName}: {conn.Weight:F2}";
                
                GUI.Label(new Rect(15, y + connY, 270, 16), connStr, labelStyle);
                connY += 14;
                shown++;
            }
            
            if (_selectedBrain.Connections.Count > maxVisible)
            {
                GUI.Label(new Rect(15, y + connY, 270, 16), $"... and {_selectedBrain.Connections.Count - maxVisible} more", labelStyle);
            }
            
            GUI.DragWindow();
        }
        
        string GetNodeName(int index)
        {
            // Input neurons
            if (index == InputNeurons.EnergyRatio) return "Energy";
            if (index == InputNeurons.HealthRatio) return "Health";
            if (index == InputNeurons.BiasClock) return "Bias";
            if (index == InputNeurons.Tic) return "Tic";
            if (index == InputNeurons.Minute) return "Minute";
            if (index == InputNeurons.PlantCloseness) return "PlantClose";
            if (index == InputNeurons.PlantAngle) return "PlantAngle";
            if (index == InputNeurons.PlantCount) return "PlantCount";
            if (index == InputNeurons.MeatCloseness) return "MeatClose";
            if (index == InputNeurons.MeatAngle) return "MeatAngle";
            if (index == InputNeurons.BibiteCloseness) return "BibiteClose";
            if (index == InputNeurons.BibiteAngle) return "BibiteAngle";
            if (index == InputNeurons.Speed) return "Speed";
            if (index == InputNeurons.RandomInput) return "Random";
            
            // Output neurons
            if (index == OutputNeurons.Accelerate) return "Accel";
            if (index == OutputNeurons.Rotate) return "Rotate";
            if (index == OutputNeurons.WantToEat) return "Eat";
            if (index == OutputNeurons.WantToAttack) return "Attack";
            if (index == OutputNeurons.WantToLay) return "Lay";
            if (index == OutputNeurons.PheromoneOut1) return "Pher1";
            if (index == OutputNeurons.PheromoneOut2) return "Pher2";
            if (index == OutputNeurons.PheromoneOut3) return "Pher3";
            
            // Hidden nodes
            if (index >= InputNeurons.COUNT + OutputNeurons.COUNT)
            {
                return $"H{index - InputNeurons.COUNT - OutputNeurons.COUNT}";
            }
            
            return $"N{index}";
        }
        
        void DrawGraphs()
        {
            Rect graphRect = new Rect(Screen.width - 320, 10, 310, 200);
            GUI.Box(graphRect, "Population History");
            
            if (_populationHistory.Count < 2) return;
            
            // Draw population line
            float maxPop = 1f;
            foreach (var p in _populationHistory)
                maxPop = Mathf.Max(maxPop, p);
            
            Texture2D lineTex = new Texture2D(1, 1);
            lineTex.SetPixel(0, 0, Color.green);
            lineTex.Apply();
            
            float graphWidth = 290f;
            float graphHeight = 160f;
            float graphX = graphRect.x + 10;
            float graphY = graphRect.y + 30;
            
            for (int i = 1; i < _populationHistory.Count; i++)
            {
                float x1 = graphX + (i - 1) * graphWidth / MAX_HISTORY;
                float x2 = graphX + i * graphWidth / MAX_HISTORY;
                float y1 = graphY + graphHeight - (_populationHistory[i - 1] / maxPop) * graphHeight;
                float y2 = graphY + graphHeight - (_populationHistory[i] / maxPop) * graphHeight;
                
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.green);
            }
            
            // Draw generation line (scaled)
            float maxGen = 1f;
            foreach (var g in _generationHistory)
                maxGen = Mathf.Max(maxGen, g);
            
            for (int i = 1; i < _generationHistory.Count; i++)
            {
                float x1 = graphX + (i - 1) * graphWidth / MAX_HISTORY;
                float x2 = graphX + i * graphWidth / MAX_HISTORY;
                float y1 = graphY + graphHeight - (_generationHistory[i - 1] / maxGen) * graphHeight;
                float y2 = graphY + graphHeight - (_generationHistory[i] / maxGen) * graphHeight;
                
                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), Color.cyan);
            }
            
            // Legend
            GUI.color = Color.green;
            GUI.Label(new Rect(graphRect.x + 15, graphRect.y + graphRect.height - 20, 100, 20), $"Pop: {_bibiteCount}");
            GUI.color = Color.cyan;
            GUI.Label(new Rect(graphRect.x + 120, graphRect.y + graphRect.height - 20, 100, 20), $"Gen: {_maxGeneration}");
            GUI.color = Color.white;
        }
        
        void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            // Simple line drawing using GUI
            Vector2 diff = end - start;
            float length = diff.magnitude;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y - 1, length, 2), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, start);
            GUI.color = Color.white;
        }
        
        void DrawSpeedIndicator()
        {
            string speedText;
            if (_isPaused)
                speedText = "⏸ PAUSED";
            else if (_timeScale < 1f)
                speedText = $"▶ {_timeScale:F2}x";
            else if (_timeScale > 1f)
                speedText = $"⏩ {_timeScale:F0}x";
            else
                speedText = "▶ 1x";
            
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            
            if (_isPaused)
                GUI.color = Color.yellow;
            else if (_timeScale > 1f)
                GUI.color = Color.cyan;
            
            GUI.Label(new Rect(Screen.width / 2 - 50, 10, 100, 25), speedText, style);
            GUI.color = Color.white;
        }
        
        void DrawControlsHelp()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 11;
            
            int y = Screen.height - 100;
            GUI.Box(new Rect(10, y, 220, 90), "");
            
            GUI.Label(new Rect(15, y + 5, 210, 18), "Controls:", style);
            GUI.Label(new Rect(15, y + 22, 210, 16), "Click: Select bibite", style);
            GUI.Label(new Rect(15, y + 36, 210, 16), "Space: Pause | ,/.: Speed", style);
            GUI.Label(new Rect(15, y + 50, 210, 16), "B: Brain panel | G: Graphs", style);
            GUI.Label(new Rect(15, y + 64, 210, 16), "WASD/Scroll: Camera", style);
        }
    }
}
