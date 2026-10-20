using System;
using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: animation/start accepts any finite speed &gt; 0, but the scaled elapsed time can overflow. With the largest
    /// double as speed, elapsed * speed reaches +Inf after about 1 s. With endTime +Inf the end condition
    /// "current timestamp &gt;= end time" then becomes true, [done] fires and the effective timestamp is
    /// Inf - Inf * T = NaN. Before that the requested timestamps are huge, and r - s * T loses all precision,
    /// although "playhead ... is always greater than or equal to zero and less than or equal to maxTime".
    /// The speed is written as the double 1.7976931348623157e308 after export (C# float can't hold it).
    ///
    ///  start:      A: [err] (e.g. the speed became +Inf in float32)   B: [out]
    ///  at 0.5 s:   A: playhead within [0, maxTime]                   B: outside or NaN
    ///  [done]:     A: fires after the overflow                       B: never fires
    ///  at 2.5 s:   A: pose NaN                                       B: pose finite
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationHugeSpeedTest : ITestCase, IDisposable
    {
        private AnimatedTestObject _target;
        private CheckBox _errCheckBox, _outCheckBox;
        private CheckBox _playheadInRangeCheckBox, _playheadOutOfRangeCheckBox;
        private CheckBox _doneCheckBox, _noDoneCheckBox;
        private CheckBox _poseNaNCheckBox, _poseFiniteCheckBox;

        public string GetTestName() => "animation/huge speed overflow";

        public string GetTestDescription() =>
            "animation/start(0, +Inf) with speed 1.7976931348623157e308: the scaled elapsed time overflows to +Inf after about 1 s. " +
            "Observed: [err] or [out]; playhead within [0, maxTime] at 0.5 s; [done] after the overflow; NaN pose at 2.5 s.";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedHugeSpeedTarget", TargetPosition, T);
            _errCheckBox = context.AddCheckBox("A: start [err]", true);
            _outCheckBox = context.AddCheckBox("B: start [out]", true);
            _playheadInRangeCheckBox = context.AddCheckBox("A: 0.5 s: playhead\nin [0, maxTime]", true);
            _playheadOutOfRangeCheckBox = context.AddCheckBox("B: 0.5 s: playhead\noutside or NaN", true);
            context.NewRow();
            _doneCheckBox = context.AddCheckBox("A: [done] fires\nafter overflow", true);
            _noDoneCheckBox = context.AddCheckBox("B: [done] never fires", true);
            _noDoneCheckBox.Negate();
            _poseNaNCheckBox = context.AddCheckBox("A: 2.5 s: pose NaN", true);
            _poseFiniteCheckBox = context.AddCheckBox("B: 2.5 s: pose finite", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var animationIndex = _target.AnimationIndex(context);

            var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, float.PositiveInfinity);
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, start)["values"][Animation_StartNode.IdValueSpeed]["value"] = new JArray(double.MaxValue));

            var earlyDelay = AnimationTestHelper.CreateDelay(nodeCreator, 0.5f);
            var lateDelay = AnimationTestHelper.CreateDelay(nodeCreator, 2.5f);
            context.NewEntryPoint(_errCheckBox.GetText(), 3.5f);
            context.AddToCurrentEntrySequence(
                start.FlowIn(Animation_StartNode.IdFlowIn),
                earlyDelay.FlowIn(Flow_SetDelayNode.IdFlowIn),
                lateDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            _errCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowError));
            _outCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowOut));

            var playheadInRange = InRange(context, State(context, animationIndex, "playhead", GltfTypes.Float), 0f,
                State(context, animationIndex, "maxTime", GltfTypes.Float));
            _playheadInRangeCheckBox.SetupCheck(playheadInRange, out var inRangeFlow, true);
            _playheadOutOfRangeCheckBox.SetupCheck(playheadInRange, out var outOfRangeFlow, false);
            context.AddSequence(earlyDelay.FlowOut(Flow_SetDelayNode.IdFlowDone), inRangeFlow, outOfRangeFlow);

            _doneCheckBox.SetupCheck(out var doneFlow);
            _noDoneCheckBox.SetupNegateCheck(out var noDoneFlow);
            context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, noDoneFlow);

            var poseNaN = IsNaN(context, AnimationTestHelper.CreateTranslationY(nodeCreator, _target.NodeIndex(context)));
            _poseNaNCheckBox.SetupCheck(poseNaN, out var nanFlow, true);
            _poseFiniteCheckBox.SetupCheck(poseNaN, out var finiteFlow, false);
            context.AddSequence(lateDelay.FlowOut(Flow_SetDelayNode.IdFlowDone), nanFlow, finiteFlow,
                AnimationTestHelper.CreateStop(nodeCreator, animationIndex).FlowIn(Animation_StopNode.IdFlowIn));
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
