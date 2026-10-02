#if HAVE_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Editor
{
    /// <summary>
    /// Generates the "PerformanceTest" sample: an FPS counter plus a menu that selects which kind of
    /// graph workload runs every tick (math, value evaluation, variables, pointers, flow, events, mixed), plus
    /// session statistics and an automatic benchmark that logs its results. The graph is built through
    /// the Visual Scripting API so it stays valid for the KHR_interactivity exporter.
    /// Re-run the menu item to regenerate the scene after changing this file.
    /// </summary>
    public static class PerformanceTestSceneBuilder
    {
        private const string PackageRoot = "Packages/com.needle.gltf-interactivity-scenes";
        private const string Folder = PackageRoot + "/Interactivity Repo Models/20261002-PerformanceTest";
        private const string TextureFolder = Folder + "/Textures";
        private const string MaterialFolder = Folder + "/Materials";
        private const string SceneName = "PerformanceTest";
        private const string DigitTemplatePath = PackageRoot + "/Test Scenes/Common/N01.mat";

        // Iterations per tick = BaseIterations * 2^(load - 1); Mixed splits that across all workloads.
        private const float BaseIterations = 50f;
        private const float MinLoad = 1f;
        private const float MaxLoad = 9f;
        private const float StartLoad = 3f;
        private const float FpsSampleInterval = 0.5f;
        private const float FpsBarFull = 120f;
        private const float FpsBarWidth = 7.6f;

        private class Mode
        {
            public string id;          // object / material names and benchmark log
            public string text;        // button label
            public int row, column;    // position in the menu grid (row = category)
            public string description; // shown below the menu while the mode is active
            public Func<GraphBuilder, SceneRefs, ControlInput> build; // workload; null for idle and mixed
            public bool inMixed = true; // baselines are left out of Mixed
        }

        private static readonly string[] CategoryNames =
            { "MATH", "VALUES", "VARIABLES", "POINTERS", "FLOW/EVENTS", "BASELINE" };

        // Mode index = value of the "mode" graph variable. Mode 0 is idle, the last one is Mixed.
        private static readonly Mode[] Modes =
        {
            new Mode { id = "IDLE", text = "IDLE", row = 5, column = 0,
                description = "IDLE: NO WORKLOAD, BASELINE FPS" },

            new Mode { id = "MATH_BASIC", text = "BASIC", row = 0, column = 0, build = (g, r) => BuildBasicMathPath(g),
                description = "MATH BASIC: SIN COS SQRT POW ABS MOD" },
            new Mode { id = "MATH_VECTOR", text = "VECTOR", row = 0, column = 1, build = (g, r) => BuildVectorMathPath(g),
                description = "MATH VECTOR: CROSS DOT NORMALIZE LERP ROTATE" },
            new Mode { id = "MATH_MATRIX", text = "MATRIX", row = 0, column = 2, build = (g, r) => BuildMatrixMathPath(g),
                description = "MATH MATRIX: TRS MUL TRANSPOSE INVERSE DET" },
            new Mode { id = "MATH_INT", text = "INT", row = 0, column = 3, build = (g, r) => BuildIntMathPath(g),
                description = "INT: AND OR XOR NOT SHIFT CLZ CTZ POPCNT" },
            new Mode { id = "MATH_CONVERT", text = "CONVERT", row = 0, column = 4, build = (g, r) => BuildConversionPath(g),
                description = "CONVERT: INT TO FLOAT, FLOAT TO INT" },

            new Mode { id = "VALUE_SHARED", text = "SHARED", row = 1, column = 0, build = (g, r) => BuildSharedValuePath(g),
                description = "SHARED: 1 MATH CHAIN USED BY 10 NODES" },
            new Mode { id = "VALUE_COPIES", text = "COPIES", row = 1, column = 1, build = (g, r) => BuildCopiedValuePath(g),
                description = "COPIES: SAME MATH CHAIN BUILT 10 TIMES" },
            new Mode { id = "VALUE_DEEP", text = "DEEP", row = 1, column = 2, build = (g, r) => BuildDeepValuePath(g),
                description = "DEEP: 100 MATH NODES IN ONE LONG CHAIN" },
            new Mode { id = "VALUE_WIDE", text = "WIDE", row = 1, column = 3, build = (g, r) => BuildWideValuePath(g),
                description = "WIDE: 100 MATH NODES AS A FLAT TREE" },

            new Mode { id = "VAR_GET", text = "GET", row = 2, column = 0, build = (g, r) => BuildVariableGetPath(g),
                description = "VARIABLES GET: READ 22 VARIABLES PER LOOP" },
            new Mode { id = "VAR_SET", text = "SET", row = 2, column = 1, build = (g, r) => BuildVariableSetPath(g),
                description = "VARIABLES SET: 22 SINGLE SET NODES" },
            new Mode { id = "VAR_MULTISET", text = "MULTI", row = 2, column = 2, build = (g, r) => BuildVariableMultiSetPath(g),
                description = "VARIABLES MULTI SET: 1 NODE SETS 22 VARS" },
            new Mode { id = "VAR_INTERPOLATE", text = "INTERP", row = 2, column = 3, build = (g, r) => BuildVariableInterpolatePath(g),
                description = "VARIABLES INTERPOLATE: RESTART 4 PER LOOP" },

            new Mode { id = "PTR_GET", text = "GET", row = 3, column = 0, build = BuildPointerGetPath,
                description = "POINTERS GET: READ 32 TRANSFORM/COLOR VALUES" },
            new Mode { id = "PTR_SET", text = "SET", row = 3, column = 1, build = BuildPointerSetPath,
                description = "POINTERS SET: WRITE 32 TRANSFORM/COLOR VALUES" },
            new Mode { id = "PTR_GETSET", text = "GET+SET", row = 3, column = 2, build = BuildPointerGetSetPath,
                description = "POINTERS GET+SET: READ+WRITE 16 VALUES" },
            new Mode { id = "PTR_INTERPOLATE", text = "INTERP", row = 3, column = 3, build = BuildPointerInterpolatePath,
                description = "POINTERS INTERPOLATE: RESTART 8 PER LOOP" },
            new Mode { id = "PTR_HIERARCHY", text = "NESTED", row = 3, column = 4, build = BuildHierarchyPath,
                description = "NESTED: ROTATE ROOT OF 20 LEVEL HIERARCHY" },

            new Mode { id = "FLOW", text = "FLOW", row = 4, column = 0, build = (g, r) => BuildFlowPath(g),
                description = "FLOW: SEQUENCE SWITCH BRANCH NESTED LOOP" },
            new Mode { id = "FLOW_NODES", text = "NODES", row = 4, column = 1, build = (g, r) => BuildFlowNodesPath(g),
                description = "NODES: WAITALL MULTIGATE DON WHILE THROTTLE" },
            new Mode { id = "FLOW_DELAYS", text = "DELAYS", row = 4, column = 2, build = (g, r) => BuildDelayPath(g),
                description = "DELAYS: START 2 AND CANCEL 1 PER LOOP" },
            new Mode { id = "EVENT", text = "EVENTS", row = 4, column = 3, build = (g, r) => BuildEventPath(g),
                description = "EVENTS: SEND 6 CUSTOM EVENTS PER LOOP" },
            new Mode { id = "EVENT_CHAIN", text = "CHAIN", row = 4, column = 4, build = (g, r) => BuildEventChainPath(g),
                description = "EVENT CHAIN: EACH LOOP RELAYS 20 EVENTS" },

            new Mode { id = "LOOP", text = "LOOP", row = 5, column = 1, build = (g, r) => BuildEmptyLoopPath(g), inMixed = false,
                description = "LOOP: EMPTY FOR LOOP, MEASURES LOOP COST" },
            new Mode { id = "MIXED", text = "MIXED", row = 5, column = 2,
                description = "MIXED: ALL TESTS, LOAD SPLIT EVENLY" },
        };

        private static int MixedMode => Modes.Length - 1;

        private const int PointerTargetCount = 8;
        private const float PointerTargetScale = 0.3f;
        private const int HierarchyDepth = 20;
        private const int EventChainDepth = 20;

        // Benchmark: every step warms up, resets the statistics and then measures.
        private const float BenchWarmup = 1f;
        private const float BenchMeasure = 3f;

        private static readonly Color ButtonColor = new Color(0.78f, 0.80f, 0.84f);
        private static readonly Color ButtonActiveColor = new Color(1.0f, 0.62f, 0.10f);
        private static readonly Color ResultUnmeasuredColor = new Color(0.55f, 0.56f, 0.6f);
        private static readonly Color ButtonDisabledColor = new Color(0.46f, 0.47f, 0.5f); // while a benchmark runs
        // panels are dark with light text, buttons light with dark text, so the two can not be mixed up
        private static readonly Color PanelColor = new Color(0.22f, 0.24f, 0.29f);
        private static readonly Color PanelTextColor = new Color(0.93f, 0.94f, 0.96f);
        private static readonly Color BackdropColor = new Color(0.13f, 0.14f, 0.17f);
        private static readonly Color TextColor = Color.black;
        private static readonly Color HeaderColor = new Color(0.85f, 0.87f, 0.9f);

        [MenuItem("glTF Interactivity/Create Performance Test Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scenePath = $"{Folder}/{SceneName}.unity";
            if (File.Exists(Physical(scenePath)) &&
                !EditorUtility.DisplayDialog("Performance Test Scene",
                    $"{scenePath} already exists. Regenerate it (scene, materials and label textures)?",
                    "Regenerate", "Cancel"))
                return;

            // Both folders only hold generated assets; clearing them drops leftovers from renamed modes
            AssetDatabase.DeleteAsset(TextureFolder);
            AssetDatabase.DeleteAsset(MaterialFolder);
            Directory.CreateDirectory(Physical(TextureFolder));
            Directory.CreateDirectory(Physical(MaterialFolder));
            AssetDatabase.Refresh();

            var digitTemplate = AssetDatabase.LoadAssetAtPath<Material>(DigitTemplatePath);
            if (!digitTemplate)
            {
                Debug.LogError($"Digit material template not found at {DigitTemplatePath}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ui = BuildSceneObjects(digitTemplate);
            BuildGraph(ui);

            EditorSceneManager.SaveScene(scene, scenePath);
            CreateOrUpdateModelDefinition(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Performance test scene written to {scenePath}");
        }

        #region Scene objects

        private class SceneRefs
        {
            public GameObject graphObject;
            public readonly GameObject[] modeButtons = new GameObject[Modes.Length];
            public readonly Material[] modeButtonMaterials = new Material[Modes.Length];
            public GameObject autoButton, resetButton, resetWorstButton;
            public Material autoButtonMaterial, resetButtonMaterial, loadButtonMaterial;
            public Material description; // one row of the description atlas per mode
            public GameObject loadMinus, loadPlus;
            public Material loadDigit;
            public readonly Material[] fpsDigits = new Material[3];   // live FPS: hundreds, tens, ones
            public readonly Material[] avgFpsDigits = new Material[3]; // average FPS since reset
            public readonly Material[] worstMsDigits = new Material[3]; // worst frame time (ms) since reset
            public Transform fpsBar;
            public Material fpsBarMaterial;
            public Transform spinner;
            public readonly Transform[] pointerTargets = new Transform[PointerTargetCount];
            public readonly Material[] pointerTargetMaterials = new Material[PointerTargetCount];
            public Transform hierarchyRoot, hierarchyLeaf;
            public readonly Material[] resultStrips = new Material[Modes.Length]; // last AUTO RUN result per mode
            public readonly Material[] summaryMeanDigits = new Material[3];
            public readonly Material[] summaryMinDigits = new Material[3];
            public Material slowestDescription; // description atlas showing the slowest mode
        }

        private const float MenuTop = 3.35f;
        private const float MenuRowStep = 0.5f;
        private const float MenuLeft = -1.6f;
        private const float MenuColumnStep = 1.22f;
        private static readonly Vector2 MenuButtonSize = new Vector2(1.12f, 0.4f);

        private static Vector3 MenuPosition(int row, float column) =>
            new Vector3(MenuLeft + column * MenuColumnStep, MenuTop - row * MenuRowStep, 0f);

        private static SceneRefs BuildSceneObjects(Material digitTemplate)
        {
            var refs = new SceneRefs();

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            camera.fieldOfView = 40f;
            // glTF models face +Z and web viewers look at them from +Z. Unity -> glTF only mirrors X, so the
            // scene is built facing Unity's +Z as well (root turned by 180 degrees) and viewed from that side.
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 1.1f, 11.6f), Quaternion.Euler(0f, 180f, 0f));

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

            var root = new GameObject("PerformanceTest").transform;
            root.localRotation = Quaternion.Euler(0f, 180f, 0f); // front faces +Z, see camera

            var backdrop = Box("Backdrop", root, new Vector3(0f, 1.1f, 0.3f), new Vector3(8.1f, 8.2f, 0.1f),
                CreateColorMaterial("Backdrop", BackdropColor), keepCollider: false);
            var panelMaterial = CreateColorMaterial("Panel", PanelColor);

            // Live FPS (left) and statistics since the last reset (right)
            var fpsPanel = Box("FpsPanel", root, new Vector3(-1.95f, 4.6f, 0f), new Vector3(3.7f, 0.9f, 0.1f),
                panelMaterial, keepCollider: false);
            Label(fpsPanel, "FPS", new Vector3(-3.05f, 4.6f, 0f), 0.5f, color: PanelTextColor);
            for (int i = 0; i < 3; i++)
            {
                refs.fpsDigits[i] = CreateDigitMaterial(digitTemplate, $"FpsDigit{i}");
                DigitQuad(fpsPanel, $"FpsDigit{i}", new Vector3(-1.9f + i * 0.6f, 4.6f, 0f), 0.7f, refs.fpsDigits[i]);
            }

            var statsPanel = Box("StatsPanel", root, new Vector3(1.95f, 4.6f, 0f), new Vector3(3.7f, 0.9f, 0.1f),
                panelMaterial, keepCollider: false);
            Label(statsPanel, "AVG FPS", new Vector3(1.0f, 4.8f, 0f), 0.26f, 1.6f, PanelTextColor);
            Label(statsPanel, "WORST MS", new Vector3(1.0f, 4.4f, 0f), 0.26f, 1.6f, PanelTextColor);
            for (int i = 0; i < 3; i++)
            {
                refs.avgFpsDigits[i] = CreateDigitMaterial(digitTemplate, $"AvgFpsDigit{i}");
                refs.worstMsDigits[i] = CreateDigitMaterial(digitTemplate, $"WorstMsDigit{i}");
                DigitQuad(statsPanel, $"AvgFpsDigit{i}", new Vector3(2.05f + i * 0.32f, 4.8f, 0f), 0.34f, refs.avgFpsDigits[i]);
                DigitQuad(statsPanel, $"WorstMsDigit{i}", new Vector3(2.05f + i * 0.32f, 4.4f, 0f), 0.34f, refs.worstMsDigits[i]);
            }
            // resets only the worst frame time
            refs.resetButtonMaterial = CreateColorMaterial("Button_RESET", ButtonColor);
            refs.resetWorstButton = Button(root, "RESET_WORST", "RESET", new Vector3(3.32f, 4.4f, -0.05f), new Vector2(0.8f, 0.3f),
                refs.resetButtonMaterial);

            Box("FpsBarBackground", root, new Vector3(0f, 3.92f, 0f), new Vector3(FpsBarWidth, 0.16f, 0.06f),
                CreateColorMaterial("FpsBarBackground", new Color(0.3f, 0.3f, 0.33f)), keepCollider: false);
            var barPivot = new GameObject("FpsBar").transform;
            barPivot.SetParent(root, false);
            barPivot.localPosition = new Vector3(-FpsBarWidth * 0.5f, 3.92f, -0.04f);
            barPivot.localScale = new Vector3(0.01f, 1f, 1f);
            refs.fpsBarMaterial = CreateColorMaterial("FpsBar", Color.green);
            Box("Fill", barPivot, new Vector3(0.5f, 0f, 0f), new Vector3(1f, 0.16f, 0.06f),
                refs.fpsBarMaterial, keepCollider: false);
            refs.fpsBar = barPivot;

            // Mode menu: one row per category, header on the left
            for (int row = 0; row < CategoryNames.Length; row++)
                Label(backdrop, CategoryNames[row], new Vector3(-3.1f, MenuTop - row * MenuRowStep, 0f), 0.2f, 1.25f, HeaderColor);

            for (int i = 0; i < Modes.Length; i++)
            {
                var mode = Modes[i];
                refs.modeButtonMaterials[i] = CreateColorMaterial($"Button_{mode.id}", ButtonColor);
                refs.modeButtons[i] = Button(root, mode.id, mode.text, MenuPosition(mode.row, mode.column),
                    MenuButtonSize, refs.modeButtonMaterials[i]);
                // thin strip at the bottom edge: grey until AUTO RUN measured this mode, then red..green by its FPS
                refs.resultStrips[i] = CreateColorMaterial($"Result_{mode.id}", ResultUnmeasuredColor);
                AttachQuad(refs.modeButtons[i], "Result", MenuPosition(mode.row, mode.column) + new Vector3(0f, -MenuButtonSize.y * 0.5f + 0.035f, 0f),
                    new Vector2(MenuButtonSize.x - 0.1f, 0.05f), refs.resultStrips[i]);
            }

            refs.autoButtonMaterial = CreateColorMaterial("Button_AUTO", ButtonColor);

            // Description of the active mode
            var descriptionY = MenuTop - CategoryNames.Length * MenuRowStep;
            var descriptionPanel = Box("DescriptionPanel", root, new Vector3(0f, descriptionY, 0f), new Vector3(7.7f, 0.42f, 0.1f),
                panelMaterial, keepCollider: false);
            var descriptions = new string[DescriptionLines];
            for (int i = 0; i < Modes.Length; i++) descriptions[i] = Modes[i].description;
            descriptions[NoRunDescription] = "NO AUTO RUN YET";
            var atlas = CreateTextTexture("Descriptions", descriptions, 6);
            refs.description = CreateCutoutMaterial("Descriptions", atlas, new Vector2(1f, 1f / DescriptionLines), DescriptionOffset(0),
                PanelTextColor);
            var lineAspect = (float)atlas.width / (atlas.height / (float)DescriptionLines);
            var lineHeight = Mathf.Min(0.26f, 7.4f / lineAspect);
            AttachQuad(descriptionPanel, "Description", new Vector3(0f, descriptionY, 0f),
                new Vector2(lineHeight * lineAspect, lineHeight), refs.description);

            // Load selector + explanation
            var loadY = descriptionY - 0.6f;
            var loadPanel = Box("LoadPanel", root, new Vector3(0f, loadY, 0f), new Vector3(7.7f, 0.6f, 0.1f),
                panelMaterial, keepCollider: false);
            Label(loadPanel, "LOAD", new Vector3(-3.15f, loadY, 0f), 0.3f, color: PanelTextColor);
            var loadButtonMaterial = refs.loadButtonMaterial = CreateColorMaterial("Button_Load", ButtonColor);
            refs.loadMinus = Button(root, "LoadMinus", "-", new Vector3(-2.3f, loadY, -0.05f), new Vector2(0.5f, 0.42f), loadButtonMaterial);
            refs.loadDigit = CreateDigitMaterial(digitTemplate, "LoadDigit");
            DigitQuad(loadPanel, "LoadDigit", new Vector3(-1.72f, loadY, 0f), 0.45f, refs.loadDigit);
            refs.loadPlus = Button(root, "LoadPlus", "+", new Vector3(-1.14f, loadY, -0.05f), new Vector2(0.5f, 0.42f), loadButtonMaterial);
            Label(loadPanel, $"{BaseIterations} X 2^(LOAD-1) LOOPS PER FRAME", new Vector3(1.4f, loadY, 0f), 0.22f, 4.5f, PanelTextColor);

            // Benchmark actions, right above their results
            var benchY = loadY - 0.55f;
            Label(backdrop, "BENCHMARK", new Vector3(-3.1f, benchY, 0f), 0.2f, 1.25f, HeaderColor);
            refs.autoButton = Button(root, "AUTO", "AUTO RUN", new Vector3(MenuLeft + 0.5f * MenuColumnStep, benchY, 0f),
                new Vector2(MenuButtonSize.x + MenuColumnStep, MenuButtonSize.y), refs.autoButtonMaterial);
            refs.resetButton = Button(root, "RESET", "RESET", new Vector3(MenuLeft + 2f * MenuColumnStep, benchY, 0f), MenuButtonSize,
                refs.resetButtonMaterial);

            // Summary of the last AUTO RUN
            var summaryY = benchY - 0.75f;
            var summaryPanel = Box("SummaryPanel", root, new Vector3(0f, summaryY, 0f), new Vector3(7.7f, 0.9f, 0.1f),
                panelMaterial, keepCollider: false);
            var summaryTop = summaryY + 0.2f;
            var summaryBottom = summaryY - 0.2f;
            Label(summaryPanel, "LAST AUTO RUN", new Vector3(-2.75f, summaryTop, 0f), 0.22f, 2.0f, PanelTextColor);
            Label(summaryPanel, "MEAN FPS", new Vector3(-0.75f, summaryTop, 0f), 0.22f, 1.2f, PanelTextColor);
            Label(summaryPanel, "MIN FPS", new Vector3(1.75f, summaryTop, 0f), 0.22f, 1.2f, PanelTextColor);
            for (int i = 0; i < 3; i++)
            {
                refs.summaryMeanDigits[i] = CreateDigitMaterial(digitTemplate, $"SummaryMeanDigit{i}");
                refs.summaryMinDigits[i] = CreateDigitMaterial(digitTemplate, $"SummaryMinDigit{i}");
                DigitQuad(summaryPanel, $"SummaryMeanDigit{i}", new Vector3(0.25f + i * 0.3f, summaryTop, 0f), 0.32f, refs.summaryMeanDigits[i]);
                DigitQuad(summaryPanel, $"SummaryMinDigit{i}", new Vector3(2.7f + i * 0.3f, summaryTop, 0f), 0.32f, refs.summaryMinDigits[i]);
            }
            Label(summaryPanel, "SLOWEST", new Vector3(-3.15f, summaryBottom, 0f), 0.2f, 1.0f, PanelTextColor);
            refs.slowestDescription = CreateCutoutMaterial("SlowestDescription", atlas, new Vector2(1f, 1f / DescriptionLines),
                DescriptionOffset(NoRunDescription), PanelTextColor);
            var slowestHeight = Mathf.Min(0.22f, 6.3f / lineAspect);
            AttachQuad(summaryPanel, "SlowestDescription", new Vector3(0.55f, summaryBottom, 0f),
                new Vector2(slowestHeight * lineAspect, slowestHeight), refs.slowestDescription);

            // Spinner: rotates from Time.time every tick, so hitches are visible
            var targetsY = summaryY - 0.9f;
            refs.spinner = Box("Spinner", root, new Vector3(-3.4f, targetsY, -0.3f), Vector3.one * 0.45f,
                CreateColorMaterial("Spinner", new Color(0.2f, 0.6f, 1f)), keepCollider: false).transform;

            // Pointer targets: read and written by the pointer tests
            for (int i = 0; i < PointerTargetCount; i++)
            {
                refs.pointerTargetMaterials[i] = CreateColorMaterial($"PointerTarget{i}",
                    Color.HSVToRGB(i / (float)PointerTargetCount, 0.6f, 0.95f));
                refs.pointerTargets[i] = Box($"PointerTarget{i}", root,
                    new Vector3(-2.55f + i * 0.68f, targetsY, -0.3f), Vector3.one * PointerTargetScale,
                    refs.pointerTargetMaterials[i], keepCollider: false).transform;
            }

            // Hierarchy: a bent chain of small boxes, rotated at the root by the NESTED test
            var chainMaterial = CreateColorMaterial("Hierarchy", new Color(0.9f, 0.9f, 0.6f));
            refs.hierarchyRoot = new GameObject("HierarchyRoot").transform;
            refs.hierarchyRoot.SetParent(root, false);
            refs.hierarchyRoot.localPosition = new Vector3(3.25f, targetsY - 0.25f, -0.3f);
            var parent = refs.hierarchyRoot;
            for (int i = 0; i < HierarchyDepth; i++)
            {
                var level = new GameObject($"Level{i}").transform;
                level.SetParent(parent, false);
                level.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, 0.03f, 0f);
                level.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 0f : -4f);
                Box("Joint", level, Vector3.zero, Vector3.one * 0.04f, chainMaterial, keepCollider: false);
                parent = level;
            }
            refs.hierarchyLeaf = parent;
            Box("Leaf", parent, Vector3.zero, Vector3.one * 0.12f, CreateColorMaterial("HierarchyLeaf", new Color(1f, 0.4f, 0.3f)),
                keepCollider: false);

            refs.graphObject = new GameObject("PerformanceGraph");
            // export the graph as authored: the exporter's clean up would otherwise optimize parts of the workloads away
            refs.graphObject.AddComponent<GltfInteractivityScenes.InteractivityExportOptions>().cleanUpAndOptimizeExportedGraph = false;
            return refs;
        }

        /// <summary>Texture offset that shows the description line of <paramref name="mode"/>.</summary>
        private static Vector2 DescriptionOffset(int line) => new Vector2(0f, (DescriptionLines - 1 - line) / (float)DescriptionLines);

        // description atlas: one line per mode plus a "no run yet" line for the summary
        private static int DescriptionLines => Modes.Length + 1;
        private static int NoRunDescription => Modes.Length;

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Clickable button: a box with a text label as child, so hits on the label reach the button.</summary>
        private static GameObject Button(Transform parent, string name, string text, Vector3 position, Vector2 size, Material material)
        {
            var go = Box("Button_" + name, parent, position, new Vector3(size.x, size.y, 0.12f), material, keepCollider: true);
            const float margin = 0.12f; // left/right padding between text and button edge
            Label(go, text, position, size.y * 0.55f, size.x - 2f * margin);
            return go;
        }

        /// <summary>Text quad placed in front of <paramref name="parent"/> (a box) at a world position.
        /// The height shrinks when the text would be wider than <paramref name="maxWidth"/>.</summary>
        private static void Label(GameObject parent, string text, Vector3 worldPosition, float height,
            float maxWidth = float.MaxValue, Color? color = null)
        {
            var textColor = color ?? TextColor;
            var name = "Label_" + SafeName(text) + (color.HasValue ? "_" + ColorUtility.ToHtmlStringRGB(textColor) : "");
            var texture = CreateTextTexture(name, new[] { text }, 8);
            var material = CreateCutoutMaterial(name, texture, Vector2.one, Vector2.zero, textColor);
            var aspect = (float)texture.width / texture.height;
            height = Mathf.Min(height, maxWidth / aspect);
            var width = height * aspect;
            AttachQuad(parent, name, worldPosition, new Vector2(width, height), material);
        }

        private static void DigitQuad(GameObject parent, string name, Vector3 worldPosition, float height, Material material)
        {
            // numberstrip.png holds 10 digits of 51.2 x 64 px
            AttachQuad(parent, name, worldPosition, new Vector2(height * 0.8f, height), material);
        }

        private static void AttachQuad(GameObject parent, string name, Vector3 worldPosition, Vector2 size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;

            var p = parent.transform;
            var front = p.localPosition.z - p.localScale.z * 0.5f - 0.005f;
            quad.transform.SetParent(p, true);
            quad.transform.localRotation = Quaternion.identity; // face the same way as the panel/button it sits on
            quad.transform.position = p.parent.TransformPoint(new Vector3(worldPosition.x, worldPosition.y, front));
            quad.transform.localScale = new Vector3(size.x / p.lossyScale.x, size.y / p.lossyScale.y, 1f);
        }

        #endregion

        #region Assets

        private static Material CreateColorMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0.2f);
            return SaveMaterial(material, name);
        }

        private static Material CreateDigitMaterial(Material template, string name)
        {
            var material = new Material(template) { color = PanelTextColor };
            material.mainTextureScale = new Vector2(0.1f, 1f);
            material.mainTextureOffset = Vector2.zero;
            return SaveMaterial(material, name);
        }

        private static Material CreateCutoutMaterial(string name, Texture2D texture, Vector2 scale, Vector2 offset, Color? color = null)
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(DigitTemplatePath)) { color = color ?? TextColor };
            material.mainTexture = texture;
            material.mainTextureScale = scale;
            material.mainTextureOffset = offset;
            material.SetTextureScale("_EmissionMap", Vector2.one);
            material.SetTextureOffset("_EmissionMap", Vector2.zero);
            return SaveMaterial(material, name);
        }

        private static Material SaveMaterial(Material material, string name)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(material, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Asset path inside this (file:) package to a path on disk.</summary>
        private static string Physical(string assetPath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageRoot);
            return Path.Combine(package.resolvedPath, assetPath.Substring(PackageRoot.Length + 1));
        }

        /// <summary>Text as a file/asset name: letters and digits kept, everything else replaced.</summary>
        private static string SafeName(string text)
        {
            switch (text)
            {
                case "+": return "Plus";
                case "-": return "Minus";
            }
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        // 5x7 pixel font: A-Z, 0-9 and the punctuation used by the labels.
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            [':'] = new[] { ".....", "..#..", "..#..", ".....", "..#..", "..#..", "....." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", "..#..", "..#.." },
            [','] = new[] { ".....", ".....", ".....", ".....", "..#..", "..#..", ".#..." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['^'] = new[] { "..#..", ".#.#.", "#...#", ".....", ".....", ".....", "....." },
            ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
            [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
            ['='] = new[] { ".....", ".....", "#####", ".....", "#####", ".....", "....." },
        };

        /// <summary>
        /// Renders lines of text (white, transparent background) into a texture asset. Lines are stacked
        /// top to bottom, each 9 font pixels high and as wide as the longest line, so a texture with several
        /// lines can be used as an atlas (tiling 1/lineCount, see <see cref="DescriptionOffset"/>).
        /// </summary>
        private static Texture2D CreateTextTexture(string name, string[] lines, int px)
        {
            const int lineRows = 9;
            int maxLength = 0;
            foreach (var line in lines) maxLength = Mathf.Max(maxLength, line.Length);
            int cols = maxLength * 6 + 1, rows = lineRows * lines.Length;
            var texture = new Texture2D(cols * px, rows * px, TextureFormat.RGBA32, false);
            var pixels = new Color32[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 0);

            for (int l = 0; l < lines.Length; l++)
            for (int c = 0; c < lines[l].Length; c++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(lines[l][c]), out var glyph))
                    throw new ArgumentException($"No glyph for '{lines[l][c]}' in \"{lines[l]}\"");
                for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (glyph[gy][gx] != '#') continue;
                    // texture origin is bottom-left; line 0 is the top line
                    int x0 = (1 + c * 6 + gx) * px, y0 = (rows - 1 - l * lineRows - 1 - gy) * px;
                    for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        pixels[(y0 + y) * texture.width + x0 + x] = new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels);

            var path = $"{TextureFolder}/{name}.png";
            File.WriteAllBytes(Physical(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None; // keep the real aspect ratio
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void CreateOrUpdateModelDefinition(string scenePath)
        {
            var path = $"{Folder}/{SceneName}.modeldef.asset";
            var definition = AssetDatabase.LoadAssetAtPath<SampleModelDefinition>(path);
            var isNew = !definition;
            if (isNew)
                definition = ScriptableObject.CreateInstance<SampleModelDefinition>();

            definition.scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            definition.modelFolderName = SceneName;
            definition.fileName = SceneName;
            definition.label = "Performance Test";
            definition.description =
                "Measures graph execution cost. Pick a workload (math, value evaluation, variables, pointers, flow, " +
                "events or mixed) and a load level " +
                $"(1-9, {BaseIterations} * 2^(level-1) loop iterations per tick); the FPS counter and bar show the result. " +
                "AUTO RUN measures every workload, logs the results and shows a summary.";
            definition.tags = new[] { "interactivity", "test", "performance" };

            if (isNew)
                AssetDatabase.CreateAsset(definition, path);
            else
                EditorUtility.SetDirty(definition);
        }

        #endregion

        #region Graph

        /// <summary>Small helper that adds units in a readable grid and wires ports.</summary>
        private class GraphBuilder
        {
            public readonly FlowGraph graph = new FlowGraph();
            private float rowY;
            private float columnX;

            public void Row(float y) { rowY = y; columnX = 0; }

            public T Add<T>(T unit, float dx = 260f, float dy = 0f) where T : IUnit
            {
                unit.position = new Vector2(columnX, rowY + dy);
                columnX += dx;
                graph.units.Add(unit);
                return unit;
            }

            public void Declare<T>(string name, T value)
            {
                graph.variables.Set(name, value);
                graph.variables.GetDeclaration(name).typeHandle = new SerializableType(typeof(T).FullName);
            }

            public static void Flow(ControlOutput from, ControlInput to) => from.ConnectToValid(to);

            public static void Value(ValueOutput from, ValueInput to)
            {
                if (!from.CanValidlyConnectTo(to))
                    throw new InvalidOperationException($"Cannot connect {from.unit}.{from.key} ({from.type}) to {to.unit}.{to.key} ({to.type})");
                from.ConnectToValid(to);
            }

            // --- unit factories ---

            public ValueOutput Get(string variable)
            {
                var unit = Add(new GetVariable { kind = VariableKind.Graph }, 0f, 120f);
                unit.name.SetDefaultValue(variable);
                return unit.value;
            }

            public SetVariable Set(string variable, ValueOutput value)
            {
                var unit = Add(new SetVariable { kind = VariableKind.Graph });
                unit.name.SetDefaultValue(variable);
                Value(value, unit.input);
                return unit;
            }

            public ValueOutput Literal<T>(T value) => Add(new Literal(typeof(T), value), 0f, 220f).output;

            public ValueOutput Sum(ValueOutput a, ValueOutput b) => Binary(new ScalarSum(), a, b);
            public ValueOutput Sum(ValueOutput a, float b) => Sum(a, Literal(b));

            public ValueOutput GenericAdd(ValueOutput a, ValueOutput b) => Binary(new GenericSum(), a, b);

            private ValueOutput Binary<TUnit>(TUnit unit, ValueOutput a, ValueOutput b) where TUnit : Unit, IMultiInputUnit
            {
                Add(unit, 0f, 60f);
                Value(a, unit.multiInputs[0]);
                Value(b, unit.multiInputs[1]);
                return unit.valueOutputs["sum"];
            }

            public ValueOutput Sub(ValueOutput a, float b)
            {
                var unit = Add(new ScalarSubtract(), 0f, 60f);
                Value(a, unit.minuend);
                Value(Literal(b), unit.subtrahend);
                return unit.difference;
            }

            public ValueOutput Sub(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new ScalarSubtract(), 0f, 60f);
                Value(a, unit.minuend);
                Value(b, unit.subtrahend);
                return unit.difference;
            }

            public ValueOutput Sub(float a, ValueOutput b)
            {
                var unit = Add(new ScalarSubtract(), 0f, 60f);
                Value(Literal(a), unit.minuend);
                Value(b, unit.subtrahend);
                return unit.difference;
            }

            public ValueOutput Mul(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new ScalarMultiply(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.product;
            }

            public ValueOutput Mul(ValueOutput a, float b) => Mul(a, Literal(b));

            public ValueOutput Div(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new ScalarDivide(), 0f, 60f);
                Value(a, unit.dividend);
                Value(b, unit.divisor);
                return unit.quotient;
            }

            public ValueOutput Div(ValueOutput a, float b) => Div(a, Literal(b));

            public ValueOutput Mod(ValueOutput a, float b)
            {
                var unit = Add(new ScalarModulo(), 0f, 60f);
                Value(a, unit.dividend);
                Value(Literal(b), unit.divisor);
                return unit.remainder;
            }

            public ValueOutput IntMod(ValueOutput a, int b)
            {
                var unit = Add(new GenericModulo(), 0f, 60f);
                Value(a, unit.dividend);
                Value(Literal(b), unit.divisor);
                return unit.remainder;
            }

            public ValueOutput GreaterOrEqual(ValueOutput a, float b)
            {
                var unit = Add(new GreaterOrEqual(), 0f, 60f);
                Value(a, unit.a);
                Value(Literal(b), unit.b);
                return unit.comparison;
            }

            public ValueOutput Not(ValueOutput a)
            {
                var unit = Add(new Negate(), 0f, 60f);
                Value(a, unit.input);
                return unit.output;
            }

            /// <summary>Static call used as a pure value (Mathf.*, constructors, Quaternion.Euler).</summary>
            public ValueOutput Call(Type type, string method, Type[] parameterTypes, params ValueOutput[] args)
            {
                var unit = Add(new InvokeMember(new Member(type, method, parameterTypes)), 0f, 160f);
                for (int i = 0; i < args.Length; i++)
                    Value(args[i], unit.inputParameters[i]);
                return unit.result;
            }

            private static readonly Type[] F1 = { typeof(float) };
            private static readonly Type[] F2 = { typeof(float), typeof(float) };
            private static readonly Type[] F3 = { typeof(float), typeof(float), typeof(float) };
            private static readonly Type[] F4 = { typeof(float), typeof(float), typeof(float), typeof(float) };

            public ValueOutput Mathf1(string method, ValueOutput a) => Call(typeof(Mathf), method, F1, a);
            public ValueOutput Mathf2(string method, ValueOutput a, ValueOutput b) => Call(typeof(Mathf), method, F2, a, b);
            public ValueOutput Clamp(ValueOutput v, float min, float max) => Call(typeof(Mathf), nameof(Mathf.Clamp), F3, v, Literal(min), Literal(max));
            public ValueOutput RoundToInt(ValueOutput v) => Call(typeof(Mathf), nameof(Mathf.RoundToInt), F1, v);
            public ValueOutput Vec2(ValueOutput x, ValueOutput y) => Call(typeof(Vector2), ".ctor", F2, x, y);
            public ValueOutput Vec3(ValueOutput x, ValueOutput y, ValueOutput z) => Call(typeof(Vector3), ".ctor", F3, x, y, z);
            public ValueOutput NewColor(ValueOutput r, ValueOutput g, ValueOutput b, ValueOutput a) => Call(typeof(Color), ".ctor", F4, r, g, b, a);
            public ValueOutput Euler(ValueOutput x, ValueOutput y, ValueOutput z) => Call(typeof(Quaternion), nameof(Quaternion.Euler), F3, x, y, z);

            public InvokeMember SetDigit(Material material, ValueOutput digit)
            {
                var offset = Vec2(Mul(digit, 0.1f), Literal(0f));
                var unit = Add(new InvokeMember(new Member(typeof(Material), nameof(Material.SetTextureOffset),
                    new[] { typeof(string), typeof(Vector2) })));
                unit.target.SetDefaultValue(material);
                unit.inputParameters[0].SetDefaultValue("_MainTex");
                Value(offset, unit.inputParameters[1]);
                return unit;
            }

            public InvokeMember SetTextureOffset(Material material, Vector2 offset)
            {
                var unit = Add(new InvokeMember(new Member(typeof(Material), nameof(Material.SetTextureOffset),
                    new[] { typeof(string), typeof(Vector2) })));
                unit.target.SetDefaultValue(material);
                unit.inputParameters[0].SetDefaultValue("_MainTex");
                unit.inputParameters[1].SetDefaultValue(offset);
                return unit;
            }

            public ValueOutput GetMaterialColor(Material material)
            {
                var unit = Add(new GetMember(new Member(typeof(Material), nameof(Material.color))), 0f, 160f);
                unit.target.SetDefaultValue(material);
                return unit.value;
            }

            public ValueOutput GetTransform(Transform target, string member)
            {
                var unit = Add(new GetMember(new Member(typeof(Transform), member)), 0f, 160f);
                unit.target.SetDefaultValue(target);
                return unit.value;
            }

            /// <summary>Runs all <paramref name="targets"/> in order. Sequence units have at most 10 outputs,
            /// so larger lists are split over nested sequences.</summary>
            public void FanOut(ControlOutput from, IList<ControlInput> targets)
            {
                const int maxOutputs = 10;
                if (targets.Count <= maxOutputs)
                {
                    var sequence = Sequence(targets.Count);
                    Flow(from, sequence.enter);
                    for (int i = 0; i < targets.Count; i++)
                        Flow(sequence.multiOutputs[i], targets[i]);
                    return;
                }

                var groups = new List<ControlInput>();
                for (int start = 0; start < targets.Count; start += maxOutputs)
                {
                    var count = Mathf.Min(maxOutputs, targets.Count - start);
                    var group = Sequence(count);
                    for (int i = 0; i < count; i++)
                        Flow(group.multiOutputs[i], targets[start + i]);
                    groups.Add(group.enter);
                }
                FanOut(from, groups);
            }

            public SetMember SetMaterialColor(Material material, ValueOutput color)
            {
                var unit = Add(new SetMember(new Member(typeof(Material), nameof(Material.color))));
                unit.target.SetDefaultValue(material);
                Value(color, unit.input);
                return unit;
            }

            public SetMember SetMaterialColor(Material material, Color color)
            {
                var unit = Add(new SetMember(new Member(typeof(Material), nameof(Material.color))));
                unit.target.SetDefaultValue(material);
                unit.input.SetDefaultValue(color);
                return unit;
            }

            public SetMember SetTransform(Transform target, string member, ValueOutput value)
            {
                var unit = Add(new SetMember(new Member(typeof(Transform), member)));
                unit.target.SetDefaultValue(target);
                Value(value, unit.input);
                return unit;
            }

            public For Loop(ValueOutput count)
            {
                var unit = Add(new For());
                unit.firstIndex.SetDefaultValue(0);
                Value(count, unit.lastIndex);
                return unit;
            }

            public For Loop(int count)
            {
                var unit = Add(new For());
                unit.firstIndex.SetDefaultValue(0);
                unit.lastIndex.SetDefaultValue(count);
                return unit;
            }

            public Sequence Sequence(int outputs) => Add(new Sequence { outputCount = outputs });

            public SwitchOnInteger Switch(ValueOutput selector, params int[] options)
            {
                var unit = Add(new SwitchOnInteger { options = new List<int>(options) });
                Value(selector, unit.selector);
                return unit;
            }

            public ControlOutput Branch(SwitchOnInteger unit, int option) => unit.controlOutputs["%" + option];

            public void DeclareBoxed(string name, object value)
            {
                graph.variables.Set(name, value);
                graph.variables.GetDeclaration(name).typeHandle = new SerializableType(value.GetType().FullName);
            }

            public ValueOutput GenericMul(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new GenericMultiply(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.product;
            }

            public ValueOutput And(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new And(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.result;
            }

            public ValueOutput Vec3Add(ValueOutput a, ValueOutput b) => Binary(new Vector3Sum(), a, b);
            public ValueOutput Vec4Add(ValueOutput a, ValueOutput b) => Binary(new Vector4Sum(), a, b);

            public ValueOutput Cross(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new Vector3CrossProduct(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.crossProduct;
            }

            public ValueOutput Dot(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new Vector3DotProduct(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.dotProduct;
            }

            public ValueOutput Normalize3(ValueOutput v)
            {
                var unit = Add(new Vector3Normalize(), 0f, 60f);
                Value(v, unit.input);
                return unit.output;
            }

            public ValueOutput Normalize4(ValueOutput v)
            {
                var unit = Add(new Vector4Normalize(), 0f, 60f);
                Value(v, unit.input);
                return unit.output;
            }

            public ValueOutput Lerp3(ValueOutput a, ValueOutput b, float t)
            {
                var unit = Add(new Vector3Lerp(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                Value(Literal(t), unit.t);
                return unit.interpolation;
            }

            public ValueOutput Distance3(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new Vector3Distance(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.distance;
            }

            /// <summary>Instance property read, e.g. Vector3.magnitude or Matrix4x4.inverse.</summary>
            public ValueOutput Property(Type type, string property, ValueOutput target)
            {
                var unit = Add(new GetMember(new Member(type, property)), 0f, 160f);
                Value(target, unit.target);
                return unit.value;
            }

            /// <summary>Instance method used as a pure value, e.g. Matrix4x4.GetPosition().</summary>
            public ValueOutput Method(Type type, string method, Type[] parameterTypes, ValueOutput target, params ValueOutput[] args)
            {
                var unit = Add(new InvokeMember(new Member(type, method, parameterTypes)), 0f, 160f);
                Value(target, unit.target);
                for (int i = 0; i < args.Length; i++)
                    Value(args[i], unit.inputParameters[i]);
                return unit.result;
            }

            /// <summary>One variable/set node for several variables (UnityGLTF's Set Variables unit).</summary>
            public SetVariables SetMany(IList<(string name, ValueOutput value)> assignments)
            {
                var unit = Add(new SetVariables { kind = VariableKind.Graph, count = assignments.Count });
                for (int i = 0; i < assignments.Count; i++)
                {
                    unit.names[i].SetDefaultValue(assignments[i].name);
                    Value(assignments[i].value, unit.values[i]);
                }
                return unit;
            }

            public TriggerCustomEvent Trigger(string eventName, params ValueOutput[] args)
            {
                var unit = Add(new TriggerCustomEvent { argumentCount = args.Length });
                unit.name.SetDefaultValue(eventName);
                for (int i = 0; i < args.Length; i++)
                    Value(args[i], unit.arguments[i]);
                return unit;
            }

            public CustomEvent Receive(string eventName, int argumentCount)
            {
                var unit = Add(new CustomEvent { argumentCount = argumentCount });
                unit.name.SetDefaultValue(eventName);
                return unit;
            }

            public ValueOutput Less(ValueOutput a, float b)
            {
                var unit = Add(new Less(), 0f, 60f);
                Value(a, unit.a);
                Value(Literal(b), unit.b);
                return unit.comparison;
            }

            public class Chain
            {
                public ControlInput enter;
                public ControlOutput exit;
            }

            public class Branches
            {
                public ControlInput enter;
                public readonly List<ControlOutput> exits = new List<ControlOutput>();
            }

            /// <summary>Entry that runs <paramref name="action"/> only while no benchmark is running (benchKind == 0).</summary>
            public ControlInput WhenIdle(ControlInput action)
            {
                var idle = Switch(Get("benchKind"), 0);
                Flow(Branch(idle, 0), action);
                return idle.enter;
            }

            /// <summary>Sets the color of all <paramref name="materials"/>, one after another.</summary>
            public Chain SetColors(IEnumerable<Material> materials, Color color)
            {
                Chain chain = null;
                foreach (var material in materials)
                {
                    var set = SetMaterialColor(material, color);
                    if (chain == null) chain = new Chain { enter = set.assign };
                    else Flow(chain.exit, set.assign);
                    chain.exit = set.assigned;
                }
                return chain;
            }

            /// <summary>Shows 0..999 with three digit quads (hundreds, tens, ones).</summary>
            public Chain ShowNumber(Material[] digits, ValueOutput value)
            {
                var clamped = Clamp(value, 0f, 999f);
                var hundreds = SetDigit(digits[0], Mod(Mathf1(nameof(Mathf.Floor), Div(clamped, 100f)), 10f));
                var tens = SetDigit(digits[1], Mod(Mathf1(nameof(Mathf.Floor), Div(clamped, 10f)), 10f));
                var ones = SetDigit(digits[2], Mod(Mathf1(nameof(Mathf.Floor), clamped), 10f));
                Flow(hundreds.exit, tens.enter);
                Flow(tens.exit, ones.enter);
                return new Chain { enter = hundreds.enter, exit = ones.exit };
            }

            public DebugLogGltf Log(string message, params ValueOutput[] args)
            {
                var unit = Add(new DebugLogGltf { message = message, argumentCount = args.Length });
                for (int i = 0; i < args.Length; i++)
                    Value(args[i], unit.argumentPorts[i]);
                return unit;
            }

            /// <summary>One log per mode ("PERF &lt;mode id&gt;" + message), selected by the "mode" variable.</summary>
            public Branches LogPerMode(string message, params ValueOutput[] args) =>
                LogPerMode("mode", i => "PERF " + Modes[i].id + message, args);

            /// <summary>One log per mode, selected by the int variable <paramref name="selector"/>.</summary>
            public Branches LogPerMode(string selector, Func<int, string> message, params ValueOutput[] args)
            {
                var select = Switch(Get(selector), AllModes());
                var result = new Branches { enter = select.enter };
                for (int i = 0; i < Modes.Length; i++)
                {
                    var log = Log(message(i), args);
                    Flow(Branch(select, i), log.enter);
                    result.exits.Add(log.exit);
                }
                result.exits.Add(select.@default);
                return result;
            }

            /// <summary>Runs one action per mode, selected by the int variable <paramref name="selector"/>.</summary>
            public Branches PerMode(string selector, Func<int, (ControlInput enter, ControlOutput exit)> action)
            {
                var select = Switch(Get(selector), AllModes());
                var result = new Branches { enter = select.enter };
                for (int i = 0; i < Modes.Length; i++)
                {
                    var (enter, exit) = action(i);
                    Flow(Branch(select, i), enter);
                    result.exits.Add(exit);
                }
                result.exits.Add(select.@default);
                return result;
            }

            private static int[] AllModes()
            {
                var modes = new int[Modes.Length];
                for (int i = 0; i < modes.Length; i++) modes[i] = i;
                return modes;
            }

            public ValueOutput Less(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new Less(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.comparison;
            }

            public ValueOutput Greater(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new Greater(), 0f, 60f);
                Value(a, unit.a);
                Value(b, unit.b);
                return unit.comparison;
            }

            public ValueOutput IntOp(GltfIntBitwise.Operation operation, ValueOutput a, ValueOutput b = null)
            {
                var unit = Add(new GltfIntBitwise { operation = operation }, 0f, 60f);
                Value(a, unit.a);
                if (b != null) Value(b, unit.b);
                return unit.value;
            }

            public ValueOutput IntMul(ValueOutput a, ValueOutput b) => GenericMul(a, b);

            public ValueOutput IntDiv(ValueOutput a, ValueOutput b)
            {
                var unit = Add(new GenericDivide(), 0f, 60f);
                Value(a, unit.dividend);
                Value(b, unit.divisor);
                return unit.quotient;
            }

            public ValueOutput FloorToInt(ValueOutput v) => Call(typeof(Mathf), nameof(Mathf.FloorToInt), new[] { typeof(float) }, v);

            public VariableInterpolate InterpolateVariable(string variable, ValueOutput target, float duration)
            {
                var unit = Add(new VariableInterpolate { kind = VariableKind.Graph });
                unit.name.SetDefaultValue(variable);
                Value(target, unit.targetValue);
                unit.duration.SetDefaultValue(duration);
                return unit;
            }

            public InterpolateMember InterpolateTransform(Transform target, string member, ValueOutput value, float duration)
            {
                var unit = Add(new InterpolateMember(new Member(typeof(Transform), member)));
                unit.target.SetDefaultValue(target);
                Value(value, unit.input);
                unit.duration.SetDefaultValue(duration);
                return unit;
            }

            public While While(ValueOutput condition)
            {
                var unit = Add(new While());
                Value(condition, unit.condition);
                return unit;
            }

            public WaitForFlow WaitAll(int inputs)
            {
                return Add(new WaitForFlow { inputCount = inputs, resetOnExit = true });
            }

            public GltfSetDelay SetDelay(float duration)
            {
                var unit = Add(new GltfSetDelay());
                unit.duration.SetDefaultValue(duration);
                return unit;
            }

            public GltfCancelDelay CancelDelay(ValueOutput delay)
            {
                var unit = Add(new GltfCancelDelay());
                Value(delay, unit.delay);
                return unit;
            }

            public GltfThrottle Throttle(float duration)
            {
                var unit = Add(new GltfThrottle());
                unit.duration.SetDefaultValue(duration);
                return unit;
            }

            public GltfDoN DoN(int n)
            {
                var unit = Add(new GltfDoN());
                unit.n.SetDefaultValue(n);
                return unit;
            }

            public GltfMultiGate MultiGate(int outputs, bool loop) => Add(new GltfMultiGate { outputCount = outputs, isLoop = loop });

            /// <summary>Counter that wraps at 100000 (int variable += 1).</summary>
            public SetVariable Count(string variable) =>
                Set(variable, IntMod(GenericAdd(Get(variable), Literal(1)), 100000));

            public OnPointerClick OnClick(GameObject target)
            {
                var unit = Add(new OnPointerClick());
                unit.target.SetDefaultValue(target);
                return unit;
            }
        }

        // Variables read by the GET test and written by the SET / MULTISET tests (a variable that is never
        // written gets folded into a constant by the exporter, so all three tests share this set).
        private static readonly (string name, object value)[] TestVariables =
        {
            ("f0", 0.5f), ("f1", 1.5f), ("f2", 2.5f), ("f3", 3.5f), ("f4", 4.5f), ("f5", 5.5f), ("f6", 6.5f), ("f7", 7.5f),
            ("i0", 0), ("i1", 1), ("i2", 2), ("i3", 3),
            ("b0", true), ("b1", false),
            ("v2a", new Vector2(1f, 2f)), ("v2b", new Vector2(3f, 4f)),
            ("v3a", new Vector3(1f, 0f, 0f)), ("v3b", new Vector3(0f, 1f, 0f)), ("v3c", new Vector3(0f, 0f, 1f)), ("v3d", Vector3.one),
            ("v4a", new Vector4(1f, 2f, 3f, 4f)), ("v4b", new Vector4(4f, 3f, 2f, 1f)),
        };

        private static void BuildGraph(SceneRefs refs)
        {
            var g = new GraphBuilder();

            g.Declare("mode", 0);
            g.Declare("load", StartLoad);
            g.Declare("loopCount", 0);
            // live window (FPS display) and session since the last reset (average / worst)
            g.Declare("fpsFrames", 0f);
            g.Declare("fpsTime", 0f);
            g.Declare("fps", 0f);
            g.Declare("sessionFrames", 0f);
            g.Declare("sessionTime", 0f);
            g.Declare("sessionMaxDelta", 0f);
            // benchmark: 0 = off, 1 = auto run over all modes
            g.Declare("benchKind", 0);
            g.Declare("benchTimer", 0f);
            g.Declare("benchMeasuring", false);
            // AUTO RUN summary (over the modes in InSummary)
            g.Declare("runSumLogFps", 0f);
            g.Declare("runCount", 0f);
            g.Declare("runStartTime", 0f);
            g.Declare("runMinFps", 0f);
            g.Declare("runMinMode", 0);
            g.Declare("runMaxFps", 0f);
            g.Declare("runMaxMode", 0);

            // ---------------------------------------------------------------- Reset statistics
            g.Row(-2400f);
            var resetStats = g.SetMany(new List<(string, ValueOutput)>
            {
                ("sessionFrames", g.Literal(0f)), ("sessionTime", g.Literal(0f)), ("sessionMaxDelta", g.Literal(0f)),
                ("fpsFrames", g.Literal(0f)), ("fpsTime", g.Literal(0f)),
            });
            var resetClick = g.OnClick(refs.resetButton);
            GraphBuilder.Flow(resetClick.trigger, g.WhenIdle(resetStats.assign));

            var resetWorstClick = g.OnClick(refs.resetWorstButton);
            var resetWorst = g.Set("sessionMaxDelta", g.Literal(0f));
            GraphBuilder.Flow(resetWorstClick.trigger, g.WhenIdle(resetWorst.assign));
            var showWorstReset = g.ShowNumber(refs.worstMsDigits, g.Literal(0f));
            GraphBuilder.Flow(resetWorst.assigned, showWorstReset.enter);

            // ---------------------------------------------------------------- Refresh
            // Shared chain entered on start and after every mode/load change: colors all buttons (greyed out while
            // a benchmark runs, the running benchmark and the measured mode stay highlighted), shows the
            // description, recomputes loopCount, shows the load digit and resets the statistics.
            g.Row(-2000f);
            var buttonMaterials = new List<Material>(refs.modeButtonMaterials)
            {
                refs.autoButtonMaterial, refs.resetButtonMaterial, refs.loadButtonMaterial,
            };
            var benchIdle = g.Switch(g.Get("benchKind"), 0);
            var refreshEntry = benchIdle.enter;
            var normalColors = g.SetColors(buttonMaterials, ButtonColor);
            var disabledColors = g.SetColors(buttonMaterials, ButtonDisabledColor);
            GraphBuilder.Flow(g.Branch(benchIdle, 0), normalColors.enter);
            GraphBuilder.Flow(benchIdle.@default, disabledColors.enter);

            var benchHighlight = g.Switch(g.Get("benchKind"), 1);
            GraphBuilder.Flow(normalColors.exit, benchHighlight.enter);
            GraphBuilder.Flow(disabledColors.exit, benchHighlight.enter);
            var allModes = new int[Modes.Length];
            for (int i = 0; i < allModes.Length; i++) allModes[i] = i;
            var highlightSwitch = g.Switch(g.Get("mode"), allModes);
            var autoActive = g.SetMaterialColor(refs.autoButtonMaterial, ButtonActiveColor);
            GraphBuilder.Flow(g.Branch(benchHighlight, 1), autoActive.assign);
            GraphBuilder.Flow(autoActive.assigned, highlightSwitch.enter);
            GraphBuilder.Flow(benchHighlight.@default, highlightSwitch.enter);

            // every mode between idle and mixed is one workload; Mixed splits the load over the workloads in it
            var mixedCount = 0;
            for (int i = 1; i < MixedMode; i++)
                if (Modes[i].inMixed) mixedCount++;
            var iterations = g.Mul(g.Mathf2(nameof(Mathf.Pow), g.Literal(2f), g.Sub(g.Get("load"), 1f)), BaseIterations);
            var loopSwitch = g.Switch(g.Get("mode"), MixedMode);
            var setShare = g.Set("loopCount", g.RoundToInt(g.Mathf2(nameof(Mathf.Max), g.Mul(iterations, 1f / mixedCount), g.Literal(1f))));
            var setFull = g.Set("loopCount", g.RoundToInt(iterations));
            GraphBuilder.Flow(g.Branch(loopSwitch, MixedMode), setShare.assign);
            GraphBuilder.Flow(loopSwitch.@default, setFull.assign);

            var showLoad = g.SetDigit(refs.loadDigit, g.Get("load"));
            GraphBuilder.Flow(setShare.assigned, showLoad.enter);
            GraphBuilder.Flow(setFull.assigned, showLoad.enter);
            var refreshResetStats = g.SetMany(new List<(string, ValueOutput)>
            {
                ("sessionFrames", g.Literal(0f)), ("sessionTime", g.Literal(0f)), ("sessionMaxDelta", g.Literal(0f)),
                ("fpsFrames", g.Literal(0f)), ("fpsTime", g.Literal(0f)),
            });
            GraphBuilder.Flow(showLoad.exit, refreshResetStats.assign);

            g.Row(-1700f);
            for (int i = 0; i < Modes.Length; i++)
            {
                var highlight = g.SetMaterialColor(refs.modeButtonMaterials[i], ButtonActiveColor);
                var describe = g.SetTextureOffset(refs.description, DescriptionOffset(i));
                GraphBuilder.Flow(g.Branch(highlightSwitch, i), highlight.assign);
                GraphBuilder.Flow(highlight.assigned, describe.enter);
                GraphBuilder.Flow(describe.exit, loopSwitch.enter);
            }
            GraphBuilder.Flow(highlightSwitch.@default, loopSwitch.enter);

            // ---------------------------------------------------------------- Menu input
            g.Row(-1300f);
            var start = g.Add(new Start());
            GraphBuilder.Flow(start.trigger, refreshEntry);

            // mode and load buttons are disabled while a benchmark runs
            for (int i = 0; i < Modes.Length; i++)
            {
                var click = g.OnClick(refs.modeButtons[i]);
                var setMode = g.Set("mode", g.Literal(i));
                GraphBuilder.Flow(click.trigger, g.WhenIdle(setMode.assign));
                GraphBuilder.Flow(setMode.assigned, refreshEntry);
            }

            g.Row(-1000f);
            var minus = g.OnClick(refs.loadMinus);
            var decrease = g.Set("load", g.Mathf2(nameof(Mathf.Max), g.Sub(g.Get("load"), 1f), g.Literal(MinLoad)));
            GraphBuilder.Flow(minus.trigger, g.WhenIdle(decrease.assign));
            GraphBuilder.Flow(decrease.assigned, refreshEntry);

            var plus = g.OnClick(refs.loadPlus);
            var increase = g.Set("load", g.Mathf2(nameof(Mathf.Min), g.Sum(g.Get("load"), 1f), g.Literal(MaxLoad)));
            GraphBuilder.Flow(plus.trigger, g.WhenIdle(increase.assign));
            GraphBuilder.Flow(increase.assigned, refreshEntry);

            // benchmark start: AUTO runs every mode from idle to mixed at the current load
            var restartStep = g.SetMany(new List<(string, ValueOutput)> { ("benchTimer", g.Literal(0f)), ("benchMeasuring", g.Literal(false)) });
            GraphBuilder.Flow(restartStep.assigned, refreshEntry);

            // a second click on the running benchmark button stops it
            var stopBench = g.Set("benchKind", g.Literal(0));
            var stoppedLog = g.Log("PERF benchmark stopped");
            GraphBuilder.Flow(stopBench.assigned, stoppedLog.enter);
            GraphBuilder.Flow(stoppedLog.exit, refreshEntry);

            var autoClick = g.OnClick(refs.autoButton);
            var autoState = g.Switch(g.Get("benchKind"), 0, 1);
            GraphBuilder.Flow(autoClick.trigger, autoState.enter);
            var startTime = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var startAuto = g.SetMany(new List<(string, ValueOutput)>
            {
                ("benchKind", g.Literal(1)), ("mode", g.Literal(0)),
                ("runSumLogFps", g.Literal(0f)), ("runCount", g.Literal(0f)), ("runStartTime", startTime),
                ("runMinFps", g.Literal(100000f)), ("runMinMode", g.Literal(0)),
                ("runMaxFps", g.Literal(0f)), ("runMaxMode", g.Literal(0)),
            });
            GraphBuilder.Flow(g.Branch(autoState, 0), startAuto.assign);
            GraphBuilder.Flow(g.Branch(autoState, 1), stopBench.assign);
            var clearResults = g.SetColors(refs.resultStrips, ResultUnmeasuredColor);
            GraphBuilder.Flow(startAuto.assigned, clearResults.enter);
            var startLog = g.Log($"PERF AUTO RUN START: load {{0}} ({BaseIterations} x 2^(load-1) loops per frame), {Modes.Length} tests, " +
                                 $"{BenchWarmup} s warmup + {BenchMeasure} s measure each", g.Get("load"));
            GraphBuilder.Flow(clearResults.exit, startLog.enter);
            GraphBuilder.Flow(startLog.exit, restartStep.assign);

            // ---------------------------------------------------------------- Tick
            g.Row(-600f);
            var update = g.Add(new Update());
            var tick = g.Sequence(4);
            GraphBuilder.Flow(update.trigger, tick.enter);

            var deltaTime = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.deltaTime))), 0f, 200f).value;
            var count = g.SetMany(new List<(string, ValueOutput)>
            {
                ("fpsFrames", g.Sum(g.Get("fpsFrames"), 1f)),
                ("fpsTime", g.Sum(g.Get("fpsTime"), deltaTime)),
                ("sessionFrames", g.Sum(g.Get("sessionFrames"), 1f)),
                ("sessionTime", g.Sum(g.Get("sessionTime"), deltaTime)),
                ("sessionMaxDelta", g.Mathf2(nameof(Mathf.Max), g.Get("sessionMaxDelta"), deltaTime)),
            });
            var sampleDue = g.Add(new If());
            GraphBuilder.Value(g.GreaterOrEqual(g.Get("fpsTime"), FpsSampleInterval), sampleDue.condition);
            GraphBuilder.Flow(tick.multiOutputs[0], count.assign);
            GraphBuilder.Flow(count.assigned, sampleDue.enter);

            var setFps = g.Set("fps", g.Div(g.Get("fpsFrames"), g.Get("fpsTime")));
            GraphBuilder.Flow(sampleDue.ifTrue, setFps.assign);

            var fps = g.Get("fps");
            var showFps = g.ShowNumber(refs.fpsDigits, fps);
            GraphBuilder.Flow(setFps.assigned, showFps.enter);
            var showAvg = g.ShowNumber(refs.avgFpsDigits, SessionAverageFps(g));
            GraphBuilder.Flow(showFps.exit, showAvg.enter);
            var showWorst = g.ShowNumber(refs.worstMsDigits, SessionWorstMs(g));
            GraphBuilder.Flow(showAvg.exit, showWorst.enter);

            var barScale = g.SetTransform(refs.fpsBar, nameof(Transform.localScale),
                g.Vec3(g.Mul(g.Clamp(g.Div(fps, FpsBarFull), 0.01f, 1f), FpsBarWidth), g.Literal(1f), g.Literal(1f)));
            GraphBuilder.Flow(showWorst.exit, barScale.assign);

            var barColor = g.SetMaterialColor(refs.fpsBarMaterial, FpsColor(g, fps));
            GraphBuilder.Flow(barScale.assigned, barColor.assign);

            var resetWindow = g.SetMany(new List<(string, ValueOutput)> { ("fpsFrames", g.Literal(0f)), ("fpsTime", g.Literal(0f)) });
            GraphBuilder.Flow(barColor.assigned, resetWindow.assign);

            g.Row(-300f);
            var time = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var spin = g.SetTransform(refs.spinner, nameof(Transform.localRotation),
                g.Euler(g.Mul(time, 25f), g.Mul(time, 90f), g.Literal(0f)));
            GraphBuilder.Flow(tick.multiOutputs[1], spin.assign);

            var workloadModes = new int[MixedMode];
            for (int i = 0; i < workloadModes.Length; i++) workloadModes[i] = i + 1;
            var workload = g.Switch(g.Get("mode"), workloadModes);
            GraphBuilder.Flow(tick.multiOutputs[2], workload.enter);

            foreach (var (name, value) in TestVariables)
                g.DeclareBoxed(name, value);

            var mixedTargets = new List<ControlInput>();
            for (int i = 1; i < MixedMode; i++)
            {
                if (Modes[i].build == null)
                    throw new InvalidOperationException($"Mode {Modes[i].id} has no workload");
                var path = Modes[i].build(g, refs);
                GraphBuilder.Flow(g.Branch(workload, i), path);
                if (Modes[i].inMixed)
                    mixedTargets.Add(path);
            }
            g.FanOut(g.Branch(workload, MixedMode), mixedTargets);

            BuildBenchmarkStep(g, refs, tick.multiOutputs[3], refreshEntry, restartStep.assign, resetStats.assign);

            var machine = refs.graphObject.AddComponent<ScriptMachine>();
            machine.nest.SwitchToEmbed(g.graph);
        }

        private static ValueOutput SessionAverageFps(GraphBuilder g) =>
            g.Div(g.Get("sessionFrames"), g.Mathf2(nameof(Mathf.Max), g.Get("sessionTime"), g.Literal(0.0001f)));

        private static ValueOutput SessionAverageMs(GraphBuilder g) =>
            g.Mul(g.Div(g.Get("sessionTime"), g.Mathf2(nameof(Mathf.Max), g.Get("sessionFrames"), g.Literal(1f))), 1000f);

        private static ValueOutput SessionWorstMs(GraphBuilder g) => g.Mul(g.Get("sessionMaxDelta"), 1000f);

        /// <summary>Red below 20 fps, green from 60 fps.</summary>
        private static ValueOutput FpsColor(GraphBuilder g, ValueOutput fps)
        {
            var health = g.Clamp(g.Div(g.Sub(fps, 20f), 40f), 0f, 1f);
            return g.NewColor(g.Sub(1f, health), health, g.Literal(0.15f), g.Literal(1f));
        }

        /// <summary>Modes that are part of the AUTO RUN summary: everything except the idle and loop baselines.</summary>
        private static bool InSummary(int mode) => mode == MixedMode || (Modes[mode].build != null && Modes[mode].inMixed);

        /// <summary>
        /// Benchmark state machine, run every tick while benchKind != 0: after the warmup the statistics are
        /// reset, after warmup + measure the step is logged and the next step starts.
        /// </summary>
        private static void BuildBenchmarkStep(GraphBuilder g, SceneRefs refs, ControlOutput tick, ControlInput refresh,
            ControlInput restartStep, ControlInput resetStats)
        {
            g.Row(4800f);
            var running = g.Switch(g.Get("benchKind"), 1);
            GraphBuilder.Flow(tick, running.enter);

            var deltaTime = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.deltaTime))), 0f, 200f).value;
            var advance = g.Set("benchTimer", g.Sum(g.Get("benchTimer"), deltaTime));
            GraphBuilder.Flow(g.Branch(running, 1), advance.assign);

            var stepDone = g.Add(new If());
            GraphBuilder.Value(g.GreaterOrEqual(g.Get("benchTimer"), BenchWarmup + BenchMeasure), stepDone.condition);
            GraphBuilder.Flow(advance.assigned, stepDone.enter);

            // warmup over: reset statistics once, from now on we measure
            var warmupDone = g.Add(new If());
            GraphBuilder.Value(g.And(g.Not(g.Get("benchMeasuring")), g.GreaterOrEqual(g.Get("benchTimer"), BenchWarmup)),
                warmupDone.condition);
            GraphBuilder.Flow(stepDone.ifFalse, warmupDone.enter);
            var startMeasuring = g.Set("benchMeasuring", g.Literal(true));
            GraphBuilder.Flow(warmupDone.ifTrue, startMeasuring.assign);
            GraphBuilder.Flow(startMeasuring.assigned, resetStats);


            // AUTO: log this mode, color its result strip, add it to the summary, then continue with the next mode
            g.Row(5100f);
            var avgFps = SessionAverageFps(g);
            var autoLog = g.LogPerMode("mode",
                i => $"PERF [{i + 1}/{Modes.Length}] {Modes[i].id}{(InSummary(i) ? "" : " (baseline)")}: " +
                     "load {0}, avg fps {1}, avg ms {2}, worst ms {3}, frames {4}",
                g.Get("load"), avgFps, SessionAverageMs(g), SessionWorstMs(g), g.Get("sessionFrames"));
            GraphBuilder.Flow(stepDone.ifTrue, autoLog.enter);

            var resultColor = FpsColor(g, avgFps);
            var showResult = g.PerMode("mode", i =>
            {
                var set = g.SetMaterialColor(refs.resultStrips[i], resultColor);
                return (set.assign, set.assigned);
            });
            foreach (var exit in autoLog.exits)
                GraphBuilder.Flow(exit, showResult.enter);

            var nextMode = g.Set("mode", g.GenericAdd(g.Get("mode"), g.Literal(1)));
            var baselines = new List<int>();
            for (int i = 0; i < Modes.Length; i++)
                if (!InSummary(i)) baselines.Add(i);
            var summarize = g.Switch(g.Get("mode"), baselines.ToArray());
            foreach (var exit in showResult.exits)
                GraphBuilder.Flow(exit, summarize.enter);
            foreach (var baseline in baselines)
                GraphBuilder.Flow(g.Branch(summarize, baseline), nextMode.assign);

            var accumulate = g.SetMany(new List<(string, ValueOutput)>
            {
                ("runSumLogFps", g.Sum(g.Get("runSumLogFps"), g.Mathf1(nameof(Mathf.Log), g.Mathf2(nameof(Mathf.Max), avgFps, g.Literal(0.001f))))),
                ("runCount", g.Sum(g.Get("runCount"), 1f)),
            });
            GraphBuilder.Flow(summarize.@default, accumulate.assign);
            var isSlowest = g.Add(new If());
            GraphBuilder.Value(g.Less(avgFps, g.Get("runMinFps")), isSlowest.condition);
            GraphBuilder.Flow(accumulate.assigned, isSlowest.enter);
            var rememberSlowest = g.SetMany(new List<(string, ValueOutput)> { ("runMinFps", avgFps), ("runMinMode", g.Get("mode")) });
            GraphBuilder.Flow(isSlowest.ifTrue, rememberSlowest.assign);
            var isFastest = g.Add(new If());
            GraphBuilder.Value(g.Greater(avgFps, g.Get("runMaxFps")), isFastest.condition);
            GraphBuilder.Flow(isSlowest.ifFalse, isFastest.enter);
            GraphBuilder.Flow(rememberSlowest.assigned, isFastest.enter);
            var rememberFastest = g.SetMany(new List<(string, ValueOutput)> { ("runMaxFps", avgFps), ("runMaxMode", g.Get("mode")) });
            GraphBuilder.Flow(isFastest.ifTrue, rememberFastest.assign);
            GraphBuilder.Flow(isFastest.ifFalse, nextMode.assign);
            GraphBuilder.Flow(rememberFastest.assigned, nextMode.assign);

            var allDone = g.Switch(g.Get("mode"), Modes.Length);
            GraphBuilder.Flow(nextMode.assigned, allDone.enter);
            GraphBuilder.Flow(allDone.@default, restartStep);

            // all modes done: summary in the log and in the scene
            g.Row(5250f);
            var meanFps = g.Mathf1(nameof(Mathf.Exp), g.Div(g.Get("runSumLogFps"), g.Mathf2(nameof(Mathf.Max), g.Get("runCount"), g.Literal(1f))));
            var now = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var summaryLog = g.Log("PERF AUTO RUN SUMMARY: load {0}, {1} tests (without baselines), mean fps {2} (geometric), " +
                                   "min fps {3}, max fps {4}, total time {5} s",
                g.Get("load"), g.Get("runCount"), meanFps, g.Get("runMinFps"), g.Get("runMaxFps"), g.Sub(now, g.Get("runStartTime")));
            GraphBuilder.Flow(g.Branch(allDone, Modes.Length), summaryLog.enter);
            var slowestLog = g.LogPerMode("runMinMode", i => $"PERF SLOWEST: {Modes[i].id} ({Modes[i].description}) with {{0}} fps", g.Get("runMinFps"));
            GraphBuilder.Flow(summaryLog.exit, slowestLog.enter);
            var fastestLog = g.LogPerMode("runMaxMode", i => $"PERF FASTEST: {Modes[i].id} ({Modes[i].description}) with {{0}} fps", g.Get("runMaxFps"));
            foreach (var exit in slowestLog.exits)
                GraphBuilder.Flow(exit, fastestLog.enter);

            var showMean = g.ShowNumber(refs.summaryMeanDigits, meanFps);
            foreach (var exit in fastestLog.exits)
                GraphBuilder.Flow(exit, showMean.enter);
            var showMin = g.ShowNumber(refs.summaryMinDigits, g.Get("runMinFps"));
            GraphBuilder.Flow(showMean.exit, showMin.enter);
            var showSlowest = g.PerMode("runMinMode", i =>
            {
                var set = g.SetTextureOffset(refs.slowestDescription, DescriptionOffset(i));
                return (set.enter, set.exit);
            });
            GraphBuilder.Flow(showMin.exit, showSlowest.enter);

            var finishAuto = g.SetMany(new List<(string, ValueOutput)> { ("benchKind", g.Literal(0)), ("mode", g.Literal(0)) });
            foreach (var exit in showSlowest.exits)
                GraphBuilder.Flow(exit, finishAuto.assign);
            var autoDoneLog = g.Log("PERF AUTO RUN DONE");
            GraphBuilder.Flow(finishAuto.assigned, autoDoneLog.enter);
            GraphBuilder.Flow(autoDoneLog.exit, refresh);

        }

        /// <summary>Basic math: a dependent chain of float math per iteration (sin, cos, sqrt, pow, abs, modulo).</summary>
        private static ControlInput BuildBasicMathPath(GraphBuilder g)
        {
            g.Row(200f);
            g.Declare("mathAcc", 0.5f);
            var loop = g.Loop(g.Get("loopCount"));
            var acc = g.Get("mathAcc");
            var s = g.Mathf1(nameof(Mathf.Sin), g.Sum(g.Mul(acc, 1.37f), 0.5f));
            var c = g.Mathf1(nameof(Mathf.Cos), g.Mul(acc, 0.73f));
            var r = g.Mathf1(nameof(Mathf.Sqrt), g.Sum(g.Mathf1(nameof(Mathf.Abs), g.Mul(s, c)), 1f));
            var mixedValue = g.Sum(s, g.Mul(c, g.Mathf2(nameof(Mathf.Pow), r, g.Literal(1.5f))));
            var next = g.Mod(g.Sum(g.Mul(g.Mathf1(nameof(Mathf.Abs), mixedValue), 3.1f), g.Mul(acc, 0.01f)), 10f);
            var store = g.Set("mathAcc", next);
            GraphBuilder.Flow(loop.body, store.assign);
            return loop.enter;
        }

        /// <summary>Vector math: cross, dot, normalize, lerp, quaternion rotation, distance, length on Vector3 and Vector4.</summary>
        private static ControlInput BuildVectorMathPath(GraphBuilder g)
        {
            g.Row(500f);
            g.Declare("vecA", new Vector3(1f, 0.2f, 0f));
            g.Declare("vecB", new Vector3(0f, 1f, 0.3f));
            g.Declare("vec4A", new Vector4(1f, 2f, 3f, 4f));
            g.Declare("vecLen", 0f);

            var loop = g.Loop(g.Get("loopCount"));
            var a = g.Get("vecA");
            var b = g.Get("vecB");
            var cross = g.Cross(a, b);
            var n = g.Normalize3(g.Vec3Add(cross, g.Literal(new Vector3(0.01f, 0.02f, 0.03f))));
            var dot = g.Dot(a, n);
            var rotation = g.Call(typeof(Quaternion), nameof(Quaternion.AngleAxis), new[] { typeof(float), typeof(Vector3) },
                g.Sum(g.Mul(dot, 10f), 1f), g.Literal(Vector3.up));
            var rotated = g.GenericMul(rotation, g.Lerp3(a, n, 0.3f));

            var setA = g.Set("vecA", g.Normalize3(g.Vec3Add(rotated, g.Literal(new Vector3(0.001f, 0f, 0f)))));
            var setB = g.Set("vecB", g.Normalize3(g.Vec3Add(g.Lerp3(b, cross, 0.5f), g.Literal(new Vector3(0f, 0.001f, 0f)))));
            var setLen = g.Set("vecLen", g.Sum(g.Distance3(a, b), g.Property(typeof(Vector3), nameof(Vector3.magnitude), cross)));
            var setV4 = g.Set("vec4A", g.Normalize4(g.Vec4Add(g.Get("vec4A"), g.Literal(new Vector4(0.1f, 0.2f, 0.3f, 0.4f)))));

            GraphBuilder.Flow(loop.body, setA.assign);
            GraphBuilder.Flow(setA.assigned, setB.assign);
            GraphBuilder.Flow(setB.assigned, setLen.assign);
            GraphBuilder.Flow(setLen.assigned, setV4.assign);
            return loop.enter;
        }

        /// <summary>Matrix math: TRS, matrix multiply, transpose, inverse, determinant and position extraction.</summary>
        private static ControlInput BuildMatrixMathPath(GraphBuilder g)
        {
            g.Row(800f);
            g.Declare("matAcc", Matrix4x4.identity);
            g.Declare("matDet", 0f);
            g.Declare("matPos", Vector3.zero);

            var loop = g.Loop(g.Get("loopCount"));
            // pure rotation step, so the accumulated matrix stays orthonormal
            var step = g.Call(typeof(Matrix4x4), nameof(Matrix4x4.TRS), new[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3) },
                g.Literal(Vector3.zero), g.Euler(g.Literal(1f), g.Literal(2f), g.Literal(3f)), g.Literal(Vector3.one));
            var setAcc = g.Set("matAcc", g.GenericMul(g.Get("matAcc"), step));

            var m = g.Get("matAcc");
            var transposed = g.Property(typeof(Matrix4x4), nameof(Matrix4x4.transpose), m);
            var inverted = g.Property(typeof(Matrix4x4), nameof(Matrix4x4.inverse), transposed);
            var setDet = g.Set("matDet", g.Property(typeof(Matrix4x4), nameof(Matrix4x4.determinant), inverted));
            var withOffset = g.GenericMul(inverted, g.Call(typeof(Matrix4x4), nameof(Matrix4x4.TRS),
                new[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3) },
                g.Literal(new Vector3(1f, 2f, 3f)), g.Literal(Quaternion.identity), g.Literal(Vector3.one)));
            var setPos = g.Set("matPos", g.Method(typeof(Matrix4x4), nameof(Matrix4x4.GetPosition), Type.EmptyTypes, withOffset));

            GraphBuilder.Flow(loop.body, setAcc.assign);
            GraphBuilder.Flow(setAcc.assigned, setDet.assign);
            GraphBuilder.Flow(setDet.assigned, setPos.assign);
            return loop.enter;
        }

        /// <summary>Flow: branching only (sequence, switch, if, nested loop) with almost no value work.</summary>
        private static ControlInput BuildFlowPath(GraphBuilder g)
        {
            g.Row(1100f);
            g.Declare("flowFlag", false);
            var loop = g.Loop(g.Get("loopCount"));
            var body = g.Sequence(2);
            GraphBuilder.Flow(loop.body, body.enter);

            var select = g.Switch(g.IntMod(loop.currentIndex, 4), 0, 1, 2, 3);
            GraphBuilder.Flow(body.multiOutputs[0], select.enter);

            // Sequences need connected outputs, the exporter removes sequences with less than two. Each output
            // ends in a branch without outputs: cheap, and no clean up pass removes it.
            ControlInput Terminal()
            {
                var terminal = g.Add(new If());
                GraphBuilder.Value(g.Get("flowFlag"), terminal.condition);
                return terminal.enter;
            }
            ControlInput SequenceOfTerminals(int outputs)
            {
                var sequence = g.Sequence(outputs);
                foreach (var output in sequence.multiOutputs)
                    GraphBuilder.Flow(output, Terminal());
                return sequence.enter;
            }

            GraphBuilder.Flow(g.Branch(select, 0), SequenceOfTerminals(3));

            var check = g.Add(new If());
            GraphBuilder.Value(g.Get("flowFlag"), check.condition);
            GraphBuilder.Flow(g.Branch(select, 1), check.enter);

            GraphBuilder.Flow(g.Branch(select, 2), g.Loop(3).enter);

            var toggle = g.Set("flowFlag", g.Not(g.Get("flowFlag")));
            GraphBuilder.Flow(g.Branch(select, 3), toggle.assign);

            var branch = g.Add(new If());
            GraphBuilder.Value(g.Get("flowFlag"), branch.condition);
            GraphBuilder.Flow(body.multiOutputs[1], branch.enter);
            GraphBuilder.Flow(branch.ifTrue, SequenceOfTerminals(2));
            GraphBuilder.Flow(branch.ifFalse, SequenceOfTerminals(2));
            return loop.enter;
        }

        /// <summary>Variable Get: reads all 22 test variables (float, int, bool, Vector2/3/4) per iteration,
        /// combined into one sink write per type.</summary>
        private static ControlInput BuildVariableGetPath(GraphBuilder g)
        {
            g.Row(1400f);
            g.Declare("sinkF", 0f);
            g.Declare("sinkI", 0);
            g.Declare("sinkB", false);
            g.Declare("sinkV2", Vector2.zero);
            g.Declare("sinkV3", Vector3.zero);
            g.Declare("sinkV4", Vector4.zero);

            var loop = g.Loop(g.Get("loopCount"));

            var floats = g.Get("f0");
            for (int i = 1; i < 8; i++) floats = g.Sum(floats, g.Get("f" + i));
            var ints = g.GenericAdd(g.GenericAdd(g.Get("i0"), g.Get("i1")), g.GenericAdd(g.Get("i2"), g.Get("i3")));
            var bools = g.And(g.Get("b0"), g.Get("b1"));
            var vec2 = g.GenericAdd(g.Get("v2a"), g.Get("v2b"));
            var vec3 = g.Vec3Add(g.Vec3Add(g.Get("v3a"), g.Get("v3b")), g.Vec3Add(g.Get("v3c"), g.Get("v3d")));
            var vec4 = g.Vec4Add(g.Get("v4a"), g.Get("v4b"));

            var sinks = new[]
            {
                g.Set("sinkF", floats), g.Set("sinkI", ints), g.Set("sinkB", bools),
                g.Set("sinkV2", vec2), g.Set("sinkV3", vec3), g.Set("sinkV4", vec4),
            };
            GraphBuilder.Flow(loop.body, sinks[0].assign);
            for (int i = 1; i < sinks.Length; i++)
                GraphBuilder.Flow(sinks[i - 1].assigned, sinks[i].assign);
            return loop.enter;
        }

        /// <summary>Values written by the Set tests; ints follow the loop index so the writes are not constant.</summary>
        private static ValueOutput TestValue(GraphBuilder g, string name, object initial, ValueOutput index)
        {
            if (initial is int)
                return g.GenericAdd(index, g.Literal((int)initial));
            switch (initial)
            {
                case float f: return g.Literal(f + 0.25f);
                case bool b: return g.Literal(!b);
                case Vector2 v: return g.Literal(v * 1.5f);
                case Vector3 v: return g.Literal(v * 1.5f);
                case Vector4 v: return g.Literal(v * 1.5f);
                default: throw new ArgumentException($"Unsupported test variable type for {name}");
            }
        }

        /// <summary>Variable Set: one variable/set node per test variable (22 sets per iteration).</summary>
        private static ControlInput BuildVariableSetPath(GraphBuilder g)
        {
            g.Row(1700f);
            var loop = g.Loop(g.Get("loopCount"));
            ControlOutput previous = loop.body;
            foreach (var (name, value) in TestVariables)
            {
                var set = g.Set(name, TestValue(g, name, value, loop.currentIndex));
                GraphBuilder.Flow(previous, set.assign);
                previous = set.assigned;
            }
            return loop.enter;
        }

        /// <summary>Variable Multi Set: a single variable/set node writing all 22 test variables at once.</summary>
        private static ControlInput BuildVariableMultiSetPath(GraphBuilder g)
        {
            g.Row(2000f);
            var loop = g.Loop(g.Get("loopCount"));
            var values = new List<(string, ValueOutput)>();
            foreach (var (name, value) in TestVariables)
                values.Add((name, TestValue(g, name, value, loop.currentIndex)));
            var setAll = g.SetMany(values);
            GraphBuilder.Flow(loop.body, setAll.assign);
            return loop.enter;
        }

        /// <summary>Events: per iteration sends six different custom events (no argument, float, int, Vector3,
        /// three mixed arguments, and a relay that triggers a second event from its receiver).</summary>
        private static ControlInput BuildEventPath(GraphBuilder g)
        {
            g.Row(2300f);
            g.Declare("evPingCount", 0);
            g.Declare("evFloatSum", 0f);
            g.Declare("evIntSum", 0);
            g.Declare("evVec", Vector3.zero);
            g.Declare("evMultiF", 0f);
            g.Declare("evMultiB", false);
            g.Declare("evMultiV", Vector3.zero);
            g.Declare("evRelayCount", 0);

            var loop = g.Loop(g.Get("loopCount"));
            var sends = new[]
            {
                g.Trigger("PerfPing"),
                g.Trigger("PerfFloat", g.Literal(1f)),
                g.Trigger("PerfInt", loop.currentIndex),
                g.Trigger("PerfVector", g.Literal(new Vector3(0.5f, 1f, 2f))),
                g.Trigger("PerfMulti", g.Literal(0.75f), g.Literal(true), g.Literal(new Vector3(1f, 2f, 3f))),
                g.Trigger("PerfRelay", g.Literal(1)),
            };
            var body = g.Sequence(sends.Length);
            GraphBuilder.Flow(loop.body, body.enter);
            for (int i = 0; i < sends.Length; i++)
                GraphBuilder.Flow(body.multiOutputs[i], sends[i].enter);

            g.Row(2600f);
            var ping = g.Receive("PerfPing", 0);
            var countPing = g.Set("evPingCount", g.IntMod(g.GenericAdd(g.Get("evPingCount"), g.Literal(1)), 100000));
            GraphBuilder.Flow(ping.trigger, countPing.assign);

            var onFloat = g.Receive("PerfFloat", 1);
            var sumFloat = g.Set("evFloatSum", g.Mod(g.Sum(g.Get("evFloatSum"), onFloat.argumentPorts[0]), 100000f));
            GraphBuilder.Flow(onFloat.trigger, sumFloat.assign);

            var onInt = g.Receive("PerfInt", 1);
            var sumInt = g.Set("evIntSum", g.IntMod(g.GenericAdd(g.Get("evIntSum"), onInt.argumentPorts[0]), 100000));
            GraphBuilder.Flow(onInt.trigger, sumInt.assign);

            g.Row(2900f);
            var onVector = g.Receive("PerfVector", 1);
            var lerpVector = g.Set("evVec", g.Lerp3(g.Get("evVec"), onVector.argumentPorts[0], 0.5f));
            GraphBuilder.Flow(onVector.trigger, lerpVector.assign);

            var onMulti = g.Receive("PerfMulti", 3);
            var storeMulti = g.SetMany(new List<(string, ValueOutput)>
            {
                ("evMultiF", onMulti.argumentPorts[0]),
                ("evMultiB", onMulti.argumentPorts[1]),
                ("evMultiV", onMulti.argumentPorts[2]),
            });
            GraphBuilder.Flow(onMulti.trigger, storeMulti.assign);

            g.Row(3200f);
            var onRelay = g.Receive("PerfRelay", 1);
            var relay = g.Trigger("PerfRelayEnd", g.Literal(1));
            GraphBuilder.Flow(onRelay.trigger, relay.enter);

            var onRelayEnd = g.Receive("PerfRelayEnd", 1);
            var countRelay = g.Set("evRelayCount", g.IntMod(g.GenericAdd(g.Get("evRelayCount"), onRelayEnd.argumentPorts[0]), 100000));
            GraphBuilder.Flow(onRelayEnd.trigger, countRelay.assign);
            return loop.enter;
        }

        /// <summary>Pointer Get: reads local position, rotation, scale and material color of all 8 pointer
        /// targets (32 pointer/get per iteration), combined into one variable write per kind.</summary>
        private static ControlInput BuildPointerGetPath(GraphBuilder g, SceneRefs refs)
        {
            g.Row(3600f);
            g.Declare("ptrSinkPosition", Vector3.zero);
            g.Declare("ptrSinkRotation", Quaternion.identity);
            g.Declare("ptrSinkScale", Vector3.zero);
            g.Declare("ptrSinkColor", Color.black);

            var loop = g.Loop(g.Get("loopCount"));
            ValueOutput positions = null, rotations = null, scales = null, colors = null;
            for (int i = 0; i < PointerTargetCount; i++)
            {
                var target = refs.pointerTargets[i];
                var position = g.GetTransform(target, nameof(Transform.localPosition));
                var rotation = g.GetTransform(target, nameof(Transform.localRotation));
                var scale = g.GetTransform(target, nameof(Transform.localScale));
                var color = g.GetMaterialColor(refs.pointerTargetMaterials[i]);
                positions = positions == null ? position : g.Vec3Add(positions, position);
                rotations = rotations == null ? rotation : g.GenericMul(rotations, rotation);
                scales = scales == null ? scale : g.Vec3Add(scales, scale);
                colors = colors == null ? color : g.GenericAdd(colors, color);
            }

            var sinks = new[]
            {
                g.Set("ptrSinkPosition", positions), g.Set("ptrSinkRotation", rotations),
                g.Set("ptrSinkScale", scales), g.Set("ptrSinkColor", colors),
            };
            GraphBuilder.Flow(loop.body, sinks[0].assign);
            for (int i = 1; i < sinks.Length; i++)
                GraphBuilder.Flow(sinks[i - 1].assigned, sinks[i].assign);
            return loop.enter;
        }

        /// <summary>Pointer Set: writes local position, rotation, scale and material color of all 8 pointer
        /// targets (32 pointer/set per iteration). Values follow the time, so the targets bob and spin.</summary>
        private static ControlInput BuildPointerSetPath(GraphBuilder g, SceneRefs refs)
        {
            g.Row(3900f);
            var loop = g.Loop(g.Get("loopCount"));
            var time = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var wave = g.Mathf1(nameof(Mathf.Sin), g.Mul(time, 3f));
            var rotation = g.Euler(g.Literal(0f), g.Mul(time, 90f), g.Literal(0f));
            var size = g.Sum(g.Mul(wave, 0.05f), PointerTargetScale);
            var scale = g.Vec3(size, size, size);
            var color = g.NewColor(g.Sum(g.Mul(wave, 0.4f), 0.5f), g.Literal(0.5f), g.Sub(0.5f, g.Mul(wave, 0.4f)), g.Literal(1f));

            ControlOutput previous = loop.body;
            for (int i = 0; i < PointerTargetCount; i++)
            {
                var target = refs.pointerTargets[i];
                var basePosition = target.localPosition;
                var position = g.Vec3(g.Literal(basePosition.x), g.Sum(g.Mul(wave, 0.1f), basePosition.y), g.Literal(basePosition.z));
                var sets = new[]
                {
                    g.SetTransform(target, nameof(Transform.localPosition), position),
                    g.SetTransform(target, nameof(Transform.localRotation), rotation),
                    g.SetTransform(target, nameof(Transform.localScale), scale),
                    g.SetMaterialColor(refs.pointerTargetMaterials[i], color),
                };
                foreach (var set in sets)
                {
                    GraphBuilder.Flow(previous, set.assign);
                    previous = set.assigned;
                }
            }
            return loop.enter;
        }

        /// <summary>Pointer Get+Set: per target reads and writes back local rotation and scale
        /// (16 pointer/get + 16 pointer/set per iteration). The targets turn faster with higher load.</summary>
        private static ControlInput BuildPointerGetSetPath(GraphBuilder g, SceneRefs refs)
        {
            g.Row(4200f);
            var loop = g.Loop(g.Get("loopCount"));
            var step = g.Euler(g.Literal(0f), g.Literal(0.05f), g.Literal(0f));
            var restScale = g.Literal(Vector3.one * PointerTargetScale);

            ControlOutput previous = loop.body;
            for (int i = 0; i < PointerTargetCount; i++)
            {
                var target = refs.pointerTargets[i];
                var turn = g.SetTransform(target, nameof(Transform.localRotation),
                    g.GenericMul(g.GetTransform(target, nameof(Transform.localRotation)), step));
                var settle = g.SetTransform(target, nameof(Transform.localScale),
                    g.Lerp3(g.GetTransform(target, nameof(Transform.localScale)), restScale, 0.01f));
                GraphBuilder.Flow(previous, turn.assign);
                GraphBuilder.Flow(turn.assigned, settle.assign);
                previous = settle.assigned;
            }
            return loop.enter;
        }

        /// <summary>Integer math: bit operations (and, or, xor, not, shifts, clz, ctz, popcnt) plus int mul/div/mod.</summary>
        private static ControlInput BuildIntMathPath(GraphBuilder g)
        {
            g.Row(6000f);
            g.Declare("intAcc", 12345);
            g.Declare("intBits", 0);
            g.Declare("intMath", 0);
            var loop = g.Loop(g.Get("loopCount"));
            var x = g.Get("intAcc");
            var shifted = g.IntOp(GltfIntBitwise.Operation.Xor,
                g.IntOp(GltfIntBitwise.Operation.ShiftLeft, x, g.Literal(1)),
                g.IntOp(GltfIntBitwise.Operation.ShiftRight, x, g.Literal(3)));
            var masked = g.IntOp(GltfIntBitwise.Operation.And, shifted, g.Literal(0x7fffffff));
            var odd = g.IntOp(GltfIntBitwise.Operation.Or, masked, g.Literal(1));
            var bitCounts = g.GenericAdd(g.GenericAdd(
                    g.IntOp(GltfIntBitwise.Operation.PopCount, shifted),
                    g.IntOp(GltfIntBitwise.Operation.CountLeadingZeros, masked)),
                g.IntOp(GltfIntBitwise.Operation.CountTrailingZeros, odd));
            var next = g.IntOp(GltfIntBitwise.Operation.And,
                g.IntOp(GltfIntBitwise.Operation.Xor, g.GenericAdd(masked, loop.currentIndex), bitCounts), g.Literal(0xfffff));

            var setAcc = g.Set("intAcc", g.GenericAdd(next, g.Literal(1)));
            var setBits = g.Set("intBits", g.IntOp(GltfIntBitwise.Operation.Not, odd));
            var setMath = g.Set("intMath", g.IntMod(g.GenericAdd(g.IntMul(x, g.Literal(3)), g.IntDiv(x, g.Literal(7))), 100000));
            GraphBuilder.Flow(loop.body, setAcc.assign);
            GraphBuilder.Flow(setAcc.assigned, setBits.assign);
            GraphBuilder.Flow(setBits.assigned, setMath.assign);
            return loop.enter;
        }

        /// <summary>Type conversion: int variables used in float math (int to float) and float results stored as int
        /// (float to int); the exporter inserts the type/* conversion nodes.</summary>
        private static ControlInput BuildConversionPath(GraphBuilder g)
        {
            g.Row(6300f);
            g.Declare("convInt", 7);
            g.Declare("convFloat", 0f);
            var loop = g.Loop(g.Get("loopCount"));
            var asFloat = g.Mathf1(nameof(Mathf.Sin), g.Get("convInt"));              // int -> float
            var rounded = g.RoundToInt(g.Mul(asFloat, 1000f));                         // float -> int
            var floored = g.FloorToInt(g.Mul(asFloat, 77.7f));                         // float -> int
            var setInt = g.Set("convInt", g.IntMod(g.GenericAdd(g.GenericAdd(rounded, floored), loop.currentIndex), 100000));
            var setFloat = g.Set("convFloat", g.Sum(g.Mul(g.Get("convInt"), 0.5f), asFloat)); // int -> float
            GraphBuilder.Flow(loop.body, setInt.assign);
            GraphBuilder.Flow(setInt.assigned, setFloat.assign);
            return loop.enter;
        }

        /// <summary>A chain of 8 float math nodes; <paramref name="salt"/> keeps copies from being deduplicated.</summary>
        private static ValueOutput ExpensiveChain(GraphBuilder g, ValueOutput x, float salt)
        {
            // the salt goes into the very first node, otherwise the exporter's deduplication could merge the
            // first nodes of all copies (same op, same inputs)
            var v = g.Mathf1(nameof(Mathf.Sin), g.Sum(g.Mul(x, 1.1f + salt), 0.3f));
            v = g.Mathf1(nameof(Mathf.Sqrt), g.Sum(g.Mathf1(nameof(Mathf.Abs), v), 1f));
            v = g.Mathf1(nameof(Mathf.Cos), g.Mul(v, 0.9f));
            return g.Mathf2(nameof(Mathf.Pow), g.Sum(g.Mathf1(nameof(Mathf.Abs), v), 0.5f), g.Literal(1.3f));
        }

        private const int ValueConsumers = 10;

        private static SetVariable StoreValueResult(GraphBuilder g, ValueOutput total) =>
            g.Set("valueOut", g.Mod(total, 1000f));

        /// <summary>Shared: one expensive chain whose single output is used by 10 nodes. Engines that cache node
        /// results within a flow evaluate the chain once, others 10 times.</summary>
        private static ControlInput BuildSharedValuePath(GraphBuilder g)
        {
            g.Row(6600f);
            g.Declare("valueIn", 0.5f);
            g.Declare("valueOut", 0f);
            var loop = g.Loop(g.Get("loopCount"));
            var shared = ExpensiveChain(g, g.Get("valueIn"), 0f);
            ValueOutput total = null;
            for (int k = 1; k <= ValueConsumers; k++)
            {
                var term = g.Mul(shared, k * 0.1f);
                total = total == null ? term : g.Sum(total, term);
            }
            var store = StoreValueResult(g, total);
            var vary = g.Set("valueIn", g.Mod(g.Sum(g.Get("valueIn"), 0.01f), 10f));
            GraphBuilder.Flow(loop.body, store.assign);
            GraphBuilder.Flow(store.assigned, vary.assign);
            return loop.enter;
        }

        /// <summary>Copies: the same as Shared, but every consumer has its own copy of the chain.</summary>
        private static ControlInput BuildCopiedValuePath(GraphBuilder g)
        {
            g.Row(6900f);
            var loop = g.Loop(g.Get("loopCount"));
            ValueOutput total = null;
            for (int k = 1; k <= ValueConsumers; k++)
            {
                var term = g.Mul(ExpensiveChain(g, g.Get("valueIn"), k * 0.0001f), k * 0.1f);
                total = total == null ? term : g.Sum(total, term);
            }
            var store = StoreValueResult(g, total);
            var vary = g.Set("valueIn", g.Mod(g.Sum(g.Get("valueIn"), 0.01f), 10f));
            GraphBuilder.Flow(loop.body, store.assign);
            GraphBuilder.Flow(store.assigned, vary.assign);
            return loop.enter;
        }

        private const int ValueTerms = 50;

        /// <summary>Deep: 50 multiply + 50 add nodes in one dependent chain (depth 100).</summary>
        private static ControlInput BuildDeepValuePath(GraphBuilder g)
        {
            g.Row(7200f);
            var loop = g.Loop(g.Get("loopCount"));
            var v = g.Get("valueIn");
            for (int k = 0; k < ValueTerms; k++)
                v = g.Sum(g.Mul(v, 0.999f), 0.0013f);
            var store = StoreValueResult(g, v);
            GraphBuilder.Flow(loop.body, store.assign);
            return loop.enter;
        }

        /// <summary>Wide: 50 independent multiply nodes summed in a balanced tree (about the same node count as Deep, depth 7).</summary>
        private static ControlInput BuildWideValuePath(GraphBuilder g)
        {
            g.Row(7500f);
            var loop = g.Loop(g.Get("loopCount"));
            var input = g.Get("valueIn");
            var level = new List<ValueOutput>();
            for (int k = 0; k < ValueTerms; k++)
                level.Add(g.Mul(input, 0.999f + k * 0.00001f));
            while (level.Count > 1)
            {
                var next = new List<ValueOutput>();
                for (int i = 0; i + 1 < level.Count; i += 2)
                    next.Add(g.Sum(level[i], level[i + 1]));
                if (level.Count % 2 == 1)
                    next.Add(level[level.Count - 1]);
                level = next;
            }
            var store = StoreValueResult(g, level[0]);
            GraphBuilder.Flow(loop.body, store.assign);
            return loop.enter;
        }

        /// <summary>Variable interpolate: restarts 4 variable interpolations per iteration (each restart replaces the running one).</summary>
        private static ControlInput BuildVariableInterpolatePath(GraphBuilder g)
        {
            g.Row(7800f);
            g.Declare("interpA", 0f);
            g.Declare("interpB", 0f);
            g.Declare("interpC", 0f);
            g.Declare("interpV", Vector3.zero);
            var loop = g.Loop(g.Get("loopCount"));
            var time = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var wave = g.Mathf1(nameof(Mathf.Sin), time);
            var interpolations = new[]
            {
                g.InterpolateVariable("interpA", wave, 0.5f),
                g.InterpolateVariable("interpB", g.Mul(wave, 2f), 0.5f),
                g.InterpolateVariable("interpC", g.Sum(wave, 1f), 0.5f),
                g.InterpolateVariable("interpV", g.Vec3(wave, g.Literal(1f), g.Literal(0f)), 0.5f),
            };
            ControlOutput previous = loop.body;
            foreach (var interpolation in interpolations)
            {
                GraphBuilder.Flow(previous, interpolation.assign);
                previous = interpolation.assigned;
            }
            return loop.enter;
        }

        /// <summary>Pointer interpolate: restarts the scale interpolation of all 8 targets per iteration.</summary>
        private static ControlInput BuildPointerInterpolatePath(GraphBuilder g, SceneRefs refs)
        {
            g.Row(8100f);
            var loop = g.Loop(g.Get("loopCount"));
            var time = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            ControlOutput previous = loop.body;
            for (int i = 0; i < PointerTargetCount; i++)
            {
                var size = g.Sum(g.Mul(g.Mathf1(nameof(Mathf.Sin), g.Sum(g.Mul(time, 4f), i * 0.8f)), 0.1f), PointerTargetScale);
                var interpolation = g.InterpolateTransform(refs.pointerTargets[i], nameof(Transform.localScale),
                    g.Vec3(size, size, size), 0.3f);
                GraphBuilder.Flow(previous, interpolation.assign);
                previous = interpolation.assigned;
            }
            return loop.enter;
        }

        /// <summary>Hierarchy: rotates the root of a 20 level node chain and reads the world position of its leaf,
        /// so the engine has to update the whole chain.</summary>
        private static ControlInput BuildHierarchyPath(GraphBuilder g, SceneRefs refs)
        {
            g.Row(8400f);
            g.Declare("hierarchyLeafPosition", Vector3.zero);
            var loop = g.Loop(g.Get("loopCount"));
            var time = g.Add(new GetMember(new Member(typeof(Time), nameof(Time.time))), 0f, 200f).value;
            var swing = g.SetTransform(refs.hierarchyRoot, nameof(Transform.localRotation),
                g.Euler(g.Literal(0f), g.Literal(0f), g.Mul(g.Mathf1(nameof(Mathf.Sin), g.Mul(time, 2f)), 25f)));
            var readLeaf = g.Set("hierarchyLeafPosition", g.GetTransform(refs.hierarchyLeaf, nameof(Transform.position)));
            GraphBuilder.Flow(loop.body, swing.assign);
            GraphBuilder.Flow(swing.assigned, readLeaf.assign);
            return loop.enter;
        }

        /// <summary>Flow nodes: doN, multiGate, throttle, a short while loop and waitAll per iteration.</summary>
        private static ControlInput BuildFlowNodesPath(GraphBuilder g)
        {
            g.Row(8700f);
            g.Declare("doNCount", 0);
            g.Declare("gateIndex", 0);
            g.Declare("throttleCount", 0);
            g.Declare("whileCounter", 0f);
            var loop = g.Loop(g.Get("loopCount"));
            var body = g.Sequence(5);
            GraphBuilder.Flow(loop.body, body.enter);

            // doN: passes 3 times, reset by the waitAll below
            var doN = g.DoN(3);
            GraphBuilder.Flow(body.multiOutputs[0], doN.enter);
            var countDoN = g.Count("doNCount");
            GraphBuilder.Flow(doN.exit, countDoN.assign);

            // multiGate: cycles through 4 outputs
            var gate = g.MultiGate(4, loop: true);
            GraphBuilder.Flow(body.multiOutputs[1], gate.enter);
            for (int i = 0; i < 4; i++)
                GraphBuilder.Flow(gate.outputs[i], g.Set("gateIndex", g.Literal(i)).assign);

            // throttle: passes at most every 10 ms
            var throttle = g.Throttle(0.01f);
            GraphBuilder.Flow(body.multiOutputs[2], throttle.enter);
            GraphBuilder.Flow(throttle.exit, g.Count("throttleCount").assign);

            // while: 3 rounds
            var startWhile = g.Set("whileCounter", g.Literal(0f));
            GraphBuilder.Flow(body.multiOutputs[3], startWhile.assign);
            var whileLoop = g.While(g.Less(g.Get("whileCounter"), 3f));
            GraphBuilder.Flow(startWhile.assigned, whileLoop.enter);
            GraphBuilder.Flow(whileLoop.body, g.Set("whileCounter", g.Sum(g.Get("whileCounter"), 1f)).assign);

            // waitAll: both inputs fire, completion resets the doN
            var both = g.Sequence(2);
            GraphBuilder.Flow(body.multiOutputs[4], both.enter);
            var waitAll = g.WaitAll(2);
            GraphBuilder.Flow(both.multiOutputs[0], waitAll.awaitedInputs[0]);
            GraphBuilder.Flow(both.multiOutputs[1], waitAll.awaitedInputs[1]);
            GraphBuilder.Flow(waitAll.exit, doN.reset);
            return loop.enter;
        }

        /// <summary>Delays: per iteration starts a short delay (counted when done) and a long one that is cancelled
        /// right away, so many delays are pending at the same time.</summary>
        private static ControlInput BuildDelayPath(GraphBuilder g)
        {
            g.Row(9000f);
            g.Declare("delayDoneCount", 0);
            var loop = g.Loop(g.Get("loopCount"));
            var shortDelay = g.SetDelay(0.25f);
            GraphBuilder.Flow(loop.body, shortDelay.enter);
            GraphBuilder.Flow(shortDelay.done, g.Count("delayDoneCount").assign);

            var longDelay = g.SetDelay(5f);
            GraphBuilder.Flow(shortDelay.exit, longDelay.enter);
            var cancel = g.CancelDelay(longDelay.lastDelay);
            GraphBuilder.Flow(longDelay.exit, cancel.enter);
            return loop.enter;
        }

        /// <summary>Event chain: per iteration sends an event whose receiver sends it again, 20 levels deep.
        /// The depth lives in a variable: an event argument computed from a received argument can not be typed
        /// by the exporter.</summary>
        private static ControlInput BuildEventChainPath(GraphBuilder g)
        {
            g.Row(9300f);
            g.Declare("chainDepth", 0);
            g.Declare("chainDoneCount", 0);
            var loop = g.Loop(g.Get("loopCount"));
            var startChain = g.Set("chainDepth", g.Literal(0));
            GraphBuilder.Flow(loop.body, startChain.assign);
            var first = g.Trigger("PerfChainStep");
            GraphBuilder.Flow(startChain.assigned, first.enter);

            var step = g.Receive("PerfChainStep", 0);
            var deeper = g.Add(new If());
            GraphBuilder.Value(g.Less(g.Get("chainDepth"), EventChainDepth), deeper.condition);
            GraphBuilder.Flow(step.trigger, deeper.enter);
            var increase = g.Count("chainDepth");
            GraphBuilder.Flow(deeper.ifTrue, increase.assign);
            var relay = g.Trigger("PerfChainStep");
            GraphBuilder.Flow(increase.assigned, relay.enter);
            GraphBuilder.Flow(deeper.ifFalse, g.Count("chainDoneCount").assign);
            return loop.enter;
        }

        /// <summary>Empty loop: only the For loop itself, as baseline for the loop overhead of all other tests.</summary>
        private static ControlInput BuildEmptyLoopPath(GraphBuilder g)
        {
            g.Row(9600f);
            return g.Loop(g.Get("loopCount")).enter;
        }

        #endregion
    }
}
#endif
