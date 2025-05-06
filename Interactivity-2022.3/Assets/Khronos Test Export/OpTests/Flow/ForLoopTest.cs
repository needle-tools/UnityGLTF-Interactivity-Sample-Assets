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
            _bodyCheck = context.AddCheckBox("Body flow");
            _loopRangeCheck = context.AddCheckBox("Loop range (0..10)");
            _completeCheck = context.AddCheckBox("Completed flow");
            _initialIndexCheck = context.AddCheckBox("Initial index");
            _completedIndexCheck = context.AddCheckBox("Index when completed");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var forLoop = nodeCreator.CreateNode(new Flow_ForLoopNode());
            context.NewEntryPoint(out var entryFlow, "Loop Entry");

            forLoop.Configuration[Flow_ForLoopNode.IdConfigInitialIndex].Value = 1;
            forLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            forLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(10);
            
            _initialIndexCheck.SetupCheck(context, forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var initialIndexCheckFlow, 1);
            context.AddSequence(entryFlow,
                new FlowInRef[]
                {
                    initialIndexCheckFlow,
                    forLoop.FlowIn(Flow_ForLoopNode.IdFlowIn),
                });
            
            _bodyCheck.SetupCheck(context, out var bodyCheckFlowIn);
            
            context.AddPlusOneCounter(out var loopRangeCounter, out var flowInToIncrease);
            _loopRangeCheck.SetupCheck(context, loopRangeCounter, out var loopRangeCheckFlowIn, 10);

            _completeCheck.SetupCheck(context, out var completeCheckFlowIn);
            
            _completedIndexCheck.SetupCheck(context, forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var completedIndexCheckFlowIn, 10);
            
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