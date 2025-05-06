using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class DoNTest : ITestCase
    {
        private CheckBox _bodyFlowCheck;
        private CheckBox _bodyIterationCheck;
        private CheckBox _currentCountCheck;
        private CheckBox _resetCheck;
        private CheckBox _limitCheck;
        
        public string GetTestName()
        {
            return "flow/doN";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _bodyFlowCheck = context.AddCheckBox("Out flow");
            _bodyIterationCheck = context.AddCheckBox("Out iteration (5)");
            _currentCountCheck = context.AddCheckBox("Current count");
            _resetCheck = context.AddCheckBox("Reset flow (N = 2, out/out/out/reset/out/out)");
            _limitCheck = context.AddCheckBox("Max Iteration flow");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var doNNode = nodeCreator.CreateNode(new Flow_DoNNode());
            
                       
            context.SetEntryPoint(out var entry, "Do N - Iterations");
            context.AddSequence(entry,
                new FlowInRef[]
                {
                    doNNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNNode.FlowIn(Flow_DoNNode.IdFlowIn),
                });
            
            
            doNNode.ValueIn(Flow_DoNNode.IdN).SetValue(5);
            
            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            
            _bodyFlowCheck.SetupCheck(context, out var bodyCheckFlowIn);
            
            var conditionNode = nodeCreator.CreateNode(new Math_EqNode());
            conditionNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(counter);
            conditionNode.ValueIn(Math_EqNode.IdValueB).SetValue(5);
            var branchNode = nodeCreator.CreateNode(new Flow_BranchNode());
            branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(conditionNode.FirstValueOut());
                 
            _bodyIterationCheck.SetupCheck(context, counter, out var bodyIterationCheckFlowIn, 5);
            _currentCountCheck.SetupCheck(context, doNNode.ValueOut(Flow_DoNNode.IdCurrentExecutionCount), out var currentCountCheckFlowIn, 5);

            context.AddSequence(branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue),
                new FlowInRef[]
                {
                    bodyIterationCheckFlowIn,
                    currentCountCheckFlowIn
                });
            
            context.AddSequence(doNNode.FlowOut(Flow_DoNNode.IdOut),
                new FlowInRef[]
                {
                    flowInToIncrease,
                    bodyCheckFlowIn,
                    branchNode.FlowIn(Flow_BranchNode.IdFlowIn),
                });
            

            var doN2Node = nodeCreator.CreateNode(new Flow_DoNNode());
            doN2Node.ValueIn(Flow_DoNNode.IdN).SetValue(2);
            context.SetEntryPoint(out var entryReset, "Do N - Reset");
            
            context.AddPlusOneCounter(out var counter2, out var flowInToIncrease2);
            doN2Node.FlowOut(Flow_DoNNode.IdOut).ConnectToFlowDestination(flowInToIncrease2);
            _resetCheck.SetupCheck(context, counter2, out var checkCountFlow, 4);

            context.AddSequence(entryReset,
                new FlowInRef[]
                {
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowReset),
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN2Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    checkCountFlow
                });
            
            var doN3Node = nodeCreator.CreateNode(new Flow_DoNNode());
            doN3Node.ValueIn(Flow_DoNNode.IdN).SetValue(2);
            context.SetEntryPoint(out var entry2, "Do N - Max Iteration");

            context.AddPlusOneCounter(out var counter3, out var flowInToIncrease3);
            doN3Node.FlowOut(Flow_DoNNode.IdOut).ConnectToFlowDestination(flowInToIncrease3);
            _limitCheck.SetupCheck(context, counter3, out var checkCountFlow2, 2);
            context.AddSequence(entry2,
                new FlowInRef[]
                {
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    checkCountFlow2
                });
        }
    }
}