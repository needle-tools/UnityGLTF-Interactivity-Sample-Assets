using UnityGLTF.Interactivity;
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
        private CheckBox _zeroNCheck;
        private CheckBox _raisedNCheck;

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
            _bodyFlowCheck = context.AddCheckBox("[out] flow");
            _bodyIterationCheck = context.AddCheckBox("[out] iteration (5)");
            _currentCountCheck = context.AddCheckBox("[currentCount]");
            _resetCheck = context.AddCheckBox("[reset] flow (N = 2, out/out/out/reset/out/out)");
            _limitCheck = context.AddCheckBox("Max Iteration flow");
            context.NewRow();
            _zeroNCheck = context.AddCheckBox("N = 0: [out] never fires");
            _zeroNCheck.Negate();
            _raisedNCheck = context.AddCheckBox("[n] re-evaluated (raised 1 -> 3 at runtime, 3x)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var doNNode = nodeCreator.CreateNode<Flow_DoNNode>();
            
                       
            context.NewEntryPoint("Do N - Iterations");
            context.AddToCurrentEntrySequence(
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
            
            _bodyFlowCheck.SetupCheck(out var bodyCheckFlowIn);
            
            var conditionNode = nodeCreator.CreateNode<Math_EqNode>();
            conditionNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(counter);
            conditionNode.ValueIn(Math_EqNode.IdValueB).SetValue(5);
            var branchNode = nodeCreator.CreateNode<Flow_BranchNode>();
            branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(conditionNode.FirstValueOut());
                 
            _bodyIterationCheck.SetupCheck(counter, out var bodyIterationCheckFlowIn, 5);
            _currentCountCheck.SetupCheck(doNNode.ValueOut(Flow_DoNNode.IdCurrentExecutionCount), out var currentCountCheckFlowIn, 5);

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
            

            var doN2Node = nodeCreator.CreateNode<Flow_DoNNode>();
            doN2Node.ValueIn(Flow_DoNNode.IdN).SetValue(2);
            context.NewEntryPoint("Do N - Reset");
            
            context.AddPlusOneCounter(out var counter2, out var flowInToIncrease2);
            doN2Node.FlowOut(Flow_DoNNode.IdOut).ConnectToFlowDestination(flowInToIncrease2);
            _resetCheck.SetupCheck(counter2, out var checkCountFlow, 4);

            context.AddToCurrentEntrySequence(
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
            
            var doN3Node = nodeCreator.CreateNode<Flow_DoNNode>();
            doN3Node.ValueIn(Flow_DoNNode.IdN).SetValue(2);
            context.NewEntryPoint("Do N - Max Iteration");

            context.AddPlusOneCounter(out var counter3, out var flowInToIncrease3);
            doN3Node.FlowOut(Flow_DoNNode.IdOut).ConnectToFlowDestination(flowInToIncrease3);
            _limitCheck.SetupCheck(counter3, out var checkCountFlow2, 2);
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    doN3Node.FlowIn(Flow_DoNNode.IdFlowIn),
                    checkCountFlow2
                });

            // N = 0: currentCount (0) is never less than n, so [out] must never fire
            var doNZeroNode = nodeCreator.CreateNode<Flow_DoNNode>();
            doNZeroNode.ValueIn(Flow_DoNNode.IdN).SetValue(0);
            context.NewEntryPoint(_zeroNCheck.GetText());
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    doNZeroNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNZeroNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNZeroNode.FlowIn(Flow_DoNNode.IdFlowIn),
                });
            _zeroNCheck.SetupNegateCheck(doNZeroNode.FlowOut(Flow_DoNNode.IdOut));

            // n is evaluated on every activation: raising it from 1 to 3 re-opens the gate.
            // in (out #1), in (blocked), n = 3, in (out #2), in (out #3), in (blocked)
            var nVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("DoN_n_" + System.Guid.NewGuid(), 1, GltfTypes.Int);
            VariablesHelpers.GetVariable(nodeCreator, nVarId, out var nVarRef);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, nVarId, 3, out var setNFlowIn, out _);

            var doNRaisedNode = nodeCreator.CreateNode<Flow_DoNNode>();
            doNRaisedNode.ValueIn(Flow_DoNNode.IdN).ConnectToSource(nVarRef);
            context.NewEntryPoint(_raisedNCheck.GetText());
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    doNRaisedNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNRaisedNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    setNFlowIn,
                    doNRaisedNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNRaisedNode.FlowIn(Flow_DoNNode.IdFlowIn),
                    doNRaisedNode.FlowIn(Flow_DoNNode.IdFlowIn),
                });
            _raisedNCheck.SetupCheckFlowTimes(out var raisedNCheckFlow, 3);
            doNRaisedNode.FlowOut(Flow_DoNNode.IdOut).ConnectToFlowDestination(raisedNCheckFlow);
        }
    }
}