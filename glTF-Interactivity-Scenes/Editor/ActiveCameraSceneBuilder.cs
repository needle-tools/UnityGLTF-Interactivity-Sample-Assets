#if HAVE_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Generates the "ActiveCamera" sample: visualizes the read-only activeCamera pointers of KHR_interactivity
    /// (position, rotation and the projection values), which test assets can only check loosely since the values
    /// depend on the viewer.
    ///  - HUD: a crosshair and two squares placed every tick in front of the camera (Camera.main.TransformPoint +
    ///    camera rotation) at 2, 4 and 8 m. They must stay centered, nested and steady while the camera moves.
    ///    An orange square uses the values of the previous tick as a reference for a one tick delay.
    ///  - Billboards use only the rotation (always face the camera), arrows use only the position (LookAt).
    ///  - A panel shows the values: position, rotation, the world origin in camera space
    ///    (InverseTransformPoint), movement per tick, field of view, aspect, near, far, orthographic size.
    /// The graph is built through the Visual Scripting API; re-run the menu item after changing this file.
    ///
    /// The graph is exported without UnityGLTF's space conversion (see <see cref="GltfInteractivityScenes.InteractivityExportOptions"/>),
    /// so it works on the glTF values directly and shows the raw pointer values: Camera.main.position/rotation are
    /// activeCamera/position/rotation, the camera looks along its local -Z, and literal positions are glTF positions.
    /// The scene objects themselves are still authored in Unity and exported with the usual conversion (X mirrored).
    /// </summary>
    public static class ActiveCameraSceneBuilder
    {
        private const string PackageRoot = "Packages/com.needle.gltf-interactivity-scenes";
        private const string ModelsFolder = PackageRoot + "/Interactivity Repo Models";
        private const string Folder = ModelsFolder + "/20261012-ActiveCamera";
        private const string TextureFolder = Folder + "/Textures";
        private const string MaterialFolder = Folder + "/Materials";
        private const string SceneName = "ActiveCamera";
        private const string IndexPath = ModelsFolder + "/SampleModelIndex.asset";
        private const string CutoutTemplatePath = PackageRoot + "/Test Scenes/Common/N01.mat";

        // HUD, in front of the camera: distance and half size relative to the distance (= angular size),
        // so on screen they are nested independent of their distance
        private const float CrosshairDistance = 2f, CrosshairSize = 0.03f;
        private const float InnerFrameDistance = 4f, InnerFrameSize = 0.06f;
        private const float OuterFrameDistance = 8f, OuterFrameSize = 0.14f;
        private const float GhostFrameDistance = 4f, GhostFrameSize = 0.10f;
        // Placed behind the camera (local +Z): only in front of it when the camera rotation is turned by 180 degrees,
        // which is exactly when the squares are behind the camera
        private const float FlippedErrorDistance = 3f;
        private const float LineWidth = 0.004f; // relative to the distance as well

        private const int ArrowCount = 8;
        private const float ArrowRadius = 3f;

        // Number glyphs: one atlas line, each glyph 6 font pixels wide
        private const string NumberGlyphs = "0123456789 -";
        private const int GlyphBlank = 10, GlyphMinus = 11;
        private static int AtlasColumns => NumberGlyphs.Length * 6 + 1;

        private static readonly Color HudColor = new Color(0.95f, 0.98f, 1f);
        private static readonly Color GhostColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color PanelColor = new Color(0.22f, 0.24f, 0.29f);
        private static readonly Color PanelTextColor = new Color(0.93f, 0.94f, 0.96f);
        private static readonly Color ValueColor = new Color(0.55f, 0.9f, 1f);
        private static readonly Color HeaderColor = new Color(1f, 0.8f, 0.4f);
        private static readonly Color IndicatorOff = new Color(0.35f, 0.36f, 0.4f);
        private static readonly Color IndicatorOn = new Color(0.3f, 1f, 0.4f);

        [MenuItem("glTF Interactivity/Create Active Camera Scene")]
        public static void Create()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scenePath = $"{Folder}/{SceneName}.unity";
            if (File.Exists(Physical(scenePath)) &&
                !EditorUtility.DisplayDialog("Active Camera Scene",
                    $"{scenePath} already exists. Regenerate it (scene, materials and label textures)?",
                    "Regenerate", "Cancel"))
                return;

            // Both folders only hold generated assets
            AssetDatabase.DeleteAsset(TextureFolder);
            AssetDatabase.DeleteAsset(MaterialFolder);
            Directory.CreateDirectory(Physical(TextureFolder));
            Directory.CreateDirectory(Physical(MaterialFolder));
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var refs = BuildSceneObjects();
            BuildGraph(refs);

            EditorSceneManager.SaveScene(scene, scenePath);
            CreateOrUpdateModelDefinition(scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Active camera scene written to {scenePath}");
        }

        #region Scene objects

        /// <summary>A number shown with one glyph quad per character: [sign] digits . decimals</summary>
        private class NumberDisplay
        {
            public bool signed;
            public int integerDigits, decimals;
            public Material sign;
            public readonly List<Material> digits = new List<Material>(); // most significant first
        }

        private class SceneRefs
        {
            public Transform camera;
            public GameObject graphObject;
            public Transform crosshair, innerFrame, outerFrame, ghostFrame, flippedError;
            public readonly List<Transform> billboards = new List<Transform>();
            public readonly List<Transform> arrows = new List<Transform>();
            public readonly NumberDisplay[] position = new NumberDisplay[3];
            public readonly NumberDisplay[] rotation = new NumberDisplay[4];
            public readonly NumberDisplay[] origin = new NumberDisplay[3];
            public NumberDisplay moved, fov, aspect, near, far, orthoSize;
            public Material orthographicIndicator;
            public Texture2D glyphAtlas;
        }

        private static SceneRefs BuildSceneObjects()
        {
            var refs = new SceneRefs();

            // Default view. glTF viewers look at the model from +Z, Unity -> glTF only mirrors X,
            // so the camera looks along Unity's -Z as well.
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 3f, 10f), Quaternion.Euler(12f, 180f, 0f));
            refs.camera = cameraGo.transform;

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

            BuildWorldReference();
            BuildArrows(refs);
            BuildBillboards(refs);
            BuildHud(refs);
            BuildPanel(refs);

            refs.graphObject = new GameObject("ActiveCameraGraph");
            var options = refs.graphObject.AddComponent<GltfInteractivityScenes.InteractivityExportOptions>();
            options.cleanUpAndOptimizeExportedGraph = true;
            options.addUnityGltfSpaceConversion = false;
            return refs;
        }

        private static void BuildWorldReference()
        {
            var world = new GameObject("WorldReference").transform;
            Box("Ground", world, new Vector3(0f, -0.02f, 0f), new Vector3(10f, 0.04f, 10f),
                CreateColorMaterial("Ground", new Color(0.18f, 0.19f, 0.22f)));
            var gridMaterial = CreateColorMaterial("Grid", new Color(0.3f, 0.31f, 0.35f));
            for (int i = -5; i <= 5; i++)
            {
                Box($"GridX{i}", world, new Vector3(0f, 0.001f, i), new Vector3(10f, 0.005f, 0.02f), gridMaterial);
                Box($"GridZ{i}", world, new Vector3(i, 0.001f, 0f), new Vector3(0.02f, 0.005f, 10f), gridMaterial);
            }

            // glTF axes. The exporter mirrors X, so glTF +X is Unity -X
            Box("AxisX", world, new Vector3(-0.5f, 0.05f, 0f), new Vector3(1f, 0.06f, 0.06f), CreateColorMaterial("AxisX", Color.red, true));
            Box("AxisY", world, new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 1f, 0.06f), CreateColorMaterial("AxisY", Color.green, true));
            Box("AxisZ", world, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.06f, 0.06f, 1f), CreateColorMaterial("AxisZ", Color.blue, true));
            Box("Origin", world, new Vector3(0f, 0.05f, 0f), Vector3.one * 0.12f, CreateColorMaterial("Origin", Color.white, true));
        }

        private static void BuildArrows(SceneRefs refs)
        {
            var shaftMaterial = CreateColorMaterial("ArrowShaft", new Color(1f, 0.85f, 0.2f));
            var tipMaterial = CreateColorMaterial("ArrowTip", new Color(1f, 0.6f, 0.1f), true);
            var okMaterial = CreateColorMaterial("ArrowOk", new Color(0.2f, 0.75f, 0.3f));
            var wrongMaterial = CreateColorMaterial("ArrowWrong", new Color(0.85f, 0.15f, 0.15f));
            var parent = new GameObject("LookAtArrows").transform;
            for (int i = 0; i < ArrowCount; i++)
            {
                var angle = i * Mathf.PI * 2f / ArrowCount;
                var arrow = new GameObject($"Arrow{i}").transform;
                arrow.SetParent(parent, false);
                arrow.localPosition = new Vector3(Mathf.Sin(angle) * ArrowRadius, 0.6f + 0.25f * (i % 2), Mathf.Cos(angle) * ArrowRadius);
                arrow.LookAt(refs.camera.position);
                Box("Shaft", arrow, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.07f, 0.6f), shaftMaterial);
                Box("Tip", arrow, new Vector3(0f, 0f, 0.36f), new Vector3(0.18f, 0.18f, 0.14f), tipMaterial);

                // Seen from the camera, a correct arrow shows only its green "OK" plate (the tip end, +Z).
                // Red "WRONG" plate = arrow points away from the camera, the long shaft visible = wrong direction.
                var front = new GameObject("Front").transform;
                front.SetParent(arrow, false);
                front.localPosition = new Vector3(0f, 0f, 0.44f);
                front.localRotation = Quaternion.Euler(0f, 180f, 0f); // quads face -Z, this one has to face +Z
                Quad(front, "Ok", Vector3.zero, new Vector2(0.3f, 0.3f), okMaterial);
                Label(front, "OK", new Vector3(0f, 0f, -0.005f), 0.12f, PanelTextColor);

                var back = new GameObject("Back").transform;
                back.SetParent(arrow, false);
                back.localPosition = new Vector3(0f, 0f, -0.31f);
                Quad(back, "Wrong", Vector3.zero, new Vector2(0.3f, 0.3f), wrongMaterial);
                Label(back, "WRONG", new Vector3(0f, 0f, -0.005f), 0.07f, PanelTextColor);
                Box("Pole", arrow.parent, arrow.localPosition - new Vector3(0f, arrow.localPosition.y * 0.5f, 0f),
                    new Vector3(0.03f, arrow.localPosition.y, 0.03f), shaftMaterial).name = $"Pole{i}";
                refs.arrows.Add(arrow);
            }
        }

        private static void BuildBillboards(SceneRefs refs)
        {
            var boardMaterial = CreateColorMaterial("Billboard", new Color(0.2f, 0.45f, 0.85f));
            var backMaterial = CreateColorMaterial("BillboardWrongSide", new Color(0.85f, 0.15f, 0.15f));
            var parent = new GameObject("Billboards").transform;
            var positions = new[] { new Vector3(-3.6f, 1.6f, -1.5f), new Vector3(3.6f, 1.6f, -1.5f), new Vector3(0f, 2.2f, 3.5f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var board = new GameObject($"Billboard{i}").transform;
                board.SetParent(parent, false);
                board.localPosition = positions[i];
                // The graph sets the rotation of the glTF camera, which looks along its local -Z. So the board's
                // local +Z points to the camera and its content faces +Z.
                board.rotation = refs.camera.rotation * Quaternion.Euler(0f, 180f, 0f);
                var content = new GameObject("Content").transform;
                content.SetParent(board, false);
                content.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Box("Background", content, Vector3.zero, new Vector3(1.9f, 0.55f, 0.04f), boardMaterial);
                Label(content, "FACES YOU", new Vector3(0f, 0.07f, -0.03f), 0.22f, PanelTextColor);
                Label(content, "ROTATION ONLY", new Vector3(0f, -0.15f, -0.03f), 0.12f, PanelTextColor);

                // Back side: only visible when the board shows its back to the camera (rotation flipped)
                var back = new GameObject("Back").transform;
                back.SetParent(content, false);
                back.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Quad(back, "WrongSide", new Vector3(0f, 0f, -0.025f), new Vector2(1.9f, 0.55f), backMaterial);
                Label(back, "WRONG SIDE", new Vector3(0f, 0.07f, -0.03f), 0.22f, PanelTextColor);
                Label(back, "ROTATION FLIPPED", new Vector3(0f, -0.15f, -0.03f), 0.12f, PanelTextColor);
                refs.billboards.Add(board);
            }
        }

        private static void BuildHud(SceneRefs refs)
        {
            var hudMaterial = CreateColorMaterial("Hud", HudColor, true);
            var ghostMaterial = CreateColorMaterial("HudLastTick", GhostColor, true);
            var parent = new GameObject("Hud").transform;

            refs.crosshair = HudObject(parent, "Crosshair", refs.camera, CrosshairDistance);
            var d = CrosshairDistance;
            Box("Horizontal", refs.crosshair, Vector3.zero, new Vector3(2f * CrosshairSize * d, LineWidth * d, LineWidth * d), hudMaterial);
            Box("Vertical", refs.crosshair, Vector3.zero, new Vector3(LineWidth * d, 2f * CrosshairSize * d, LineWidth * d), hudMaterial);

            refs.innerFrame = HudObject(parent, "InnerFrame", refs.camera, InnerFrameDistance);
            Frame(refs.innerFrame, InnerFrameSize * InnerFrameDistance, LineWidth * InnerFrameDistance, hudMaterial);
            refs.outerFrame = HudObject(parent, "OuterFrame", refs.camera, OuterFrameDistance);
            Frame(refs.outerFrame, OuterFrameSize * OuterFrameDistance, LineWidth * OuterFrameDistance, hudMaterial);
            refs.ghostFrame = HudObject(parent, "LastTickFrame", refs.camera, GhostFrameDistance);
            Frame(refs.ghostFrame, GhostFrameSize * GhostFrameDistance, LineWidth * GhostFrameDistance, ghostMaterial);

            // Error plate, see FlippedErrorDistance. Starts where the graph puts it for a correct rotation: behind the
            // camera, turned like the glTF camera. Its content faces the camera when the rotation is flipped.
            refs.flippedError = new GameObject("FlippedRotationError").transform;
            refs.flippedError.SetParent(parent, false);
            refs.flippedError.SetPositionAndRotation(refs.camera.TransformPoint(0f, 0f, -FlippedErrorDistance),
                refs.camera.rotation * Quaternion.Euler(0f, 180f, 0f));
            var errorMaterial = CreateColorMaterial("HudError", new Color(0.85f, 0.1f, 0.1f), true);
            var e = FlippedErrorDistance;
            Quad(refs.flippedError, "Background", Vector3.zero, new Vector2(0.5f * e, 0.13f * e), errorMaterial);
            Label(refs.flippedError, "ERROR: CAMERA ROTATION FLIPPED", new Vector3(0f, 0.022f * e, -0.005f), 0.022f * e, PanelTextColor);
            Label(refs.flippedError, "THE SQUARES ARE BEHIND THE CAMERA", new Vector3(0f, -0.025f * e, -0.005f), 0.017f * e, PanelTextColor);
        }

        private static Transform HudObject(Transform parent, string name, Transform camera, float distance)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(camera.TransformPoint(0f, 0f, distance), camera.rotation);
            return t;
        }

        private static void Frame(Transform parent, float halfSize, float width, Material material)
        {
            var length = 2f * halfSize + width;
            Box("Top", parent, new Vector3(0f, halfSize, 0f), new Vector3(length, width, width), material);
            Box("Bottom", parent, new Vector3(0f, -halfSize, 0f), new Vector3(length, width, width), material);
            Box("Left", parent, new Vector3(-halfSize, 0f, 0f), new Vector3(width, length, width), material);
            Box("Right", parent, new Vector3(halfSize, 0f, 0f), new Vector3(width, length, width), material);
        }

        private const float PanelWidth = 8.4f;
        private const float RowHeight = 0.36f;
        private const float GlyphHeight = 0.22f;
        private static float GlyphWidth => GlyphHeight * 6f / 9f;

        private static void BuildPanel(SceneRefs refs)
        {
            refs.glyphAtlas = CreateTextTexture("NumberGlyphs", new[] { NumberGlyphs }, 8);

            // Fixed in the world behind the scene, facing the default camera (front = local -Z)
            var panel = new GameObject("ValuePanel").transform;
            panel.SetPositionAndRotation(new Vector3(0f, 3.1f, -5.2f), Quaternion.Euler(0f, 180f, 0f));
            const int rows = 13;
            var height = rows * RowHeight + 0.3f;
            Box("Background", panel, new Vector3(0f, 0f, 0.05f), new Vector3(PanelWidth, height, 0.1f), CreateColorMaterial("Panel", PanelColor));

            var top = height * 0.5f - 0.3f;
            float RowY(int row) => top - row * RowHeight;
            const float left = -PanelWidth * 0.5f + 0.2f;
            const float valuesX = left + 2.75f;

            Label(panel, "KHR INTERACTIVITY ACTIVE CAMERA POINTERS", new Vector3(left, RowY(0), -0.01f), 0.2f, HeaderColor, leftAligned: true);

            var axes = new[] { "X", "Y", "Z", "W" };
            NumberDisplay[] VectorRow(int row, string label, int count, int integerDigits, int decimals, float step)
            {
                Label(panel, label, new Vector3(left, RowY(row), -0.01f), 0.16f, PanelTextColor, leftAligned: true);
                var result = new NumberDisplay[count];
                for (int i = 0; i < count; i++)
                {
                    var x = valuesX + i * step;
                    Label(panel, axes[i], new Vector3(x, RowY(row), -0.01f), 0.16f, HeaderColor, leftAligned: true);
                    result[i] = Number(panel, $"{SafeName(label)}_{axes[i]}", new Vector3(x + 0.2f, RowY(row), -0.01f), true, integerDigits, decimals, refs.glyphAtlas);
                }
                return result;
            }

            NumberDisplay ScalarRow(int row, float x, string label, bool signed, int integerDigits, int decimals)
            {
                Label(panel, label, new Vector3(x, RowY(row), -0.01f), 0.16f, PanelTextColor, leftAligned: true);
                return Number(panel, SafeName(label), new Vector3(x + 1.6f, RowY(row), -0.01f), signed, integerDigits, decimals, refs.glyphAtlas);
            }

            var p = VectorRow(1, "POSITION", 3, 3, 2, 1.75f);
            var r = VectorRow(2, "ROTATION", 4, 1, 3, 1.35f);
            var o = VectorRow(3, "ORIGIN IN CAM", 3, 3, 2, 1.75f);
            Array.Copy(p, refs.position, 3);
            Array.Copy(r, refs.rotation, 4);
            Array.Copy(o, refs.origin, 3);
            refs.moved = ScalarRow(4, left, "MOVED / TICK", false, 2, 3);

            refs.fov = ScalarRow(5, left, "FOV (DEG)", false, 3, 1);
            refs.aspect = ScalarRow(5, left + 4.1f, "ASPECT", false, 1, 3);
            refs.near = ScalarRow(6, left, "NEAR", false, 3, 3);
            refs.far = ScalarRow(6, left + 4.1f, "FAR", false, 5, 1);
            refs.orthoSize = ScalarRow(7, left, "ORTHO SIZE", false, 3, 2);
            Label(panel, "ORTHOGRAPHIC", new Vector3(left + 4.1f, RowY(7), -0.01f), 0.16f, PanelTextColor, leftAligned: true);
            refs.orthographicIndicator = CreateColorMaterial("OrthographicIndicator", IndicatorOff, true);
            Box("OrthographicIndicator", panel, new Vector3(left + 5.95f, RowY(7), -0.02f), new Vector3(0.22f, 0.22f, 0.04f), refs.orthographicIndicator);

            Label(panel, "WHITE SQUARES: CAMERA OF THIS TICK. CENTERED, NESTED AND STEADY = CORRECT.", new Vector3(left, RowY(8), -0.01f), 0.13f, PanelTextColor, leftAligned: true);
            Label(panel, "ORANGE SQUARE: CAMERA OF THE LAST TICK (1 TICK DELAY) FOR COMPARISON.", new Vector3(left, RowY(9), -0.01f), 0.13f, GhostColor, leftAligned: true);
            Label(panel, "ARROWS: POSITION ONLY (LOOK AT), GREEN OK FACE = CORRECT, RED OR SIDE VIEW = WRONG.", new Vector3(left, RowY(10), -0.01f), 0.13f, PanelTextColor, leftAligned: true);
            Label(panel, "BLUE BOARDS: ROTATION ONLY, FACE THE CAMERA. RED BACK SIDE = ROTATION FLIPPED.", new Vector3(left, RowY(11), -0.01f), 0.13f, PanelTextColor, leftAligned: true);
            Label(panel, "RED ERROR PLATE IN THE VIEW: CAMERA ROTATION FLIPPED, SQUARES ARE BEHIND THE CAMERA.", new Vector3(left, RowY(12), -0.01f), 0.13f, PanelTextColor, leftAligned: true);
        }

        private static NumberDisplay Number(Transform parent, string name, Vector3 position, bool signed, int integerDigits, int decimals, Texture2D atlas)
        {
            var display = new NumberDisplay { signed = signed, integerDigits = integerDigits, decimals = decimals };
            var x = position.x + GlyphWidth * 0.5f;
            Material Glyph(string glyphName, int glyph)
            {
                var material = CreateGlyphMaterial($"{name}_{glyphName}", atlas, glyph);
                Quad(parent, $"{name}_{glyphName}", new Vector3(x, position.y, position.z), new Vector2(GlyphWidth, GlyphHeight), material);
                x += GlyphWidth;
                return material;
            }

            if (signed)
                display.sign = Glyph("Sign", GlyphBlank);
            for (int i = 0; i < integerDigits + decimals; i++)
            {
                if (i == integerDigits)
                {
                    Label(parent, ".", new Vector3(x - GlyphWidth * 0.15f, position.y, position.z), GlyphHeight, ValueColor);
                    x += GlyphWidth * 0.4f;
                }
                display.digits.Add(Glyph($"D{i}", 0));
            }
            return display;
        }

        private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Quad facing local -Z of <paramref name="parent"/> (the front of panels and boards).</summary>
        private static void Quad(Transform parent, string name, Vector3 localPosition, Vector2 size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static void Label(Transform parent, string text, Vector3 localPosition, float height, Color color, bool leftAligned = false)
        {
            var name = "Label_" + SafeName(text) + "_" + ColorUtility.ToHtmlStringRGB(color);
            var texture = CreateTextTexture(name, new[] { text }, 8);
            var material = CreateCutoutMaterial(name, texture, Vector2.one, Vector2.zero, color);
            var width = height * texture.width / texture.height;
            if (leftAligned)
                localPosition.x += width * 0.5f;
            Quad(parent, name, localPosition, new Vector2(width, height), material);
        }

        #endregion

        #region Assets

        private static Material CreateColorMaterial(string name, Color color, bool emissive = false)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0.2f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.6f);
            }
            return SaveMaterial(material, name);
        }

        private static Material CreateGlyphMaterial(string name, Texture2D atlas, int glyph) =>
            CreateCutoutMaterial(name, atlas, new Vector2(6f / AtlasColumns, 1f), new Vector2(GlyphOffset(glyph), 0f), ValueColor);

        /// <summary>Texture offset of a glyph of <see cref="NumberGlyphs"/> (glyph i starts at font pixel 1 + 6i).</summary>
        private static float GlyphOffset(float glyph) => (glyph * 6f + 0.5f) / AtlasColumns;

        private static Material CreateCutoutMaterial(string name, Texture2D texture, Vector2 scale, Vector2 offset, Color color)
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(CutoutTemplatePath)) { color = color };
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

        private static string Physical(string assetPath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageRoot);
            return Path.Combine(package.resolvedPath, assetPath.Substring(PackageRoot.Length + 1));
        }

        private static string SafeName(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        /// <summary>Lines of text (white on transparent) as a texture asset, with the 5x7 font of the performance test scene.</summary>
        private static Texture2D CreateTextTexture(string name, string[] lines, int px)
        {
            var path = $"{TextureFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing)
                return existing;

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
                if (!PerformanceTestSceneBuilder.Glyphs.TryGetValue(char.ToUpperInvariant(lines[l][c]), out var glyph))
                    throw new ArgumentException($"No glyph for '{lines[l][c]}' in \"{lines[l]}\"");
                for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (glyph[gy][gx] != '#') continue;
                    int x0 = (1 + c * 6 + gx) * px, y0 = (rows - 1 - l * lineRows - 1 - gy) * px;
                    for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                        pixels[(y0 + y) * texture.width + x0 + x] = new Color32(255, 255, 255, 255);
                }
            }
            texture.SetPixels32(pixels);

            File.WriteAllBytes(Physical(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point; // glyph atlas cells must not bleed into each other
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
            definition.label = "Active Camera";
            definition.description =
                "Visualizes the read-only activeCamera pointers. A crosshair and squares are placed in front of the " +
                "camera every tick and must stay centered and steady while the camera moves; an orange square shows " +
                "the camera of the previous tick for comparison. Billboards use only the camera rotation, arrows only " +
                "the camera position. A panel shows position, rotation, the origin in camera space and the projection values.";
            definition.tags = new[] { "interactivity", "test", "camera" };

            if (isNew)
                AssetDatabase.CreateAsset(definition, path);
            else
                EditorUtility.SetDirty(definition);

            var index = AssetDatabase.LoadAssetAtPath<SampleModelIndex>(IndexPath);
            if (index && !index.models.Contains(definition))
            {
                index.models.Add(definition);
                EditorUtility.SetDirty(index);
            }
        }

        #endregion

        #region Graph

        private static void BuildGraph(SceneRefs refs)
        {
            var g = new GraphBuilder();
            // Initial values (only used in the first tick) in glTF space
            g.Declare("lastTickPoint", ToGltf(refs.camera.TransformPoint(0f, 0f, GhostFrameDistance)));
            g.Declare("lastTickRotation", ToGltfCamera(refs.camera.rotation));
            g.Declare("lastTickPosition", ToGltf(refs.camera.position));

            g.Row(0f);
            var update = g.Add(new Update());
            var tick = g.Sequence(5);
            GraphBuilder.Flow(update.trigger, tick.enter);

            // Camera.main has to be connected directly to the target of the transform/camera members,
            // that's how the exporter maps them to the activeCamera pointers
            var cameraMain = g.Add(new GetMember(new Member(typeof(Camera), nameof(Camera.main))), 0f, 160f).value;
            ValueOutput CameraMember(Type type, string member)
            {
                var unit = g.Add(new GetMember(new Member(type, member)), 0f, 160f);
                GraphBuilder.Value(cameraMain, unit.target);
                return unit.value;
            }
            ValueOutput CameraPoint(string method, Vector3 point)
            {
                var unit = g.Add(new InvokeMember(new Member(typeof(Transform), method, new[] { typeof(Vector3) })), 0f, 160f);
                GraphBuilder.Value(cameraMain, unit.target);
                unit.inputParameters[0].SetDefaultValue(point);
                return unit.result;
            }

            var cameraPosition = CameraMember(typeof(Transform), nameof(Transform.position));
            var cameraRotation = CameraMember(typeof(Transform), nameof(Transform.rotation));

            // ---------------------------------------------------------------- 0: last tick reference
            g.Row(400f);
            var ghostPosition = g.SetTransform(refs.ghostFrame, nameof(Transform.position), g.Get("lastTickPoint"));
            var ghostRotation = g.SetTransform(refs.ghostFrame, nameof(Transform.rotation), g.Get("lastTickRotation"));
            GraphBuilder.Flow(tick.multiOutputs[0], ghostPosition.assign);
            GraphBuilder.Flow(ghostPosition.assigned, ghostRotation.assign);

            // ---------------------------------------------------------------- 1: HUD of this tick
            g.Row(800f);
            ControlInput first = null;
            ControlOutput last = null;
            void Chain(ControlInput enter, ControlOutput exit)
            {
                if (first == null) first = enter;
                else GraphBuilder.Flow(last, enter);
                last = exit;
            }
            foreach (var (target, distance) in new[]
                     {
                         (refs.crosshair, CrosshairDistance), (refs.innerFrame, InnerFrameDistance), (refs.outerFrame, OuterFrameDistance),
                     })
            {
                var setPosition = g.SetTransform(target, nameof(Transform.position), CameraPoint(nameof(Transform.TransformPoint), new Vector3(0f, 0f, -distance)));
                var setRotation = g.SetTransform(target, nameof(Transform.rotation), cameraRotation);
                Chain(setPosition.assign, setPosition.assigned);
                Chain(setRotation.assign, setRotation.assigned);
            }
            var errorPosition = g.SetTransform(refs.flippedError, nameof(Transform.position),
                CameraPoint(nameof(Transform.TransformPoint), new Vector3(0f, 0f, FlippedErrorDistance)));
            var errorRotation = g.SetTransform(refs.flippedError, nameof(Transform.rotation), cameraRotation);
            Chain(errorPosition.assign, errorPosition.assigned);
            Chain(errorRotation.assign, errorRotation.assigned);
            GraphBuilder.Flow(tick.multiOutputs[1], first);

            // ---------------------------------------------------------------- 2: billboards and arrows
            g.Row(1200f);
            first = null;
            foreach (var board in refs.billboards)
            {
                var setRotation = g.SetTransform(board, nameof(Transform.rotation), cameraRotation);
                Chain(setRotation.assign, setRotation.assigned);
            }
            foreach (var arrow in refs.arrows)
            {
                var lookAt = g.Add(new InvokeMember(new Member(typeof(Transform), nameof(Transform.LookAt), new[] { typeof(Vector3) })));
                lookAt.target.SetDefaultValue(arrow);
                GraphBuilder.Value(cameraPosition, lookAt.inputParameters[0]);
                Chain(lookAt.enter, lookAt.exit);
            }
            GraphBuilder.Flow(tick.multiOutputs[2], first);

            // ---------------------------------------------------------------- 3: values
            g.Row(1600f);
            first = null;
            void Show(NumberDisplay display, ValueOutput value)
            {
                var chain = ShowNumber(g, display, value);
                Chain(chain.enter, chain.exit);
            }
            var origin = CameraPoint(nameof(Transform.InverseTransformPoint), Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                Show(refs.position[i], Component(g, typeof(Vector3), cameraPosition, i));
                Show(refs.origin[i], Component(g, typeof(Vector3), origin, i));
            }
            for (int i = 0; i < 4; i++)
                Show(refs.rotation[i], Component(g, typeof(Quaternion), cameraRotation, i));
            Show(refs.moved, g.Call(typeof(Vector3), nameof(Vector3.Distance), new[] { typeof(Vector3), typeof(Vector3) },
                cameraPosition, g.Get("lastTickPosition")));
            Show(refs.fov, CameraMember(typeof(Camera), nameof(Camera.fieldOfView)));
            Show(refs.aspect, CameraMember(typeof(Camera), nameof(Camera.aspect)));
            Show(refs.near, CameraMember(typeof(Camera), nameof(Camera.nearClipPlane)));
            Show(refs.far, CameraMember(typeof(Camera), nameof(Camera.farClipPlane)));
            Show(refs.orthoSize, CameraMember(typeof(Camera), nameof(Camera.orthographicSize)));

            var isOrthographic = g.Add(new If());
            GraphBuilder.Value(CameraMember(typeof(Camera), nameof(Camera.orthographic)), isOrthographic.condition);
            var indicatorOn = g.SetMaterialColor(refs.orthographicIndicator, IndicatorOn);
            var indicatorOff = g.SetMaterialColor(refs.orthographicIndicator, IndicatorOff);
            GraphBuilder.Flow(isOrthographic.ifTrue, indicatorOn.assign);
            GraphBuilder.Flow(isOrthographic.ifFalse, indicatorOff.assign);
            GraphBuilder.Flow(last, isOrthographic.enter);
            GraphBuilder.Flow(tick.multiOutputs[3], first);

            // ---------------------------------------------------------------- 4: remember this tick
            g.Row(2000f);
            var remember = g.SetMany(new List<(string, ValueOutput)>
            {
                ("lastTickPoint", CameraPoint(nameof(Transform.TransformPoint), new Vector3(0f, 0f, -GhostFrameDistance))),
                ("lastTickRotation", cameraRotation),
                ("lastTickPosition", cameraPosition),
            });
            GraphBuilder.Flow(tick.multiOutputs[4], remember.assign);

            var machine = refs.graphObject.AddComponent<ScriptMachine>();
            machine.nest.SwitchToEmbed(g.graph);
        }

        private static Vector3 ToGltf(Vector3 position) => new Vector3(-position.x, position.y, position.z);

        /// <summary>Rotation of the exported glTF camera node: X mirrored and turned by 180 degrees around Y,
        /// since glTF cameras look along -Z.</summary>
        private static Quaternion ToGltfCamera(Quaternion rotation) =>
            new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w) * new Quaternion(0f, 1f, 0f, 0f);

        /// <summary>x, y, z (or w) of a Vector3 / Quaternion.</summary>
        private static ValueOutput Component(GraphBuilder g, Type type, ValueOutput value, int index)
        {
            // Expose unit: what the exporter maps to math/extract
            var unit = g.Add(new Expose(type), 0f, 160f);
            GraphBuilder.Value(value, unit.target);
            return unit.valueOutputs[new[] { "x", "y", "z", "w" }[index]];
        }

        /// <summary>
        /// Sets the glyphs of <paramref name="display"/>: the value is rounded to its decimals and clamped to the
        /// largest number that fits, digit i = floor(scaled / 10^i) mod 10.
        /// </summary>
        private static GraphBuilder.Chain ShowNumber(GraphBuilder g, NumberDisplay display, ValueOutput value)
        {
            var max = Mathf.Pow(10f, display.integerDigits) - Mathf.Pow(10f, -display.decimals);
            var clamped = g.Mathf2(nameof(Mathf.Min), g.Mathf1(nameof(Mathf.Abs), value), g.Literal(max));
            var scaled = g.Mathf1(nameof(Mathf.Round), g.Mul(clamped, Mathf.Pow(10f, display.decimals)));

            GraphBuilder.Chain chain = null;
            void Append(Material material, ValueOutput glyph)
            {
                var offsetX = g.Sum(g.Mul(glyph, 6f / AtlasColumns), 0.5f / AtlasColumns);
                var unit = g.Add(new InvokeMember(new Member(typeof(Material), nameof(Material.SetTextureOffset),
                    new[] { typeof(string), typeof(Vector2) })));
                unit.target.SetDefaultValue(material);
                unit.inputParameters[0].SetDefaultValue("_MainTex");
                GraphBuilder.Value(g.Vec2(offsetX, g.Literal(0f)), unit.inputParameters[1]);
                if (chain == null) chain = new GraphBuilder.Chain { enter = unit.enter };
                else GraphBuilder.Flow(chain.exit, unit.enter);
                chain.exit = unit.exit;
            }

            if (display.signed)
            {
                // ceil(-0.5 * sign): 1 for negative values, 0 otherwise (math/sign(0) is 0, Mathf.Sign(0) is 1)
                var isNegative = g.Mathf1(nameof(Mathf.Ceil), g.Mul(g.Mathf1(nameof(Mathf.Sign), value), -0.5f));
                Append(display.sign, g.Sum(g.Mul(isNegative, GlyphMinus - GlyphBlank), GlyphBlank));
            }

            var count = display.digits.Count;
            for (int i = 0; i < count; i++)
            {
                var power = Mathf.Pow(10f, count - 1 - i);
                var digit = g.Mod(g.Mathf1(nameof(Mathf.Floor), g.Div(scaled, power)), 10f);
                Append(display.digits[i], digit);
            }
            return chain;
        }

        #endregion
    }
}
#endif
