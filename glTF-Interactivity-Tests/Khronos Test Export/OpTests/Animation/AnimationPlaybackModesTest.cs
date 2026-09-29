using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// animation/start with the timeline rules of the spec.
    /// Every case plays its own linear clip (origin -> TargetPosition over T seconds), so the node's Y
    /// translation tells which effective timestamp was applied: Y = TargetY * t / T.
    ///
    /// Checks:
    ///  - reverse playback (startTime > endTime)
    ///  - endTime +Inf loops forward and endTime -Inf loops in reverse: the timestamp wraps with period T
    ///    and [done] never fires
    ///  - startTime / endTime outside [0, T] wrap onto the clip instead of being clamped
    ///  - startTime == endTime applies the pose at startTime and fires [done]
    ///  - starting an animation that is already playing replaces the entry: the first [done] never fires
    ///  - an animation started from another animation's [done] plays and completes
    /// </summary>
    public class AnimationPlaybackModesTest : ITestCase, IDisposable
    {
        private const float T = 2f;
        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);
        private static float TargetY => TargetPosition.y;

        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _reverse;
        private AnimatedTestObject _loopForward;
        private AnimatedTestObject _loopReverse;
        private AnimatedTestObject _wrapped;
        private AnimatedTestObject _startEqualsEnd;
        private AnimatedTestObject _restart;
        private AnimatedTestObject _chainFirst;
        private AnimatedTestObject _chainSecond;

        private CheckBox _reverseMidCheckBox;
        private CheckBox _reverseDoneCheckBox;
        private CheckBox _reverseEndPositionCheckBox;

        private CheckBox _loopForwardPositionCheckBox;
        private CheckBox _loopForwardNoDoneCheckBox;
        private CheckBox _loopForwardIsPlayingCheckBox;
        private CheckBox _loopForwardPlayheadCheckBox;
        private CheckBox _loopForwardVirtualPlayheadCheckBox;
        private CheckBox _loopReversePositionCheckBox;
        private CheckBox _loopReverseNoDoneCheckBox;

        private CheckBox _wrappedDoneCheckBox;
        private CheckBox _wrappedPositionCheckBox;
        private CheckBox _startEqualsEndDoneCheckBox;
        private CheckBox _startEqualsEndPositionCheckBox;
        private CheckBox _restartFirstDoneCheckBox;
        private CheckBox _restartSecondDoneCheckBox;
        private CheckBox _chainedDoneCheckBox;
        private CheckBox _chainedPositionCheckBox;

        public string GetTestName()
        {
            return "animation/start playback modes";
        }

        public string GetTestDescription()
        {
            return "Reverse playback, infinite loops in both directions, start/end times outside the clip, startTime == endTime, restarting a playing animation, and starting an animation from another animation's [done].";
        }

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, T);
                _objects.Add(obj);
                return obj;
            }
            _reverse = Create("AnimationReverseTarget");
            _loopForward = Create("AnimationLoopForwardTarget");
            _loopReverse = Create("AnimationLoopReverseTarget");
            _wrapped = Create("AnimationWrappedTimesTarget");
            _startEqualsEnd = Create("AnimationStartEqualsEndTarget");
            _restart = Create("AnimationRestartTarget");
            _chainFirst = Create("AnimationChainFirstTarget");
            _chainSecond = Create("AnimationChainSecondTarget");

            _reverseMidCheckBox = context.AddCheckBox("Reverse (T..0):\nposition at 50%", true);
            _reverseDoneCheckBox = context.AddCheckBox("Reverse (T..0):\n[done]", true);
            _reverseEndPositionCheckBox = context.AddCheckBox("Reverse (T..0):\nposition 0 on [done]", true);
            _wrappedDoneCheckBox = context.AddCheckBox("Times outside [0,T]\n(1.25T..1.75T): [done]", true);
            _wrappedPositionCheckBox = context.AddCheckBox("Times outside [0,T]:\nwrapped pose on [done]", true);
            context.NewRow();
            _loopForwardPositionCheckBox = context.AddCheckBox("endTime +Inf:\nwraps (25% at 1.25T)", true);
            _loopForwardNoDoneCheckBox = context.AddCheckBox("endTime +Inf:\n[done] not fired", true);
            _loopForwardNoDoneCheckBox.Negate();
            _loopForwardIsPlayingCheckBox = context.AddCheckBox("endTime +Inf:\nisPlaying at 1.25T", true);
            _loopForwardPlayheadCheckBox = context.AddCheckBox("endTime +Inf:\nplayhead 0.25T", true);
            _loopForwardVirtualPlayheadCheckBox = context.AddCheckBox("endTime +Inf:\nvirtualPlayhead 1.25T", true);
            context.NewRow();
            _loopReversePositionCheckBox = context.AddCheckBox("endTime -Inf:\nwraps (75% at 1.25T)", true);
            _loopReverseNoDoneCheckBox = context.AddCheckBox("endTime -Inf:\n[done] not fired", true);
            _loopReverseNoDoneCheckBox.Negate();
            _startEqualsEndDoneCheckBox = context.AddCheckBox("startTime == endTime:\n[done]", true);
            _startEqualsEndPositionCheckBox = context.AddCheckBox("startTime == endTime:\npose at startTime", true);
            _restartFirstDoneCheckBox = context.AddCheckBox("Restart: 1st\n[done] not fired", true);
            _restartFirstDoneCheckBox.Negate();
            _restartSecondDoneCheckBox = context.AddCheckBox("Restart: 2nd\n[done]", true);
            context.NewRow();
            _chainedDoneCheckBox = context.AddCheckBox("Started from another\n[done]: [done]", true);
            _chainedPositionCheckBox = context.AddCheckBox("Started from another\n[done]: end pose", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Y check helper with a float tolerance
            void SetupYCheck(CheckBox checkBox, AnimatedTestObject obj, float expectedY, float tolerance, out FlowInRef flow)
            {
                checkBox.proximityCheckDistance = tolerance;
                checkBox.SetupCheck(AnimationTestHelper.CreateTranslationY(nodeCreator, obj.NodeIndex(context)), out flow, expectedY, true);
            }

            // ── Reverse playback: startTime > endTime ──────────────────────────────
            {
                var animationIndex = _reverse.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, T, 0f);
                var midDelay = AnimationTestHelper.CreateDelay(nodeCreator, T * 0.5f);
                context.NewEntryPoint(_reverseMidCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), midDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                SetupYCheck(_reverseMidCheckBox, _reverse, TargetY * 0.5f, 0.3f, out var midFlow);
                midDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(midFlow);

                _reverseDoneCheckBox.SetupCheck(out var doneFlow);
                SetupYCheck(_reverseEndPositionCheckBox, _reverse, 0f, 0.01f, out var endFlow);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, endFlow);
            }

            // ── endTime +Inf: forward loop ─────────────────────────────────────────
            // At 1.25T the requested timestamp is 1.25T, the effective one 0.25T.
            {
                var animationIndex = _loopForward.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, float.PositiveInfinity);
                var sampleDelay = AnimationTestHelper.CreateDelay(nodeCreator, T * 1.25f);
                context.NewEntryPoint(_loopForwardPositionCheckBox.GetText(), T * 1.25f + 0.5f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), sampleDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                SetupYCheck(_loopForwardPositionCheckBox, _loopForward, TargetY * 0.25f, 0.3f, out var positionFlow);
                _loopForwardIsPlayingCheckBox.SetupCheck(
                    AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "isPlaying", GltfTypes.Bool),
                    out var isPlayingFlow, true);
                _loopForwardPlayheadCheckBox.proximityCheckDistance = 0.15f;
                _loopForwardPlayheadCheckBox.SetupCheck(
                    AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "playhead", GltfTypes.Float),
                    out var playheadFlow, T * 0.25f, true);
                _loopForwardVirtualPlayheadCheckBox.proximityCheckDistance = 0.15f;
                _loopForwardVirtualPlayheadCheckBox.SetupCheck(
                    AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "virtualPlayhead", GltfTypes.Float),
                    out var virtualPlayheadFlow, T * 1.25f, true);
                context.AddSequence(sampleDelay.FlowOut(Flow_SetDelayNode.IdFlowDone), positionFlow, isPlayingFlow, playheadFlow, virtualPlayheadFlow);

                _loopForwardNoDoneCheckBox.SetupNegateCheck(start.FlowOut(Animation_StartNode.IdFlowDone));
            }

            // ── endTime -Inf: reverse loop ─────────────────────────────────────────
            // Starting at T, at 1.25T the requested timestamp is -0.25T, the effective one 0.75T.
            {
                var animationIndex = _loopReverse.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, T, float.NegativeInfinity);
                var sampleDelay = AnimationTestHelper.CreateDelay(nodeCreator, T * 1.25f);
                context.NewEntryPoint(_loopReversePositionCheckBox.GetText(), T * 1.25f + 0.5f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), sampleDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                SetupYCheck(_loopReversePositionCheckBox, _loopReverse, TargetY * 0.75f, 0.3f, out var positionFlow);
                sampleDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(positionFlow);

                _loopReverseNoDoneCheckBox.SetupNegateCheck(start.FlowOut(Animation_StartNode.IdFlowDone));
            }

            // ── Times outside [0, T] wrap onto the clip ────────────────────────────
            // endTime 1.75T -> effective 0.75T. An implementation that clamps to [0, T] plays T..T instead.
            {
                var animationIndex = _wrapped.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, T * 1.25f, T * 1.75f);
                context.NewEntryPoint(_wrappedDoneCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn));

                _wrappedDoneCheckBox.SetupCheck(out var doneFlow);
                SetupYCheck(_wrappedPositionCheckBox, _wrapped, TargetY * 0.75f, 0.01f, out var positionFlow);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, positionFlow);
            }

            // ── startTime == endTime ───────────────────────────────────────────────
            {
                var animationIndex = _startEqualsEnd.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, T * 0.5f, T * 0.5f);
                context.NewEntryPoint(_startEqualsEndDoneCheckBox.GetText(), 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn));

                _startEqualsEndDoneCheckBox.SetupCheck(out var doneFlow);
                SetupYCheck(_startEqualsEndPositionCheckBox, _startEqualsEnd, TargetY * 0.5f, 0.01f, out var positionFlow);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, positionFlow);
            }

            // ── Restart: the second start replaces the entry of the first ──────────
            {
                var animationIndex = _restart.AnimationIndex(context);
                var firstStart = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                var secondStart = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                context.NewEntryPoint(_restartFirstDoneCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(
                    firstStart.FlowIn(Animation_StartNode.IdFlowIn),
                    secondStart.FlowIn(Animation_StartNode.IdFlowIn));

                _restartFirstDoneCheckBox.SetupNegateCheck(firstStart.FlowOut(Animation_StartNode.IdFlowDone));
                _restartSecondDoneCheckBox.SetupCheck(secondStart.FlowOut(Animation_StartNode.IdFlowDone));
            }

            // ── Started from another animation's [done] ───────────────────────────
            // The [done] of the first animation starts the second one, which must play and complete.
            {
                var firstIndex = _chainFirst.AnimationIndex(context);
                var secondIndex = _chainSecond.AnimationIndex(context);
                var firstStart = AnimationTestHelper.CreateStart(nodeCreator, firstIndex, 0f, T);
                var secondStart = AnimationTestHelper.CreateStart(nodeCreator, secondIndex, 0f, T);
                firstStart.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(secondStart.FlowIn(Animation_StartNode.IdFlowIn));

                context.NewEntryPoint(_chainedDoneCheckBox.GetText(), T * 2f + 1f);
                context.AddToCurrentEntrySequence(firstStart.FlowIn(Animation_StartNode.IdFlowIn));

                _chainedDoneCheckBox.SetupCheck(out var doneFlow);
                SetupYCheck(_chainedPositionCheckBox, _chainSecond, TargetY, 0.01f, out var positionFlow);
                context.AddSequence(secondStart.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, positionFlow);
            }
        }

        public void Dispose()
        {
            foreach (var obj in _objects)
                obj.Destroy();
            _objects.Clear();
        }
    }
}
