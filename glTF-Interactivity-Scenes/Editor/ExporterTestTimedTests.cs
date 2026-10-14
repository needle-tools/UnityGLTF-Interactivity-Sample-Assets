#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Timing for exporter test scenes with tests that take time: a run clock (sum of Time.deltaTime in Update, reset on
    /// every run), actions scheduled once per run at clock times, actions on every frame until the deadline, and checks
    /// that all run at the deadline. Running the checks at a fixed time, instead of when a test finishes, makes a test
    /// that never finishes (e.g. a hanging coroutine) fail instead of staying grey.
    /// Call <see cref="Build"/> after all tests were added.
    /// </summary>
    internal class ExporterTestTimedTests
    {
        public const string ClockVariable = "clock";
        private const string DeadlineReachedVariable = "deadlineReached";
        private const string MaxFrameTimeVariable = "maxFrameTime";

        public readonly float deadline;
        private readonly ExporterTestSceneKit kit;
        private readonly GraphBuilder g;
        private readonly List<(float time, ControlInput action)> scheduled = new List<(float, ControlInput)>();
        private readonly List<ControlInput> everyFrame = new List<ControlInput>();
        private readonly List<ControlInput> atDeadline = new List<ControlInput>();

        public ExporterTestTimedTests(ExporterTestSceneKit kit, float deadline)
        {
            this.kit = kit;
            this.deadline = deadline;
            g = kit.g;
            g.Declare(ClockVariable, 0f);
            g.Declare(DeadlineReachedVariable, false);
            g.Declare(MaxFrameTimeVariable, 0f);
            kit.AddReset(g.Set(ClockVariable, g.Literal(0f)).assign);
            kit.AddReset(g.Set(DeadlineReachedVariable, g.Literal(false)).assign);
            kit.AddReset(g.Set(MaxFrameTimeVariable, g.Literal(0f)).assign);
        }

        public ValueOutput Clock() => g.Get(ClockVariable);

        /// <summary>Longest frame (Time.deltaTime) of this run so far, not counting the run's first frame.</summary>
        public ValueOutput MaxFrameTime() => g.Get(MaxFrameTimeVariable);

        /// <summary>True from the start of a run until the deadline.</summary>
        public ValueOutput Running() => g.Not(g.Get(DeadlineReachedVariable));

        public ValueOutput DeltaTime() => StaticGet(g, typeof(Time), nameof(Time.deltaTime));

        public static ValueOutput StaticGet(GraphBuilder g, System.Type type, string member) =>
            g.Add(new GetMember(new Member(type, member)), 0f, 160f).value;

        /// <summary>Float variable, reset to <paramref name="initial"/> on every run.</summary>
        public string Variable(string name, float initial = 0f)
        {
            g.Declare(name, initial);
            kit.AddReset(g.Set(name, g.Literal(initial)).assign);
            return name;
        }

        /// <summary>variable = clock</summary>
        public SetVariable Mark(string variable) => g.Set(variable, Clock());

        /// <summary>variable += 1</summary>
        public SetVariable Count(string variable) => g.Set(variable, g.Sum(g.Get(variable), 1f));

        /// <summary>variable += value</summary>
        public SetVariable Add(string variable, ValueOutput value) => g.Set(variable, g.Sum(g.Get(variable), value));

        /// <summary>Runs <paramref name="action"/> once per run, on the first frame with clock &gt;= <paramref name="time"/>.</summary>
        public void At(float time, ControlInput action) => scheduled.Add((time, action));

        /// <summary>Runs <paramref name="action"/> on every frame (Update) until the deadline.</summary>
        public void EveryFrame(ControlInput action) => everyFrame.Add(action);

        /// <summary>Runs <paramref name="check"/> at the deadline.</summary>
        public void AtDeadline(ControlInput check) => atDeadline.Add(check);

        /// <summary>|a - b| &lt;= tolerance</summary>
        public ValueOutput Within(ValueOutput a, ValueOutput b, float tolerance) =>
            g.Less(g.Mathf1(nameof(Mathf.Abs), g.Sub(a, b)), tolerance);

        public ValueOutput Within(ValueOutput a, float b, float tolerance) => Within(a, g.Literal(b), tolerance);

        /// <summary>
        /// expected - tolerance - 1 frame &lt;= actual &lt;= expected + tolerance + <paramref name="frames"/> frames (frame =
        /// <see cref="MaxFrameTime"/>): waits end on frames (and some engines run the flow after a wait only on the next
        /// frame), and the clock (summed Time.deltaTime) can be a frame behind the engine's timers. Keeps slow runtimes from
        /// failing timing checks.
        /// </summary>
        public ValueOutput OnTime(ValueOutput actual, float expected, float tolerance, int frames)
        {
            var earliest = g.Sub(g.Literal(expected - tolerance), MaxFrameTime());
            var latest = g.Sum(g.Mul(MaxFrameTime(), (float)frames), expected + tolerance);
            return g.And(AtLeast(actual, earliest), g.Less(actual, latest));
        }

        /// <summary>a &gt;= b</summary>
        public ValueOutput AtLeast(ValueOutput a, ValueOutput b)
        {
            var unit = g.Add(new GreaterOrEqual(), 0f, 60f);
            GraphBuilder.Value(a, unit.a);
            GraphBuilder.Value(b, unit.b);
            return unit.comparison;
        }

        public void Build()
        {
            g.Row(-3000f);
            var update = g.Add(new Update());
            var tick = g.Set(ClockVariable, g.Sum(Clock(), DeltaTime()));
            GraphBuilder.Flow(update.trigger, tick.assign);

            // longest frame, from the run's second frame on (the first one can include loading)
            var trackFrame = g.Add(new If());
            GraphBuilder.Value(g.And(g.GreaterOrEqual(g.Sub(Clock(), DeltaTime()), 1e-6f), Running()), trackFrame.condition);
            GraphBuilder.Flow(trackFrame.ifTrue, g.Set(MaxFrameTimeVariable, g.Mathf2(nameof(Mathf.Max), MaxFrameTime(), DeltaTime())).assign);

            var targets = new List<ControlInput> { trackFrame.enter };
            for (int i = 0; i < scheduled.Count; i++)
            {
                var (time, action) = scheduled[i];
                var flag = $"scheduled{i:00}";
                g.Declare(flag, false);
                kit.AddReset(g.Set(flag, g.Literal(false)).assign);

                var due = g.Add(new If());
                GraphBuilder.Value(g.And(g.GreaterOrEqual(Clock(), time), g.Not(g.Get(flag))), due.condition);
                var setFlag = g.Set(flag, g.Literal(true));
                GraphBuilder.Flow(due.ifTrue, setFlag.assign);
                GraphBuilder.Flow(setFlag.assigned, action);
                targets.Add(due.enter);
            }

            if (everyFrame.Count > 0)
            {
                var running = g.Add(new If());
                GraphBuilder.Value(g.Less(Clock(), deadline), running.condition);
                g.FanOut(running.ifTrue, everyFrame);
                targets.Add(running.enter);
            }

            var reached = g.Add(new If());
            GraphBuilder.Value(g.And(g.GreaterOrEqual(Clock(), deadline), Running()), reached.condition);
            var setReached = g.Set(DeadlineReachedVariable, g.Literal(true));
            GraphBuilder.Flow(reached.ifTrue, setReached.assign);
            if (atDeadline.Count > 0)
                g.FanOut(setReached.assigned, atDeadline);
            targets.Add(reached.enter);

            g.FanOut(tick.assigned, targets);
        }
    }
}
#endif
