#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for units that keep state: Once, Toggle Flow and Toggle Value (checked right away on every
    /// run) and the time based Cooldown and Timer (incl. pause/resume and toggle), checked at the deadline with a
    /// tolerance of <see cref="TimeTolerance"/> seconds for frame timing. The units keep their state between runs like in
    /// Visual Scripting, so each test first brings its unit into a known state. Re-run the menu item after changing this file.
    /// </summary>
    public static class StatefulUnitsTestSceneBuilder
    {
        public const string SceneName = "20261014-StatefulUnits";

        private const float Deadline = 1.9f;
        private const float TimeTolerance = 0.12f;
        private const float CooldownDuration = 0.5f;
        private const float CooldownWindow = 1.75f; // enter every frame until then: ready after 0.5, 1.0 and 1.5 s
        private const float LabelWidth = 3.1f;
        private const float FirstCellX = 3.2f;
        private const float ColumnWidth = FirstCellX + 3 * ExporterTestSceneKit.CheckCellStep;

        private static ExporterTestSceneKit kit;
        private static GraphBuilder g;
        private static ExporterTestTimedTests timed;
        private static Transform row;
        private static int cell, rowIndex;

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Stateful Units")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            kit = ExporterTestSceneKit.Begin(SceneName, "STATEFUL UNITS",
                $"ONCE AND TOGGLES ARE CHECKED ON EVERY RUN, COOLDOWN AND TIMER AFTER {ExporterTestSceneKit.Format(Deadline)} S. TIMES IN MS, +-{TimeTolerance * 1000} MS.",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            g = kit.g;
            timed = new ExporterTestTimedTests(kit, Deadline);
            rowIndex = 0;

            kit.Section("ONCE, TOGGLE FLOW, TOGGLE VALUE");
            OnceTest();
            ToggleFlowTest();
            ToggleValueTest();

            kit.Section($"COOLDOWN {ExporterTestSceneKit.Format(CooldownDuration)} S, ENTERED EVERY FRAME UNTIL {ExporterTestSceneKit.Format(CooldownWindow)} S");
            CooldownTest();

            kit.Section("TIMER");
            TimerTest();
            TimerPauseTest();
            TimerToggleTest();

            timed.Build();
            kit.Finish();
            kit = null;
            g = null;
            timed = null;
        }

        #region Tests

        /// <summary>reset, enter x3, reset, enter: Once fires twice, After twice.</summary>
        private static void OnceTest()
        {
            var once = g.Add(new Once());
            var vOnce = timed.Variable("onceCount");
            var vAfter = timed.Variable("onceAfterCount");
            GraphBuilder.Flow(once.once, timed.Count(vOnce).assign);
            GraphBuilder.Flow(once.after, timed.Count(vAfter).assign);
            kit.AddStep(Run(once.reset, once.enter, once.enter, once.enter, once.reset, once.enter));

            Row("ONCE: RESET, ENTER X3, RESET, ENTER");
            SyncCell("ONCE", 2f, g.Get(vOnce), "ONCE fired {0} times");
            SyncCell("AFTER", 2f, g.Get(vAfter), "ONCE after fired {0} times");
        }

        /// <summary>
        /// turn off, turn on (known state: on), then enter, toggle, enter, turn off (no change), turn on, toggle, enter.
        /// </summary>
        private static void ToggleFlowTest()
        {
            var toggle = g.Add(new ToggleFlow());
            var vExitOn = timed.Variable("toggleFlowExitOn");
            var vExitOff = timed.Variable("toggleFlowExitOff");
            var vTurnedOn = timed.Variable("toggleFlowTurnedOn");
            var vTurnedOff = timed.Variable("toggleFlowTurnedOff");
            var vIsOn = timed.Variable("toggleFlowIsOn");
            GraphBuilder.Flow(toggle.exitOn, timed.Count(vExitOn).assign);
            GraphBuilder.Flow(toggle.exitOff, timed.Count(vExitOff).assign);
            GraphBuilder.Flow(toggle.turnedOn, timed.Count(vTurnedOn).assign);
            GraphBuilder.Flow(toggle.turnedOff, timed.Count(vTurnedOff).assign);

            kit.AddStep(Run(toggle.turnOff, toggle.turnOn, ResetVariables(vTurnedOn, vTurnedOff),
                toggle.enter, toggle.toggle, toggle.enter, toggle.turnOff, toggle.turnOn, toggle.toggle, toggle.enter,
                StoreBool(vIsOn, toggle.isOn)));

            Row("TOGGLE FLOW: EXITS, IS ON");
            SyncCell("EXIT ON", 1f, g.Get(vExitOn), "TOGGLE FLOW exit on fired {0} times");
            SyncCell("EXIT OFF", 2f, g.Get(vExitOff), "TOGGLE FLOW exit off fired {0} times");
            SyncCell("IS ON", 0f, g.Get(vIsOn), "TOGGLE FLOW is on = {0}");
            Row("TOGGLE FLOW: TURNED ON, TURNED OFF");
            SyncCell("ON", 1f, g.Get(vTurnedOn), "TOGGLE FLOW turned on fired {0} times");
            SyncCell("OFF", 2f, g.Get(vTurnedOff), "TOGGLE FLOW turned off fired {0} times");
        }

        /// <summary>On value 10, off value 20: turn on, toggle, toggle, turn off, turn off (no change); the value after each.</summary>
        private static void ToggleValueTest()
        {
            var toggle = g.Add(new ToggleValue());
            GraphBuilder.Value(g.Literal(10f), toggle.onValue);
            GraphBuilder.Value(g.Literal(20f), toggle.offValue);
            var v = new string[5];
            for (int i = 0; i < v.Length; i++)
                v[i] = timed.Variable($"toggleValue{i}");
            var vTurnedOn = timed.Variable("toggleValueTurnedOn");
            var vTurnedOff = timed.Variable("toggleValueTurnedOff");
            GraphBuilder.Flow(toggle.turnedOn, timed.Count(vTurnedOn).assign);
            GraphBuilder.Flow(toggle.turnedOff, timed.Count(vTurnedOff).assign);

            ControlInput Store(int i) => g.Set(v[i], toggle.value).assign;
            kit.AddStep(Run(toggle.turnOn, ResetVariables(vTurnedOn, vTurnedOff), Store(0),
                toggle.toggle, Store(1), toggle.toggle, Store(2), toggle.turnOff, Store(3), toggle.turnOff, Store(4)));

            Row("TOGGLE VALUE (ON 10, OFF 20)");
            SyncCell("ON", 10f, g.Get(v[0]), "TOGGLE VALUE after turn on is {0}");
            SyncCell("TOGGLED", 20f, g.Get(v[1]), "TOGGLE VALUE after toggle is {0}");
            SyncCell("AGAIN", 10f, g.Get(v[2]), "TOGGLE VALUE after the 2nd toggle is {0}");
            Row("TOGGLE VALUE: TURN OFF, TURN OFF");
            SyncCell("OFF", 20f, g.Get(v[3]), "TOGGLE VALUE after turn off is {0}");
            SyncCell("OFF AGAIN", 20f, g.Get(v[4]), "TOGGLE VALUE after the 2nd turn off is {0}");
            SyncCell("TURNED", 2f, g.Get(vTurnedOff), "TOGGLE VALUE turned off fired {0} times (toggle, turn off)");
        }

        /// <summary>
        /// Reset on every run (exit ready), then enter on every frame until <see cref="CooldownWindow"/>: ready again after
        /// 0.5, 1.0 and 1.5 s (plus frame time). Tick checks the remaining time and ratio.
        /// </summary>
        private static void CooldownTest()
        {
            var cooldown = g.Add(new Cooldown());
            cooldown.duration.SetDefaultValue(CooldownDuration);
            var vReady = timed.Variable("cooldownReady");
            var vNotReady = timed.Variable("cooldownNotReady");
            var vBecameReady = timed.Variable("cooldownBecameReady");
            var vTicks = timed.Variable("cooldownTicks");
            var vMaxRemaining = timed.Variable("cooldownMaxRemaining");
            var vRatioMismatch = timed.Variable("cooldownRatioMismatch");

            GraphBuilder.Flow(cooldown.exitReady, timed.Count(vReady).assign);
            GraphBuilder.Flow(cooldown.exitNotReady, timed.Count(vNotReady).assign);
            GraphBuilder.Flow(cooldown.becameReady, timed.Count(vBecameReady).assign);

            // tick: remaining time and ratio are assigned for the tick flow
            var tick = g.Sequence(3);
            GraphBuilder.Flow(cooldown.tick, tick.enter);
            GraphBuilder.Flow(tick.multiOutputs[0], timed.Count(vTicks).assign);
            GraphBuilder.Flow(tick.multiOutputs[1],
                g.Set(vMaxRemaining, g.Mathf2(nameof(Mathf.Max), g.Get(vMaxRemaining), cooldown.remainingSeconds)).assign);
            var ratioWrong = g.Add(new If());
            GraphBuilder.Value(g.Not(timed.Within(g.Mul(cooldown.remainingRatio, CooldownDuration), cooldown.remainingSeconds, 1e-3f)),
                ratioWrong.condition);
            GraphBuilder.Flow(ratioWrong.ifTrue, timed.Count(vRatioMismatch).assign);
            GraphBuilder.Flow(tick.multiOutputs[2], ratioWrong.enter);

            kit.AddStep(cooldown.reset);
            var inWindow = g.Add(new If());
            GraphBuilder.Value(g.Less(timed.Clock(), CooldownWindow), inWindow.condition);
            GraphBuilder.Flow(inWindow.ifTrue, cooldown.enter);
            timed.EveryFrame(inWindow.enter);

            Row("COOLDOWN EXITS (READY INCL. RESET)");
            TimedCell("READY", "=4", g.Get(vReady), 2, timed.Within(g.Get(vReady), 4f, 0.5f), "COOLDOWN exit ready fired {0} times");
            TimedCell("BECAME", "=3", g.Get(vBecameReady), 2, timed.Within(g.Get(vBecameReady), 3f, 0.5f),
                "COOLDOWN became ready fired {0} times");
            TimedCell("NOT READY", "> 3", g.Get(vNotReady), 4, g.GreaterOrEqual(g.Get(vNotReady), 3f),
                "COOLDOWN exit not ready fired {0} times");
            Row("COOLDOWN TICK: REMAINING, RATIO");
            TimedCell("TICKS", "> 3", g.Get(vTicks), 4, g.GreaterOrEqual(g.Get(vTicks), 3f), "COOLDOWN tick fired {0} times");
            TimedCell("MAX LEFT", $"<={CooldownDuration * 1000}", g.Mul(g.Get(vMaxRemaining), 1000f), 4,
                g.And(g.Less(g.Get(vMaxRemaining), CooldownDuration + 1e-4f), g.GreaterOrEqual(g.Get(vMaxRemaining), CooldownDuration - 0.25f)),
                "COOLDOWN max remaining seconds in tick is {0}", g.Get(vMaxRemaining));
            TimedCell("BAD RATIO", "=0", g.Get(vRatioMismatch), 2, g.Less(g.Get(vRatioMismatch), 0.5f),
                "COOLDOWN remaining ratio didn't match remaining seconds in {0} ticks");
        }

        /// <summary>Timer 1 s started on every run: completes once at 1 s, elapsed = duration at completion, consistent tick values.</summary>
        private static void TimerTest()
        {
            var timer = g.Add(new Timer());
            timer.duration.SetDefaultValue(1f);
            var vStarted = timed.Variable("timerStarted");
            var vCompleted = timed.Variable("timerCompleted");
            var vDone = timed.Variable("timerDoneAt", -1f);
            var vElapsedAtDone = timed.Variable("timerElapsedAtDone", -1f);
            var vTicks = timed.Variable("timerTicks");
            var vMismatch = timed.Variable("timerTickMismatch");

            GraphBuilder.Flow(timer.started, timed.Count(vStarted).assign);
            var countDone = timed.Count(vCompleted);
            var markDone = timed.Mark(vDone);
            var storeElapsed = g.Set(vElapsedAtDone, timer.elapsedSeconds);
            GraphBuilder.Flow(timer.completed, countDone.assign);
            GraphBuilder.Flow(countDone.assigned, markDone.assign);
            GraphBuilder.Flow(markDone.assigned, storeElapsed.assign);

            // tick: elapsed + remaining = duration, elapsed ratio = elapsed / duration
            var countTick = timed.Count(vTicks);
            GraphBuilder.Flow(timer.tick, countTick.assign);
            var wrong = g.Add(new If());
            GraphBuilder.Value(g.Not(g.And(
                timed.Within(g.Sum(timer.elapsedSeconds, timer.remainingSeconds), 1f, 1e-4f),
                timed.Within(timer.elapsedRatio, timer.elapsedSeconds, 1e-4f))), wrong.condition);
            GraphBuilder.Flow(countTick.assigned, wrong.enter);
            GraphBuilder.Flow(wrong.ifTrue, timed.Count(vMismatch).assign);

            kit.AddStep(timer.start);

            Row("TIMER 1 S: STARTED, COMPLETED");
            TimedCell("STARTED", "=1", g.Get(vStarted), 2, timed.Within(g.Get(vStarted), 1f, 0.5f), "TIMER started fired {0} times");
            TimedCell("DONE", "=1", g.Get(vCompleted), 2, timed.Within(g.Get(vCompleted), 1f, 0.5f), "TIMER completed fired {0} times");
            TimedCell("DONE AT", "=1000", g.Mul(g.Get(vDone), 1000f), 4, timed.Within(g.Get(vDone), 1f, TimeTolerance),
                "TIMER completed at {0} s, expected 1 s", g.Get(vDone));
            Row("TIMER 1 S: ELAPSED, TICK VALUES");
            TimedCell("ELAPSED", "=1000", g.Mul(g.Get(vElapsedAtDone), 1000f), 4, timed.Within(g.Get(vElapsedAtDone), 1f, 1e-4f),
                "TIMER elapsed seconds at completion is {0}, expected the duration (1)", g.Get(vElapsedAtDone));
            TimedCell("TICKS", "> 3", g.Get(vTicks), 4, g.GreaterOrEqual(g.Get(vTicks), 3f), "TIMER tick fired {0} times");
            TimedCell("BAD TICK", "=0", g.Get(vMismatch), 2, g.Less(g.Get(vMismatch), 0.5f),
                "TIMER elapsed/remaining/ratio didn't match in {0} ticks");
        }

        /// <summary>Timer 0.5 s: started on every run, paused at 0.2 s, resumed at 0.6 s, so it completes at 0.9 s.</summary>
        private static void TimerPauseTest()
        {
            var timer = g.Add(new Timer());
            timer.duration.SetDefaultValue(0.5f);
            var vDone = timed.Variable("timerPauseDoneAt", -1f);
            GraphBuilder.Flow(timer.completed, timed.Mark(vDone).assign);
            kit.AddStep(timer.start);
            timed.At(0.2f, timer.pause);
            timed.At(0.6f, timer.resume);

            Row("TIMER 0.5 S, PAUSE 0.2-0.6 S");
            TimedCell("DONE AT", "=900", g.Mul(g.Get(vDone), 1000f), 4, timed.Within(g.Get(vDone), 0.9f, TimeTolerance),
                "TIMER with pause completed at {0} s, expected 0.9 s", g.Get(vDone));
        }

        /// <summary>Timer 0.4 s controlled by Toggle: starts at 0 s (inactive), pauses at 0.3 s, resumes at 0.5 s: completes at 0.6 s.</summary>
        private static void TimerToggleTest()
        {
            var timer = g.Add(new Timer());
            timer.duration.SetDefaultValue(0.4f);
            var vDone = timed.Variable("timerToggleDoneAt", -1f);
            var vStarted = timed.Variable("timerToggleStarted");
            GraphBuilder.Flow(timer.completed, timed.Mark(vDone).assign);
            GraphBuilder.Flow(timer.started, timed.Count(vStarted).assign);
            timed.At(0f, timer.toggle);
            timed.At(0.3f, timer.toggle);
            timed.At(0.5f, timer.toggle);

            Row("TIMER 0.4 S, TOGGLE AT 0, 0.3, 0.5 S");
            TimedCell("STARTED", "=1", g.Get(vStarted), 2, timed.Within(g.Get(vStarted), 1f, 0.5f), "TIMER toggle started {0} times");
            TimedCell("DONE AT", "=600", g.Mul(g.Get(vDone), 1000f), 4, timed.Within(g.Get(vDone), 0.6f, TimeTolerance),
                "TIMER with toggle completed at {0} s, expected 0.6 s", g.Get(vDone));
        }

        #endregion

        #region Helpers

        /// <summary>Runs the inputs one after another (a Sequence, split if needed).</summary>
        private static ControlInput Run(params ControlInput[] inputs)
        {
            var entry = g.Sequence(1);
            g.FanOut(entry.multiOutputs[0], new List<ControlInput>(inputs));
            return entry.enter;
        }

        private static ControlInput ResetVariables(params string[] variables)
        {
            var entry = g.Sequence(1);
            var sets = new List<ControlInput>();
            foreach (var variable in variables)
                sets.Add(g.Set(variable, g.Literal(0f)).assign);
            g.FanOut(entry.multiOutputs[0], sets);
            return entry.enter;
        }

        /// <summary>variable = value ? 1 : 0</summary>
        private static ControlInput StoreBool(string variable, ValueOutput value)
        {
            var branch = g.Add(new If());
            GraphBuilder.Value(value, branch.condition);
            GraphBuilder.Flow(branch.ifTrue, g.Set(variable, g.Literal(1f)).assign);
            GraphBuilder.Flow(branch.ifFalse, g.Set(variable, g.Literal(0f)).assign);
            return branch.enter;
        }

        private static void Row(string label)
        {
            row = kit.Row();
            row.name = $"Row{++rowIndex:00}";
            kit.Label(row, label, Vector2.zero, LabelWidth);
            cell = 0;
        }

        private static float NextCellX() => FirstCellX + cell++ * ExporterTestSceneKit.CheckCellStep;

        /// <summary>Checked right after the test ran: value == expected.</summary>
        private static void SyncCell(string name, float expected, ValueOutput value, string message)
        {
            kit.AddStep(kit.CheckCell(row, NextCellX(), name, $"={expected}", value, 2, timed.Within(value, expected, 1e-3f),
                $"{message}, expected {expected}", value));
        }

        /// <summary>Checked at the deadline.</summary>
        private static void TimedCell(string name, string expected, ValueOutput shown, int digits, ValueOutput ok, string message,
            params ValueOutput[] logArgs)
        {
            if (logArgs.Length == 0)
                logArgs = new[] { shown };
            timed.AtDeadline(kit.CheckCell(row, NextCellX(), name, expected, shown, digits, ok, message, logArgs));
        }

        #endregion
    }
}
#endif
