using System;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: animation/start runs its steps "on each asset animation update", which the spec doesn't define,
    /// also not its order relative to event/onTick within a frame.
    ///
    /// A one-shot animation/start (startTime == endTime == 0.5T) applies its pose on the next update. The first
    /// onTick after the start reads the translation:
    ///  A: the update runs before onTick, the tick sees the new pose (Y == 0.5T)
    ///  B: the update runs after onTick, the tick still sees the old pose (Y == 0)
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationUpdateOrderTest : ITestCase, IDisposable
    {
        private AnimatedTestObject _target;
        private CheckBox _updateBeforeTickCheckBox;
        private CheckBox _updateAfterTickCheckBox;

        public string GetTestName() => "animation/update order vs onTick";

        public string GetTestDescription() =>
            "The \"asset animation update\" is not defined, also not its order relative to event/onTick. " +
            "A one-shot animation/start (startTime == endTime) applies its pose on the next update; the first onTick after the start reads the translation. " +
            "A: update before onTick (the tick sees the new pose). B: update after onTick (the tick sees the old pose).";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedUpdateOrderTarget", TargetPosition, T);
            _updateBeforeTickCheckBox = context.AddCheckBox("A: update before onTick\n(1st tick sees new pose)", true);
            _updateAfterTickCheckBox = context.AddCheckBox("B: update after onTick\n(1st tick sees old pose)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var armedVar = AddVariable(context, "UndefinedUpdateOrder_armed", false, GltfTypes.Bool);

            var start = AnimationTestHelper.CreateStart(nodeCreator, _target.AnimationIndex(context), 0.5f * T, 0.5f * T);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, armedVar, true, out var armFlow, out _);
            start.FlowOut(Animation_StartNode.IdFlowOut).ConnectToFlowDestination(armFlow);
            context.NewEntryPoint(start.FlowIn(Animation_StartNode.IdFlowIn), _updateBeforeTickCheckBox.GetText(), 1f);

            // First onTick after the start: disarm, then read the pose
            var onTick = nodeCreator.CreateNode<Event_OnTickNode>();
            var branch = nodeCreator.CreateNode<Flow_BranchNode>();
            branch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(Get(context, armedVar));
            onTick.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(branch.FlowIn(Flow_BranchNode.IdFlowIn));
            VariablesHelpers.SetVariableStaticValue(nodeCreator, armedVar, false, out var disarmFlow, out var disarmedFlow);
            branch.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(disarmFlow);

            var y = AnimationTestHelper.CreateTranslationY(nodeCreator, _target.NodeIndex(context));
            _updateBeforeTickCheckBox.proximityCheckDistance = 0.01f;
            _updateBeforeTickCheckBox.SetupCheck(y, out var beforeFlow, 0.5f * TargetPosition.y, true);
            _updateAfterTickCheckBox.proximityCheckDistance = 0.01f;
            _updateAfterTickCheckBox.SetupCheck(y, out var afterFlow, 0f, true);
            context.AddSequence(disarmedFlow, beforeFlow, afterFlow);
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
