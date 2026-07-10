using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests animation/stopAt by playing a custom, Unity-authored AnimationClip that linearly moves
    /// an empty node towards a target and scheduling a stop half-way along the clip's timeline.
    ///
    /// Checks:
    ///  - stopAt [out] fires immediately when the input values are valid
    ///  - stopAt [done] fires once the animation reaches the scheduled stop time
    ///  - the node position sampled on [done] equals the pose at the stop time (~50% of the way)
    ///  - the start node's [done] flow never fires, because the animation is stopped before its end
    ///  - stopAt [err] fires for invalid inputs: NaN stopTime and an invalid animation reference
    /// </summary>
    public class AnimationStopAtTest : ITestCase, IDisposable
    {
        private GameObject _target;
        private AnimationClip _clip;

        private CheckBox _outCheckBox;
        private CheckBox _doneCheckBox;
        private CheckBox _positionAtStopCheckBox;
        private CheckBox _startDoneNotFiredCheckBox;
        private CheckBox _errStopTimeNaNCheckBox;
        private CheckBox _errInvalidRefCheckBox;

        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);
        private const float Duration = 4f;
        private const float StopTime = Duration / 2f;

        public string GetTestName()
        {
            return "animation/stopAt";
        }

        public string GetTestDescription()
        {
            return "Plays a custom AnimationClip and schedules a stop half-way, checking the object position on [done].";
        }

        public void PrepareObjects(TestContext context)
        {
            _target = AnimationTestHelper.CreateAnimatedObject(context.Root, "AnimationStopAtTarget", TargetPosition, Duration, out _clip);

            _outCheckBox = context.AddCheckBox("Flow [out]");
            _doneCheckBox = context.AddCheckBox("Flow [done]", true);
            _positionAtStopCheckBox = context.AddCheckBox("Position at \nstopTime", true);
            _startDoneNotFiredCheckBox = context.AddCheckBox("Start [done] \nnot fired", true);
            _startDoneNotFiredCheckBox.Negate();
            context.NewRow();
            _errStopTimeNaNCheckBox = context.AddCheckBox("[err] flow \n(stopTime NaN)");
            _errInvalidRefCheckBox = context.AddCheckBox("[err] flow \n(invalid ref)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var nodeIndex = nodeCreator.Context.exporter.GetTransformIndex(_target.transform);
            var animationIndex = nodeCreator.Context.exporter.GetAnimationId(_clip, _target.transform);

            // Expected Y at the stop time (linear clip): target Y scaled by stopTime / duration.
            var expectedStopY = TargetPosition.y * (StopTime / Duration);

            // ── Start, then schedule a stop half-way ───────────────────────────────
            var startNode = nodeCreator.CreateNode<Animation_StartNode>();
            startNode.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            startNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(0f);
            startNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(Duration);
            startNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(1f);

            var stopAtNode = nodeCreator.CreateNode<Animation_StopAtNode>();
            stopAtNode.ValueIn(Animation_StopAtNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            stopAtNode.ValueIn(Animation_StopAtNode.IdValueStopTime).SetValue(StopTime);

            var pGet = AnimationTestHelper.CreateTranslationGet(nodeCreator, nodeIndex);
            var extractNode = nodeCreator.CreateNode<Math_Extract3Node>();
            extractNode.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));

            // stopAt must run after start has added the animation state entry.
            context.NewEntryPoint("Play and stopAt", Duration + 1f);
            context.AddToCurrentEntrySequence(
                startNode.FlowIn(Animation_StartNode.IdFlowIn),
                stopAtNode.FlowIn(Animation_StopAtNode.IdFlowIn));

            _outCheckBox.SetupCheck(stopAtNode.FlowOut(Animation_StopAtNode.IdFlowOut));
            _startDoneNotFiredCheckBox.SetupNegateCheck(startNode.FlowOut(Animation_StartNode.IdFlowDone));

            // [done] + the position sampled when it fires.
            _doneCheckBox.SetupCheck(out var doneCheckFlow);
            _positionAtStopCheckBox.proximityCheckDistance = 0.4f;
            _positionAtStopCheckBox.SetupCheck(out var stopValueRef, out var stopValueFlowIn, expectedStopY, true);
            stopValueRef.ConnectToSource(extractNode.ValueOut(Math_Extract3Node.IdValueOutY));
            context.AddSequence(stopAtNode.FlowOut(Animation_StopAtNode.IdFlowDone), new[]
            {
                doneCheckFlow,
                stopValueFlowIn
            });

            // ── Error flows ────────────────────────────────────────────────────────
            context.NewEntryPoint(_errStopTimeNaNCheckBox.GetText());
            var errNaNNode = nodeCreator.CreateNode<Animation_StopAtNode>();
            errNaNNode.ValueIn(Animation_StopAtNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            errNaNNode.ValueIn(Animation_StopAtNode.IdValueStopTime).SetValue(float.NaN);
            context.AddToCurrentEntrySequence(errNaNNode.FlowIn(Animation_StopAtNode.IdFlowIn));
            _errStopTimeNaNCheckBox.SetupCheck(errNaNNode.FlowOut(Animation_StopAtNode.IdFlowError));

            context.NewEntryPoint(_errInvalidRefCheckBox.GetText());
            var errRefNode = nodeCreator.CreateNode<Animation_StopAtNode>();
            errRefNode.ValueIn(Animation_StopAtNode.IdValueAnimationRef)
                .SetValue(new StaticRefPointer($"/animations/{animationIndex + 1000}"));
            errRefNode.ValueIn(Animation_StopAtNode.IdValueStopTime).SetValue(StopTime);
            context.AddToCurrentEntrySequence(errRefNode.FlowIn(Animation_StopAtNode.IdFlowIn));
            _errInvalidRefCheckBox.SetupCheck(errRefNode.FlowOut(Animation_StopAtNode.IdFlowError));
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
