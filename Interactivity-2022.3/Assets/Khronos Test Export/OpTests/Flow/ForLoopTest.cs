using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ForLoopTest : ITestCase
    {
        private CheckBox bodyCheck;
        private CheckBox loopRangeCheck;
        private CheckBox completeCheck;

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
            bodyCheck = context.AddCheckBox("Body flow");
            loopRangeCheck = context.AddCheckBox("Loop range (0..10)");
            completeCheck = context.AddCheckBox("Completed flow");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var forLoop = nodeCreator.CreateNode(new Flow_ForLoopNode());
            context.SetEntryPoint(forLoop.FlowIn(Flow_ForLoopNode.IdFlowIn), "Loop Entry");

            forLoop.Configuration[Flow_ForLoopNode.IdConfigInitialIndex].Value = 0;
            forLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            forLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(10);
            
            bodyCheck.SetupCheck(context, out var bodyCheckFlowIn);
            
            context.AddPlusOneCounter(out var loopRangeCounter, out var flowInToIncrease);
            loopRangeCheck.SetupCheck(context, loopRangeCounter, out var loopRangeCheckFlowIn, 10);

            completeCheck.SetupCheck(context, out var completeCheckFlowIn);
            context.AddSequence( forLoop.FlowOut(Flow_ForLoopNode.IdCompleted),
                new FlowInRef[]
                {
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