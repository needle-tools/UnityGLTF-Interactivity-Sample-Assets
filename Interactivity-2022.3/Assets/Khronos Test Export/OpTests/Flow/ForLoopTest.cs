using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ForLoopTest : ITestCase
    {
        private CheckBox _bodyCheck;
        private CheckBox _loopRangeCheck;
        private CheckBox _completeCheck;
        private CheckBox _initialIndexCheck;
        private CheckBox _completedIndexCheck;

        public string GetTestName()
        {
            return "flow/for";
        }

        public string GetTestDescription()
        {
            return "";
        }
        
        public void PrepareObjects(TestContext context)
        {
            _bodyCheck = context.AddCheckBox("[body] flow");
            _loopRangeCheck = context.AddCheckBox("Loop range (0..10)");
            _completeCheck = context.AddCheckBox("[completed] flow");
            _initialIndexCheck = context.AddCheckBox("Initial index");
            _completedIndexCheck = context.AddCheckBox("[index] when completed");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var forLoop = nodeCreator.CreateNode(new Flow_ForLoopNode());
            context.NewEntryPoint("Loop Entry");

            forLoop.Configuration[Flow_ForLoopNode.IdConfigInitialIndex].Value = 1;
            forLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            forLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(10);
            
            _initialIndexCheck.SetupCheck(forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var initialIndexCheckFlow, 1);
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    initialIndexCheckFlow,
                    forLoop.FlowIn(Flow_ForLoopNode.IdFlowIn),
                });
            
            _bodyCheck.SetupCheck(out var bodyCheckFlowIn);
            
            context.AddPlusOneCounter(out var loopRangeCounter, out var flowInToIncrease);
            _loopRangeCheck.SetupCheck(loopRangeCounter, out var loopRangeCheckFlowIn, 10);

            _completeCheck.SetupCheck(out var completeCheckFlowIn);
            
            _completedIndexCheck.SetupCheck(forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var completedIndexCheckFlowIn, 10);
            
            context.AddSequence( forLoop.FlowOut(Flow_ForLoopNode.IdCompleted),
                new FlowInRef[]
                {
                    completedIndexCheckFlowIn,
                    loopRangeCheckFlowIn,
                    completeCheckFlowIn
                });
            
            context.AddSequence(forLoop.FlowOut(Flow_ForLoopNode.IdLoopBody), new FlowInRef[]
            {
                bodyCheckFlowIn,
                flowInToIncrease
            });
        }
    }
}