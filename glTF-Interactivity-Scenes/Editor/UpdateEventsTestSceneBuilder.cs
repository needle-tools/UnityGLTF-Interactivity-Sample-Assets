#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for the frame events and Time members: Update, Late Update and Fixed Update count their calls
    /// and sum Time.deltaTime for <see cref="Window"/> seconds, then the checks compare the results. The checks only use
    /// what holds in Unity and in glTF (where Late Update and Fixed Update are exported as tick events), e.g. the sum of
    /// Time.deltaTime in Fixed Update matches the elapsed time in both, although Unity calls it at a fixed rate.
    /// "LATE UPDATE AFTER UPDATE" checks the order within a frame: glTF doesn't define an order between tick events, so
    /// this one shows whether a viewer happens to keep it. Re-run the menu item after changing this file.
    /// </summary>
    public static class UpdateEventsTestSceneBuilder
    {
        public const string SceneName = "20261014-UpdateEvents";

        private const float Window = 2f;
        private const float LabelWidth = 2.9f;
        private const float FirstCellX = 3.0f;
        private const float ColumnWidth = FirstCellX + 3 * ExporterTestSceneKit.CheckCellStep;

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Update Events")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            var kit = ExporterTestSceneKit.Begin(SceneName, "UPDATE, LATE UPDATE, FIXED UPDATE",
                $"EVENTS COUNT CALLS AND SUM TIME.DELTATIME FOR {Window} S, THEN THE CHECKS RUN. TIMES IN MS. RUN STARTS AGAIN.",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            var g = kit.g;
            var timed = new ExporterTestTimedTests(kit, Window);

            ValueOutput Time(string member) => ExporterTestTimedTests.StaticGet(g, typeof(Time), member);
            ValueOutput Get(string variable) => g.Get(variable);
            ValueOutput Ms(ValueOutput seconds) => g.Mul(seconds, 1000f);

            // --- measurement variables (reset on every run)
            var uCount = timed.Variable("updateCount");
            var uSum = timed.Variable("updateDeltaSum");
            var uSumAfterFirst = timed.Variable("updateDeltaSumAfterFirst");
            var uUnscaledSum = timed.Variable("updateUnscaledDeltaSum");
            var uFirstTime = timed.Variable("updateFirstTime", -1f);
            var uLastTime = timed.Variable("updateLastTime");
            var uFirstFrame = timed.Variable("updateFirstFrame", -1f);
            var uLastFrame = timed.Variable("updateLastFrame");
            var lCount = timed.Variable("lateCount");
            var lSum = timed.Variable("lateDeltaSum");
            var lAfterUpdate = timed.Variable("lateAfterUpdate");
            var lTimeMismatch = timed.Variable("lateTimeMismatch");
            var fCount = timed.Variable("fixedCount");
            var fSum = timed.Variable("fixedDeltaSum");

            // --- Update: counts, sums and the first/last Time.time and Time.frameCount of the window
            g.Row(0f);
            {
                var count = timed.Count(uCount);
                var sum = timed.Add(uSum, timed.DeltaTime());
                var unscaled = timed.Add(uUnscaledSum, Time(nameof(UnityEngine.Time.unscaledDeltaTime)));
                GraphBuilder.Flow(count.assigned, sum.assign);
                GraphBuilder.Flow(sum.assigned, unscaled.assign);
                timed.EveryFrame(count.assign);

                // first frame of the window: remember time and frame, later frames: sum the deltas since then
                var first = g.Add(new If());
                GraphBuilder.Value(g.Less(Get(uFirstTime), 0f), first.condition);
                var setFirstTime = g.Set(uFirstTime, Time(nameof(UnityEngine.Time.time)));
                var setFirstFrame = g.Set(uFirstFrame, Time(nameof(UnityEngine.Time.frameCount)));
                GraphBuilder.Flow(first.ifTrue, setFirstTime.assign);
                GraphBuilder.Flow(setFirstTime.assigned, setFirstFrame.assign);
                GraphBuilder.Flow(first.ifFalse, timed.Add(uSumAfterFirst, timed.DeltaTime()).assign);
                timed.EveryFrame(first.enter);

                var setLastTime = g.Set(uLastTime, Time(nameof(UnityEngine.Time.time)));
                var setLastFrame = g.Set(uLastFrame, Time(nameof(UnityEngine.Time.frameCount)));
                GraphBuilder.Flow(setLastTime.assigned, setLastFrame.assign);
                timed.EveryFrame(setLastTime.assign);
            }

            // --- Late Update: same counts, whether Update already ran in this frame, same Time.time as Update
            g.Row(1500f);
            {
                var late = g.Add(new LateUpdate());
                var measuring = g.Add(new If());
                GraphBuilder.Value(g.And(timed.Running(), g.Less(timed.Clock(), Window)), measuring.condition);
                GraphBuilder.Flow(late.trigger, measuring.enter);

                var steps = g.Sequence(3);
                GraphBuilder.Flow(measuring.ifTrue, steps.enter);

                // Update ran in this frame: it counted one more call than Late Update so far
                var afterUpdate = g.Add(new If());
                GraphBuilder.Value(timed.Within(Get(uCount), g.Sum(Get(lCount), 1f), 0.5f), afterUpdate.condition);
                GraphBuilder.Flow(afterUpdate.ifTrue, timed.Count(lAfterUpdate).assign);
                GraphBuilder.Flow(steps.multiOutputs[0], afterUpdate.enter);

                var timeDiffers = g.Add(new If());
                GraphBuilder.Value(g.Not(timed.Within(Time(nameof(UnityEngine.Time.time)), Get(uLastTime), 1e-4f)), timeDiffers.condition);
                GraphBuilder.Flow(timeDiffers.ifTrue, timed.Count(lTimeMismatch).assign);
                GraphBuilder.Flow(steps.multiOutputs[1], timeDiffers.enter);

                var count = timed.Count(lCount);
                var sum = timed.Add(lSum, timed.DeltaTime());
                GraphBuilder.Flow(count.assigned, sum.assign);
                GraphBuilder.Flow(steps.multiOutputs[2], count.assign);
            }

            // --- Fixed Update: counts and sums Time.deltaTime (= Time.fixedDeltaTime in Unity's Fixed Update)
            g.Row(3000f);
            {
                var fixedUpdate = g.Add(new FixedUpdate());
                var measuring = g.Add(new If());
                GraphBuilder.Value(g.And(timed.Running(), g.Less(timed.Clock(), Window)), measuring.condition);
                GraphBuilder.Flow(fixedUpdate.trigger, measuring.enter);
                var count = timed.Count(fCount);
                var sum = timed.Add(fSum, timed.DeltaTime());
                GraphBuilder.Flow(measuring.ifTrue, count.assign);
                GraphBuilder.Flow(count.assigned, sum.assign);
            }

            // --- checks at the end of the window
            g.Row(4500f);
            Transform row = null;
            int cell = 0, rowIndex = 0;
            void Row(string label)
            {
                row = kit.Row();
                row.name = $"Row{++rowIndex:00}";
                kit.Label(row, label, Vector2.zero, LabelWidth);
                cell = 0;
            }
            void Cell(string name, string expected, ValueOutput shown, int digits, ValueOutput ok, string message, params ValueOutput[] logArgs)
            {
                timed.AtDeadline(kit.CheckCell(row, FirstCellX + cell++ * ExporterTestSceneKit.CheckCellStep, name, expected, shown, digits, ok,
                    message, logArgs));
            }

            kit.Section("UPDATE");
            Row("UPDATE CALLS, DELTATIME");
            Cell("CALLS", "> 10", Get(uCount), 4, g.GreaterOrEqual(Get(uCount), 10f),
                "UPDATE ran {0} times in " + Window + " s", Get(uCount));
            Cell("AVG DT", "MS", Ms(g.Div(Get(uSum), g.Mathf2(nameof(Mathf.Max), Get(uCount), g.Literal(1f)))), 4,
                g.And(g.GreaterOrEqual(Get(uSum), 1e-6f), g.Less(g.Div(Get(uSum), g.Mathf2(nameof(Mathf.Max), Get(uCount), g.Literal(1f))), 0.25f)),
                "UPDATE average delta time {0} s is not in (0, 0.25)", g.Div(Get(uSum), Get(uCount)));
            Cell("UNSCALED", "=DT", Ms(Get(uUnscaledSum)), 4, timed.Within(Get(uUnscaledSum), Get(uSum), 0.05f),
                "UPDATE sum of unscaledDeltaTime {0} differs from the sum of deltaTime {1}", Get(uUnscaledSum), Get(uSum));

            Row("TIME.TIME AND TIME.FRAMECOUNT");
            var timeSpan = g.Sub(Get(uLastTime), Get(uFirstTime));
            Cell("TIME", "=DT SUM", Ms(timeSpan), 4, timed.Within(timeSpan, Get(uSumAfterFirst), 0.005f),
                "TIME.TIME advanced {0} s, the delta times add up to {1} s", timeSpan, Get(uSumAfterFirst));
            var frames = g.Sub(Get(uLastFrame), Get(uFirstFrame));
            Cell("FRAMES", "=CALLS-1", frames, 4, timed.Within(frames, g.Sub(Get(uCount), 1f), 1.5f),
                "TIME.FRAMECOUNT advanced {0} frames in {1} Update calls", frames, Get(uCount));

            kit.Section("LATE UPDATE");
            Row("LATE UPDATE CALLS");
            Cell("CALLS", "=UPDATE", Get(lCount), 4, timed.Within(Get(lCount), Get(uCount), 1.5f),
                "LATE UPDATE ran {0} times, UPDATE {1} times", Get(lCount), Get(uCount));
            Cell("DT SUM", "=UPDATE", Ms(Get(lSum)), 4, timed.Within(Get(lSum), Get(uSum), 0.1f),
                "LATE UPDATE delta times add up to {0} s, UPDATE to {1} s", Get(lSum), Get(uSum));
            Cell("SAME TIME", "TIME.TIME", g.Sub(Get(lCount), Get(lTimeMismatch)), 4, g.Less(Get(lTimeMismatch), 1.5f),
                "LATE UPDATE saw a different Time.time than UPDATE in {0} frames", Get(lTimeMismatch));
            Row("LATE UPDATE AFTER UPDATE (ORDER)");
            Cell("FRAMES", "=CALLS", Get(lAfterUpdate), 4, timed.AtLeast(Get(lAfterUpdate), g.Sub(Get(lCount), 1f)),
                "LATE UPDATE ran after UPDATE in {0} of {1} frames (glTF has no order between tick events)", Get(lAfterUpdate), Get(lCount));

            kit.Section("FIXED UPDATE");
            Row("FIXED UPDATE CALLS, DELTATIME");
            Cell("CALLS", "> 0", Get(fCount), 4, g.GreaterOrEqual(Get(fCount), 1f),
                "FIXED UPDATE ran {0} times", Get(fCount));
            Cell("DT SUM", "=UPDATE", Ms(Get(fSum)), 4, timed.Within(Get(fSum), Get(uSum), 0.1f),
                "FIXED UPDATE delta times add up to {0} s, UPDATE to {1} s", Get(fSum), Get(uSum));

            timed.Build();
            kit.Finish();
        }
    }
}
#endif
