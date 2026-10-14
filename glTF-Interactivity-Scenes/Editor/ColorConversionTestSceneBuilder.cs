#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for the color functions: Color.linear/gamma, Mathf.GammaToLinearSpace/LinearToGammaSpace,
    /// grayscale, maxColorComponent, Color.HSVToRGB/RGBToHSV, the glTF OkLCh units, Color.Lerp and the color operators.
    /// Each row has a plane with the color calculated by the graph, a plane with the color calculated by Unity
    /// (while generating the scene) and a check that compares the values. Both planes are set by the graph on Start,
    /// so they go through the same pointer and look the same when the values match.
    /// Re-run the menu item after changing this file.
    /// </summary>
    public static class ColorConversionTestSceneBuilder
    {
        public const string SceneName = "20261014-ColorConversion";

        private const float LabelWidth = 3.1f;
        private const float GraphSwatchX = 3.2f, UnitySwatchX = 3.8f, IndicatorX = 4.45f;
        private const float ColumnWidth = 4.7f;
        private static readonly Vector2 SwatchSize = new Vector2(0.5f, 0.27f);
        private const float DefaultTolerance = 1e-3f;

        private static readonly Type[] ColorColor = { typeof(Color), typeof(Color) };
        private static readonly Type[] ColorFloat = { typeof(Color), typeof(float) };
        private static readonly Type[] F3 = { typeof(float), typeof(float), typeof(float) };

        private class Result
        {
            public ValueOutput value;   // compared with the expected value
            public ValueOutput display; // shown on the graph plane
            public GraphBuilder.Chain before; // runs before the planes are set, e.g. RGBToHSV with out parameters
        }

        private class ColorTest
        {
            public string label;
            public object expected;  // Color or float
            public Color display;    // shown on the Unity plane, the same transform of the expected value as Result.display
            public Func<GraphBuilder, Result> build;
            public float tolerance = DefaultTolerance;
        }

        private class Section
        {
            public string title;
            public readonly List<ColorTest> tests = new List<ColorTest>();
        }

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Color Conversion")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            var sections = DefineTests();
            var kit = ExporterTestSceneKit.Begin(SceneName, "COLOR CONVERSION",
                "GRAPH PLANE = CALCULATED AT RUNTIME, UNITY PLANE = CALCULATED IN UNITY. BOTH ARE SET BY THE GRAPH ON START.",
                2, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;

            var g = kit.g;
            var index = 0;
            foreach (var section in sections)
            {
                kit.Section(section.title, (GraphSwatchX, "GRAPH"), (UnitySwatchX, "UNITY"));
                foreach (var test in section.tests)
                {
                    index++;
                    var row = kit.Row();
                    row.name = $"Row{index:00}"; // also the prefix of the row's material names
                    kit.Label(row, test.label, new Vector2(0f, 0f), LabelWidth);
                    var graphPlane = kit.Swatch(row, "Graph", GraphSwatchX, SwatchSize, ExporterTestSceneKit.PendingColor);
                    var unityPlane = kit.Swatch(row, "Unity", UnitySwatchX, SwatchSize, test.display);
                    var indicator = kit.Indicator(row, "Check", IndicatorX);

                    g.Row(index * 400f);
                    var result = test.build(g);
                    var setGraph = g.SetMaterialColor(graphPlane, result.display);
                    var setUnity = g.SetMaterialColor(unityPlane, test.display);
                    GraphBuilder.Flow(setGraph.assigned, setUnity.assign);
                    var before = new GraphBuilder.Chain { enter = setGraph.assign, exit = setUnity.assigned };
                    if (result.before != null)
                    {
                        GraphBuilder.Flow(result.before.exit, setGraph.assign);
                        before.enter = result.before.enter;
                    }

                    var ok = kit.Matches(result.value, test.expected, test.tolerance);
                    kit.AddStep(kit.Check(indicator, ok, $"{test.label}: graph {{0}}, expected {ExporterTestSceneKit.Format(test.expected)}",
                        new[] { result.value }, before));
                }
            }

            kit.Finish();
        }

        #region Tests

        private static List<Section> DefineTests()
        {
            var sections = new List<Section>();
            Section current = null;
            void Begin(string title) => sections.Add(current = new Section { title = title });

            // Result is a color, shown as it is
            void ColorTest(string label, Color expected, Func<GraphBuilder, ValueOutput> build, float tolerance = DefaultTolerance) =>
                current.tests.Add(new ColorTest
                {
                    label = label, expected = expected, display = expected, tolerance = tolerance,
                    build = g => { var v = build(g); return new Result { value = v, display = v }; }
                });

            // Result is a color outside of [0, 1], shown scaled (a base color factor must be in [0, 1])
            void ScaledColorTest(string label, Color expected, float scale, Func<GraphBuilder, ValueOutput> build) =>
                current.tests.Add(new ColorTest
                {
                    label = label, expected = expected, display = expected * scale,
                    build = g =>
                    {
                        var v = build(g);
                        return new Result { value = v, display = g.Call(typeof(Color), "op_Multiply", ColorFloat, v, g.Literal(scale)) };
                    }
                });

            // Result is a float, shown as gray: (v * scale + offset)
            void FloatTest(string label, float expected, Func<GraphBuilder, ValueOutput> build, float scale = 1f, float offset = 0f,
                float tolerance = DefaultTolerance)
            {
                var gray = expected * scale + offset;
                current.tests.Add(new ColorTest
                {
                    label = label, expected = expected, display = new Color(gray, gray, gray, 1f), tolerance = tolerance,
                    build = g =>
                    {
                        var v = build(g);
                        var shown = g.Sum(g.Mul(v, scale), offset);
                        return new Result { value = v, display = g.NewColor(shown, shown, shown, g.Literal(1f)) };
                    }
                });
            }

            // Color.RGBToHSV(rgb, out H, out S, out V), compared and shown as Color(H, S, V)
            void RgbToHsvTest(string label, Color rgb)
            {
                Color.RGBToHSV(rgb, out var h, out var s, out var v);
                var expected = new Color(h, s, v, 1f);
                current.tests.Add(new ColorTest
                {
                    label = label, expected = expected, display = expected,
                    build = g =>
                    {
                        var hsv = RgbToHsv(g, g.Literal(rgb));
                        var value = g.NewColor(hsv.valueOutputs["&H"], hsv.valueOutputs["&S"], hsv.valueOutputs["&V"], g.Literal(1f));
                        return new Result { value = value, display = value, before = new GraphBuilder.Chain { enter = hsv.enter, exit = hsv.exit } };
                    }
                });
            }

            // ------------------------------------------------------------------ gamma / linear
            Begin("GAMMA AND LINEAR SPACE");
            {
                var c = new Color(0.5f, 0.25f, 0.75f, 1f);
                ColorTest("COLOR.LINEAR (0.5 0.25 0.75)", c.linear, g => Linear(g, g.Literal(c)));
                var dark = new Color(0.03f, 0.02f, 0.01f, 1f);
                ColorTest("COLOR.LINEAR DARK (0.03 0.02 0.01)", dark.linear, g => Linear(g, g.Literal(dark)));
                var edges = new Color(1f, 0f, 0.04045f, 1f);
                ColorTest("COLOR.LINEAR EDGES (1 0 0.04045)", edges.linear, g => Linear(g, g.Literal(edges)));
                var l = new Color(0.2f, 0.5f, 0.8f, 1f);
                ColorTest("COLOR.GAMMA (0.2 0.5 0.8)", l.gamma, g => Gamma(g, g.Literal(l)));
                var lDark = new Color(0.002f, 0.001f, 0.003f, 1f);
                ColorTest("COLOR.GAMMA DARK (0.002 0.001 0.003)", lDark.gamma, g => Gamma(g, g.Literal(lDark)));
                var roundTrip = new Color(0.3f, 0.6f, 0.9f, 1f);
                ColorTest("COLOR.LINEAR.GAMMA ROUND TRIP", roundTrip.linear.gamma, g => Gamma(g, Linear(g, g.Literal(roundTrip))));
                var alpha = new Color(0.4f, 0.6f, 0.2f, 0.3f);
                ColorTest("COLOR.GAMMA KEEPS ALPHA 0.3", alpha.gamma, g => Gamma(g, g.Literal(alpha)));
                FloatTest("MATHF.GAMMATOLINEARSPACE 0.5", Mathf.GammaToLinearSpace(0.5f),
                    g => g.Mathf1(nameof(Mathf.GammaToLinearSpace), g.Literal(0.5f)));
                FloatTest("MATHF.LINEARTOGAMMASPACE 0.2", Mathf.LinearToGammaSpace(0.2f),
                    g => g.Mathf1(nameof(Mathf.LinearToGammaSpace), g.Literal(0.2f)));
                FloatTest("MATHF.LINEARTOGAMMASPACE 0.001", Mathf.LinearToGammaSpace(0.001f),
                    g => g.Mathf1(nameof(Mathf.LinearToGammaSpace), g.Literal(0.001f)));
            }

            // ------------------------------------------------------------------ properties
            Begin("COLOR PROPERTIES");
            {
                var c = new Color(0.9f, 0.4f, 0.1f, 1f);
                FloatTest("COLOR.GRAYSCALE (0.9 0.4 0.1)", c.grayscale, g => g.Property(typeof(Color), nameof(Color.grayscale), g.Literal(c)));
                var m = new Color(0.3f, 0.8f, 0.5f, 1f);
                FloatTest("COLOR.MAXCOLORCOMPONENT (0.3 0.8 0.5)", m.maxColorComponent,
                    g => g.Property(typeof(Color), nameof(Color.maxColorComponent), g.Literal(m)));
                var a = new Color(0.2f, 0.3f, 0.1f, 0.9f);
                FloatTest("MAXCOLORCOMPONENT IGNORES ALPHA", a.maxColorComponent,
                    g => g.Property(typeof(Color), nameof(Color.maxColorComponent), g.Literal(a)));
            }

            // ------------------------------------------------------------------ HSV
            Begin("HSV");
            {
                // one hue per sector of Unity's switch over (int)(H * 6)
                foreach (var h in new[] { 0.05f, 0.22f, 0.4f, 0.55f, 0.72f, 0.9f })
                    ColorTest($"COLOR.HSVTORGB H {ExporterTestSceneKit.Format(h)} S 0.8 V 0.9", Color.HSVToRGB(h, 0.8f, 0.9f), g => HsvToRgb(g, h, 0.8f, 0.9f));
                ColorTest("COLOR.HSVTORGB S 0 (GRAY)", Color.HSVToRGB(0.3f, 0f, 0.6f), g => HsvToRgb(g, 0.3f, 0f, 0.6f));
                ColorTest("COLOR.HSVTORGB H 1 (SECTOR 6)", Color.HSVToRGB(1f, 0.7f, 0.8f), g => HsvToRgb(g, 1f, 0.7f, 0.8f));
                ColorTest("COLOR.HSVTORGB V 1.5 HDR FALSE", Color.HSVToRGB(0.1f, 0.5f, 1.5f, false), g => HsvToRgb(g, 0.1f, 0.5f, 1.5f, false));
                ScaledColorTest("COLOR.HSVTORGB V 1.5 HDR TRUE (X0.5)", Color.HSVToRGB(0.1f, 0.5f, 1.5f, true), 0.5f,
                    g => HsvToRgb(g, 0.1f, 0.5f, 1.5f, true));

                RgbToHsvTest("COLOR.RGBTOHSV RED (0.8 0.3 0.2)", new Color(0.8f, 0.3f, 0.2f));
                RgbToHsvTest("COLOR.RGBTOHSV GREEN (0.3 0.9 0.4)", new Color(0.3f, 0.9f, 0.4f));
                RgbToHsvTest("COLOR.RGBTOHSV BLUE (0.2 0.4 0.9)", new Color(0.2f, 0.4f, 0.9f));
                RgbToHsvTest("COLOR.RGBTOHSV MAGENTA (0.9 0.2 0.7)", new Color(0.9f, 0.2f, 0.7f));
                RgbToHsvTest("COLOR.RGBTOHSV GRAY (0.5 0.5 0.5)", new Color(0.5f, 0.5f, 0.5f));
                RgbToHsvTest("COLOR.RGBTOHSV BLACK", Color.black);

                var rgb = new Color(0.25f, 0.7f, 0.55f, 1f);
                Color.RGBToHSV(rgb, out var h0, out var s0, out var v0);
                current.tests.Add(new ColorTest
                {
                    label = "RGBTOHSV THEN HSVTORGB", expected = Color.HSVToRGB(h0, s0, v0), display = Color.HSVToRGB(h0, s0, v0),
                    build = g =>
                    {
                        var hsv = RgbToHsv(g, g.Literal(rgb));
                        var value = g.Call(typeof(Color), nameof(Color.HSVToRGB), F3,
                            hsv.valueOutputs["&H"], hsv.valueOutputs["&S"], hsv.valueOutputs["&V"]);
                        return new Result { value = value, display = value, before = new GraphBuilder.Chain { enter = hsv.enter, exit = hsv.exit } };
                    }
                });
            }

            // ------------------------------------------------------------------ OkLCh
            Begin("OKLCH (GLTF UNITS, LINEAR RGB)");
            {
                var orange = new Color(1f, 0.5f, 0f, 1f);
                var lch = GltfRgbToOkLCh.ToOkLCh(orange.linear.r, orange.linear.g, orange.linear.b);
                FloatTest("RGB TO OKLCH (1 0.5 0) L", lch.x, g => ToOkLCh(g, Linear(g, g.Literal(orange))).l);
                FloatTest("RGB TO OKLCH (1 0.5 0) C (X4)", lch.y, g => ToOkLCh(g, Linear(g, g.Literal(orange))).c, 4f);
                FloatTest("RGB TO OKLCH (1 0.5 0) H (RAD)", lch.z, g => ToOkLCh(g, Linear(g, g.Literal(orange))).h,
                    1f / (2f * Mathf.PI), 0.5f);

                var beige = new Color(0.7f, 0.55f, 0.45f, 1f);
                var beigeLch = GltfRgbToOkLCh.ToOkLCh(beige.linear.r, beige.linear.g, beige.linear.b);
                ColorTest("OKLCH ROUND TRIP (0.7 0.55 0.45)", FromOkLCh(beigeLch.x, beigeLch.y, beigeLch.z).gamma, g =>
                {
                    var u = ToOkLCh(g, Linear(g, g.Literal(beige)));
                    return Gamma(g, FromOkLCh(g, u.l, u.c, u.h));
                });
                ColorTest("OKLCH HUE +2 RAD", FromOkLCh(beigeLch.x, beigeLch.y, beigeLch.z + 2f).gamma, g =>
                {
                    var u = ToOkLCh(g, Linear(g, g.Literal(beige)));
                    return Gamma(g, FromOkLCh(g, u.l, u.c, g.Sum(u.h, 2f)));
                });
                ColorTest("OKLCH LIGHTER L +0.1", FromOkLCh(beigeLch.x + 0.1f, beigeLch.y, beigeLch.z).gamma, g =>
                {
                    var u = ToOkLCh(g, Linear(g, g.Literal(beige)));
                    return Gamma(g, FromOkLCh(g, g.Sum(u.l, 0.1f), u.c, u.h));
                });
                ColorTest("RGB FROM OKLCH C 0 (GRAY)", FromOkLCh(0.6f, 0f, 1f).gamma,
                    g => Gamma(g, FromOkLCh(g, g.Literal(0.6f), g.Literal(0f), g.Literal(1f))));
            }

            // ------------------------------------------------------------------ blend / arithmetic
            Begin("LERP AND OPERATORS");
            {
                var a = new Color(0.9f, 0.2f, 0.1f, 1f);
                var b = new Color(0.1f, 0.3f, 0.9f, 1f);
                ColorTest("COLOR.LERP T 0.3", Color.Lerp(a, b, 0.3f), g => ColorLerp(g, nameof(Color.Lerp), a, b, 0.3f));
                ColorTest("COLOR.LERP T 1.5 (CLAMPED)", Color.Lerp(a, b, 1.5f), g => ColorLerp(g, nameof(Color.Lerp), a, b, 1.5f));
                var c = new Color(0.2f, 0.3f, 0.4f, 1f);
                var d = new Color(0.6f, 0.5f, 0.8f, 1f);
                ColorTest("COLOR.LERPUNCLAMPED T 1.25", Color.LerpUnclamped(c, d, 1.25f), g => ColorLerp(g, nameof(Color.LerpUnclamped), c, d, 1.25f));

                var p = new Color(0.3f, 0.2f, 0.5f, 0.5f);
                var q = new Color(0.4f, 0.6f, 0.1f, 0.5f);
                ColorTest("COLOR + COLOR", p + q, g => g.Call(typeof(Color), "op_Addition", ColorColor, g.Literal(p), g.Literal(q)));
                ColorTest("COLOR - COLOR", q - p * 0.5f, g => g.Call(typeof(Color), "op_Subtraction", ColorColor, g.Literal(q), g.Literal(p * 0.5f)));
                var r = new Color(0.8f, 0.5f, 0.9f, 1f);
                ColorTest("COLOR * COLOR", r * d, g => g.Call(typeof(Color), "op_Multiply", ColorColor, g.Literal(r), g.Literal(d)));
                ColorTest("COLOR * FLOAT 0.6", r * 0.6f, g => g.Call(typeof(Color), "op_Multiply", ColorFloat, g.Literal(r), g.Literal(0.6f)));
                ColorTest("COLOR / FLOAT 2", r / 2f, g => g.Call(typeof(Color), "op_Division", ColorFloat, g.Literal(r), g.Literal(2f)));
                ColorTest("NEW COLOR (0.1 0.7 0.4)", new Color(0.1f, 0.7f, 0.4f, 1f),
                    g => g.NewColor(g.Literal(0.1f), g.Literal(0.7f), g.Literal(0.4f), g.Literal(1f)));
            }

            foreach (var section in sections)
            foreach (var test in section.tests)
                if (!InRange(test.display))
                    Debug.LogWarning($"Color test \"{test.label}\": the shown color {test.display} is outside of [0, 1], " +
                                     "glTF base color factors must be in [0, 1]. Scale it for display.");
            return sections;
        }

        private static bool InRange(Color c) =>
            c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f && c.b >= 0f && c.b <= 1f && c.a >= 0f && c.a <= 1f;

        #endregion

        #region Graph helpers

        private static ValueOutput Linear(GraphBuilder g, ValueOutput color) => g.Property(typeof(Color), nameof(Color.linear), color);
        private static ValueOutput Gamma(GraphBuilder g, ValueOutput color) => g.Property(typeof(Color), nameof(Color.gamma), color);

        private static ValueOutput HsvToRgb(GraphBuilder g, float h, float s, float v) =>
            g.Call(typeof(Color), nameof(Color.HSVToRGB), F3, g.Literal(h), g.Literal(s), g.Literal(v));

        private static ValueOutput HsvToRgb(GraphBuilder g, float h, float s, float v, bool hdr) =>
            g.Call(typeof(Color), nameof(Color.HSVToRGB), new[] { typeof(float), typeof(float), typeof(float), typeof(bool) },
                g.Literal(h), g.Literal(s), g.Literal(v), g.Literal(hdr));

        /// <summary>Color.RGBToHSV with out parameters: outputs "&amp;H", "&amp;S", "&amp;V", set when the unit is entered.</summary>
        private static InvokeMember RgbToHsv(GraphBuilder g, ValueOutput rgb)
        {
            var method = typeof(Color).GetMethod(nameof(Color.RGBToHSV));
            var unit = g.Add(new InvokeMember(new Member(typeof(Color), method)));
            GraphBuilder.Value(rgb, unit.inputParameters[0]);
            return unit;
        }

        private static ValueOutput ColorLerp(GraphBuilder g, string method, Color a, Color b, float t) =>
            g.Call(typeof(Color), method, new[] { typeof(Color), typeof(Color), typeof(float) }, g.Literal(a), g.Literal(b), g.Literal(t));

        private static GltfRgbToOkLCh ToOkLCh(GraphBuilder g, ValueOutput linearColor)
        {
            var unit = g.Add(new GltfRgbToOkLCh(), 0f, 160f);
            GraphBuilder.Value(linearColor, unit.color);
            return unit;
        }

        private static ValueOutput FromOkLCh(GraphBuilder g, ValueOutput l, ValueOutput c, ValueOutput h)
        {
            var unit = g.Add(new GltfRgbFromOkLCh(), 0f, 160f);
            GraphBuilder.Value(l, unit.l);
            GraphBuilder.Value(c, unit.c);
            GraphBuilder.Value(h, unit.h);
            return unit.color;
        }

        private static Color FromOkLCh(float l, float c, float h)
        {
            var rgb = GltfRgbFromOkLCh.FromOkLCh(l, c, h);
            return new Color(rgb.x, rgb.y, rgb.z, 1f);
        }

        #endregion
    }
}
#endif
