using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export.ExtraTests
{
    public class LoopInLoopTests : ITestCase
    {
        private CheckBox _loopInLoopCheckBox;
        private CheckBox _loopInLoopCheckBox2;
        
        public string GetTestName()
        {
            return "Extras/Loop in Loop Tests";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _loopInLoopCheckBox = context.AddCheckBox("For-Loop in While-Loop-Body (Complete Count equal)");
            _loopInLoopCheckBox2 = context.AddCheckBox("For-Loop in While-Loop-Body (For-Body count)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var whileLoopNode = nodeCreator.CreateNode<Flow_WhileNode>();
            context.AddPlusOneCounter(out var whileLoopBodyCount, out var whileLoopCompleteCounterFLowIn);
            var lt = nodeCreator.CreateNode<Math_LtNode>();
            lt.ValueIn("a").ConnectToSource(whileLoopBodyCount);
            lt.ValueIn("b").SetValue(3);
            whileLoopNode.ValueIn(Flow_WhileNode.IdCondition).ConnectToSource(lt.FirstValueOut());
            
            var forLoopNode = nodeCreator.CreateNode<Flow_ForLoopNode>();
            forLoopNode.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            forLoopNode.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(5);
            
            context.AddSequence(whileLoopNode.FlowOut(Flow_WhileNode.IdLoopBody), whileLoopCompleteCounterFLowIn, forLoopNode.FlowIn());
            
            context.AddPlusOneCounter(out var forLoopCompleteCount, out var forLoopCompleteCounterFLowIn);
            context.AddPlusOneCounter(out var forLoopBodyCount, out var forLoopBodyCounterFLowIn);
            forLoopNode.FlowOut(Flow_ForLoopNode.IdCompleted).ConnectToFlowDestination(forLoopCompleteCounterFLowIn);
            forLoopNode.FlowOut(Flow_ForLoopNode.IdLoopBody).ConnectToFlowDestination(forLoopBodyCounterFLowIn);
            
            context.NewEntryPoint(whileLoopNode.FlowIn(), "Loop in Loop" );
            
            var eq = nodeCreator.CreateNode<Math_EqNode>();
            eq.ValueIn("a").ConnectToSource(forLoopCompleteCount);
            eq.ValueIn("b").ConnectToSource(whileLoopBodyCount);
            
            _loopInLoopCheckBox.SetupCheck(eq.FirstValueOut(), out var flowInCheck1, true);
            _loopInLoopCheckBox2.SetupCheck(forLoopBodyCount, out var flowInCheck2, 3 * 5);
            context.AddToCurrentEntrySequence(flowInCheck1, flowInCheck2);
        }
    }
}