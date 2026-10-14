#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for static members (Get Member units without a target):
    ///  - constant-like members are baked into the export: const and readonly fields (Mathf.PI, Mathf.Epsilon,
    ///    float.MaxValue, Vector3.kEpsilon) and static properties of structs (Vector3.zero, Color.red, Matrix4x4.identity).
    ///    Each is compared with the value Unity has while generating the scene.
    ///  - runtime members must not be baked (they were frozen to their editor value before): Random.value, rotation,
    ///    rotationUniform and insideUnitCircle are sampled 64 times per run (range, spread, mean and that the values
    ///    change), the Time members are read at 0.5 s and 1.5 s and must advance like the run clock.
    /// Unregistered runtime members (e.g. Random.onUnitSphere, Screen.width) aren't exported at all, so they aren't
    /// part of this scene. Re-run the menu item after changing this file.
    /// </summary>
    public static class StaticMembersTestSceneBuilder
    {
        public const string SceneName = "20261014-StaticMembers";

        private const float Deadline = 2f;
        private const float MarkStart = 0.5f, MarkEnd = 1.5f;
        private const int Samples = 64;

        private const float LabelWidth = 2.8f;
        private const float FirstCellX = 2.9f;
        private const float ColumnWidth = FirstCellX + 4 * ExporterTestSceneKit.CheckCellStep;
        private const int ConstantsPerRow = 5;
        private const float ConstantCellWidth = ColumnWidth / ConstantsPerRow;

        private static readonly Type[] QuaternionQuaternion = { typeof(Quaternion), typeof(Quaternion) };
        private static readonly Type[] QuaternionVector3 = { typeof(Quaternion), typeof(Vector3) };

        private class ConstantTest
        {
            public string label;
            public Type type;
            public string member;
            public string expectedText;
            public Func<ValueOutput, ValueOutput> ok;
        }

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Static Members")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            var kit = ExporterTestSceneKit.Begin(SceneName, "STATIC MEMBERS",
                $"CONSTANTS ARE BAKED AT EXPORT, RANDOM AND TIME MEMBERS MUST CHANGE AT RUNTIME. TIME CHECKS AT {Deadline} S, TIMES IN MS.",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            var timed = new ExporterTestTimedTests(kit, Deadline);

            BuildConstants(kit, "CONSTANT AND READONLY FIELDS (BAKED)", ConstantFields(kit), 0f);
            BuildConstants(kit, "STATIC PROPERTIES OF STRUCTS (BAKED)", StructProperties(kit), 6000f);
            BuildRandom(kit, 12000f);
            BuildTime(kit, timed, 18000f);

            timed.Build();
            kit.Finish();
        }

        #region Constants

        private static ValueOutput StaticGet(ExporterTestSceneKit kit, Type type, string member) =>
            ExporterTestTimedTests.StaticGet(kit.g, type, member);

        /// <summary>Compared with the member's value in Unity, read by reflection.</summary>
        private static ConstantTest Same(ExporterTestSceneKit kit, Type type, string member, float tolerance = 0f, string typeLabel = null)
        {
            var expected = (object)type.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                           ?? type.GetProperty(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (expected == null)
                throw new ArgumentException($"{type.Name}.{member} has no static value");
            return new ConstantTest
            {
                label = $"{typeLabel ?? type.Name}.{member}".ToUpperInvariant(), type = type, member = member,
                expectedText = ExporterTestSceneKit.Format(expected),
                ok = actual => kit.Matches(actual, expected, tolerance),
            };
        }

        private const float Scale = 1e20f;

        private static List<ConstantTest> ConstantFields(ExporterTestSceneKit kit)
        {
            var g = kit.g;
            // Visual Scripting serializes float literals as decimals, which only hold about 1e-28 to 7.9e28 (larger
            // values fail to serialize, smaller ones become 0), so huge and tiny values are scaled before comparing
            ConstantTest Huge(string label, string member, float expected) => new ConstantTest
            {
                label = label, type = typeof(float), member = member, expectedText = ExporterTestSceneKit.Format(expected),
                ok = v => kit.Near(g.Mul(v, 1f / Scale), expected / Scale),
            };
            return new List<ConstantTest>
            {
                Same(kit, typeof(Mathf), nameof(Mathf.PI)),
                Same(kit, typeof(Mathf), nameof(Mathf.Deg2Rad)),
                Same(kit, typeof(Mathf), nameof(Mathf.Rad2Deg)),
                // readonly (1.4e-45, or the smallest normal float when denormals are flushed to zero)
                new ConstantTest
                {
                    label = "MATHF.EPSILON", type = typeof(Mathf), member = nameof(Mathf.Epsilon), expectedText = "(0, 1E-30)",
                    ok = v => g.And(g.Greater(v, g.Literal(0f)), g.Less(g.Mul(v, Scale), 1e-30f * Scale)),
                },
                new ConstantTest
                {
                    label = "MATHF.INFINITY", type = typeof(Mathf), member = nameof(Mathf.Infinity), expectedText = "> 1E38",
                    ok = v => g.GreaterOrEqual(g.Mul(v, 1f / Scale), 1e38f / Scale),
                },
                // Mathf.NegativeInfinity isn't tested: a baked -Infinity can't be written to the glTF JSON
                Huge("FLOAT.MAXVALUE", nameof(float.MaxValue), float.MaxValue),
                Huge("FLOAT.MINVALUE", nameof(float.MinValue), float.MinValue),
                Same(kit, typeof(int), nameof(int.MaxValue), typeLabel: "INT"),
                Same(kit, typeof(int), nameof(int.MinValue), typeLabel: "INT"),
                Same(kit, typeof(Vector3), nameof(Vector3.kEpsilon), 1e-8f),
                Same(kit, typeof(Quaternion), nameof(Quaternion.kEpsilon), 1e-9f),
            };
        }

        private static List<ConstantTest> StructProperties(ExporterTestSceneKit kit)
        {
            var g = kit.g;
            var tests = new List<ConstantTest>();
            foreach (var member in new[] { "zero", "one", "up", "down", "left", "right" })
                tests.Add(Same(kit, typeof(Vector2), member));
            foreach (var member in new[] { "zero", "one", "up", "down", "left", "right", "forward", "back" })
                tests.Add(Same(kit, typeof(Vector3), member));
            tests.Add(Same(kit, typeof(Vector4), nameof(Vector4.zero)));
            tests.Add(Same(kit, typeof(Vector4), nameof(Vector4.one)));
            tests.Add(Same(kit, typeof(Quaternion), nameof(Quaternion.identity)));
            foreach (var member in new[] { "red", "green", "blue", "white", "black", "yellow", "cyan", "magenta", "gray", "clear" })
                tests.Add(Same(kit, typeof(Color), member));

            ValueOutput M(ValueOutput m, string element) => g.Property(typeof(Matrix4x4), element, m);
            ValueOutput AbsSum(ValueOutput m, params string[] elements)
            {
                ValueOutput sum = null;
                foreach (var element in elements)
                {
                    var abs = g.Mathf1(nameof(Mathf.Abs), M(m, element));
                    sum = sum == null ? abs : g.Sum(sum, abs);
                }
                return sum;
            }
            tests.Add(new ConstantTest
            {
                label = "MATRIX4X4.IDENTITY", type = typeof(Matrix4x4), member = nameof(Matrix4x4.identity), expectedText = "DIAGONAL 1",
                ok = m => g.And(
                    kit.Near(g.Sum(g.Sum(M(m, "m00"), M(m, "m11")), g.Sum(M(m, "m22"), M(m, "m33"))), 4f),
                    kit.Near(AbsSum(m, "m01", "m10", "m03", "m30", "m12", "m23", "m32", "m20"), 0f)),
            });
            tests.Add(new ConstantTest
            {
                label = "MATRIX4X4.ZERO", type = typeof(Matrix4x4), member = nameof(Matrix4x4.zero), expectedText = "ALL 0",
                ok = m => kit.Near(AbsSum(m, "m00", "m11", "m22", "m33", "m03", "m30"), 0f),
            });
            return tests;
        }

        /// <summary>Rows of <see cref="ConstantsPerRow"/> cells (name, indicator), checked on Start and RUN.</summary>
        private static void BuildConstants(ExporterTestSceneKit kit, string title, List<ConstantTest> tests, float graphY)
        {
            var g = kit.g;
            kit.Section(title);
            Transform row = null;
            for (int i = 0; i < tests.Count; i++)
            {
                var test = tests[i];
                if (i % ConstantsPerRow == 0)
                {
                    row = kit.Row();
                    row.name = $"Row_{title.Substring(0, 8).Replace(' ', '_')}{i / ConstantsPerRow:00}";
                }
                var x = (i % ConstantsPerRow) * ConstantCellWidth;
                kit.Label(row, test.label, new Vector2(x, 0f), ConstantCellWidth - 0.45f, 0.12f, ExporterTestSceneKit.DimTextColor);
                var indicator = kit.Indicator(row, test.label, x + ConstantCellWidth - 0.4f);

                g.Row(graphY + i * 300f);
                var actual = StaticGet(kit, test.type, test.member);
                kit.AddStep(kit.Check(indicator, test.ok(actual), $"{test.label}: graph {{0}}, expected {test.expectedText}", new[] { actual }));
            }
        }

        #endregion

        #region Random

        /// <summary>
        /// Each random member is sampled <see cref="Samples"/> times in a For loop on Start and RUN. A baked value would be
        /// the same every time, so "CHANGES" counts the samples that differ from the previous one.
        /// </summary>
        private static void BuildRandom(ExporterTestSceneKit kit, float graphY)
        {
            var g = kit.g;
            kit.Section($"RANDOM (RUNTIME VALUES, {Samples} SAMPLES PER RUN)");

            string Var(string name, object initial)
            {
                g.DeclareBoxed(name, initial);
                kit.AddReset(g.Set(name, kit.LiteralBoxed(initial)).assign);
                return name;
            }
            ValueOutput Get(string name) => g.Get(name);
            SetVariable Count(string name) => g.Set(name, g.Sum(Get(name), 1f));
            ControlInput CountIf(ValueOutput condition, string counter)
            {
                var branch = g.Add(new If());
                GraphBuilder.Value(condition, branch.condition);
                GraphBuilder.Flow(branch.ifTrue, Count(counter).assign);
                return branch.enter;
            }
            ValueOutput Abs(ValueOutput v) => g.Mathf1(nameof(Mathf.Abs), v);
            var expectedSamples = $"={Samples}";
            var expectedChanges = $"> {Samples - 2}";
            ValueOutput AllSamples(string counter) => g.GreaterOrEqual(Get(counter), Samples - 0.5f);
            ValueOutput MostChanged(string counter) => g.GreaterOrEqual(Get(counter), Samples - 1.5f);

            // one row per member: For loop -> sample into a variable -> statistics, checks after the loop
            Transform row = null;
            int cell = 0;
            List<ControlInput> checks = null;
            void Row(string label, float y)
            {
                row = kit.Row();
                row.name = "Row_" + label.Replace(' ', '_').Replace('.', '_');
                kit.Label(row, label, Vector2.zero, LabelWidth);
                cell = 0;
                checks = new List<ControlInput>();
                g.Row(y);
            }
            void Cell(string name, string expected, ValueOutput shown, int digits, ValueOutput ok, string message, params ValueOutput[] logArgs) =>
                checks.Add(kit.CheckCell(row, FirstCellX + cell++ * ExporterTestSceneKit.CheckCellStep, name, expected, shown, digits, ok,
                    message, logArgs));
            void Loop(SetVariable sample, List<ControlInput> statistics)
            {
                var loop = g.Loop(Samples);
                GraphBuilder.Flow(loop.body, sample.assign);
                g.FanOut(sample.assigned, statistics);
                g.FanOut(loop.exit, checks);
                kit.AddStep(loop.enter);
            }

            // --- Random.value
            {
                const string p = "randomValue";
                var sample = Var(p + "Sample", 0f);
                var previous = Var(p + "Previous", -1f);
                var inRange = Var(p + "InRange", 0f);
                var changes = Var(p + "Changes", 0f);
                var min = Var(p + "Min", 2f);
                var max = Var(p + "Max", -1f);
                var sum = Var(p + "Sum", 0f);

                Row("RANDOM.VALUE", graphY);
                var setSample = g.Set(sample, StaticGet(kit, typeof(Random), nameof(Random.value)));
                var s = Get(sample);
                var statistics = new List<ControlInput>
                {
                    CountIf(g.And(g.GreaterOrEqual(s, 0f), g.Less(s, 1f)), inRange),
                    CountIf(g.Greater(Abs(g.Sub(s, Get(previous))), g.Literal(1e-9f)), changes),
                    g.Set(min, g.Mathf2(nameof(Mathf.Min), Get(min), s)).assign,
                    g.Set(max, g.Mathf2(nameof(Mathf.Max), Get(max), s)).assign,
                    g.Set(sum, g.Sum(Get(sum), s)).assign,
                    g.Set(previous, s).assign,
                };

                Cell("IN [0,1)", expectedSamples, Get(inRange), 2, AllSamples(inRange),
                    "RANDOM.VALUE: {0} of " + Samples + " samples in [0, 1)", Get(inRange));
                var spread = g.Mul(g.Sub(Get(max), Get(min)), 100f);
                Cell("SPREAD", "MIN<25 MAX>75", spread, 3, g.And(g.Less(Get(min), 0.25f), g.GreaterOrEqual(Get(max), 0.75f)),
                    "RANDOM.VALUE: samples from {0} to {1}, expected below 0.25 and above 0.75", Get(min), Get(max));
                var mean = g.Div(Get(sum), Samples);
                Cell("MEAN", "35-65 %", g.Mul(mean, 100f), 3, g.And(g.GreaterOrEqual(mean, 0.35f), g.Less(mean, 0.65f)),
                    "RANDOM.VALUE: mean of the samples {0}, expected 0.35 to 0.65", mean);
                Cell("CHANGES", expectedChanges, Get(changes), 2, MostChanged(changes),
                    "RANDOM.VALUE: only {0} of " + Samples + " samples differ from the previous one (baked value?)", Get(changes));
                Loop(setSample, statistics);
            }

            // --- Random.rotation and Random.rotationUniform: unit quaternions, rotated forward vectors spread over the sphere
            foreach (var member in new[] { nameof(Random.rotation), nameof(Random.rotationUniform) })
            {
                var p = "random_" + member;
                var label = "RANDOM." + member.ToUpperInvariant();
                var sample = Var(p + "Sample", Quaternion.identity);
                var previous = Var(p + "Previous", Quaternion.identity);
                var unit = Var(p + "Unit", 0f);
                var changes = Var(p + "Changes", 0f);
                var forwardSum = Var(p + "ForwardSum", Vector3.zero);

                Row(label, graphY + (member == nameof(Random.rotation) ? 1000f : 2000f));
                var setSample = g.Set(sample, StaticGet(kit, typeof(Random), member));
                var q = Get(sample);
                var lengthSquared = g.Call(typeof(Quaternion), nameof(Quaternion.Dot), QuaternionQuaternion, q, q);
                var dotPrevious = g.Call(typeof(Quaternion), nameof(Quaternion.Dot), QuaternionQuaternion, q, Get(previous));
                var forward = g.Call(typeof(Quaternion), "op_Multiply", QuaternionVector3, q, g.Literal(Vector3.forward));
                var statistics = new List<ControlInput>
                {
                    CountIf(g.Less(Abs(g.Sub(lengthSquared, 1f)), 1e-3f), unit),
                    CountIf(g.Greater(g.Sub(1f, Abs(dotPrevious)), g.Literal(1e-6f)), changes),
                    g.Set(forwardSum, g.Vec3Add(Get(forwardSum), forward)).assign,
                    g.Set(previous, q).assign,
                };

                Cell("UNIT", expectedSamples, Get(unit), 2, AllSamples(unit),
                    label + ": {0} of " + Samples + " samples are unit quaternions", Get(unit));
                // uniform rotations: the mean of the rotated forward vectors is close to 0 (about 0.07 per axis)
                var meanForward = g.Div(g.Property(typeof(Vector3), nameof(Vector3.magnitude), Get(forwardSum)), Samples);
                Cell("MEAN DIR", "< 50 %", g.Mul(meanForward, 100f), 3, g.Less(meanForward, 0.5f),
                    label + ": rotated forward vectors average to length {0}, expected < 0.5 (not spread)", meanForward);
                Cell("CHANGES", expectedChanges, Get(changes), 2, MostChanged(changes),
                    label + ": only {0} of " + Samples + " samples differ from the previous one (baked value?)", Get(changes));
                Loop(setSample, statistics);
            }

            // --- Random.insideUnitCircle: inside the circle, 3/4 of the points outside radius 0.5, mean close to 0
            {
                const string p = "randomCircle";
                var sample = Var(p + "Sample", Vector2.zero);
                var previous = Var(p + "Previous", new Vector2(2f, 2f));
                var inside = Var(p + "Inside", 0f);
                var outer = Var(p + "Outer", 0f);
                var changes = Var(p + "Changes", 0f);
                var sumX = Var(p + "SumX", 0f);
                var sumY = Var(p + "SumY", 0f);

                Row("RANDOM.INSIDEUNITCIRCLE", graphY + 3000f);
                var setSample = g.Set(sample, StaticGet(kit, typeof(Random), nameof(Random.insideUnitCircle)));
                var v = Get(sample);
                ValueOutput X(ValueOutput vector) => g.Property(typeof(Vector2), nameof(Vector2.x), vector);
                ValueOutput Y(ValueOutput vector) => g.Property(typeof(Vector2), nameof(Vector2.y), vector);
                var radius = g.Property(typeof(Vector2), nameof(Vector2.magnitude), v);
                var statistics = new List<ControlInput>
                {
                    CountIf(g.Less(radius, 1f + 1e-4f), inside),
                    CountIf(g.GreaterOrEqual(radius, 0.5f), outer),
                    CountIf(g.Greater(g.Sum(Abs(g.Sub(X(v), X(Get(previous)))), Abs(g.Sub(Y(v), Y(Get(previous))))), g.Literal(1e-9f)), changes),
                    g.Set(sumX, g.Sum(Get(sumX), X(v))).assign,
                    g.Set(sumY, g.Sum(Get(sumY), Y(v))).assign,
                    g.Set(previous, v).assign,
                };

                Cell("INSIDE", expectedSamples, Get(inside), 2, AllSamples(inside),
                    "RANDOM.INSIDEUNITCIRCLE: {0} of " + Samples + " samples inside the unit circle", Get(inside));
                // expected 48 of 64 (area outside radius 0.5), the range is more than 3 standard deviations wide
                Cell("R > 0.5", "36-60", Get(outer), 2, g.And(g.GreaterOrEqual(Get(outer), 35.5f), g.Less(Get(outer), 60.5f)),
                    "RANDOM.INSIDEUNITCIRCLE: {0} of " + Samples + " samples outside radius 0.5, expected 36 to 60 (not uniform)", Get(outer));
                var meanLength = g.Div(g.Mathf1(nameof(Mathf.Sqrt), g.Sum(g.Mul(Get(sumX), Get(sumX)), g.Mul(Get(sumY), Get(sumY)))), Samples);
                Cell("MEAN", "< 30 %", g.Mul(meanLength, 100f), 3, g.Less(meanLength, 0.3f),
                    "RANDOM.INSIDEUNITCIRCLE: samples average to length {0}, expected < 0.3", meanLength);
                Cell("CHANGES", expectedChanges, Get(changes), 2, MostChanged(changes),
                    "RANDOM.INSIDEUNITCIRCLE: only {0} of " + Samples + " samples differ from the previous one (baked value?)", Get(changes));
                Loop(setSample, statistics);
            }
        }

        #endregion

        #region Time

        /// <summary>
        /// Time members are read at <see cref="MarkStart"/> and <see cref="MarkEnd"/> (run clock); the delta times are
        /// summed on every frame and the sums read at the same marks. The checks at the deadline compare the advance
        /// between the marks with the run clock. glTF has no time scale and no fixed update, so the exporter maps the
        /// unscaled, fixed and double variants to the same values; in Unity they only agree within the tolerances
        /// (fixedDeltaTime is constant there, so its check only asks for a plausible value).
        /// </summary>
        private static void BuildTime(ExporterTestSceneKit kit, ExporterTestTimedTests timed, float graphY)
        {
            var g = kit.g;
            kit.Section($"TIME (READ AT {MarkStart} S AND {MarkEnd} S OF THE RUN CLOCK)");
            g.Row(graphY);

            ValueOutput Time(string member) => StaticGet(kit, typeof(Time), member);
            ValueOutput Get(string name) => g.Get(name);
            ValueOutput Ms(ValueOutput seconds) => g.Mul(seconds, 1000f);
            ValueOutput Advance(string name) => g.Sub(Get(name + "End"), Get(name + "Start"));
            // tolerance for values from different reads in a frame: seconds + frames * the longest frame of the run
            ValueOutput Tolerance(float seconds, float frames) => g.Sum(g.Mul(timed.MaxFrameTime(), frames), seconds);
            ValueOutput Close(ValueOutput a, ValueOutput b, ValueOutput tolerance) =>
                g.Less(g.Mathf1(nameof(Mathf.Abs), g.Sub(a, b)), tolerance);

            var startMarks = new List<SetVariable>();
            var endMarks = new List<SetVariable>();
            // name + "Start" and name + "End" = value at the marks
            void Marked(string name, Func<ValueOutput> value)
            {
                timed.Variable(name + "Start");
                timed.Variable(name + "End");
                startMarks.Add(g.Set(name + "Start", value()));
                endMarks.Add(g.Set(name + "End", value()));
            }

            Marked("clock", timed.Clock);
            var timeMembers = new (string member, string cellName)[]
            {
                (nameof(UnityEngine.Time.time), "TIME"),
                (nameof(UnityEngine.Time.unscaledTime), "UNSCALED"),
                (nameof(UnityEngine.Time.fixedTime), "FIXED"),
                (nameof(UnityEngine.Time.fixedUnscaledTime), "FIX UNSC"),
                (nameof(UnityEngine.Time.realtimeSinceStartup), "REALTIME"),
                (nameof(UnityEngine.Time.timeSinceLevelLoad), "LEVEL"),
                (nameof(UnityEngine.Time.timeAsDouble), "TIME DBL"),
                (nameof(UnityEngine.Time.unscaledTimeAsDouble), "UNSC DBL"),
                (nameof(UnityEngine.Time.realtimeSinceStartupAsDouble), "REAL DBL"),
            };
            foreach (var (member, _) in timeMembers)
                Marked("time_" + member, () => Time(member));
            Marked("frameCount", () => Time(nameof(UnityEngine.Time.frameCount)));

            // delta times: summed on every frame, the sums are marked
            var deltaMembers = new[]
            {
                nameof(UnityEngine.Time.deltaTime), nameof(UnityEngine.Time.unscaledDeltaTime), nameof(UnityEngine.Time.smoothDeltaTime),
                nameof(UnityEngine.Time.fixedDeltaTime), nameof(UnityEngine.Time.fixedUnscaledDeltaTime),
            };
            var sums = new List<SetVariable>();
            foreach (var member in deltaMembers)
            {
                var sum = timed.Variable("sum_" + member);
                sums.Add(timed.Add(sum, Time(member)));
                Marked(sum, () => Get(sum));
            }
            var updates = timed.Variable("updates");
            sums.Add(timed.Count(updates));
            Marked(updates, () => Get(updates));

            timed.EveryFrame(Chain(sums));
            timed.At(MarkStart, Chain(startMarks));
            timed.At(MarkEnd, Chain(endMarks));

            // --- checks at the deadline
            g.Row(graphY + 1500f);
            Transform row = null;
            int cell = 0;
            void Row(string label)
            {
                row = kit.Row();
                row.name = "Row_" + label.Replace(' ', '_').Replace(',', '_');
                kit.Label(row, label, Vector2.zero, LabelWidth);
                cell = 0;
            }
            void Cell(string name, string expected, ValueOutput shown, int digits, ValueOutput ok, string message, params ValueOutput[] logArgs) =>
                timed.AtDeadline(kit.CheckCell(row, FirstCellX + cell++ * ExporterTestSceneKit.CheckCellStep, name, expected, shown, digits, ok,
                    message, logArgs));

            var clockAdvance = Advance("clock");
            string[] rowLabels = { "TIME, UNSCALED, FIXED", "REALTIME, LEVEL, DOUBLE", "DOUBLE, FRAMECOUNT" };
            for (int i = 0; i < timeMembers.Length; i++)
            {
                if (i % 4 == 0)
                    Row(rowLabels[i / 4]);
                var (member, cellName) = timeMembers[i];
                var advance = Advance("time_" + member);
                // a baked value doesn't advance; the reads can be a frame apart from the clock
                Cell(cellName, "=CLOCK", Ms(advance), 4,
                    g.And(Close(advance, clockAdvance, Tolerance(0.1f, 2f)), g.GreaterOrEqual(advance, 0.25f)),
                    $"TIME.{member.ToUpperInvariant()} advanced {{0}} s between the marks, the run clock {{1}} s", advance, clockAdvance);
            }
            var frames = Advance("frameCount");
            var updateCalls = Advance(updates);
            Cell("FRAMES", "=UPDATES", frames, 4, g.And(timed.Within(frames, updateCalls, 2.5f), g.GreaterOrEqual(frames, 1f)),
                "TIME.FRAMECOUNT advanced {0} frames between the marks, Update ran {1} times", frames, updateCalls);

            Row("DELTA TIMES (SUM)");
            var deltaSum = Advance("sum_deltaTime");
            Cell("DT", "=CLOCK", Ms(deltaSum), 4, Close(deltaSum, clockAdvance, Tolerance(0.02f, 2f)),
                "TIME.DELTATIME adds up to {0} s between the marks, the run clock advanced {1} s", deltaSum, clockAdvance);
            var unscaledSum = Advance("sum_unscaledDeltaTime");
            Cell("UNSCALED", "=DT", Ms(unscaledSum), 4, Close(unscaledSum, deltaSum, Tolerance(0.05f, 2f)),
                "TIME.UNSCALEDDELTATIME adds up to {0} s between the marks, deltaTime to {1} s", unscaledSum, deltaSum);
            var smoothSum = Advance("sum_smoothDeltaTime");
            Cell("SMOOTH", "NEAR DT", Ms(smoothSum), 4, Close(smoothSum, deltaSum, Tolerance(0.2f, 2f)),
                "TIME.SMOOTHDELTATIME adds up to {0} s between the marks, deltaTime to {1} s", smoothSum, deltaSum);

            Row("FIXED DELTA TIMES (AVERAGE)");
            var frameCount = g.Mathf2(nameof(Mathf.Max), updateCalls, g.Literal(1f));
            foreach (var (member, cellName) in new[] { (nameof(UnityEngine.Time.fixedDeltaTime), "FIXED DT"), (nameof(UnityEngine.Time.fixedUnscaledDeltaTime), "FIX UNSC") })
            {
                // Unity: constant (0.02 s by default) in Update, glTF: the frame's delta time
                var average = g.Div(Advance("sum_" + member), frameCount);
                Cell(cellName, "0-500", Ms(average), 4, g.And(g.Greater(average, g.Literal(1e-5f)), g.Less(average, 0.5f)),
                    $"TIME.{member.ToUpperInvariant()} averages {{0}} s per frame, expected (0, 0.5)", average);
            }
        }

        /// <summary>Connects the sets one after another; returns the first one's input.</summary>
        private static ControlInput Chain(List<SetVariable> sets)
        {
            for (int i = 0; i + 1 < sets.Count; i++)
                GraphBuilder.Flow(sets[i].assigned, sets[i + 1].assign);
            return sets[0].assign;
        }

        #endregion
    }
}
#endif
