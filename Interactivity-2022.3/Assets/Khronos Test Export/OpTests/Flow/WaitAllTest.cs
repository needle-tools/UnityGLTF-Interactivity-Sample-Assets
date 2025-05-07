using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class WaitAllTest : ITestCase
    {
        private CheckBox _completedCheckBox;
        private CheckBox _remainingInputCheckBox;
        private CheckBox _resetCheckBox;
        private CheckBox _resetCompletedCheckBox;
        
        public string GetTestName()
        {
            return "flow/waitAll";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _completedCheckBox = context.AddCheckBox("Completed");
            _remainingInputCheckBox = context.AddCheckBox("Remaining Input");
            _resetCheckBox = context.AddCheckBox("Reset");
            _resetCompletedCheckBox = context.AddCheckBox("Reset Completed");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            // Completed
            var waitAllNode = nodeCreator.CreateNode(new Flow_WaitAllNode());
            waitAllNode.Configuration[Flow_WaitAllNode.IdConfigInputFlows].Value = 3;
            
            context.NewEntryPoint(out var entryFlow, "Wait All - Completed");
            context.AddSequence(entryFlow,
                new FlowInRef[]
                {
                    waitAllNode.FlowIn("0"),
                    waitAllNode.FlowIn("1"),
                    waitAllNode.FlowIn("2"),
                });
            
            _completedCheckBox.SetupCheck(context, waitAllNode.FlowOut(Flow_WaitAllNode.IdFlowOutCompleted));
         
            // Remaining
            
            var waitAllNodeRemaining = nodeCreator.CreateNode(new Flow_WaitAllNode());
            waitAllNodeRemaining.Configuration[Flow_WaitAllNode.IdConfigInputFlows].Value = 3;
            
            context.NewEntryPoint(out var entryFlowRemaining, "Wait All - Remaining");

            _remainingInputCheckBox.SetupCheck(context, waitAllNodeRemaining.ValueOut(Flow_WaitAllNode.IdOutRemainingInputs),
                out var remCheckFlow, 2, false);
         
            context.AddSequence(entryFlowRemaining,
                new FlowInRef[]
                {
                    waitAllNodeRemaining.FlowIn("0"),
                    remCheckFlow,
                    waitAllNodeRemaining.FlowIn("1"),
                });
            var dummySequenceR = nodeCreator.CreateNode(new Flow_SequenceNode());
            dummySequenceR.FlowOut("0").ConnectToFlowDestination(waitAllNodeRemaining.FlowIn("2"));
            waitAllNodeRemaining.FlowIn("2");
            
            // Reset
            
            var waitAllNodeReset = nodeCreator.CreateNode(new Flow_WaitAllNode());
            waitAllNodeReset.Configuration[Flow_WaitAllNode.IdConfigInputFlows].Value = 3;
            
            context.NewEntryPoint(out var entryFlowReset, "Wait All - Reset");
            _resetCheckBox.SetupCheck(context, waitAllNodeReset.ValueOut(Flow_WaitAllNode.IdOutRemainingInputs),
               out var flowCheckReset, 2, false);

            context.AddSequence(entryFlowReset,
                new FlowInRef[]
                {
                    waitAllNodeReset.FlowIn("0"),
                    waitAllNodeReset.FlowIn("1"),
                    waitAllNodeReset.FlowIn(Flow_WaitAllNode.IdFlowInReset),
                    waitAllNodeReset.FlowIn("1"),
                    flowCheckReset
                });

            var dummySequence = nodeCreator.CreateNode(new Flow_SequenceNode());
            dummySequence.FlowOut("0").ConnectToFlowDestination(waitAllNodeReset.FlowIn("2"));

            
            // Reset Completed
            
            var waitAllNodeResetCompl = nodeCreator.CreateNode(new Flow_WaitAllNode());
            waitAllNodeResetCompl.Configuration[Flow_WaitAllNode.IdConfigInputFlows].Value = 3;
            
            context.NewEntryPoint(out var entryFlowResetCompl, "Wait All - Reset Completed");
            context.AddSequence(entryFlowResetCompl,
                new FlowInRef[]
                {
                    waitAllNodeResetCompl.FlowIn("0"),
                    waitAllNodeResetCompl.FlowIn("1"),
                    waitAllNodeResetCompl.FlowIn(Flow_WaitAllNode.IdFlowInReset),
                    waitAllNodeResetCompl.FlowIn("1"),
                    waitAllNodeResetCompl.FlowIn("2"),
                    waitAllNodeResetCompl.FlowIn(Flow_WaitAllNode.IdFlowInReset),
                    waitAllNodeResetCompl.FlowIn("1"),
                    waitAllNodeResetCompl.FlowIn("2"),
                    waitAllNodeResetCompl.FlowIn("0"),
                });

            _resetCompletedCheckBox.SetupCheckFlowTimes(context, out var flowInTimes, 1);
            waitAllNodeResetCompl.FlowOut(Flow_WaitAllNode.IdFlowOutCompleted).ConnectToFlowDestination(flowInTimes);

        }
    }
}