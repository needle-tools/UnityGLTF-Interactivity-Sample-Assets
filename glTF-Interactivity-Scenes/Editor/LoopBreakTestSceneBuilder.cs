#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for the Break unit: For (flow/for and the custom loop for steps other than 1), While and
    /// For Each loops, broken at different iterations, nested loops, a Break followed by more body flow, a loop that runs
    /// twice (the break state must reset) and a loop with a Wait in its body (coroutine loop, started by a custom event).
    /// Every test counts iterations into int variables, which are checked after the loop against the counts Visual
    /// Scripting gives in Unity's play mode. Re-run the menu item after changing this file.
    /// </summary>
    public static class LoopBreakTestSceneBuilder
    {
        public const string SceneName = "20261014-LoopBreak";

        private const float LabelWidth = 3.4f;
        private const float FirstCheckX = 3.5f, CheckStep = 1.6f;
        private const float ColumnWidth = FirstCheckX + 3 * CheckStep;

        private static ExporterTestSceneKit kit;
        private static GraphBuilder g;
        private static int testIndex;
        private static readonly List<ControlInput> asyncResets = new List<ControlInput>();

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Loop Break")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            kit = ExporterTestSceneKit.Begin(SceneName, "LOOP BREAK",
                "LOOPS COUNT THEIR ITERATIONS AND BREAK. EXPECTED COUNTS = VISUAL SCRIPTING IN UNITY. RUN REPEATS ALL TESTS.",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            g = kit.g;
            testIndex = 0;
            asyncResets.Clear();

            kit.Section("FOR");
            ForBreakAt("FOR 0 TO 10, BREAK AT I = 4", 0, 10, 1, 4, count: 5, lastIndex: 4);
            ForBreakAt("FOR 0 TO 20 STEP 3, BREAK AT I = 12", 0, 20, 3, 12, count: 5, lastIndex: 12);
            ForBreakAt("FOR 10 TO 0 STEP -1, BREAK AT I = 7", 10, 0, -1, 7, count: 4, lastIndex: 7);
            ForBreakAt("FOR 0 TO 10, BREAK IN 1ST ITERATION", 0, 10, 1, 0, count: 1, lastIndex: 0);
            ForBreakAt("FOR 0 TO 6, BREAK NEVER REACHED", 0, 6, 1, 99, count: 6, lastIndex: 5);

            kit.Section("WHILE");
            WhileBreakAt("WHILE COUNT < 100, BREAK AT COUNT = 7", 100, 7, count: 7);
            WhileBreakAt("WHILE COUNT < 5, BREAK NEVER REACHED", 5, 50, count: 5);

            kit.Section("FOR EACH");
            ForEachBreak();

            kit.Section("NESTED AND SPECIAL CASES");
            NestedInnerBreak();
            NestedOuterBreak();
            BreakInSequence();
            LoopRunTwice();
            CoroutineLoopBreak();

            kit.Finish();
            kit = null;
            g = null;
        }

        #region Tests

        /// <summary>For loop: LAST = index, COUNT += 1, Break when index == breakAt. EXIT counts the exit flow.</summary>
        private static void ForBreakAt(string label, int first, int end, int step, int breakAt, int count, int lastIndex)
        {
            var row = BeginTest(label);
            var vCount = Variable("count");
            var vLast = Variable("last");
            var vExit = Variable("exit");

            var loop = g.Add(new For());
            loop.firstIndex.SetDefaultValue(first);
            loop.lastIndex.SetDefaultValue(end);
            loop.step.SetDefaultValue(step);

            var setLast = g.Set(vLast, loop.currentIndex);
            var inc = Increment(vCount);
            GraphBuilder.Flow(loop.body, setLast.assign);
            GraphBuilder.Flow(setLast.assigned, inc.assign);
            GraphBuilder.Flow(inc.assigned, BreakIf(IsInt(loop.currentIndex, breakAt)));
            GraphBuilder.Flow(loop.exit, Increment(vExit).assign);

            kit.AddStep(loop.enter);
            Checks(row, label, ("COUNT", vCount, count), ("LAST", vLast, lastIndex), ("EXIT", vExit, 1));
        }

        /// <summary>While COUNT &lt; limit: COUNT += 1, Break when COUNT == breakAt.</summary>
        private static void WhileBreakAt(string label, int limit, int breakAt, int count)
        {
            var row = BeginTest(label);
            var vCount = Variable("count");
            var vExit = Variable("exit");

            var loop = g.Add(new While());
            GraphBuilder.Value(g.Less(g.Get(vCount), limit), loop.condition);
            var inc = Increment(vCount);
            GraphBuilder.Flow(loop.body, inc.assign);
            GraphBuilder.Flow(inc.assigned, BreakIf(IsInt(g.Get(vCount), breakAt)));
            GraphBuilder.Flow(loop.exit, Increment(vExit).assign);

            kit.AddStep(loop.enter);
            Checks(row, label, ("COUNT", vCount, count), ("EXIT", vExit, 1));
        }

        /// <summary>For Each over a created list: COUNT += 1, Break at the first item &gt; 4, else SUM += item.</summary>
        private static void ForEachBreak()
        {
            int[] items = { 3, 1, 4, 1, 5, 9, 2, 6 };
            const string label = "FOR EACH (3 1 4 1 5 9 2 6), BREAK AT ITEM > 4";
            var row = BeginTest(label);
            var vCount = Variable("count");
            var vSum = Variable("sum");
            var vExit = Variable("exit");

            var list = g.Add(new CreateList { inputCount = items.Length });
            for (int i = 0; i < items.Length; i++)
                GraphBuilder.Value(g.Literal(items[i]), list.multiInputs[i]);

            var loop = g.Add(new ForEach());
            GraphBuilder.Value(list.list, loop.collection);
            var inc = Increment(vCount);
            GraphBuilder.Flow(loop.body, inc.assign);

            var isLarge = g.Add(new If());
            var greater = g.Add(new Greater(), 0f, 60f);
            GraphBuilder.Value(loop.currentItem, greater.a);
            greater.b.SetDefaultValue(4f);
            GraphBuilder.Value(greater.comparison, isLarge.condition);
            GraphBuilder.Flow(inc.assigned, isLarge.enter);
            GraphBuilder.Flow(isLarge.ifTrue, g.Add(new Break()).enter);
            GraphBuilder.Flow(isLarge.ifFalse, g.Set(vSum, g.GenericAdd(g.Get(vSum), loop.currentItem)).assign);
            GraphBuilder.Flow(loop.exit, Increment(vExit).assign);

            kit.AddStep(loop.enter);
            Checks(row, label, ("COUNT", vCount, 5), ("SUM", vSum, 3 + 1 + 4 + 1), ("EXIT", vExit, 1));
        }

        /// <summary>For I 0..3 { For J 0..10 { INNER += 1, Break at J == 2 }, OUTER += 1 }: only the inner loop breaks.</summary>
        private static void NestedInnerBreak()
        {
            const string label = "NESTED: INNER BREAKS AT J = 2";
            var row = BeginTest(label);
            var vInner = Variable("inner");
            var vOuter = Variable("outer");
            var vExit = Variable("exit");

            var outer = g.Loop(3);
            var inner = g.Loop(10);
            GraphBuilder.Flow(outer.body, inner.enter);
            var incInner = Increment(vInner);
            GraphBuilder.Flow(inner.body, incInner.assign);
            GraphBuilder.Flow(incInner.assigned, BreakIf(IsInt(inner.currentIndex, 2)));
            GraphBuilder.Flow(inner.exit, Increment(vOuter).assign);
            GraphBuilder.Flow(outer.exit, Increment(vExit).assign);

            kit.AddStep(outer.enter);
            Checks(row, label, ("INNER", vInner, 9), ("OUTER", vOuter, 3), ("EXIT", vExit, 1));
        }

        /// <summary>For I 0..5 { For J 0..4 { INNER += 1 }, OUTER += 1, Break at I == 2 }: the outer loop breaks after its inner loop.</summary>
        private static void NestedOuterBreak()
        {
            const string label = "NESTED: OUTER BREAKS AT I = 2";
            var row = BeginTest(label);
            var vInner = Variable("inner");
            var vOuter = Variable("outer");
            var vExit = Variable("exit");

            var outer = g.Loop(5);
            var inner = g.Loop(4);
            GraphBuilder.Flow(outer.body, inner.enter);
            GraphBuilder.Flow(inner.body, Increment(vInner).assign);
            var incOuter = Increment(vOuter);
            GraphBuilder.Flow(inner.exit, incOuter.assign);
            GraphBuilder.Flow(incOuter.assigned, BreakIf(IsInt(outer.currentIndex, 2)));
            GraphBuilder.Flow(outer.exit, Increment(vExit).assign);

            kit.AddStep(outer.enter);
            Checks(row, label, ("OUTER", vOuter, 3), ("INNER", vInner, 12), ("EXIT", vExit, 1));
        }

        /// <summary>
        /// Body = Sequence { COUNT += 1, Break at I == 3, AFTER += 1 }. A Break only stops the next iterations,
        /// the rest of the current body still runs (in Visual Scripting as well).
        /// </summary>
        private static void BreakInSequence()
        {
            const string label = "BREAK IN SEQUENCE, REST OF BODY RUNS";
            var row = BeginTest(label);
            var vCount = Variable("count");
            var vAfter = Variable("after");
            var vExit = Variable("exit");

            var loop = g.Loop(10);
            var body = g.Sequence(3);
            GraphBuilder.Flow(loop.body, body.enter);
            GraphBuilder.Flow(body.multiOutputs[0], Increment(vCount).assign);
            GraphBuilder.Flow(body.multiOutputs[1], BreakIf(IsInt(loop.currentIndex, 3)));
            GraphBuilder.Flow(body.multiOutputs[2], Increment(vAfter).assign);
            GraphBuilder.Flow(loop.exit, Increment(vExit).assign);

            kit.AddStep(loop.enter);
            Checks(row, label, ("COUNT", vCount, 4), ("AFTER", vAfter, 4), ("EXIT", vExit, 1));
        }

        /// <summary>The same loop (Break at I == 2) entered twice: the second run must start unbroken.</summary>
        private static void LoopRunTwice()
        {
            const string label = "SAME LOOP RUNS TWICE, BREAK AT I = 2";
            var row = BeginTest(label);
            var vCount = Variable("count");
            var vExit = Variable("exit");

            var loop = g.Loop(10);
            var inc = Increment(vCount);
            GraphBuilder.Flow(loop.body, inc.assign);
            GraphBuilder.Flow(inc.assigned, BreakIf(IsInt(loop.currentIndex, 2)));
            GraphBuilder.Flow(loop.exit, Increment(vExit).assign);

            var twice = g.Sequence(2);
            GraphBuilder.Flow(twice.multiOutputs[0], loop.enter);
            GraphBuilder.Flow(twice.multiOutputs[1], loop.enter);

            kit.AddStep(twice.enter);
            Checks(row, label, ("COUNT", vCount, 6), ("EXIT", vExit, 2));
        }

        /// <summary>
        /// For 0..10 { Wait 0.05 s, COUNT += 1, Break at I == 3 } exported as coroutine loop. The run only sends a
        /// custom event, which starts the loop in a coroutine flow, so the other tests don't wait for it (and the run's
        /// sequence stays synchronous); its checks run when the loop exits. Its counters are reset by that flow, after
        /// the run reset the pass/fail counters. A second click event on the RUN button is avoided on purpose: with two
        /// onSelect events on one node, viewers may only run one of them.
        /// </summary>
        private static void CoroutineLoopBreak()
        {
            const string label = "FOR WITH WAIT IN BODY, BREAK AT I = 3";
            var row = BeginTest(label);
            var vCount = Variable("count", reset: false);
            var vExit = Variable("exit", reset: false);

            var loop = g.Loop(10);
            var wait = g.Add(new WaitForSecondsUnit());
            wait.seconds.SetDefaultValue(0.05f);
            var inc = Increment(vCount);
            GraphBuilder.Flow(loop.body, wait.enter);
            GraphBuilder.Flow(wait.exit, inc.assign);
            GraphBuilder.Flow(inc.assigned, BreakIf(IsInt(loop.currentIndex, 3)));
            var incExit = Increment(vExit);
            GraphBuilder.Flow(loop.exit, incExit.assign);

            // checks after the loop: count and exit
            var checks = CreateChecks(row, label, ("COUNT", vCount, 4), ("EXIT", vExit, 1));
            g.FanOut(incExit.assigned, checks);

            // the run sends a custom event, received by a coroutine flow: reset the variables, then run the loop
            var receive = g.Receive(CoroutineTestEvent, 0);
            receive.coroutine = true;
            var resetCount = g.Set(vCount, g.Literal(0));
            var resetExit = g.Set(vExit, g.Literal(0));
            GraphBuilder.Flow(receive.trigger, resetCount.assign);
            GraphBuilder.Flow(resetCount.assigned, resetExit.assign);
            GraphBuilder.Flow(resetExit.assigned, loop.enter);

            kit.AddStep(g.Trigger(CoroutineTestEvent).enter);
        }

        private const string CoroutineTestEvent = "runCoroutineLoopTest";

        #endregion

        #region Helpers

        private static string currentPrefix;

        private static Transform BeginTest(string label)
        {
            testIndex++;
            currentPrefix = $"t{testIndex:00}";
            g.Row(testIndex * 500f);
            var row = kit.Row();
            row.name = $"Row_{currentPrefix}";
            kit.Label(row, label, Vector2.zero, LabelWidth);
            return row;
        }

        /// <summary>Int variable of the current test, reset to 0 before every run.</summary>
        private static string Variable(string name, bool reset = true)
        {
            var variable = $"{currentPrefix}_{name}";
            g.Declare(variable, 0);
            if (reset)
                kit.AddReset(g.Set(variable, g.Literal(0)).assign);
            return variable;
        }

        private static SetVariable Increment(string variable) => g.Set(variable, g.GenericAdd(g.Get(variable), g.Literal(1)));

        private static ValueOutput IsInt(ValueOutput value, int expected)
        {
            var equal = g.Add(new Equal { numeric = false }, 0f, 60f);
            GraphBuilder.Value(value, equal.a);
            GraphBuilder.Value(g.Literal(expected), equal.b);
            return equal.comparison;
        }

        /// <summary>If <paramref name="condition"/>: Break. Both branches end there.</summary>
        private static ControlInput BreakIf(ValueOutput condition)
        {
            var branch = g.Add(new If());
            GraphBuilder.Value(condition, branch.condition);
            GraphBuilder.Flow(branch.ifTrue, g.Add(new Break()).enter);
            return branch.enter;
        }

        private static void Checks(Transform row, string label, params (string name, string variable, int expected)[] checks)
        {
            foreach (var entry in CreateChecks(row, label, checks))
                kit.AddStep(entry);
        }

        /// <summary>One cell per check: NAME, the value (2 digits), = expected, indicator.</summary>
        private static List<ControlInput> CreateChecks(Transform row, string label, params (string name, string variable, int expected)[] checks)
        {
            var entries = new List<ControlInput>();
            for (int i = 0; i < checks.Length; i++)
            {
                var (name, variable, expected) = checks[i];
                var x = FirstCheckX + i * CheckStep;
                kit.Label(row, name, new Vector2(x, 0f), 0.55f, 0.12f, ExporterTestSceneKit.DimTextColor);
                var digits = kit.Digits(row, name, x + 0.6f, 2, 0.22f);
                kit.Label(row, $"={expected}", new Vector2(x + 0.6f + ExporterTestSceneKit.DigitsWidth(2) + 0.04f, 0f), 0.35f, 0.14f,
                    ExporterTestSceneKit.DimTextColor);
                var indicator = kit.Indicator(row, name, x + 1.3f);

                var show = kit.ShowNumber(digits, g.Get(variable));
                entries.Add(kit.Check(indicator, kit.Matches(g.Get(variable), expected),
                    $"{label}: {name} is {{0}}, expected {expected}", new[] { g.Get(variable) }, show));
            }
            return entries;
        }

        #endregion
    }
}
#endif
