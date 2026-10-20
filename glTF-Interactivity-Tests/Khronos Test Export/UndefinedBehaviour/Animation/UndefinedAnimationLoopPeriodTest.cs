using System;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec contradiction: animation/start takes the loop period T from "all animation sampler input accessors of the
    /// animation", while the maxTime property ignores samplers no channel uses (and the authoring tip says to use maxTime
    /// as endTime for a full playback). "playhead ... is always less than or equal to maxTime" only holds if both agree.
    ///
    /// The channel's keys run 0..T, an unused sampler runs to 2T. The animation loops (endTime +Inf); at 1.25T:
    ///  A: wraps at maxTime (period T): playhead 0.25T, pose at 0.25T
    ///  B: wraps at the all-sampler T (period 2T): playhead 1.25T &gt; maxTime, pose clamped at the last key
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationLoopPeriodTest : ITestCase, IDisposable
    {
        private const float Tolerance = 0.1f;

        private AnimatedTestObject _target;
        private CheckBox _maxTimeCheckBox;
        private CheckBox _playheadMaxTimeCheckBox;
        private CheckBox _playheadAllSamplersCheckBox;
        private CheckBox _poseMaxTimeCheckBox;
        private CheckBox _poseAllSamplersCheckBox;

        public string GetTestName() => "animation/loop period with unused sampler";

        public string GetTestDescription() =>
            "animation/start takes the loop period T from all samplers, maxTime ignores unused ones. Channel keys 0..T, unused sampler to 2T, looping with endTime +Inf, read at 1.25T. " +
            "A: wraps at maxTime (playhead 0.25T). B: wraps at 2T (playhead 1.25T > maxTime, pose clamped at the last key).";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedLoopPeriodTarget", TargetPosition, T);
            _maxTimeCheckBox = context.AddCheckBox("maxTime == T\n(spec reference)");
            context.NewRow();
            _playheadMaxTimeCheckBox = context.AddCheckBox("A: wraps at maxTime:\nplayhead 0.25T", true);
            _playheadAllSamplersCheckBox = context.AddCheckBox("B: wraps at 2T:\nplayhead 1.25T", true);
            _poseMaxTimeCheckBox = context.AddCheckBox("A: wraps at maxTime:\npose at 0.25T", true);
            _poseAllSamplersCheckBox = context.AddCheckBox("B: wraps at 2T:\npose clamped at T", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var animationIndex = _target.AnimationIndex(context);
            AnimationStateTest.AddUnusedSampler(nodeCreator.Context.exporter, animationIndex, 2f * T);

            var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, float.PositiveInfinity);
            var delay = AnimationTestHelper.CreateDelay(nodeCreator, 1.25f * T);
            var stop = AnimationTestHelper.CreateStop(nodeCreator, animationIndex);

            context.NewEntryPoint(_maxTimeCheckBox.GetText(), 1.25f * T + 1f);
            _maxTimeCheckBox.proximityCheckDistance = 0.01f;
            _maxTimeCheckBox.SetupCheck(State(context, animationIndex, "maxTime", GltfTypes.Float), out var maxTimeFlow, T, true);
            context.AddToCurrentEntrySequence(maxTimeFlow, start.FlowIn(Animation_StartNode.IdFlowIn), delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            var playhead = State(context, animationIndex, "playhead", GltfTypes.Float);
            var y = AnimationTestHelper.CreateTranslationY(nodeCreator, _target.NodeIndex(context));
            _playheadMaxTimeCheckBox.proximityCheckDistance = Tolerance;
            _playheadMaxTimeCheckBox.SetupCheck(playhead, out var playheadMaxTimeFlow, 0.25f * T, true);
            _playheadAllSamplersCheckBox.proximityCheckDistance = Tolerance;
            _playheadAllSamplersCheckBox.SetupCheck(playhead, out var playheadAllSamplersFlow, 1.25f * T, true);
            _poseMaxTimeCheckBox.proximityCheckDistance = Tolerance;
            _poseMaxTimeCheckBox.SetupCheck(y, out var poseMaxTimeFlow, 0.25f * TargetPosition.y, true);
            _poseAllSamplersCheckBox.proximityCheckDistance = Tolerance;
            _poseAllSamplersCheckBox.SetupCheck(y, out var poseAllSamplersFlow, TargetPosition.y, true);
            context.AddSequence(delay.FlowOut(Flow_SetDelayNode.IdFlowDone),
                playheadMaxTimeFlow, playheadAllSamplersFlow, poseMaxTimeFlow, poseAllSamplersFlow,
                stop.FlowIn(Animation_StopNode.IdFlowIn));
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
