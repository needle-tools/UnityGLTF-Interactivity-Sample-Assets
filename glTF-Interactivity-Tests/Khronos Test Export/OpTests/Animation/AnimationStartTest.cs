using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests animation/start by playing a custom, Unity-authored AnimationClip that linearly moves
    /// an empty node from the origin to a target position, and reading the node translation back.
    ///
    /// Checks:
    ///  - [out] fires as the *synchronous* continuation of [in] (a flow/sequence starts the animation
    ///    on output [0] -> [in] -> [out] -> flowcheck-1 and fires flowcheck-2 on output [1];
    ///    SetupOrderFlowCheck passes only if flowcheck-1 precedes flowcheck-2, catching impls that
    ///    defer [out] to completion)
    ///  - the translation read back at 50% of the duration is heading towards the target
    ///  - [done] fires once the animation reaches its end time
    ///  - the translation read back on [done] equals the target position
    ///  - [err] fires for invalid inputs: speed <= 0, speed NaN, speed +Inf, startTime NaN,
    ///    startTime +Inf, endTime NaN, and an invalid animation reference
    ///    (per spec these activate the err output flow)
    /// </summary>
    public class AnimationStartTest : ITestCase, IDisposable
    {
        private GameObject _target;
        private AnimationClip _clip;

        private CheckBox _outOrderCheckBox;
        private CheckBox _valueAt50CheckBox;
        private CheckBox _flowDoneCheckBox;
        private CheckBox _valueAt100CheckBox;

        private CheckBox _errSpeedNegCheckBox;
        private CheckBox _errSpeedZeroCheckBox;
        private CheckBox _errSpeedNaNCheckBox;
        private CheckBox _errSpeedInfCheckBox;
        private CheckBox _errStartTimeNaNCheckBox;
        private CheckBox _errStartTimeInfCheckBox;
        private CheckBox _errEndTimeNaNCheckBox;
        private CheckBox _errInvalidRefCheckBox;

        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);
        private const float Duration = 2f;

        public string GetTestName()
        {
            return "animation/start";
        }

        public string GetTestDescription()
        {
            return "Plays a custom AnimationClip moving an object and checks the object position at 50%/100% plus the error flows.";
        }

        public void PrepareObjects(TestContext context)
        {
            _target = AnimationTestHelper.CreateAnimatedObject(context.Root, "AnimationStartTarget", TargetPosition, Duration, out _clip);

            _outOrderCheckBox = context.AddCheckBox("[out] fired right after [in]");
            _valueAt50CheckBox = context.AddCheckBox("Position at 50%", true);
            _flowDoneCheckBox = context.AddCheckBox("Flow [done]", true);
            _valueAt100CheckBox = context.AddCheckBox("Position at 100%", true);
            context.NewRow();
            _errSpeedNegCheckBox = context.AddCheckBox("[err] flow (speed -1)");
            _errSpeedZeroCheckBox = context.AddCheckBox("[err] flow (speed 0)");
            _errSpeedNaNCheckBox = context.AddCheckBox("[err] flow (speed NaN)");
            _errSpeedInfCheckBox = context.AddCheckBox("[err] flow (speed +Inf)");
            _errStartTimeNaNCheckBox = context.AddCheckBox("[err] flow (startTime NaN)");
            context.NewRow();
            _errStartTimeInfCheckBox = context.AddCheckBox("[err] flow (startTime +Inf)");
            _errEndTimeNaNCheckBox = context.AddCheckBox("[err] flow (endTime NaN)");
            _errInvalidRefCheckBox = context.AddCheckBox("[err] flow (invalid ref)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var nodeIndex = nodeCreator.Context.exporter.GetTransformIndex(_target.transform);
            var animationIndex = nodeCreator.Context.exporter.GetAnimationId(_clip, _target.transform);

            var expectedMid = AnimationTestHelper.ToGltf(TargetPosition * 0.5f);
            var expectedEnd = AnimationTestHelper.ToGltf(TargetPosition);

            // ── Start the animation ────────────────────────────────────────────────
            var startNode = nodeCreator.CreateNode<Animation_StartNode>();
            startNode.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            startNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(0f);
            startNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(Duration);
            startNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(1f);

            // Single pointer/get to read the node translation back at the two check times.
            var pGet = AnimationTestHelper.CreateTranslationGet(nodeCreator, nodeIndex);

            var delayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(Duration / 2f);

            // See PointerInterpolateTest: use a flow/sequence to assert [out] is the *synchronous*
            // continuation of [in].
            //   Sequence[0] -> start [in] -> start [out] -> flowcheck-1
            //   Sequence[1] -> flowcheck-2
            //   Sequence[2] -> start the delay for the 50% value check
            var startSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
            startSequence.FlowOut("0").ConnectToFlowDestination(startNode.FlowIn(Animation_StartNode.IdFlowIn));
            startSequence.FlowOut("2").ConnectToFlowDestination(delayNode.FlowIn(Flow_SetDelayNode.IdFlowIn));

            context.NewEntryPoint("Play animation", Duration + 1f);
            context.AddToCurrentEntrySequence(startSequence.FlowIn(Flow_SequenceNode.IdFlowIn));

            _outOrderCheckBox.SetupOrderFlowCheck(new[]
            {
                startNode.FlowOut(Animation_StartNode.IdFlowOut), // flowcheck-1 (via [in] -> [out])
                startSequence.FlowOut("1"),                       // flowcheck-2 (Sequence[1])
            });

            // Position at 50%: read back after half the duration.
            _valueAt50CheckBox.proximityCheckDistance = 0.3f;
            _valueAt50CheckBox.SetupCheck(out var midValueRef, out var midFlowIn, expectedMid, true);
            midValueRef.ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));
            delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(midFlowIn);

            // Position at 100% + [done].
            _flowDoneCheckBox.SetupCheck(out var flowDoneCheckFlow);
            _valueAt100CheckBox.proximityCheckDistance = 0.01f;
            _valueAt100CheckBox.SetupCheck(out var endValueRef, out var endFlowIn, expectedEnd, true);
            endValueRef.ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));
            context.AddSequence(startNode.FlowOut(Animation_StartNode.IdFlowDone), new[]
            {
                flowDoneCheckFlow,
                endFlowIn
            });

            // ── Error flows ────────────────────────────────────────────────────────
            void AddErrorFlowCheck(CheckBox checkBox, float startTime, float endTime, float speed)
            {
                context.NewEntryPoint(checkBox.GetText());
                var errNode = nodeCreator.CreateNode<Animation_StartNode>();
                errNode.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
                errNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(startTime);
                errNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(endTime);
                errNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(speed);
                context.AddToCurrentEntrySequence(errNode.FlowIn(Animation_StartNode.IdFlowIn));
                checkBox.SetupCheck(errNode.FlowOut(Animation_StartNode.IdFlowError));
            }

            AddErrorFlowCheck(_errSpeedNegCheckBox,       0f,          Duration,       -1f);
            AddErrorFlowCheck(_errSpeedZeroCheckBox,      0f,          Duration,        0f);
            AddErrorFlowCheck(_errSpeedNaNCheckBox,       0f,          Duration,       float.NaN);
            AddErrorFlowCheck(_errSpeedInfCheckBox,       0f,          Duration,       float.PositiveInfinity);
            AddErrorFlowCheck(_errStartTimeNaNCheckBox,   float.NaN,   Duration,        1f);
            AddErrorFlowCheck(_errStartTimeInfCheckBox,   float.PositiveInfinity, Duration, 1f);
            AddErrorFlowCheck(_errEndTimeNaNCheckBox,     0f,          float.NaN,       1f);

            // Invalid animation reference (out-of-range index → invalid ref → err).
            context.NewEntryPoint(_errInvalidRefCheckBox.GetText());
            var errRefNode = nodeCreator.CreateNode<Animation_StartNode>();
            errRefNode.ValueIn(Animation_StartNode.IdValueAnimationRef)
                .SetValue(new StaticRefPointer($"/animations/{animationIndex + 1000}"));
            errRefNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(0f);
            errRefNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(Duration);
            errRefNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(1f);
            context.AddToCurrentEntrySequence(errRefNode.FlowIn(Animation_StartNode.IdFlowIn));
            _errInvalidRefCheckBox.SetupCheck(errRefNode.FlowOut(Animation_StartNode.IdFlowError));
        }

        public void Dispose()
        {
            if (_target != null)
                Object.DestroyImmediate(_target);
            if (_clip != null)
                Object.DestroyImmediate(_clip);
        }
    }
}
