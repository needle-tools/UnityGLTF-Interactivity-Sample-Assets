#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for coroutines: Wait For Seconds (also 0 s and two in a row), Wait Until, Wait While,
    /// Wait For Flow, Sequences that must wait for outputs with waits (also in a branch that isn't taken), and For, While,
    /// For Each and nested loops with a wait in the body. Each test runs in its own coroutine flow, started by a custom
    /// event on every run, and marks the run clock at its steps. All checks run at the deadline, so a coroutine that hangs
    /// fails instead of staying grey. Re-run the menu item after changing this file.
    /// </summary>
    public static class CoroutinesTestSceneBuilder
    {
        public const string SceneName = "20261014-Coroutines";

        private const string StartEvent = "startCoroutineTests";
        private const float Deadline = 2f;
        private const float TimeTolerance = 0.1f;
        private const float LabelWidth = 3.4f;
        private const float FirstCellX = 3.5f;
        private const float ColumnWidth = FirstCellX + 3 * ExporterTestSceneKit.CheckCellStep;

        private static ExporterTestSceneKit kit;
        private static GraphBuilder g;
        private static ExporterTestTimedTests timed;
        private static Transform row;
        private static int cell, testIndex;
        private static string prefix;

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Coroutines")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            kit = ExporterTestSceneKit.Begin(SceneName, "COROUTINES",
                $"EACH TEST RUNS IN ITS OWN COROUTINE FLOW. CHECKS AFTER {ExporterTestSceneKit.Format(Deadline)} S. TIMES IN MS FROM THE TEST START, -{TimeTolerance * 1000} MS - 1 FRAME / +{TimeTolerance * 1000} MS + 2 FRAMES PER WAIT.",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            g = kit.g;
            timed = new ExporterTestTimedTests(kit, Deadline);
            testIndex = 0;
            kit.AddStep(g.Trigger(StartEvent).enter); // runs after the resets, starts all test flows

            // info: slow runtimes end waits late, the timing checks allow 2 frames per wait
            kit.Section("RUNTIME");
            var info = kit.Row();
            info.name = "RowInfo";
            kit.Label(info, "LONGEST FRAME (INFO, NO CHECK)", Vector2.zero, LabelWidth);
            kit.Label(info, "MS", new Vector2(FirstCellX, 0f), 0.62f, 0.12f, ExporterTestSceneKit.DimTextColor);
            var frameDigits = kit.Digits(info, "MaxFrame", FirstCellX + 0.66f, 4, 0.2f);
            timed.AtDeadline(kit.ShowNumber(frameDigits, g.Mul(timed.MaxFrameTime(), 1000f)).enter);

            kit.Section("WAIT UNITS");
            WaitTest("WAIT 0.5 S", 0.5f, 1, end => Chain(Wait(0.5f), end));
            WaitTest("WAIT 0.3 S, THEN WAIT 0.2 S", 0.5f, 2, end => Chain(Wait(0.3f), Chain(Wait(0.2f), end)));
            WaitTest("WAIT 0 S", 0f, 1, end => Chain(Wait(0f), end));
            WaitTest("WAIT UNTIL CLOCK >= 0.4 S", 0.4f, 1, end => Chain(WaitUntil(g.GreaterOrEqual(timed.Clock(), 0.4f)), end));
            WaitTest("WAIT WHILE CLOCK < 0.3 S", 0.3f, 1, end => Chain(WaitWhile(g.Less(timed.Clock(), 0.3f)), end));
            WaitForFlowTest();

            kit.Section("SEQUENCE WAITS FOR ITS OUTPUTS");
            SequenceTest("SEQUENCE: WAIT 0.3 S IN OUTPUT 1", 0.3f, 1, first => Chain(Wait(0.3f), first));
            SequenceTest("SEQUENCE: WAIT UNTIL 0.3 S IN OUTPUT 1", 0.3f, 1,
                first => Chain(WaitUntil(g.GreaterOrEqual(timed.Clock(), 0.3f)), first));
            SequenceTest("SEQUENCE: WAIT IN BRANCH NOT TAKEN", 0f, 0, first =>
            {
                var branch = g.Add(new If());
                GraphBuilder.Value(g.Literal(false), branch.condition);
                GraphBuilder.Flow(branch.ifTrue, Chain(Wait(0.3f), first));
                GraphBuilder.Flow(branch.ifFalse, first);
                return branch.enter;
            });
            SequenceBothTest();

            kit.Section("LOOPS WITH A WAIT 0.1 S IN THE BODY");
            LoopTest("FOR 0 TO 3", 3, 0.3f, (body, exit) =>
            {
                var loop = g.Loop(3);
                GraphBuilder.Flow(loop.body, body);
                GraphBuilder.Flow(loop.exit, exit);
                return loop.enter;
            });
            LoopTest("WHILE COUNT < 3", 3, 0.3f, (body, exit) =>
            {
                var loop = g.Add(new While());
                GraphBuilder.Value(g.Less(g.Get($"{prefix}_count"), 3f), loop.condition);
                GraphBuilder.Flow(loop.body, body);
                GraphBuilder.Flow(loop.exit, exit);
                return loop.enter;
            });
            LoopTest("FOR EACH (1 2 3)", 3, 0.3f, (body, exit) =>
            {
                var list = g.Add(new CreateList { inputCount = 3 });
                for (int i = 0; i < 3; i++)
                    GraphBuilder.Value(g.Literal(i + 1), list.multiInputs[i]);
                var loop = g.Add(new ForEach());
                GraphBuilder.Value(list.list, loop.collection);
                GraphBuilder.Flow(loop.body, body);
                GraphBuilder.Flow(loop.exit, exit);
                return loop.enter;
            });
            LoopTest("NESTED FOR 2 X 2", 4, 0.4f, (body, exit) =>
            {
                var outer = g.Loop(2);
                var inner = g.Loop(2);
                GraphBuilder.Flow(outer.body, inner.enter);
                GraphBuilder.Flow(inner.body, body);
                GraphBuilder.Flow(outer.exit, exit);
                return outer.enter;
            });

            timed.Build();
            kit.Finish();
            kit = null;
            g = null;
            timed = null;
        }

        #region Tests

        /// <summary>START = clock, then the test chain, END = clock. Checks: END - START.</summary>
        private static void WaitTest(string label, float duration, int waits, System.Func<ControlInput, ControlInput> build)
        {
            var (start, end) = BeginTest(label);
            var markEnd = timed.Mark(end);
            var test = build(markEnd.assign);
            StartFlow(Chain(timed.Mark(start), test));

            DurationCell("END", start, end, duration, waits, label);
        }

        /// <summary>Wait For Flow with 2 inputs, entered after 0.2 s and 0.4 s by two flows: exits once, at 0.4 s.</summary>
        private static void WaitForFlowTest()
        {
            const string label = "WAIT FOR FLOW: INPUTS AT 0.2 AND 0.4 S";
            var (start, end) = BeginTest(label);
            var exits = timed.Variable($"{prefix}_exits");
            var waitForFlow = g.Add(new WaitForFlow { inputCount = 2, resetOnExit = true });
            var count = timed.Count(exits);
            GraphBuilder.Flow(waitForFlow.exit, count.assign);
            GraphBuilder.Flow(count.assigned, timed.Mark(end).assign);

            kit.AddReset(waitForFlow.reset);
            StartFlow(Chain(timed.Mark(start), Chain(Wait(0.2f), waitForFlow.awaitedInputs[0])));
            StartFlow(Chain(Wait(0.4f), waitForFlow.awaitedInputs[1]));

            DurationCell("EXIT", start, end, 0.4f, 1, label);
            CountCell("EXITS", exits, 1, label);
        }

        /// <summary>
        /// Sequence { output 1: <paramref name="build"/> (ends with marking FIRST), output 2: mark SECOND }: the second output
        /// must only run when the first one finished, at <paramref name="duration"/>.
        /// </summary>
        private static void SequenceTest(string label, float duration, int waits, System.Func<ControlInput, ControlInput> build)
        {
            var (start, second) = BeginTest(label);
            var first = timed.Variable($"{prefix}_first", -1f);
            var sequence = g.Sequence(2);
            GraphBuilder.Flow(sequence.multiOutputs[0], build(timed.Mark(first).assign));
            GraphBuilder.Flow(sequence.multiOutputs[1], timed.Mark(second).assign);
            StartFlow(Chain(timed.Mark(start), sequence.enter));

            DurationCell("OUT 2", start, second, duration, waits, label);
            var order = g.Sub(g.Get(second), g.Get(first));
            Cell("AFTER 1", ">=0", g.Mul(order, 1000f), 4, timed.AtLeast(order, g.Literal(-1e-4f)),
                $"{label}: output 2 ran {{0}} s after output 1 (not after it)", order);
        }

        /// <summary>Sequence { Wait 0.2 s, Wait 0.2 s }: the second output starts after the first one, so it ends at 0.4 s.</summary>
        private static void SequenceBothTest()
        {
            const string label = "SEQUENCE: WAIT 0.2 S IN BOTH OUTPUTS";
            var (start, end) = BeginTest(label);
            var first = timed.Variable($"{prefix}_first", -1f);
            var sequence = g.Sequence(2);
            GraphBuilder.Flow(sequence.multiOutputs[0], Chain(Wait(0.2f), timed.Mark(first).assign));
            GraphBuilder.Flow(sequence.multiOutputs[1], Chain(Wait(0.2f), timed.Mark(end).assign));
            StartFlow(Chain(timed.Mark(start), sequence.enter));

            DurationCell("OUT 1", start, first, 0.2f, 1, label);
            DurationCell("OUT 2", start, end, 0.4f, 2, label);
        }

        /// <summary>Loop whose body waits 0.1 s and counts; the exit marks END.</summary>
        private static void LoopTest(string label, int iterations, float duration, System.Func<ControlInput, ControlInput, ControlInput> build)
        {
            var (start, end) = BeginTest(label);
            var count = timed.Variable($"{prefix}_count");
            var loop = build(Chain(Wait(0.1f), timed.Count(count).assign), timed.Mark(end).assign);
            StartFlow(Chain(timed.Mark(start), loop));

            CountCell("COUNT", count, iterations, label);
            DurationCell("EXIT", start, end, duration, iterations, label);
        }

        #endregion

        #region Helpers

        private static (string start, string end) BeginTest(string label)
        {
            testIndex++;
            prefix = $"c{testIndex:00}";
            g.Row(testIndex * 500f);
            row = kit.Row();
            row.name = $"Row{testIndex:00}";
            kit.Label(row, label, Vector2.zero, LabelWidth);
            cell = 0;
            return (timed.Variable($"{prefix}_start", -1f), timed.Variable($"{prefix}_end", -1f));
        }

        /// <summary>Coroutine flow started by the run's custom event.</summary>
        private static void StartFlow(ControlInput entry)
        {
            var receive = g.Receive(StartEvent, 0);
            receive.coroutine = true;
            GraphBuilder.Flow(receive.trigger, entry);
        }

        private static WaitForSecondsUnit Wait(float seconds)
        {
            var unit = g.Add(new WaitForSecondsUnit());
            unit.seconds.SetDefaultValue(seconds);
            return unit;
        }

        private static WaitUntilUnit WaitUntil(ValueOutput condition)
        {
            var unit = g.Add(new WaitUntilUnit());
            GraphBuilder.Value(condition, unit.condition);
            return unit;
        }

        private static WaitWhileUnit WaitWhile(ValueOutput condition)
        {
            var unit = g.Add(new WaitWhileUnit());
            GraphBuilder.Value(condition, unit.condition);
            return unit;
        }

        /// <summary>wait.exit -> next, returns wait.enter</summary>
        private static ControlInput Chain(WaitUnit wait, ControlInput next)
        {
            GraphBuilder.Flow(wait.exit, next);
            return wait.enter;
        }

        /// <summary>set.assigned -> next, returns set.assign</summary>
        private static ControlInput Chain(SetVariable set, ControlInput next)
        {
            GraphBuilder.Flow(set.assigned, next);
            return set.assign;
        }

        private static void Cell(string name, string expected, ValueOutput shown, int digits, ValueOutput ok, string message,
            params ValueOutput[] logArgs)
        {
            timed.AtDeadline(kit.CheckCell(row, FirstCellX + cell++ * ExporterTestSceneKit.CheckCellStep, name, expected, shown, digits,
                ok, message, logArgs));
        }

        /// <summary>
        /// to - from == duration: not earlier than the tolerance, not later than the tolerance plus 2 frames per wait on the
        /// way (see <see cref="ExporterTestTimedTests.OnTime"/>); fails when "to" was never marked.
        /// </summary>
        private static void DurationCell(string name, string from, string to, float duration, int waits, string label)
        {
            var elapsed = g.Sub(g.Get(to), g.Get(from));
            var marked = g.And(g.GreaterOrEqual(g.Get(from), 0f), g.GreaterOrEqual(g.Get(to), 0f));
            Cell(name, $"={duration * 1000f}", g.Mul(elapsed, 1000f), 4,
                g.And(marked, timed.OnTime(elapsed, duration, TimeTolerance, 2 * waits + 1)),
                $"{label}: {name} after {{0}} s, expected {ExporterTestSceneKit.Format(duration)} s (mark {{1}}, -1 = never; longest frame {{2}} s, allows {2 * waits + 1} frames late)",
                elapsed, g.Get(to), timed.MaxFrameTime());
        }

        private static void CountCell(string name, string variable, int expected, string label)
        {
            Cell(name, $"={expected}", g.Get(variable), 2, timed.Within(g.Get(variable), expected, 0.5f),
                $"{label}: {name} is {{0}}, expected {expected}", g.Get(variable));
        }

        #endregion
    }
}
#endif
