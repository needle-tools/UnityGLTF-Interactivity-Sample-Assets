using System;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: the elapsed time is measured from the "system time when this entry was added". Which clock that is
    /// (frame time or the time of the call) is not defined, and an animation started from [done] loses the time the
    /// previous one overshot its end, so chained animations drift.
    ///
    /// A short animation (D = 0.2 s) is played 10 times in a row, each start from the previous [done]. A reference
    /// animation started at the same time is read when the chain ends; nominally 10 * D = 2 s have passed:
    ///  A: the overshoot is dropped, the chain is late by more than 1.5 frames at 60 fps (drift &gt; 0.025 s)
    ///  B: the overshoot is carried over (|drift| &lt; 0.025 s)
    /// The result variable of B holds the reference playhead.
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationChainDriftTest : ITestCase, IDisposable
    {
        private const float StepDuration = 0.2f;
        private const int Steps = 10;
        private const float ChainDuration = StepDuration * Steps;
        private const float ReferenceDuration = 2f * ChainDuration;
        private const float DriftLimit = 0.025f;

        private AnimatedTestObject _step;
        private AnimatedTestObject _reference;
        private CheckBox _driftCheckBox;
        private CheckBox _noDriftCheckBox;

        public string GetTestName() => "animation/drift of chained starts";

        public string GetTestDescription() =>
            "A 0.2 s animation played 10 times, each start from the previous [done], against a reference animation started at the same time. " +
            "A: the overshoot of each step is dropped (drift > 1.5 frames). B: it is carried over (no drift).";

        public void PrepareObjects(TestContext context)
        {
            _step = new AnimatedTestObject(context.Root, "UndefinedChainStepTarget", TargetPosition, StepDuration);
            _reference = new AnimatedTestObject(context.Root, "UndefinedChainReferenceTarget", TargetPosition, ReferenceDuration);
            _driftCheckBox = context.AddCheckBox("A: chain drifts\n(> 0.025 s after 10 steps)", true);
            _noDriftCheckBox = context.AddCheckBox("B: no drift\n(reference playhead 2 s)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var stepIndex = _step.AnimationIndex(context);
            var referenceIndex = _reference.AnimationIndex(context);

            var referenceStart = AnimationTestHelper.CreateStart(nodeCreator, referenceIndex, 0f, ReferenceDuration);
            var firstStep = AnimationTestHelper.CreateStart(nodeCreator, stepIndex, 0f, StepDuration);
            var lastStep = firstStep;
            for (int i = 1; i < Steps; i++)
            {
                var next = AnimationTestHelper.CreateStart(nodeCreator, stepIndex, 0f, StepDuration);
                lastStep.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(next.FlowIn(Animation_StartNode.IdFlowIn));
                lastStep = next;
            }

            context.NewEntryPoint(_driftCheckBox.GetText(), ChainDuration + 1.5f);
            context.AddToCurrentEntrySequence(referenceStart.FlowIn(Animation_StartNode.IdFlowIn), firstStep.FlowIn(Animation_StartNode.IdFlowIn));

            var referencePlayhead = State(context, referenceIndex, "playhead", GltfTypes.Float);
            var drift = nodeCreator.CreateNode<Math_SubNode>();
            drift.ValueIn(Math_SubNode.IdValueA).ConnectToSource(referencePlayhead);
            drift.ValueIn("b").SetValue(ChainDuration);

            _driftCheckBox.SetupCheck(GreaterThan(context, drift.FirstValueOut(), DriftLimit), out var driftFlow, true);
            _noDriftCheckBox.proximityCheckDistance = DriftLimit;
            _noDriftCheckBox.SetupCheck(referencePlayhead, out var noDriftFlow, ChainDuration, true);
            context.AddSequence(lastStep.FlowOut(Animation_StartNode.IdFlowDone), driftFlow, noDriftFlow,
                AnimationTestHelper.CreateStop(nodeCreator, referenceIndex).FlowIn(Animation_StopNode.IdFlowIn));
        }

        public void Dispose()
        {
            _step?.Destroy();
            _reference?.Destroy();
            _step = null;
            _reference = null;
        }
    }
}
