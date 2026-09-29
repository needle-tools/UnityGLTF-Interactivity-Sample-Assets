using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class MultiGateTest : ITestCase
    {
        private CheckBox _loopCheckBox;
        private CheckBox _randomCheckBox;
        private CheckBox _orderCheckBox;
        private CheckBox _resetCheckBox;
        private CheckBox _invalidConfigCheckBox;
        private CheckBox _noLoopExhaustedCheckBox;
        private CheckBox _lastIndexInitialCheckBox;
        private CheckBox _lastIndexAfterTwoCheckBox;
        private CheckBox _lastIndexExhaustedCheckBox;
        private CheckBox _lastIndexResetCheckBox;

        public string GetTestName()
        {
            return "flow/multiGate";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _loopCheckBox = context.AddCheckBox("Loop");
            _randomCheckBox = context.AddCheckBox("Random (Check if all out flows are triggered once)");
            _orderCheckBox = context.AddCheckBox("Order (008, 004, 001) > (001, 004, 008)");
            _resetCheckBox = context.AddCheckBox("Reset Loop");
            _invalidConfigCheckBox = context.AddCheckBox("isRandom \"yes\" uses default configuration (in order)");
            context.NewRow();
            _noLoopExhaustedCheckBox = context.AddCheckBox("No loop: 4th [in] fires nothing (3x total)");
            _lastIndexInitialCheckBox = context.AddCheckBox("[lastIndex] -1 before activation");
            _lastIndexAfterTwoCheckBox = context.AddCheckBox("[lastIndex] 1 after two activations");
            _lastIndexExhaustedCheckBox = context.AddCheckBox("[lastIndex] stays 2 when all outputs are used");
            _lastIndexResetCheckBox = context.AddCheckBox("[lastIndex] -1 after [reset]");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            // Order Tests
            context.NewEntryPoint(_orderCheckBox.GetText());
            var multiGateNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            multiGateNode.FlowOut("008");
            multiGateNode.FlowOut("004");
            multiGateNode.FlowOut("001");
            _orderCheckBox.SetupOrderFlowCheck(new FlowOutRef[]
            {
                multiGateNode.FlowOut("001"),
                multiGateNode.FlowOut("004"),
                multiGateNode.FlowOut("008"),
            });
            
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    multiGateNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                });
            
            
            // Invalid configuration: isRandom is not a boolean literal, so the default configuration
            // (isRandom and isLoop false) is used for both properties and the outputs are activated in order.
            context.NewEntryPoint(_invalidConfigCheckBox.GetText());
            var multiGateInvalidConfigNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            multiGateInvalidConfigNode.Configuration[Flow_MultiGateNode.IdConfigIsRandom].Value = "yes";
            multiGateInvalidConfigNode.Configuration[Flow_MultiGateNode.IdConfigIsLoop].Value = false;
            _invalidConfigCheckBox.SetupOrderFlowCheck(new FlowOutRef[]
            {
                multiGateInvalidConfigNode.FlowOut("001"),
                multiGateInvalidConfigNode.FlowOut("002"),
                multiGateInvalidConfigNode.FlowOut("003"),
            });

            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    multiGateInvalidConfigNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateInvalidConfigNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateInvalidConfigNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                });

            // Random Tests
            var multiGateRandomNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            context.NewEntryPoint(_randomCheckBox.GetText());
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    multiGateRandomNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateRandomNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateRandomNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateRandomNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                });
            
            multiGateRandomNode.Configuration[Flow_MultiGateNode.IdConfigIsRandom].Value = true;
            _randomCheckBox.SetupMultiFlowCheck(4, out var randomCheckFlowIn);
            multiGateRandomNode.FlowOut("001").ConnectToFlowDestination(randomCheckFlowIn[0]);
            multiGateRandomNode.FlowOut("002").ConnectToFlowDestination(randomCheckFlowIn[1]);
            multiGateRandomNode.FlowOut("003").ConnectToFlowDestination(randomCheckFlowIn[2]);
            multiGateRandomNode.FlowOut("004").ConnectToFlowDestination(randomCheckFlowIn[3]);
            
            
            // Loop Tests
            var multiGateLoopNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            context.NewEntryPoint(_loopCheckBox.GetText());
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    multiGateLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                });
            
            multiGateLoopNode.Configuration[Flow_MultiGateNode.IdConfigIsLoop].Value = true;
            _loopCheckBox.SetupMultiFlowCheck(4, out var loopCheckFlowIn, new string[] {"Flow0", "Flow1", "Flow2", "Flow0 (2.)"});
            
            context.AddPlusOneCounter(out var loopCounter, out var flowInToIncrease);
            var branchConditionSecondFlow = nodeCreator.CreateNode<Math_EqNode>();
            branchConditionSecondFlow.ValueIn(Math_EqNode.IdValueA).SetValue(2);
            branchConditionSecondFlow.ValueIn(Math_EqNode.IdValueB).ConnectToSource(loopCounter);
            
            var branchSecondFlow = nodeCreator.CreateNode<Flow_BranchNode>();
            branchSecondFlow.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(branchConditionSecondFlow.FirstValueOut());
            branchSecondFlow.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(loopCheckFlowIn[0]);
            branchSecondFlow.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(loopCheckFlowIn[3]);
            
            context.AddSequence(multiGateLoopNode.FlowOut("001"),
                new FlowInRef[]
                {
                    flowInToIncrease,
                    branchSecondFlow.FlowIn(Flow_BranchNode.IdFlowIn)
                });
            
            multiGateLoopNode.FlowOut("002").ConnectToFlowDestination(loopCheckFlowIn[1]);
            multiGateLoopNode.FlowOut("003").ConnectToFlowDestination(loopCheckFlowIn[2]);
            
            // Reset Loop Tests
            var multiGateResetLoopNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            context.NewEntryPoint(_loopCheckBox.GetText());
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    multiGateResetLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateResetLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateResetLoopNode.FlowIn(Flow_MultiGateNode.IdFlowInReset),
                    multiGateResetLoopNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                });

            multiGateResetLoopNode.Configuration[Flow_MultiGateNode.IdConfigIsLoop].Value = true;
            _resetCheckBox.SetupMultiFlowCheck(3, out var loopResetCheckFlowIn);
            
            context.AddPlusOneCounter(out var loopCounterReset, out var flowInToIncreaseReset);
            var branchConditionSecondFlowReset = nodeCreator.CreateNode<Math_EqNode>();
            branchConditionSecondFlowReset.ValueIn(Math_EqNode.IdValueA).SetValue(2);
            branchConditionSecondFlowReset.ValueIn(Math_EqNode.IdValueB).ConnectToSource(loopCounterReset);
            
            var branchSecondFlowReset = nodeCreator.CreateNode<Flow_BranchNode>();
            branchSecondFlowReset.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(branchConditionSecondFlowReset.FirstValueOut());
            branchSecondFlowReset.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(loopResetCheckFlowIn[0]);
            branchSecondFlowReset.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(loopResetCheckFlowIn[2]);
            
            context.AddSequence(multiGateResetLoopNode.FlowOut("001"),
                new FlowInRef[]
                {
                    flowInToIncreaseReset,
                    branchSecondFlowReset.FlowIn(Flow_BranchNode.IdFlowIn)
                });
            
            multiGateResetLoopNode.FlowOut("002").ConnectToFlowDestination(loopResetCheckFlowIn[1]);
            // Add dummy connection to 003
            context.AddSequence(multiGateResetLoopNode.FlowOut("003"), new FlowInRef[]
            {
            });

            // No loop + lastIndex: three outputs, four activations. The 4th activation finds no unused
            // output (i = -1) and, without isLoop, activates nothing and leaves lastIndex unchanged.
            var multiGateLastIndexNode = nodeCreator.CreateNode<Flow_MultiGateNode>();
            multiGateLastIndexNode.Configuration[Flow_MultiGateNode.IdConfigIsRandom].Value = false;
            multiGateLastIndexNode.Configuration[Flow_MultiGateNode.IdConfigIsLoop].Value = false;
            context.NewEntryPoint(_noLoopExhaustedCheckBox.GetText());

            var lastIndexOut = multiGateLastIndexNode.ValueOut(Flow_MultiGateNode.IdLastIndex);
            _lastIndexInitialCheckBox.SetupCheck(lastIndexOut, out var lastIndexInitialFlow, -1, false);
            _lastIndexAfterTwoCheckBox.SetupCheck(lastIndexOut, out var lastIndexAfterTwoFlow, 1, false);
            _lastIndexExhaustedCheckBox.SetupCheck(lastIndexOut, out var lastIndexExhaustedFlow, 2, false);
            _lastIndexResetCheckBox.SetupCheck(lastIndexOut, out var lastIndexResetFlow, -1, false);

            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    lastIndexInitialFlow,
                    multiGateLastIndexNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateLastIndexNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    lastIndexAfterTwoFlow,
                    multiGateLastIndexNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    multiGateLastIndexNode.FlowIn(Flow_MultiGateNode.IdFlowIn),
                    lastIndexExhaustedFlow,
                    multiGateLastIndexNode.FlowIn(Flow_MultiGateNode.IdFlowInReset),
                    lastIndexResetFlow,
                });

            _noLoopExhaustedCheckBox.SetupCheckFlowTimes(out var noLoopCountFlow, 3);
            multiGateLastIndexNode.FlowOut("0").ConnectToFlowDestination(noLoopCountFlow);
            multiGateLastIndexNode.FlowOut("1").ConnectToFlowDestination(noLoopCountFlow);
            multiGateLastIndexNode.FlowOut("2").ConnectToFlowDestination(noLoopCountFlow);
        }
    }
}