using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests animation/stop by playing a custom, Unity-authored AnimationClip that linearly moves an
    /// empty node towards a target and then stopping it half-way through.
    ///
    /// Checks:
    ///  - stop [out] fires when the animation reference is valid
    ///  - the animated property keeps its current value after the stop (spec: "The animated properties
    ///    MUST keep their current values"): the Y position, read well past the animation's end time,
    ///    is still ~50% of the way (i.e. it did not continue to the target)
    ///  - the start node's [done] flow never fires, because the animation was stopped before its end
    ///  - stop [err] fires for an invalid animation reference
    /// </summary>
    public class AnimationStopTest : ITestCase, IDisposable
    {
        private GameObject _target;
        private AnimationClip _clip;

        private CheckBox _stopOutCheckBox;
        private CheckBox _frozenPositionCheckBox;
        private CheckBox _startDoneNotFiredCheckBox;
        private CheckBox _errCheckBox;

        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);
        private const float Duration = 3f;
        private const float StopDelay = Duration / 2f;

        public string GetTestName()
        {
            return "animation/stop";
        }

        public string GetTestDescription()
        {
            return "Plays a custom AnimationClip, stops it half-way and checks the object position stays frozen.";
        }

        public void PrepareObjects(TestContext context)
        {
            _target = AnimationTestHelper.CreateAnimatedObject(context.Root, "AnimationStopTarget", TargetPosition, Duration, out _clip);

            _stopOutCheckBox = context.AddCheckBox("Flow [out]", true);
            _frozenPositionCheckBox = context.AddCheckBox("Position frozen \nat ~50%", true);
            _startDoneNotFiredCheckBox = context.AddCheckBox("Start [done] \nnot fired", true);
            _startDoneNotFiredCheckBox.Negate();
            _errCheckBox = context.AddCheckBox("[err] flow \n(invalid ref)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var nodeIndex = nodeCreator.Context.exporter.GetTransformIndex(_target.transform);
            var animationIndex = nodeCreator.Context.exporter.GetAnimationId(_clip, _target.transform);

            // Expected frozen Y is half of the target Y (linear clip stopped at half the duration).
            var expectedFrozenY = TargetPosition.y * 0.5f;

            // ── Start, then stop half-way ──────────────────────────────────────────
            var startNode = nodeCreator.CreateNode<Animation_StartNode>();
            startNode.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            startNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(0f);
            startNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(Duration);
            startNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(1f);

            var stopNode = nodeCreator.CreateNode<Animation_StopNode>();
            stopNode.ValueIn(Animation_StopNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));

            var stopDelayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            stopDelayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(StopDelay);
            stopDelayNode.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(stopNode.FlowIn(Animation_StopNode.IdFlowIn));

            // Read the translation back well after the animation would have finished, extract the Y
            // component and assert it is still at ~50% (a two-sided float proximity check, so it fails
            // both if the object never moved and if it continued on to the target).
            var pGet = AnimationTestHelper.CreateTranslationGet(nodeCreator, nodeIndex);
            var extractNode = nodeCreator.CreateNode<Math_Extract3Node>();
            extractNode.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));

            var readDelayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            readDelayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(Duration + 0.5f);

            context.NewEntryPoint("Play and stop", Duration + 1f);
            context.AddToCurrentEntrySequence(
                startNode.FlowIn(Animation_StartNode.IdFlowIn),
                stopDelayNode.FlowIn(Flow_SetDelayNode.IdFlowIn),
                readDelayNode.FlowIn(Flow_SetDelayNode.IdFlowIn));

            _stopOutCheckBox.SetupCheck(stopNode.FlowOut(Animation_StopNode.IdFlowOut));
            _startDoneNotFiredCheckBox.SetupNegateCheck(startNode.FlowOut(Animation_StartNode.IdFlowDone));

            _frozenPositionCheckBox.proximityCheckDistance = 0.4f;
            _frozenPositionCheckBox.SetupCheck(out var frozenValueRef, out var frozenFlowIn, expectedFrozenY, true);
            frozenValueRef.ConnectToSource(extractNode.ValueOut(Math_Extract3Node.IdValueOutY));
            readDelayNode.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(frozenFlowIn);

            // ── Error flow: invalid animation reference ────────────────────────────
            context.NewEntryPoint(_errCheckBox.GetText());
            var errStopNode = nodeCreator.CreateNode<Animation_StopNode>();
            // An out-of-range animation index resolves to an invalid/null ref.
            errStopNode.ValueIn(Animation_StopNode.IdValueAnimationRef)
                .SetValue(new StaticRefPointer($"/animations/{animationIndex + 1000}"));
            context.AddToCurrentEntrySequence(errStopNode.FlowIn(Animation_StopNode.IdFlowIn));
            _errCheckBox.SetupCheck(errStopNode.FlowOut(Animation_StopNode.IdFlowError));
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
