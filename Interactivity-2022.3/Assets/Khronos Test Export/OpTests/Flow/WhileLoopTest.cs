using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class WhileLoopTest : ITestCase
    {
        private CheckBox _bodyFlowCheck;
        private CheckBox _completedFlowCheck;
        private CheckBox _bodyIterationCheck;
        
        private CheckBox _bodyFlowChechWhenFalse;
        private CheckBox _completedFlowCheckWhenFalse;

        public string GetTestName()
        {
            return "flow/while";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _bodyFlowCheck = context.AddCheckBox("[body] flow");
            _completedFlowCheck = context.AddCheckBox("[completed] flow");
            _bodyIterationCheck = context.AddCheckBox("[body] iteration (2)");
            
            _bodyFlowChechWhenFalse = context.AddCheckBox("[body] flow when false");
            _bodyFlowChechWhenFalse.Negate();
            _completedFlowCheckWhenFalse = context.AddCheckBox("[completed] flow when false");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var whileLoop = nodeCreator.CreateNode<Flow_WhileNode>();
            
            context.NewEntryPoint(whileLoop.FlowIn(Flow_WhileNode.IdFlowIn), "While Loop flowIn");
            
            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            _bodyFlowCheck.SetupCheck(out var bodyCheckFlowIn);
            
            var conditionNode = nodeCreator.CreateNode<Math_LtNode>();
            whileLoop.ValueIn(Flow_WhileNode.IdCondition).ConnectToSource(conditionNode.FirstValueOut());
            conditionNode.ValueIn(Math_LtNode.IdValueA).ConnectToSource(counter);
            conditionNode.ValueIn(Math_LtNode.IdValueB).SetValue(2);
            
            context.AddSequence(whileLoop.FlowOut(Flow_WhileNode.IdLoopBody),
                new FlowInRef[]
                {
                    bodyCheckFlowIn,
                    flowInToIncrease,
                });
            
            
            _bodyIterationCheck.SetupCheck(counter, out var bodyIterationCheckFlowIn, 2);
            
            _completedFlowCheck.SetupCheck(out var completedFlowCheckFlowIn);
            
            context.AddSequence(whileLoop.FlowOut(Flow_WhileNode.IdCompleted),
                new FlowInRef[]
                {
                    completedFlowCheckFlowIn,
                    bodyIterationCheckFlowIn,
                });
            
            var whileLoop2 = nodeCreator.CreateNode<Flow_WhileNode>();
            context.NewEntryPoint(whileLoop2.FlowIn(Flow_WhileNode.IdFlowIn), "While Loop flowIn (false Condition)");
            whileLoop2.ValueIn(Flow_WhileNode.IdCondition).SetValue(false);
            
            _bodyFlowChechWhenFalse.SetupNegateCheck(whileLoop2.FlowOut(Flow_WhileNode.IdLoopBody));
            _completedFlowCheckWhenFalse.SetupCheck(whileLoop2.FlowOut(Flow_WhileNode.IdCompleted));
            
        }
    }
}

