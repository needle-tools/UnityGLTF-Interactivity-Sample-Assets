using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: [done] is activated while the animation update is still iterating the animation state array, and the
    /// flow can change that array. All animations here are one-shot (startTime == endTime), so they end on the next update.
    ///
    /// Started from [done]: animation 1's [done] starts animation 2. An onTick counter compares the frames of both [done]:
    ///  A: animation 2 is processed in the same update (same tick count)
    ///  B: animation 2 is processed in the next update (tick count + 1)
    ///
    /// Stopped from [done]: animations 3 and 4 end in the same update; 3's [done] stops 4:
    ///  A: 4 is skipped, its [done] never fires
    ///  B: 4 is still processed, its [done] fires after the stop
    ///  C: 4 was processed before 3, its [done] fires before the stop
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationChangesDuringUpdateTest : ITestCase, IDisposable
    {
        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _first, _chained, _stopper, _stopped;

        private CheckBox _chainedSameUpdateCheckBox;
        private CheckBox _chainedNextUpdateCheckBox;
        private CheckBox _stoppedSkippedCheckBox;
        private CheckBox _stoppedStillProcessedCheckBox;
        private CheckBox _stoppedProcessedFirstCheckBox;

        public string GetTestName() => "animation/changes during update";

        public string GetTestDescription() =>
            "[done] runs while the animation update iterates the animation state array. " +
            "Started from [done]: A: the new animation is processed in the same update. B: in the next update. " +
            "Stopped from [done] before it was processed: A: skipped. B: still processed ([done] fires). C: it was processed first.";

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, T);
                _objects.Add(obj);
                return obj;
            }
            _first = Create("UndefinedChangesFirstTarget");
            _chained = Create("UndefinedChangesChainedTarget");
            _stopper = Create("UndefinedChangesStopperTarget");
            _stopped = Create("UndefinedChangesStoppedTarget");

            _chainedSameUpdateCheckBox = context.AddCheckBox("A: started from [done]:\nsame update", true);
            _chainedNextUpdateCheckBox = context.AddCheckBox("B: started from [done]:\nnext update", true);
            context.NewRow();
            _stoppedSkippedCheckBox = context.AddCheckBox("A: stopped from [done]:\nskipped, no [done]", true);
            _stoppedSkippedCheckBox.Negate();
            _stoppedStillProcessedCheckBox = context.AddCheckBox("B: stopped from [done]:\nstill processed", true);
            _stoppedProcessedFirstCheckBox = context.AddCheckBox("C: stopped one was\nprocessed first", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── Started from [done] ────────────────────────────────────────────────
            {
                var ticks = CreateTickCounter(context);
                var tickAtFirstDoneVar = AddVariable(context, "UndefinedChanges_tickAtFirstDone", 0, GltfTypes.Int);

                var startFirst = AnimationTestHelper.CreateStart(nodeCreator, _first.AnimationIndex(context), 0.5f * T, 0.5f * T);
                var startChained = AnimationTestHelper.CreateStart(nodeCreator, _chained.AnimationIndex(context), 0.25f * T, 0.25f * T);
                VariablesHelpers.SetVariable(nodeCreator, tickAtFirstDoneVar, ticks,
                    startFirst.FlowOut(Animation_StartNode.IdFlowDone), startChained.FlowIn(Animation_StartNode.IdFlowIn));
                context.NewEntryPoint(startFirst.FlowIn(Animation_StartNode.IdFlowIn), _chainedSameUpdateCheckBox.GetText(), 1.5f);

                var tickDifference = Sub(context, ticks, Get(context, tickAtFirstDoneVar));
                _chainedSameUpdateCheckBox.SetupCheck(tickDifference, out var sameFlow, 0);
                _chainedNextUpdateCheckBox.SetupCheck(tickDifference, out var nextFlow, 1);
                context.AddSequence(startChained.FlowOut(Animation_StartNode.IdFlowDone), sameFlow, nextFlow);
            }

            // ── Stopped from [done] ────────────────────────────────────────────────
            {
                var stoppedVar = AddVariable(context, "UndefinedChanges_stopCalled", false, GltfTypes.Bool);
                var stoppedIndex = _stopped.AnimationIndex(context);

                var startStopper = AnimationTestHelper.CreateStart(nodeCreator, _stopper.AnimationIndex(context), 0.5f * T, 0.5f * T);
                var startStopped = AnimationTestHelper.CreateStart(nodeCreator, stoppedIndex, 0.5f * T, 0.5f * T);
                var stop = AnimationTestHelper.CreateStop(nodeCreator, stoppedIndex);
                VariablesHelpers.SetVariableStaticValue(nodeCreator, stoppedVar, true, out var markFlow, out var markedFlow);
                startStopper.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(markFlow);
                markedFlow.ConnectToFlowDestination(stop.FlowIn(Animation_StopNode.IdFlowIn));

                context.NewEntryPoint(_stoppedSkippedCheckBox.GetText(), 1.5f);
                context.AddToCurrentEntrySequence(startStopper.FlowIn(Animation_StartNode.IdFlowIn), startStopped.FlowIn(Animation_StartNode.IdFlowIn));

                var stopCalled = Get(context, stoppedVar);
                _stoppedSkippedCheckBox.SetupNegateCheck(out var skippedFlow);
                _stoppedStillProcessedCheckBox.SetupCheck(stopCalled, out var stillProcessedFlow, true);
                _stoppedProcessedFirstCheckBox.SetupCheck(stopCalled, out var processedFirstFlow, false);
                context.AddSequence(startStopped.FlowOut(Animation_StartNode.IdFlowDone), skippedFlow, stillProcessedFlow, processedFirstFlow);
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
