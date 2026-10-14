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
    /// Exporter test scenes for math: Mathf, the Visual Scripting math units, Vector and Quaternion methods/operators and
    /// int/bool math, each compared with the value Unity calculates while generating the scene.
    ///  - "Math": exported without the graph clean up, so every calculation runs in the viewer exactly as exported.
    ///  - "MathCleanUp": exported with the clean up. Each test is built twice: from literals ("FOLDED", the clean up
    ///    pre-calculates these math nodes at export) and from variables set on Start ("RUNTIME", calculated in the
    ///    viewer). A section of clean up cases follows: removed +0 / *1 / double negations, NaN and infinity results
    ///    that must stay runtime calculations, and partially folded expressions.
    /// Re-run the menu items after changing this file.
    /// </summary>
    public static class MathTestSceneBuilder
    {
        public const string SceneName = "20261014-Math";
        public const string CleanUpSceneName = "20261014-MathCleanUp";

        private const int Columns = 4;
        private const float LabelWidth = 3.7f;
        private const float IndicatorX = 3.85f, FoldedX = 3.85f, RuntimeX = 4.45f;
        private const float ColumnWidth = 4.2f, CleanUpColumnWidth = 4.75f;

        private delegate ValueOutput Build(GraphBuilder g, ValueOutput[] x);

        private class MathTest
        {
            public string label;
            public object[] inputs;
            public Build build;
            public object expected;
            public float tolerance;
        }

        private class Section
        {
            public string title;
            public readonly List<MathTest> tests = new List<MathTest>();
        }

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Math (no Clean Up)")]
        public static void CreateMath() => CreateMath(true);

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Math (Clean Up)")]
        public static void CreateMathCleanUp() => CreateMathCleanUp(true);

        public static void CreateMath(bool askToOverwrite)
        {
            var sections = DefineTests();
            var kit = ExporterTestSceneKit.Begin(SceneName, "MATH (NO CLEAN UP)",
                "EXPORTED WITHOUT GRAPH CLEAN UP: ALL MATH RUNS IN THE VIEWER. EXPECTED VALUES = UNITY. FAILURES ARE LOGGED.",
                Columns, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;

            var g = kit.g;
            var index = 0;
            foreach (var section in sections)
            {
                kit.Section(section.title);
                foreach (var test in section.tests)
                {
                    var row = TestRow(kit, ++index, test.label);
                    var indicator = kit.Indicator(row, "Check", IndicatorX);
                    g.Row(index * 300f);
                    var inputs = Array.ConvertAll(test.inputs, kit.LiteralBoxed);
                    AddCheck(kit, indicator, test, test.build(g, inputs), "");
                }
            }
            kit.Finish();
        }

        public static void CreateMathCleanUp(bool askToOverwrite)
        {
            var sections = DefineTests();
            var kit = ExporterTestSceneKit.Begin(CleanUpSceneName, "MATH WITH CLEAN UP",
                "EXPORTED WITH GRAPH CLEAN UP. FOLDED = LITERAL INPUTS, PRE-CALCULATED AT EXPORT. RUNTIME = VARIABLE INPUTS.",
                Columns, CleanUpColumnWidth, cleanUp: true, askToOverwrite);
            if (kit == null)
                return;

            var g = kit.g;
            var index = 0;
            foreach (var section in sections)
            {
                kit.Section(section.title, (FoldedX - 0.08f, "FOLDED"), (RuntimeX - 0.1f, "RUNTIME"));
                foreach (var test in section.tests)
                {
                    var row = TestRow(kit, ++index, test.label);
                    var folded = kit.Indicator(row, "Folded", FoldedX);
                    var runtime = kit.Indicator(row, "Runtime", RuntimeX);

                    g.Row(index * 300f);
                    var literals = Array.ConvertAll(test.inputs, kit.LiteralBoxed);
                    AddCheck(kit, folded, test, test.build(g, literals), " (folded)");

                    // runtime inputs: variables that are written on every run, so the clean up can't fold them
                    var variables = new ValueOutput[test.inputs.Length];
                    for (int i = 0; i < test.inputs.Length; i++)
                    {
                        var name = $"in{index:000}_{i}";
                        g.DeclareBoxed(name, test.inputs[i]);
                        kit.AddReset(g.Set(name, kit.LiteralBoxed(test.inputs[i])).assign);
                        variables[i] = g.Get(name);
                    }
                    AddCheck(kit, runtime, test, test.build(g, variables), " (runtime)");
                }
            }

            BuildCleanUpCases(kit, ref index);
            kit.Finish();
        }

        private static Transform TestRow(ExporterTestSceneKit kit, int index, string label)
        {
            var row = kit.Row();
            row.name = $"Row{index:000}";
            kit.Label(row, label, Vector2.zero, LabelWidth);
            return row;
        }

        private static void AddCheck(ExporterTestSceneKit kit, Material indicator, MathTest test, ValueOutput actual, string variant)
        {
            kit.AddStep(kit.Check(indicator, kit.Matches(actual, test.expected, test.tolerance),
                $"{test.label}{variant}: graph {{0}}, expected {ExporterTestSceneKit.Format(test.expected)}", new[] { actual }));
        }

        #region Clean up cases

        /// <summary>
        /// Cases for the clean up rules. x, v and n are variables (written on every run), so expressions with them stay
        /// runtime calculations and only the rules for removing +0, *1 and double negations apply.
        /// </summary>
        private static void BuildCleanUpCases(ExporterTestSceneKit kit, ref int index)
        {
            var g = kit.g;
            const float x = 3.7f;
            var v = new Vector3(1f, -2f, 3f);
            const int n = 5;
            g.Declare("cleanUpX", x);
            g.Declare("cleanUpV", v);
            g.Declare("cleanUpN", n);
            kit.AddReset(g.Set("cleanUpX", g.Literal(x)).assign);
            kit.AddReset(g.Set("cleanUpV", g.Literal(v)).assign);
            kit.AddReset(g.Set("cleanUpN", g.Literal(n)).assign);

            var localIndex = index;
            void Case(string label, bool folded, object expected, Func<ValueOutput> build)
            {
                var row = TestRow(kit, ++localIndex, label);
                var indicator = kit.Indicator(row, folded ? "Folded" : "Runtime", folded ? FoldedX : RuntimeX);
                g.Row(localIndex * 300f);
                var test = new MathTest { label = label, expected = expected };
                AddCheck(kit, indicator, test, build(), "");
            }

            ValueOutput X() => g.Get("cleanUpX");
            ValueOutput V() => g.Get("cleanUpV");
            ValueOutput Vec3Mul(ValueOutput a, ValueOutput b) => Wire(g, new Vector3Multiply(), "product", new[] { a, b }, "a", "b");
            ValueOutput ScalarMul(ValueOutput a, ValueOutput b) => g.Mul(a, b);

            kit.Section("CLEAN UP: REMOVED NODES (X = 3.7, V = (1 -2 3), N = 5)", (RuntimeX - 0.1f, "RUNTIME"));
            Case("X + 0", false, x, () => g.Sum(X(), 0f));
            Case("0 + X", false, x, () => g.Sum(g.Literal(0f), X()));
            Case("X * 1", false, x, () => g.Mul(X(), 1f));
            Case("1 * X", false, x, () => ScalarMul(g.Literal(1f), X()));
            Case("N + 0 (INT)", false, n, () => g.GenericAdd(g.Get("cleanUpN"), g.Literal(0)));
            Case("V + (0 0 0)", false, v, () => g.Vec3Add(V(), g.Literal(Vector3.zero)));
            Case("V * (1 1 1)", false, v, () => Vec3Mul(V(), g.Literal(Vector3.one)));
            Case("(X * -1) * -1", false, x, () => g.Mul(g.Mul(X(), -1f), -1f));
            Case("(V * -1) * -1 (VECTOR)", false, v, () => Vec3Mul(Vec3Mul(V(), g.Literal(-Vector3.one)), g.Literal(-Vector3.one)));

            // y = x * -1 is used twice, so its negation must stay
            var y = g.Mul(X(), -1f);
            Case("Y = X * -1 USED TWICE: Y * -1", false, x, () => g.Mul(y, -1f));
            Case("Y = X * -1 USED TWICE: Y", false, -x, () => y);

            Case("(2 * 3) + X (PARTIAL FOLD)", false, 6f + x, () => g.Sum(g.Mul(g.Literal(2f), 3f), X()));
            Case("(3 4 0).MAGNITUDE + X (PARTIAL FOLD)", false, 5f + x,
                () => g.Sum(g.Property(typeof(Vector3), nameof(Vector3.magnitude), g.Literal(new Vector3(3f, 4f, 0f))), X()));

            kit.Section("CLEAN UP: CONSTANT EXPRESSIONS", (FoldedX - 0.08f, "FOLDED"));
            Case("((2 + 3) * 4 - 6) / 7", true, ((2f + 3f) * 4f - 6f) / 7f,
                () => g.Div(g.Sub(g.Mul(g.Sum(g.Literal(2f), 3f), 4f), 6f), 7f));
            Case("SIN(COS(0.5)) + SQRT(2)", true, Mathf.Sin(Mathf.Cos(0.5f)) + Mathf.Sqrt(2f),
                () => g.Sum(g.Mathf1(nameof(Mathf.Sin), g.Mathf1(nameof(Mathf.Cos), g.Literal(0.5f))), g.Mathf1(nameof(Mathf.Sqrt), g.Literal(2f))));
            Case("SQRT(-1) IS NAN (NOT FOLDED)", true, true,
                () => g.Call(typeof(float), nameof(float.IsNaN), F1, g.Mathf1(nameof(Mathf.Sqrt), g.Literal(-1f))));
            Case("1 / 0 IS INFINITY (NOT FOLDED)", true, true,
                () => g.Call(typeof(float), nameof(float.IsInfinity), F1, g.Div(g.Literal(1f), 0f)));
            Case("NORMALIZE UNIT (0 0 0)", true, Vector3.zero,
                () => Wire(g, new Vector3Normalize(), "output", new[] { g.Literal(Vector3.zero) }));
            Case("DOT((1 2 3) (4 -5 6)) * 2", true, Vector3.Dot(new Vector3(1, 2, 3), new Vector3(4, -5, 6)) * 2f,
                () => g.Mul(g.Dot(g.Literal(new Vector3(1, 2, 3)), g.Literal(new Vector3(4, -5, 6))), 2f));
            Case("CROSS((1 0 0) (0 1 0)) + (1 1 1)", true, Vector3.Cross(Vector3.right, Vector3.up) + Vector3.one,
                () => g.Vec3Add(g.Cross(g.Literal(Vector3.right), g.Literal(Vector3.up)), g.Literal(Vector3.one)));

            index = localIndex;
        }

        #endregion

        #region Tests

        private static readonly Type[] F1 = { typeof(float) };
        private static readonly Type[] F2 = { typeof(float), typeof(float) };
        private static readonly Type[] F3 = { typeof(float), typeof(float), typeof(float) };
        private static readonly Type[] V2V2 = { typeof(Vector2), typeof(Vector2) };
        private static readonly Type[] V3 = { typeof(Vector3) };
        private static readonly Type[] V3V3 = { typeof(Vector3), typeof(Vector3) };
        private static readonly Type[] V3V3V3 = { typeof(Vector3), typeof(Vector3), typeof(Vector3) };
        private static readonly Type[] V3V3F = { typeof(Vector3), typeof(Vector3), typeof(float) };
        private static readonly Type[] V3F = { typeof(Vector3), typeof(float) };
        private static readonly Type[] FV3 = { typeof(float), typeof(Vector3) };
        private static readonly Type[] V4V4 = { typeof(Vector4), typeof(Vector4) };
        private static readonly Type[] Q1 = { typeof(Quaternion) };
        private static readonly Type[] QQ = { typeof(Quaternion), typeof(Quaternion) };
        private static readonly Type[] QQF = { typeof(Quaternion), typeof(Quaternion), typeof(float) };
        private static readonly Type[] QV3 = { typeof(Quaternion), typeof(Vector3) };

        private static List<Section> DefineTests()
        {
            var sections = new List<Section>();
            Section s = null;
            void Begin(string title) => sections.Add(s = new Section { title = title });
            void T(string label, object expected, Build build, params object[] inputs) =>
                s.tests.Add(new MathTest { label = label, expected = expected, build = build, inputs = inputs });
            void Tolerance(float tolerance) => s.tests[s.tests.Count - 1].tolerance = tolerance;

            var mf = typeof(Mathf);
            var v2 = typeof(Vector2);
            var v3 = typeof(Vector3);
            var q = typeof(Quaternion);

            // ------------------------------------------------------------------ Mathf rounding
            Begin("MATHF ROUNDING");
            T("MATHF.ROUND 2.5 (HALF TO EVEN)", Mathf.Round(2.5f), Call(mf, "Round", F1), 2.5f);
            T("MATHF.ROUND -3.5", Mathf.Round(-3.5f), Call(mf, "Round", F1), -3.5f);
            T("MATHF.ROUND 1.4999", Mathf.Round(1.4999f), Call(mf, "Round", F1), 1.4999f);
            T("MATHF.ROUNDTOINT 3.5", Mathf.RoundToInt(3.5f), Call(mf, "RoundToInt", F1), 3.5f);
            T("MATHF.ROUNDTOINT -2.5", Mathf.RoundToInt(-2.5f), Call(mf, "RoundToInt", F1), -2.5f);
            T("MATHF.ROUNDTOINT 2.7", Mathf.RoundToInt(2.7f), Call(mf, "RoundToInt", F1), 2.7f);
            T("MATHF.FLOORTOINT -1.5", Mathf.FloorToInt(-1.5f), Call(mf, "FloorToInt", F1), -1.5f);
            T("MATHF.CEILTOINT 1.2", Mathf.CeilToInt(1.2f), Call(mf, "CeilToInt", F1), 1.2f);
            T("MATHF.FLOOR -0.5", Mathf.Floor(-0.5f), Call(mf, "Floor", F1), -0.5f);
            T("MATHF.CEIL 1.01", Mathf.Ceil(1.01f), Call(mf, "Ceil", F1), 1.01f);
            T("MATHF.SIGN -2.5", Mathf.Sign(-2.5f), Call(mf, "Sign", F1), -2.5f);
            T("MATHF.SIGN 0 (= 1 IN UNITY)", Mathf.Sign(0f), Call(mf, "Sign", F1), 0f);
            T("MATHF.ABS -3.25", Mathf.Abs(-3.25f), Call(mf, "Abs", F1), -3.25f);
            T("MATHF.CLAMP 5 0 3", Mathf.Clamp(5f, 0f, 3f), Call(mf, "Clamp", F3), 5f, 0f, 3f);
            T("MATHF.CLAMP01 1.5", Mathf.Clamp01(1.5f), Call(mf, "Clamp01", F1), 1.5f);

            // ------------------------------------------------------------------ Mathf functions
            Begin("MATHF FUNCTIONS");
            T("MATHF.SQRT 2", Mathf.Sqrt(2f), Call(mf, "Sqrt", F1), 2f);
            T("MATHF.POW 2 10", Mathf.Pow(2f, 10f), Call(mf, "Pow", F2), 2f, 10f);
            T("MATHF.EXP 1.5", Mathf.Exp(1.5f), Call(mf, "Exp", F1), 1.5f);
            T("MATHF.LOG 10", Mathf.Log(10f), Call(mf, "Log", F1), 10f);
            T("MATHF.LOG 8 BASE 2", Mathf.Log(8f, 2f), Call(mf, "Log", F2), 8f, 2f);
            T("MATHF.LOG10 1000", Mathf.Log10(1000f), Call(mf, "Log10", F1), 1000f);
            T("MATHF.SIN 1", Mathf.Sin(1f), Call(mf, "Sin", F1), 1f);
            T("MATHF.COS 1", Mathf.Cos(1f), Call(mf, "Cos", F1), 1f);
            T("MATHF.TAN 0.5", Mathf.Tan(0.5f), Call(mf, "Tan", F1), 0.5f);
            T("MATHF.ASIN 0.5", Mathf.Asin(0.5f), Call(mf, "Asin", F1), 0.5f);
            T("MATHF.ACOS 0.5", Mathf.Acos(0.5f), Call(mf, "Acos", F1), 0.5f);
            T("MATHF.ATAN 2", Mathf.Atan(2f), Call(mf, "Atan", F1), 2f);
            T("MATHF.ATAN2 1 -1", Mathf.Atan2(1f, -1f), Call(mf, "Atan2", F2), 1f, -1f);
            T("MATHF.MIN 3 -2", Mathf.Min(3f, -2f), Call(mf, "Min", F2), 3f, -2f);
            T("MATHF.MAX 3 -2", Mathf.Max(3f, -2f), Call(mf, "Max", F2), 3f, -2f);
            T("MATHF.REPEAT 7.5 2", Mathf.Repeat(7.5f, 2f), Call(mf, "Repeat", F2), 7.5f, 2f);
            T("MATHF.REPEAT -0.5 2", Mathf.Repeat(-0.5f, 2f), Call(mf, "Repeat", F2), -0.5f, 2f);
            T("MATHF.PINGPONG 3.2 2", Mathf.PingPong(3.2f, 2f), Call(mf, "PingPong", F2), 3.2f, 2f);
            T("MATHF.INVERSELERP 2 6 5", Mathf.InverseLerp(2f, 6f, 5f), Call(mf, "InverseLerp", F3), 2f, 6f, 5f);
            T("MATHF.INVERSELERP 3 3 5 (A = B)", Mathf.InverseLerp(3f, 3f, 5f), Call(mf, "InverseLerp", F3), 3f, 3f, 5f);
            T("MATHF.LERP 0 10 1.5 (CLAMPED)", Mathf.Lerp(0f, 10f, 1.5f), Call(mf, "Lerp", F3), 0f, 10f, 1.5f);
            T("MATHF.LERPUNCLAMPED 0 10 1.5", Mathf.LerpUnclamped(0f, 10f, 1.5f), Call(mf, "LerpUnclamped", F3), 0f, 10f, 1.5f);
            T("MATHF.MOVETOWARDS 1 5 1.5", Mathf.MoveTowards(1f, 5f, 1.5f), Call(mf, "MoveTowards", F3), 1f, 5f, 1.5f);
            T("MATHF.MOVETOWARDS 1 2 5 (REACHED)", Mathf.MoveTowards(1f, 2f, 5f), Call(mf, "MoveTowards", F3), 1f, 2f, 5f);
            T("MATHF.SMOOTHSTEP 0 10 0.3", Mathf.SmoothStep(0f, 10f, 0.3f), Call(mf, "SmoothStep", F3), 0f, 10f, 0.3f);
            T("MATHF.DELTAANGLE 30 300", Mathf.DeltaAngle(30f, 300f), Call(mf, "DeltaAngle", F2), 30f, 300f);
            T("MATHF.DELTAANGLE 350 10", Mathf.DeltaAngle(350f, 10f), Call(mf, "DeltaAngle", F2), 350f, 10f);
            T("MATHF.LERPANGLE 350 30 0.5", Mathf.LerpAngle(350f, 30f, 0.5f), Call(mf, "LerpAngle", F3), 350f, 30f, 0.5f);
            T("MATHF.APPROXIMATELY 1 1.0000001", Mathf.Approximately(1f, 1.0000001f), Call(mf, "Approximately", F2), 1f, 1.0000001f);
            T("MATHF.APPROXIMATELY 1 1.001", Mathf.Approximately(1f, 1.001f), Call(mf, "Approximately", F2), 1f, 1.001f);

            // ------------------------------------------------------------------ Visual Scripting units
            Begin("VISUAL SCRIPTING MATH UNITS");
            T("ROUND UNIT FLOOR -1.5", Mathf.FloorToInt(-1.5f),
                (g, x) => Wire(g, new ScalarRound { rounding = ScalarRound.Rounding.Floor }, "output", x), -1.5f);
            T("ROUND UNIT CEILING 1.1", Mathf.CeilToInt(1.1f),
                (g, x) => Wire(g, new ScalarRound { rounding = ScalarRound.Rounding.Ceiling }, "output", x), 1.1f);
            T("ROUND UNIT AWAY FROM ZERO 2.5", Mathf.RoundToInt(2.5f),
                (g, x) => Wire(g, new ScalarRound { rounding = ScalarRound.Rounding.AwayFromZero }, "output", x), 2.5f);
            var round3 = new Vector3(1.5f, 2.5f, -0.6f);
            T("VECTOR3 ROUND UNIT (1.5 2.5 -0.6)", new Vector3(Mathf.Round(round3.x), Mathf.Round(round3.y), Mathf.Round(round3.z)),
                (g, x) => Wire(g, new Vector3Round(), "output", x), round3);
            var round2 = new Vector2(1.7f, -1.2f);
            T("VECTOR2 ROUND UNIT FLOOR (1.7 -1.2)", new Vector2(Mathf.Floor(round2.x), Mathf.Floor(round2.y)),
                (g, x) => Wire(g, new Vector2Round { rounding = Vector2Round.Rounding.Floor }, "output", x), round2);
            T("AVERAGE UNIT 1 2 6", (1f + 2f + 6f) / 3f, (g, x) => Multi(g, new ScalarAverage { inputCount = 3 }, "average", x), 1f, 2f, 6f);
            T("VECTOR3 AVERAGE UNIT", (new Vector3(1, 2, 3) + new Vector3(3, 2, 1)) / 2f,
                (g, x) => Multi(g, new Vector3Average(), "average", x), new Vector3(1, 2, 3), new Vector3(3, 2, 1));
            T("EXPONENTIATE UNIT 3 ^ 4", Mathf.Pow(3f, 4f), (g, x) => Wire(g, new ScalarExponentiate(), "power", x, "base", "exponent"), 3f, 4f);
            T("ROOT UNIT 27 DEGREE 3", Mathf.Pow(27f, 1f / 3f), (g, x) => Wire(g, new ScalarRoot(), "root", x, "radicand", "degree"), 27f, 3f);
            T("ROOT UNIT 2 DEGREE 2", Mathf.Sqrt(2f), (g, x) => Wire(g, new ScalarRoot(), "root", x, "radicand", "degree"), 2f, 2f);
            T("SCALAR NORMALIZE UNIT -4", -1f, (g, x) => Wire(g, new ScalarNormalize(), "output", x), -4f);
            T("SCALAR NORMALIZE UNIT 0", 0f, (g, x) => Wire(g, new ScalarNormalize(), "output", x), 0f);
            T("SCALAR MOVETOWARDS UNIT 0 10 3", Mathf.MoveTowards(0f, 10f, 3f),
                (g, x) => Wire(g, new ScalarMoveTowards(), "result", x, "current", "target", "maxDelta"), 0f, 10f, 3f);
            T("SCALAR MODULO UNIT -7.5 % 2", -7.5f % 2f, (g, x) => Wire(g, new ScalarModulo(), "remainder", x, "dividend", "divisor"), -7.5f, 2f);
            T("VECTOR3 MODULO UNIT", new Vector3(7f % 3f, -7f % 3f, 5.5f % 2f),
                (g, x) => Wire(g, new Vector3Modulo(), "remainder", x, "dividend", "divisor"), new Vector3(7f, -7f, 5.5f), new Vector3(3f, 3f, 2f));
            T("VECTOR3 ANGLE UNIT", Vector3.Angle(new Vector3(1, 0, 0), new Vector3(1, 1, 0)),
                (g, x) => Wire(g, new Vector3Angle(), "angle", x, "a", "b"), new Vector3(1, 0, 0), new Vector3(1, 1, 0));
            Tolerance(1e-3f);
            T("VECTOR2 ANGLE UNIT", Vector2.Angle(new Vector2(1, 0), new Vector2(-1, 1)),
                (g, x) => Wire(g, new Vector2Angle(), "angle", x, "a", "b"), new Vector2(1, 0), new Vector2(-1, 1));
            Tolerance(1e-3f);
            T("VECTOR3 PROJECT UNIT", Vector3.Project(new Vector3(2, 3, 4), new Vector3(0, 1, 2)),
                (g, x) => Wire(g, new Vector3Project(), "projection", x, "a", "b"), new Vector3(2, 3, 4), new Vector3(0, 1, 2));
            // Visual Scripting's Vector2 Project is Dot(a, b) * b.normalized
            T("VECTOR2 PROJECT UNIT (VS FORMULA)", Vector2.Dot(new Vector2(3, 4), new Vector2(2, 0)) * new Vector2(2, 0).normalized,
                (g, x) => Wire(g, new Vector2Project(), "projection", x, "a", "b"), new Vector2(3, 4), new Vector2(2, 0));
            T("VECTOR3 LERP UNIT 0.25", Vector3.Lerp(new Vector3(0, 2, 4), new Vector3(4, 2, 0), 0.25f),
                (g, x) => Wire(g, new Vector3Lerp(), "interpolation", x, "a", "b", "t"), new Vector3(0, 2, 4), new Vector3(4, 2, 0), 0.25f);
            T("VECTOR3 DISTANCE UNIT", Vector3.Distance(new Vector3(1, 2, 3), new Vector3(4, 6, 3)),
                (g, x) => Wire(g, new Vector3Distance(), "distance", x, "a", "b"), new Vector3(1, 2, 3), new Vector3(4, 6, 3));
            T("VECTOR3 NORMALIZE UNIT (3 0 4)", new Vector3(3, 0, 4).normalized,
                (g, x) => Wire(g, new Vector3Normalize(), "output", x), new Vector3(3, 0, 4));
            T("VECTOR3 CROSS PRODUCT UNIT", Vector3.Cross(new Vector3(1, 2, 3), new Vector3(-2, 0, 1)),
                (g, x) => Wire(g, new Vector3CrossProduct(), "crossProduct", x, "a", "b"), new Vector3(1, 2, 3), new Vector3(-2, 0, 1));
            T("VECTOR3 DOT PRODUCT UNIT", Vector3.Dot(new Vector3(1, 2, 3), new Vector3(-2, 0, 1)),
                (g, x) => Wire(g, new Vector3DotProduct(), "dotProduct", x, "a", "b"), new Vector3(1, 2, 3), new Vector3(-2, 0, 1));
            T("VECTOR3 MINIMUM UNIT", Vector3.Min(new Vector3(1, 5, -3), new Vector3(2, -1, -4)),
                (g, x) => Multi(g, new Vector3Minimum(), "minimum", x), new Vector3(1, 5, -3), new Vector3(2, -1, -4));
            T("VECTOR3 MAXIMUM UNIT", Vector3.Max(new Vector3(1, 5, -3), new Vector3(2, -1, -4)),
                (g, x) => Multi(g, new Vector3Maximum(), "maximum", x), new Vector3(1, 5, -3), new Vector3(2, -1, -4));
            T("VECTOR3 ABSOLUTE UNIT", new Vector3(1.5f, 2f, 0f), (g, x) => Wire(g, new Vector3Absolute(), "output", x), new Vector3(-1.5f, 2f, -0f));

            // ------------------------------------------------------------------ Vector methods
            Begin("VECTOR METHODS AND OPERATORS");
            var a3 = new Vector3(1f, 2f, 3f);
            var b3 = new Vector3(-2f, 0.5f, 4f);
            T("VECTOR3.DOT", Vector3.Dot(a3, b3), Call(v3, "Dot", V3V3), a3, b3);
            T("VECTOR3.SCALE", Vector3.Scale(a3, b3), Call(v3, "Scale", V3V3), a3, b3);
            T("VECTOR3.MIN", Vector3.Min(a3, b3), Call(v3, "Min", V3V3), a3, b3);
            T("VECTOR3.MAX", Vector3.Max(a3, b3), Call(v3, "Max", V3V3), a3, b3);
            T("VECTOR3.CROSS", Vector3.Cross(a3, b3), Call(v3, "Cross", V3V3), a3, b3);
            T("VECTOR3.NORMALIZE (3 0 4)", Vector3.Normalize(new Vector3(3, 0, 4)), Call(v3, "Normalize", V3), new Vector3(3, 0, 4));
            T("VECTOR3.NORMALIZE (0 0 0)", Vector3.Normalize(Vector3.zero), Call(v3, "Normalize", V3), Vector3.zero);
            T("VECTOR3.NORMALIZE (1E-6 0 0) = ZERO", Vector3.Normalize(new Vector3(1e-6f, 0, 0)), Call(v3, "Normalize", V3), new Vector3(1e-6f, 0, 0));
            T("VECTOR2.NORMALIZED (3 4)", new Vector2(3, 4).normalized, Prop(v2, "normalized"), new Vector2(3, 4));
            T("VECTOR3.MAGNITUDE", a3.magnitude, Prop(v3, "magnitude"), a3);
            T("VECTOR3.SQRMAGNITUDE", a3.sqrMagnitude, Prop(v3, "sqrMagnitude"), a3);
            T("VECTOR3.DISTANCE", Vector3.Distance(a3, b3), Call(v3, "Distance", V3V3), a3, b3);
            T("VECTOR3.PROJECT", Vector3.Project(a3, b3), Call(v3, "Project", V3V3), a3, b3);
            T("VECTOR3.PROJECT ON (0 0 0) = ZERO", Vector3.Project(a3, Vector3.zero), Call(v3, "Project", V3V3), a3, Vector3.zero);
            T("VECTOR3.ANGLE", Vector3.Angle(a3, b3), Call(v3, "Angle", V3V3), a3, b3);
            Tolerance(1e-3f);
            T("VECTOR3.ANGLE OPPOSITE = 180", Vector3.Angle(Vector3.right, Vector3.left), Call(v3, "Angle", V3V3), Vector3.right, Vector3.left);
            Tolerance(1e-3f);
            T("VECTOR3.ANGLE WITH (0 0 0) = 0", Vector3.Angle(Vector3.zero, Vector3.right), Call(v3, "Angle", V3V3), Vector3.zero, Vector3.right);
            Tolerance(1e-3f);
            T("VECTOR3.SIGNEDANGLE X TO Z AROUND Y", Vector3.SignedAngle(Vector3.right, Vector3.forward, Vector3.up),
                Call(v3, "SignedAngle", V3V3V3), Vector3.right, Vector3.forward, Vector3.up);
            Tolerance(1e-3f);
            T("VECTOR2.SIGNEDANGLE X TO Y", Vector2.SignedAngle(Vector2.right, Vector2.up), Call(v2, "SignedAngle", V2V2), Vector2.right, Vector2.up);
            Tolerance(1e-3f);
            T("VECTOR3.CLAMPMAGNITUDE (3 4 0) 2.5", Vector3.ClampMagnitude(new Vector3(3, 4, 0), 2.5f), Call(v3, "ClampMagnitude", V3F),
                new Vector3(3, 4, 0), 2.5f);
            T("VECTOR3.CLAMPMAGNITUDE SHORTER", Vector3.ClampMagnitude(new Vector3(0.3f, 0.4f, 0), 2.5f), Call(v3, "ClampMagnitude", V3F),
                new Vector3(0.3f, 0.4f, 0), 2.5f);
            T("VECTOR3.REFLECT", Vector3.Reflect(new Vector3(1, -1, 0.5f), Vector3.up), Call(v3, "Reflect", V3V3), new Vector3(1, -1, 0.5f), Vector3.up);
            T("VECTOR3.MOVETOWARDS", Vector3.MoveTowards(Vector3.zero, new Vector3(3, 4, 0), 2f), Call(v3, "MoveTowards", V3V3F),
                Vector3.zero, new Vector3(3, 4, 0), 2f);
            T("VECTOR3.LERP T 1.5 (CLAMPED)", Vector3.Lerp(a3, b3, 1.5f), Call(v3, "Lerp", V3V3F), a3, b3, 1.5f);
            T("VECTOR3.LERPUNCLAMPED T 1.5", Vector3.LerpUnclamped(a3, b3, 1.5f), Call(v3, "LerpUnclamped", V3V3F), a3, b3, 1.5f);
            T("VECTOR3.SLERP UNIT VECTORS 0.5", Vector3.Slerp(Vector3.right, Vector3.forward, 0.5f), Call(v3, "Slerp", V3V3F),
                Vector3.right, Vector3.forward, 0.5f);
            T("VECTOR3 * 2.5", a3 * 2.5f, Call(v3, "op_Multiply", V3F), a3, 2.5f);
            T("2.5 * VECTOR3", 2.5f * a3, Call(v3, "op_Multiply", FV3), 2.5f, a3);
            T("VECTOR3 / 4", a3 / 4f, Call(v3, "op_Division", V3F), a3, 4f);
            T("VECTOR3 + VECTOR3", a3 + b3, Call(v3, "op_Addition", V3V3), a3, b3);
            T("VECTOR3 - VECTOR3", a3 - b3, Call(v3, "op_Subtraction", V3V3), a3, b3);
            T("-VECTOR3", -a3, Call(v3, "op_UnaryNegation", V3), a3);
            T("VECTOR2 + VECTOR2", new Vector2(1, 2) + new Vector2(0.5f, -3), Call(v2, "op_Addition", V2V2), new Vector2(1, 2), new Vector2(0.5f, -3));
            T("VECTOR4.DOT", Vector4.Dot(new Vector4(1, 2, 3, 4), new Vector4(-1, 0.5f, 2, 1)), Call(typeof(Vector4), "Dot", V4V4),
                new Vector4(1, 2, 3, 4), new Vector4(-1, 0.5f, 2, 1));

            // ------------------------------------------------------------------ Quaternion
            Begin("QUATERNION");
            var q1 = Quaternion.Euler(30f, 45f, 60f);
            var q2 = Quaternion.Euler(-20f, 10f, 70f);
            T("QUATERNION.EULER (30 45 60)", q1, Call(q, "Euler", F3), 30f, 45f, 60f);
            T("QUATERNION.EULERANGLES OF (30 45 60)", q1.eulerAngles, Prop(q, "eulerAngles"), q1);
            Tolerance(0.01f);
            T("QUATERNION * QUATERNION", q1 * q2, Call(q, "op_Multiply", QQ), q1, q2);
            T("QUATERNION * VECTOR3", q1 * a3, Call(q, "op_Multiply", QV3), q1, a3);
            T("QUATERNION.ANGLEAXIS 90 (0 1 0)", Quaternion.AngleAxis(90f, Vector3.up), Call(q, "AngleAxis", FV3), 90f, Vector3.up);
            T("QUATERNION.ANGLE", Quaternion.Angle(q1, q2), Call(q, "Angle", QQ), q1, q2);
            Tolerance(0.01f);
            T("QUATERNION.INVERSE", Quaternion.Inverse(q1), Call(q, "Inverse", Q1), q1);
            T("QUATERNION.DOT", Quaternion.Dot(q1, q2), Call(q, "Dot", QQ), q1, q2);
            T("QUATERNION.LERP 0.3", Quaternion.Lerp(q1, q2, 0.3f), Call(q, "Lerp", QQF), q1, q2, 0.3f);
            T("QUATERNION.LERP 1.5 (CLAMPED)", Quaternion.Lerp(q1, q2, 1.5f), Call(q, "Lerp", QQF), q1, q2, 1.5f);
            T("QUATERNION.LERPUNCLAMPED 1.5", Quaternion.LerpUnclamped(q1, q2, 1.5f), Call(q, "LerpUnclamped", QQF), q1, q2, 1.5f);
            T("QUATERNION.SLERP 0.3", Quaternion.Slerp(q1, q2, 0.3f), Call(q, "Slerp", QQF), q1, q2, 0.3f);
            T("QUATERNION.ROTATETOWARDS 20", Quaternion.RotateTowards(q1, q2, 20f), Call(q, "RotateTowards", QQF), q1, q2, 20f);
            T("QUATERNION.ROTATETOWARDS 500 (REACHED)", Quaternion.RotateTowards(q1, q2, 500f), Call(q, "RotateTowards", QQF), q1, q2, 500f);
            var unnormalized = new Quaternion(1f, 2f, 3f, 4f);
            T("QUATERNION.NORMALIZE (1 2 3 4)", Quaternion.Normalize(unnormalized), Call(q, "Normalize", Q1), unnormalized);
            T("QUATERNION.NORMALIZED (1 2 3 4)", unnormalized.normalized, Prop(q, "normalized"), unnormalized);
            T("QUATERNION.LOOKROTATION (1 0 1) UP Y", Quaternion.LookRotation(new Vector3(1, 0, 1), Vector3.up), Call(q, "LookRotation", V3V3),
                new Vector3(1, 0, 1), Vector3.up);
            T("QUATERNION.FROMTOROTATION X TO (0 1 1)", Quaternion.FromToRotation(Vector3.right, new Vector3(0, 1, 1)),
                Call(q, "FromToRotation", V3V3), Vector3.right, new Vector3(0, 1, 1));

            // ------------------------------------------------------------------ int / bool
            Begin("INT AND BOOL");
            T("INT MAX + 1 (WRAPS)", unchecked(int.MaxValue + 1), (g, x) => Multi(g, new GenericSum(), "sum", x), int.MaxValue, 1);
            T("INT 46341 * 46341 (WRAPS)", unchecked(46341 * 46341), (g, x) => Wire(g, new GenericMultiply(), "product", x, "a", "b"), 46341, 46341);
            T("INT 7 - 10", 7 - 10, (g, x) => Wire(g, new GenericSubtract(), "difference", x, "minuend", "subtrahend"), 7, 10);
            T("INT 7 / 2", 7 / 2, (g, x) => Wire(g, new GenericDivide(), "quotient", x, "dividend", "divisor"), 7, 2);
            T("INT -7 / 2", -7 / 2, (g, x) => Wire(g, new GenericDivide(), "quotient", x, "dividend", "divisor"), -7, 2);
            T("INT -7 % 3", -7 % 3, (g, x) => Wire(g, new GenericModulo(), "remainder", x, "dividend", "divisor"), -7, 3);
            T("TRUE AND FALSE", false, (g, x) => Wire(g, new And(), "result", x, "a", "b"), true, false);
            T("TRUE OR FALSE", true, (g, x) => Wire(g, new Or(), "result", x, "a", "b"), true, false);
            T("TRUE XOR TRUE", false, (g, x) => Wire(g, new ExclusiveOr(), "result", x, "a", "b"), true, true);
            T("NOT FALSE", true, (g, x) => Wire(g, new Negate(), "output", x), false);
            T("2 <= 2", true, (g, x) => Wire(g, new LessOrEqual(), "comparison", x, "a", "b"), 2f, 2f);
            T("3 > 4", false, (g, x) => Wire(g, new Greater(), "comparison", x, "a", "b"), 3f, 4f);
            T("0.1 + 0.2 == 0.3 (NUMERIC EQUAL)", true, (g, x) => Wire(g, new Equal { numeric = true }, "equal",
                new[] { g.Sum(x[0], x[1]), x[2] }, "a", "b"), 0.1f, 0.2f, 0.3f);
            T("1 != 1.001 (NUMERIC NOT EQUAL)", true, (g, x) => Wire(g, new NotEqual { numeric = true }, "notEqual", x, "a", "b"), 1f, 1.001f);

            return sections;
        }

        #endregion

        #region Graph helpers

        /// <summary>Static method call with the inputs as arguments.</summary>
        private static Build Call(Type type, string method, Type[] parameterTypes) => (g, x) => g.Call(type, method, parameterTypes, x);

        /// <summary>Instance property of the first input.</summary>
        private static Build Prop(Type type, string property) => (g, x) => g.Property(type, property, x[0]);

        /// <summary>Adds a unit, connects the inputs to the given ports (default: "input") and returns the output.</summary>
        private static ValueOutput Wire(GraphBuilder g, IUnit unit, string output, ValueOutput[] x, params string[] inputs)
        {
            g.Add(unit, 0f, 60f);
            if (inputs.Length == 0)
                inputs = new[] { "input" };
            for (int i = 0; i < inputs.Length; i++)
                GraphBuilder.Value(x[i], unit.valueInputs[inputs[i]]);
            return unit.valueOutputs[output];
        }

        /// <summary>Multi input unit (sum, average, minimum, ...): one input port per value.</summary>
        private static ValueOutput Multi(GraphBuilder g, IMultiInputUnit unit, string output, ValueOutput[] x)
        {
            g.Add(unit, 0f, 60f);
            for (int i = 0; i < x.Length; i++)
                GraphBuilder.Value(x[i], unit.multiInputs[i]);
            return unit.valueOutputs[output];
        }

        #endregion
    }
}
#endif
