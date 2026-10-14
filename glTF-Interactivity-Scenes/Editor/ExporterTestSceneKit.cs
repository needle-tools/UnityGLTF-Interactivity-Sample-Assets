#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    internal static class ExporterTestScenesMenu
    {
        [MenuItem("glTF Interactivity/Exporter Test Scenes/Create All")]
        private static void CreateAll()
        {
            if (!EditorUtility.DisplayDialog("Exporter Test Scenes",
                    $"Create or regenerate all exporter test scenes in {ExporterTestSceneKit.ScenesFolder}?", "Create", "Cancel"))
                return;
            ColorConversionTestSceneBuilder.Create(false);
            LoopBreakTestSceneBuilder.Create(false);
            MathTestSceneBuilder.CreateMath(false);
            MathTestSceneBuilder.CreateMathCleanUp(false);
            UpdateEventsTestSceneBuilder.Create(false);
            StatefulUnitsTestSceneBuilder.Create(false);
            CoroutinesTestSceneBuilder.Create(false);
            StaticMembersTestSceneBuilder.Create(false);
        }
    }

    /// <summary>
    /// Shared setup for the generated exporter test scenes in "Test Scenes/--Unity Specific Export Tests--" (internal, not published).
    /// A board shows one row per test with a pass/fail indicator per check, and a summary on top (passed, failed,
    /// overall state, RUN button). The tests run on Start and again on RUN.
    ///
    /// Every check is a leaf of the run sequence (If -> indicator color -> counter -> log -> summary), so branch
    /// exits never merge back into the main chain (see <see cref="GraphBuilder.PerMode"/> for why that matters).
    /// The graphs run in Unity's play mode as well, which verifies the expected values before exporting.
    /// </summary>
    internal class ExporterTestSceneKit
    {
        private const string PackageRoot = "Packages/com.needle.gltf-interactivity-scenes";
        public const string ScenesFolder = PackageRoot + "/Test Scenes/--Unity Specific Export Tests--";
        private const string DigitTemplatePath = PackageRoot + "/Test Scenes/Common/N01.mat";
        /// <summary>Label font atlas and label materials, shared by all exporter test scenes.</summary>
        private const string SharedFolder = ScenesFolder + "/ExporterTestsShared";

        public static readonly Color PendingColor = new Color(0.38f, 0.39f, 0.43f);
        public static readonly Color PassColor = new Color(0.2f, 0.85f, 0.3f);
        public static readonly Color FailColor = new Color(0.95f, 0.18f, 0.12f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color DimTextColor = new Color(0.6f, 0.62f, 0.68f);
        public static readonly Color HeaderColor = new Color(1f, 0.8f, 0.4f);
        private static readonly Color BackdropColor = new Color(0.13f, 0.14f, 0.17f);
        private static readonly Color ButtonColor = new Color(0.78f, 0.80f, 0.84f);

        public const float RowHeight = 0.34f;
        public const float TextHeight = 0.15f;
        public const float IndicatorSize = 0.2f;
        private const float ColumnGap = 0.5f;
        private const float Margin = 0.4f;
        private const float HeaderHeight = 1.5f;

        public readonly GraphBuilder g = new GraphBuilder();
        public readonly string sceneName;
        public readonly float columnWidth;
        private readonly int columns;
        private readonly string folder, scenePath;
        private readonly UnityEngine.SceneManagement.Scene scene;
        private string MaterialFolder => folder + "/Materials";
        private string LabelMeshesPath => folder + "/LabelMeshes.asset";

        private readonly Transform root, header;
        private readonly GameObject graphObject, runButton;
        private readonly List<(Transform row, bool section)> rows = new List<(Transform, bool)>();
        private readonly List<ControlInput> resetSteps = new List<ControlInput>();
        private readonly List<ControlInput> steps = new List<ControlInput>();
        private readonly Material[] passedDigits = new Material[3], failedDigits = new Material[3];
        private readonly Material overallIndicator;
        private Material runDigit;
        private ControlInput summaryEntry;
        private Equal allPassed;
        private int checkCount;
        private readonly Dictionary<string, Mesh> labelMeshes = new Dictionary<string, Mesh>();

        // label font atlas (written once per editor session, see EnsureFontAtlas)
        private static Texture2D fontAtlas;
        private static Dictionary<char, Rect> glyphUvs;

        private static readonly Dictionary<char, string[]> ExtraGlyphs = new Dictionary<char, string[]>
        {
            ['*'] = new[] { ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....." },
            ['<'] = new[] { "...#.", "..#..", ".#...", "#....", ".#...", "..#..", "...#." },
            ['>'] = new[] { ".#...", "..#..", "...#.", "....#", "...#.", "..#..", ".#..." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['_'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "#####" },
            ['['] = new[] { ".###.", ".#...", ".#...", ".#...", ".#...", ".#...", ".###." },
            [']'] = new[] { ".###.", "...#.", "...#.", "...#.", "...#.", "...#.", ".###." },
            ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
            ['&'] = new[] { ".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#" },
            ['|'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        };

        /// <summary>
        /// Starts a new scene "Test Scenes/--Unity Specific Export Tests--/&lt;sceneName&gt;.unity" with its generated assets in a folder of the
        /// same name. Returns null when the user cancels.
        /// </summary>
        /// <param name="cleanUp">Run UnityGLTF's graph clean up (constant folding, deduplication, ...) on export.</param>
        public static ExporterTestSceneKit Begin(string sceneName, string title, string description, int columns, float columnWidth,
            bool cleanUp, bool askToOverwrite = true)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return null;

            var folder = $"{ScenesFolder}/{sceneName}";
            var scenePath = $"{ScenesFolder}/{sceneName}.unity";
            if (askToOverwrite && File.Exists(Physical(scenePath)) &&
                !EditorUtility.DisplayDialog(title, $"{scenePath} already exists. Regenerate it (scene, materials and label textures)?",
                    "Regenerate", "Cancel"))
                return null;

            // Only generated assets (Textures: label textures of older versions)
            AssetDatabase.DeleteAsset(folder + "/Materials");
            AssetDatabase.DeleteAsset(folder + "/Textures");
            AssetDatabase.DeleteAsset(folder + "/LabelMeshes.asset");
            Directory.CreateDirectory(Physical(folder + "/Materials"));
            Directory.CreateDirectory(Physical(SharedFolder));
            AssetDatabase.Refresh();

            if (!AssetDatabase.LoadAssetAtPath<Material>(DigitTemplatePath))
            {
                Debug.LogError($"Digit material template not found at {DigitTemplatePath}");
                return null;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return new ExporterTestSceneKit(scene, sceneName, folder, scenePath, title, description, columns, columnWidth, cleanUp);
        }

        private ExporterTestSceneKit(UnityEngine.SceneManagement.Scene scene, string sceneName, string folder, string scenePath,
            string title, string description, int columns, float columnWidth, bool cleanUp)
        {
            this.scene = scene;
            this.sceneName = sceneName;
            this.folder = folder;
            this.scenePath = scenePath;
            this.columns = columns;
            this.columnWidth = columnWidth;

            // glTF viewers look at the model from +Z and Unity -> glTF only mirrors X, so the board faces Unity's +Z
            // (root turned by 180 degrees) like in the PerformanceTest scene. Board coordinates: x right, y up, top-left at 0.
            root = new GameObject(sceneName.Substring(sceneName.IndexOf('-') + 1)).transform;
            root.localRotation = Quaternion.Euler(0f, 180f, 0f);

            header = new GameObject("Header").transform;
            header.SetParent(root, false);
            var boardWidth = BoardWidth;
            Label(header, title, new Vector2(0f, -0.25f), boardWidth, 0.32f, HeaderColor);
            Label(header, description, new Vector2(0f, -0.62f), boardWidth, 0.13f, DimTextColor);

            // summary line: overall state, PASSED nnn / total, FAILED nnn, RUN button, RUNS n (needs a board width of 7.1)
            overallIndicator = CreateColorMaterial("Overall", PendingColor);
            Box("Overall", header, new Vector3(0.22f, SummaryY, -0.05f), new Vector3(0.44f, 0.44f, 0.1f), overallIndicator, false);
            Label(header, "PASSED", new Vector2(0.6f, SummaryY), 1.2f, 0.2f, TextColor);
            Label(header, "FAILED", new Vector2(3.3f, SummaryY), 1.2f, 0.2f, TextColor);
            for (int i = 0; i < 3; i++)
            {
                passedDigits[i] = CreateDigitMaterial($"PassedDigit{i}", PassColor);
                failedDigits[i] = CreateDigitMaterial($"FailedDigit{i}", FailColor);
                DigitQuad(header, $"PassedDigit{i}", new Vector2(1.6f + i * 0.26f, SummaryY), 0.32f, passedDigits[i]);
                DigitQuad(header, $"FailedDigit{i}", new Vector2(4.3f + i * 0.26f, SummaryY), 0.32f, failedDigits[i]);
            }

            runButton = Box("Button_RUN", header, new Vector3(5.65f, SummaryY, -0.06f), new Vector3(0.9f, 0.36f, 0.12f),
                CreateColorMaterial("Button_RUN", ButtonColor), true);
            // the label is a child of the button, so hits on the label reach the button
            var runLabel = Label(header, "RUN", new Vector2(5.65f - 0.19f, SummaryY), 0.5f, 0.18f, Color.black, -0.13f);
            runLabel.SetParent(runButton.transform, true);
            runDigit = CreateDigitMaterial("RunDigit", TextColor);
            Label(header, "RUNS", new Vector2(6.3f, SummaryY), 0.8f, 0.16f, DimTextColor);
            DigitQuad(header, "RunDigit", new Vector2(6.95f, SummaryY), 0.26f, runDigit);

            graphObject = new GameObject($"{root.name}Graph");
            graphObject.transform.SetParent(root, false);
            var options = graphObject.AddComponent<GltfInteractivityScenes.InteractivityExportOptions>();
            options.cleanUpAndOptimizeExportedGraph = cleanUp;

            g.Declare("passed", 0f);
            g.Declare("failed", 0f);
            g.Declare("runs", 0f);
            BuildSummary();
        }

        private const float SummaryY = -1.1f;

        private float BoardWidth => columns * columnWidth + (columns - 1) * ColumnGap;

        #region Rows

        /// <summary>Adds a row; place its content in row coordinates (x from 0 to <see cref="columnWidth"/>, y = 0 is the row center).</summary>
        public Transform Row()
        {
            var row = new GameObject($"Row{rows.Count:000}").transform;
            row.SetParent(root, false);
            rows.Add((row, false));
            return row;
        }

        /// <summary>Section header row. <paramref name="legend"/> labels the columns of the following rows: (x, text).</summary>
        public Transform Section(string text, params (float x, string text)[] legend)
        {
            var row = new GameObject($"Row{rows.Count:000}_Section").transform;
            row.SetParent(root, false);
            rows.Add((row, true));
            Label(row, text, new Vector2(0f, -0.02f), columnWidth, 0.17f, HeaderColor);
            foreach (var (x, legendText) in legend)
                Label(row, legendText, new Vector2(x, -0.04f), 1.2f, 0.11f, DimTextColor);
            return row;
        }

        /// <summary>Left aligned text; the height shrinks when the text would be wider than <paramref name="maxWidth"/>.</summary>
        public Transform Label(Transform parent, string text, Vector2 position, float maxWidth, float height = TextHeight, Color? color = null,
            float z = -0.01f)
        {
            var mesh = LabelMesh(text, out var aspect);
            height = Mathf.Min(height, maxWidth / aspect);
            var width = height * aspect;
            var label = new GameObject("Label_" + SafeName(text));
            label.AddComponent<MeshFilter>().sharedMesh = mesh;
            label.AddComponent<MeshRenderer>().sharedMaterial = LabelMaterial(color ?? TextColor);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = new Vector3(position.x + width * 0.5f, position.y, z);
            label.transform.localScale = new Vector3(width, height, 1f);
            return label.transform;
        }

        /// <summary>Flat colored box (front at z = -0.04), e.g. to show a color calculated by the graph.</summary>
        public Material Swatch(Transform parent, string name, float x, Vector2 size, Color color)
        {
            var material = CreateColorMaterial($"{parent.name}_{name}", color);
            Box(name, parent, new Vector3(x + size.x * 0.5f, 0f, -0.02f), new Vector3(size.x, size.y, 0.04f), material, false);
            return material;
        }

        /// <summary>Pass/fail indicator, grey until the check ran.</summary>
        public Material Indicator(Transform parent, string name, float x) =>
            Swatch(parent, name, x, new Vector2(IndicatorSize, IndicatorSize), PendingColor);

        /// <summary>Digit quads (0-9 from the number strip), most significant first; set them with <see cref="ShowNumber"/>.</summary>
        public Material[] Digits(Transform parent, string name, float x, int count, float height = 0.22f, Color? color = null)
        {
            var digits = new Material[count];
            for (int i = 0; i < count; i++)
            {
                digits[i] = CreateDigitMaterial($"{parent.name}_{name}{i}", color ?? TextColor);
                DigitQuad(parent, $"{name}{i}", new Vector2(x + i * height * 0.8f + height * 0.4f, 0f), height, digits[i]);
            }
            return digits;
        }

        public static float DigitsWidth(int count, float height = 0.22f) => count * height * 0.8f;

        #endregion

        #region Graph

        /// <summary>Runs on Start and RUN, before the steps (e.g. to reset test variables).</summary>
        public void AddReset(ControlInput reset) => resetSteps.Add(reset);

        /// <summary>Runs on Start and RUN, in the order the steps were added.</summary>
        public void AddStep(ControlInput step) => steps.Add(step);

        public void AddStep(GraphBuilder.Chain chain) => steps.Add(chain.enter);

        /// <summary>
        /// Check leaf: colors the indicator, counts the result, logs failures (with <paramref name="logArgs"/> as {0}, {1}, ...)
        /// and refreshes the summary. <paramref name="before"/> runs first, e.g. to show the checked value.
        /// </summary>
        public ControlInput Check(Material indicator, ValueOutput ok, string failMessage, ValueOutput[] logArgs = null,
            GraphBuilder.Chain before = null)
        {
            checkCount++;
            var branch = g.Add(new If());
            GraphBuilder.Value(ok, branch.condition);

            var pass = g.SetMaterialColor(indicator, PassColor);
            GraphBuilder.Flow(branch.ifTrue, pass.assign);
            var countPass = g.Set("passed", g.Sum(g.Get("passed"), 1f));
            GraphBuilder.Flow(pass.assigned, countPass.assign);
            GraphBuilder.Flow(countPass.assigned, summaryEntry);

            var fail = g.SetMaterialColor(indicator, FailColor);
            GraphBuilder.Flow(branch.ifFalse, fail.assign);
            var countFail = g.Set("failed", g.Sum(g.Get("failed"), 1f));
            GraphBuilder.Flow(fail.assigned, countFail.assign);
            var log = g.Log($"[{sceneName}] FAIL {failMessage}", logArgs ?? Array.Empty<ValueOutput>());
            log.logVerbosity = DebugLogGltf.LogVerbosity.Error;
            GraphBuilder.Flow(countFail.assigned, log.enter);
            GraphBuilder.Flow(log.exit, summaryEntry);

            if (before == null)
                return branch.enter;
            GraphBuilder.Flow(before.exit, branch.enter);
            return before.enter;
        }

        public const float CheckCellStep = 2.4f;

        /// <summary>
        /// Check cell at <paramref name="x"/> (<see cref="CheckCellStep"/> wide): NAME, <paramref name="shown"/> as digits,
        /// the expected value as text, indicator. The value is shown when the check runs.
        /// </summary>
        public ControlInput CheckCell(Transform row, float x, string name, string expected, ValueOutput shown, int digits,
            ValueOutput ok, string failMessage, params ValueOutput[] logArgs)
        {
            const float digitHeight = 0.2f;
            Label(row, name, new Vector2(x, 0f), 0.62f, 0.12f, DimTextColor);
            var digitQuads = Digits(row, name, x + 0.66f, digits, digitHeight);
            var expectedX = x + 0.7f + DigitsWidth(digits, digitHeight);
            Label(row, expected, new Vector2(expectedX, 0f), 0.62f, 0.12f, DimTextColor);
            var indicator = Indicator(row, name, expectedX + 0.64f);
            return Check(indicator, ok, failMessage, logArgs, ShowNumber(digitQuads, shown));
        }

        /// <summary>Shows 0..10^digits-1 (clamped, floored) with the digit quads.</summary>
        public GraphBuilder.Chain ShowNumber(Material[] digits, ValueOutput value)
        {
            var clamped = g.Clamp(value, 0f, Mathf.Pow(10f, digits.Length) - 1f);
            GraphBuilder.Chain chain = null;
            for (int i = 0; i < digits.Length; i++)
            {
                var power = Mathf.Pow(10f, digits.Length - 1 - i);
                var digit = g.Mod(g.Mathf1(nameof(Mathf.Floor), power == 1f ? clamped : g.Div(clamped, power)), 10f);
                var set = g.SetDigit(digits[i], digit);
                if (chain == null) chain = new GraphBuilder.Chain { enter = set.enter };
                else GraphBuilder.Flow(chain.exit, set.enter);
                chain.exit = set.exit;
            }
            return chain;
        }

        // --- comparisons ---

        /// <summary>
        /// actual == expected: exact for bool and int, within <paramref name="tolerance"/> for floats and vector/color
        /// components (0: 1e-4 relative to the magnitude, at least 1e-4). Quaternions compare by |dot| (q and -q are equal).
        /// </summary>
        public ValueOutput Matches(ValueOutput actual, object expected, float tolerance = 0f)
        {
            switch (expected)
            {
                case bool _:
                case int _:
                {
                    var equal = g.Add(new Equal { numeric = false }, 0f, 60f);
                    GraphBuilder.Value(actual, equal.a);
                    GraphBuilder.Value(LiteralBoxed(expected), equal.b);
                    return equal.comparison;
                }
                case float f:
                    return Near(actual, f, tolerance);
                case Vector2 v2:
                    return AllNear(actual, typeof(Vector2), new[] { "x", "y" }, new[] { v2.x, v2.y }, tolerance);
                case Vector3 v3:
                    return AllNear(actual, typeof(Vector3), new[] { "x", "y", "z" }, new[] { v3.x, v3.y, v3.z }, tolerance);
                case Vector4 v4:
                    return AllNear(actual, typeof(Vector4), new[] { "x", "y", "z", "w" }, new[] { v4.x, v4.y, v4.z, v4.w }, tolerance);
                case Color c:
                    return AllNear(actual, typeof(Color), new[] { "r", "g", "b", "a" }, new[] { c.r, c.g, c.b, c.a }, tolerance);
                case Quaternion q:
                {
                    var dot = g.Call(typeof(Quaternion), nameof(Quaternion.Dot), new[] { typeof(Quaternion), typeof(Quaternion) },
                        actual, g.Literal(q));
                    return g.Less(g.Sub(1f, g.Mathf1(nameof(Mathf.Abs), dot)), tolerance > 0f ? tolerance : 2e-6f);
                }
                default:
                    throw new ArgumentException($"Unsupported expected value type {expected?.GetType()}");
            }
        }

        public ValueOutput Near(ValueOutput actual, float expected, float tolerance = 0f)
        {
            if (tolerance <= 0f)
                tolerance = 1e-4f * Mathf.Max(1f, Mathf.Abs(expected));
            // NaN fails: NaN < x is false
            return g.Less(g.Mathf1(nameof(Mathf.Abs), g.Sub(actual, g.Literal(expected))), tolerance);
        }

        private ValueOutput AllNear(ValueOutput actual, Type type, string[] members, float[] expected, float tolerance)
        {
            ValueOutput all = null;
            for (int i = 0; i < members.Length; i++)
            {
                var near = Near(g.Property(type, members[i], actual), expected[i], tolerance);
                all = all == null ? near : g.And(all, near);
            }
            return all;
        }

        /// <summary>Literal of the runtime type of <paramref name="value"/>.</summary>
        public ValueOutput LiteralBoxed(object value) => g.Add(new Literal(value.GetType(), value), 0f, 220f).output;

        /// <summary>Readable value for labels and logs.</summary>
        public static string Format(object value)
        {
            switch (value)
            {
                case float f: return FormatFloat(f);
                case bool b: return b ? "TRUE" : "FALSE";
                case Vector2 v: return $"({FormatFloat(v.x)} {FormatFloat(v.y)})";
                case Vector3 v: return $"({FormatFloat(v.x)} {FormatFloat(v.y)} {FormatFloat(v.z)})";
                case Vector4 v: return $"({FormatFloat(v.x)} {FormatFloat(v.y)} {FormatFloat(v.z)} {FormatFloat(v.w)})";
                case Quaternion q: return $"({FormatFloat(q.x)} {FormatFloat(q.y)} {FormatFloat(q.z)} {FormatFloat(q.w)})";
                case Color c: return $"({FormatFloat(c.r)} {FormatFloat(c.g)} {FormatFloat(c.b)} {FormatFloat(c.a)})";
                default: return value?.ToString() ?? "NULL";
            }
        }

        private static string FormatFloat(float f) => f.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

        private void BuildSummary()
        {
            g.Row(-2000f);
            var showPassed = ShowNumber(passedDigits, g.Get("passed"));
            var showFailed = ShowNumber(failedDigits, g.Get("failed"));
            GraphBuilder.Flow(showPassed.exit, showFailed.enter);

            // red as soon as one check failed, green when all checks passed, grey while checks are outstanding
            var anyFailed = g.Add(new If());
            GraphBuilder.Value(g.GreaterOrEqual(g.Get("failed"), 0.5f), anyFailed.condition);
            GraphBuilder.Flow(showFailed.exit, anyFailed.enter);
            GraphBuilder.Flow(anyFailed.ifTrue, g.SetMaterialColor(overallIndicator, FailColor).assign);

            allPassed = g.Add(new Equal { numeric = true });
            GraphBuilder.Value(g.Get("passed"), allPassed.a);
            var complete = g.Add(new If());
            GraphBuilder.Value(allPassed.comparison, complete.condition);
            GraphBuilder.Flow(anyFailed.ifFalse, complete.enter);
            GraphBuilder.Flow(complete.ifTrue, g.SetMaterialColor(overallIndicator, PassColor).assign);
            GraphBuilder.Flow(complete.ifFalse, g.SetMaterialColor(overallIndicator, PendingColor).assign);

            summaryEntry = showPassed.enter;
        }

        /// <summary>Lays out the board, wires Start/RUN, places camera and light and saves the scene.</summary>
        public void Finish()
        {
            allPassed.b.SetDefaultValue((float)checkCount);
            Label(header, $"/ {checkCount}", new Vector2(2.32f, SummaryY), 0.8f, 0.2f, DimTextColor);

            // --- run: reset counters, the tests' resets, then all steps in order, then the summary
            g.Row(-2400f);
            var resetPassed = g.Set("passed", g.Literal(0f));
            var resetFailed = g.Set("failed", g.Literal(0f));
            var countRun = g.Set("runs", g.Mod(g.Sum(g.Get("runs"), 1f), 10f));
            var showRun = g.SetDigit(runDigit, g.Get("runs"));
            GraphBuilder.Flow(resetPassed.assigned, resetFailed.assign);
            GraphBuilder.Flow(resetFailed.assigned, countRun.assign);
            GraphBuilder.Flow(countRun.assigned, showRun.enter);
            var resetOverall = g.SetMaterialColor(overallIndicator, PendingColor);
            GraphBuilder.Flow(showRun.exit, resetOverall.assign);

            var targets = new List<ControlInput> { resetPassed.assign };
            targets.AddRange(resetSteps);
            targets.AddRange(steps);
            targets.Add(summaryEntry);

            var start = g.Add(new Start());
            var click = g.OnClick(runButton);
            var entry = g.Sequence(1);
            GraphBuilder.Flow(start.trigger, entry.enter);
            GraphBuilder.Flow(click.trigger, entry.enter);
            g.FanOut(entry.multiOutputs[0], targets);

            PerformanceTestSceneBuilder.CheckControlPaths(g.graph);
            var machine = graphObject.AddComponent<ScriptMachine>();
            machine.nest.SwitchToEmbed(g.graph);

            LayOut();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Exporter test scene written to {scenePath} ({checkCount} checks, {g.graph.units.Count} units)");
        }

        /// <summary>Distributes the rows over the columns, then adds the backdrop, camera and light.</summary>
        private void LayOut()
        {
            var slots = 0;
            foreach (var (_, section) in rows)
                slots += section ? 2 : 1; // sections get a gap above
            var rowsPerColumn = Mathf.Max(1, Mathf.CeilToInt(slots / (float)columns));
            int column = 0, slot = 0;
            foreach (var (row, section) in rows)
            {
                // a section header never ends a column; the last column takes what is left
                if (column < columns - 1 && (slot >= rowsPerColumn || (section && slot >= rowsPerColumn - 2)))
                {
                    column++;
                    slot = 0;
                }
                if (section && slot > 0)
                    slot++; // gap above a section
                row.localPosition = new Vector3(column * (columnWidth + ColumnGap), -HeaderHeight - (slot + 0.5f) * RowHeight, 0f);
                slot++;
            }

            var lowest = 0f;
            foreach (var (row, _) in rows)
                lowest = Mathf.Min(lowest, row.localPosition.y - RowHeight * 0.5f);

            var width = BoardWidth + 2f * Margin;
            var height = -lowest + 2f * Margin;
            var center = new Vector2(BoardWidth * 0.5f, lowest * 0.5f);
            var backdrop = Box("Backdrop", root, new Vector3(center.x, center.y, 0.06f), new Vector3(width, height, 0.1f),
                CreateColorMaterial("Backdrop", BackdropColor), false);
            backdrop.transform.SetAsFirstSibling();

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            var halfFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var distance = Mathf.Max(height * 0.5f / halfFov, width * 0.5f / (halfFov * 16f / 9f)) * 1.05f;
            // the camera looks at the front of the board, which faces root-local -Z
            cameraGo.transform.SetPositionAndRotation(root.TransformPoint(new Vector3(center.x, center.y, -distance)),
                Quaternion.Euler(0f, 180f, 0f));

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
        }

        #endregion

        #region Objects and assets

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

        private static Transform Quad(Transform parent, string name, Vector3 position, Vector2 size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            return quad.transform;
        }

        private static void DigitQuad(Transform parent, string name, Vector2 position, float height, Material material)
        {
            // numberstrip.png holds 10 digits of 51.2 x 64 px
            Quad(parent, name, new Vector3(position.x, position.y, -0.01f), new Vector2(height * 0.8f, height), material);
        }

        private Material CreateColorMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0.2f);
            return SaveMaterial(material, name);
        }

        private Material CreateDigitMaterial(string name, Color color)
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(DigitTemplatePath)) { color = color };
            material.mainTextureScale = new Vector2(0.1f, 1f);
            material.mainTextureOffset = Vector2.zero;
            return SaveMaterial(material, name);
        }

        // names come from row and cell names, which can hold characters that aren't valid in file names (e.g. "R > 0.5")
        private Material SaveMaterial(Material material, string name) => SaveMaterial(material, MaterialFolder, SafeName(name));

        private static Material SaveMaterial(Material material, string folder, string name)
        {
            var path = $"{folder}/{name}.mat";
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

        // --- labels: one shared font atlas, one material per text color, one mesh per text (a quad per character) ---

        private const int FontPixel = 8;                 // texels per font pixel
        private const int CharColumns = 6, CharRows = 9; // character box in font pixels: 5x7 glyph, spacing column, a row above and below
        private const int AtlasCellColumns = CharColumns + 2, AtlasCellRows = CharRows + 2; // + empty border against filtering bleed
        private const int AtlasCellsPerRow = 16;

        /// <summary>Label material of <paramref name="color"/>, shared by all scenes.</summary>
        private static Material LabelMaterial(Color color)
        {
            EnsureFontAtlas();
            var name = "Label_" + ColorUtility.ToHtmlStringRGB(color);
            var existing = AssetDatabase.LoadAssetAtPath<Material>($"{SharedFolder}/{name}.mat");
            if (existing && existing.mainTexture == fontAtlas)
                return existing;

            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(DigitTemplatePath)) { color = color };
            material.mainTexture = fontAtlas;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            material.SetTextureScale("_EmissionMap", Vector2.one);
            material.SetTextureOffset("_EmissionMap", Vector2.zero);
            return SaveMaterial(material, SharedFolder, name);
        }

        /// <summary>
        /// Mesh for one line of text in quad space (x and y from -0.5 to 0.5, facing -Z like Unity's quad); the label
        /// transform scales it to width = <paramref name="aspect"/> * height. Same text, same mesh.
        /// </summary>
        private Mesh LabelMesh(string text, out float aspect)
        {
            var columns = text.Length * CharColumns + 1;
            aspect = columns / (float)CharRows;
            if (labelMeshes.TryGetValue(text, out var mesh))
                return mesh;

            EnsureFontAtlas();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int c = 0; c < text.Length; c++)
            {
                if (!glyphUvs.TryGetValue(char.ToUpperInvariant(text[c]), out var uv))
                    throw new ArgumentException($"No glyph for '{text[c]}' in \"{text}\"");
                if (uv.width <= 0f)
                    continue; // blank

                float x0 = (1 + c * CharColumns) / (float)columns - 0.5f, x1 = x0 + CharColumns / (float)columns;
                int i = vertices.Count;
                vertices.Add(new Vector3(x0, -0.5f)); uvs.Add(new Vector2(uv.xMin, uv.yMin));
                vertices.Add(new Vector3(x1, -0.5f)); uvs.Add(new Vector2(uv.xMax, uv.yMin));
                vertices.Add(new Vector3(x0, 0.5f)); uvs.Add(new Vector2(uv.xMin, uv.yMax));
                vertices.Add(new Vector3(x1, 0.5f)); uvs.Add(new Vector2(uv.xMax, uv.yMax));
                triangles.AddRange(new[] { i, i + 3, i + 1, i + 3, i, i + 2 }); // winding of Unity's quad
            }

            mesh = new Mesh { name = "Label_" + SafeName(text) };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(Enumerable.Repeat(Vector3.back, vertices.Count).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            if (labelMeshes.Count == 0)
                AssetDatabase.CreateAsset(mesh, LabelMeshesPath);
            else
                AssetDatabase.AddObjectToAsset(mesh, LabelMeshesPath);
            labelMeshes.Add(text, mesh);
            return mesh;
        }

        /// <summary>
        /// Writes the label font atlas (all glyphs, white on transparent) once per editor session and maps each character
        /// to the UV rectangle of its character box. Blank glyphs get an empty rectangle.
        /// </summary>
        private static void EnsureFontAtlas()
        {
            if (fontAtlas && glyphUvs != null)
                return;

            var glyphs = new Dictionary<char, string[]>(PerformanceTestSceneBuilder.Glyphs);
            foreach (var pair in ExtraGlyphs)
                glyphs[pair.Key] = pair.Value;
            var chars = glyphs.Keys.Where(ch => glyphs[ch].Any(line => line.Contains('#'))).OrderBy(ch => ch).ToList();

            int width = AtlasCellsPerRow * AtlasCellColumns * FontPixel;
            int height = Mathf.CeilToInt(chars.Count / (float)AtlasCellsPerRow) * AtlasCellRows * FontPixel;
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 0);

            var uvs = glyphs.Keys.ToDictionary(ch => ch, ch => Rect.zero);
            for (int n = 0; n < chars.Count; n++)
            {
                // character box in texels, texture origin is bottom-left
                int left = ((n % AtlasCellsPerRow) * AtlasCellColumns + 1) * FontPixel;
                int top = height - ((n / AtlasCellsPerRow) * AtlasCellRows + 1) * FontPixel;
                var glyph = glyphs[chars[n]];
                for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (glyph[gy][gx] != '#') continue;
                    int x0 = left + gx * FontPixel, y0 = top - (gy + 2) * FontPixel;
                    for (int y = 0; y < FontPixel; y++)
                    for (int x = 0; x < FontPixel; x++)
                        pixels[(y0 + y) * width + x0 + x] = new Color32(255, 255, 255, 255);
                }
                uvs[chars[n]] = new Rect(left / (float)width, (top - CharRows * FontPixel) / (float)height,
                    CharColumns * FontPixel / (float)width, CharRows * FontPixel / (float)height);
            }

            var path = SharedFolder + "/LabelFont.png";
            Directory.CreateDirectory(Physical(SharedFolder));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(Physical(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            fontAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            glyphUvs = uvs;
        }

        /// <summary>Asset path inside this (file:) package to a path on disk.</summary>
        private static string Physical(string assetPath)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(PackageRoot);
            return Path.Combine(package.resolvedPath, assetPath.Substring(PackageRoot.Length + 1));
        }

        /// <summary>Text as a file/asset name: letters and digits kept, everything else replaced, long names shortened.</summary>
        private static string SafeName(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            var safe = new string(chars);
            if (safe.Length > 48)
            {
                uint hash = 2166136261; // FNV-1a, stable between editor sessions (unlike string.GetHashCode)
                foreach (var ch in text)
                    hash = (hash ^ ch) * 16777619;
                safe = safe.Substring(0, 40) + "_" + hash.ToString("x8");
            }
            return safe;
        }

        #endregion
    }
}
#endif
