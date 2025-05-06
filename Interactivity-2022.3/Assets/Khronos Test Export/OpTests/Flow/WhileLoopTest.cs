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
            _bodyFlowCheck = context.AddCheckBox("Body flow");
            _completedFlowCheck = context.AddCheckBox("Completed flow");
            _bodyIterationCheck = context.AddCheckBox("Body iteration (2)");
            
            _bodyFlowChechWhenFalse = context.AddCheckBox("Body flow when false");
            _bodyFlowChechWhenFalse.Negate();
            _completedFlowCheckWhenFalse = context.AddCheckBox("Completed flow when false");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var whileLoop = nodeCreator.CreateNode(new Flow_WhileNode());
            
            context.SetEntryPoint(whileLoop.FlowIn(Flow_WhileNode.IdFlowIn), "While Loop flowIn");
            
            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            _bodyFlowCheck.SetupCheck(context, out var bodyCheckFlowIn);
            
            var conditionNode = nodeCreator.CreateNode(new Math_LtNode());
            whileLoop.ValueIn(Flow_WhileNode.IdCondition).ConnectToSource(conditionNode.FirstValueOut());
            conditionNode.ValueIn(Math_LtNode.IdValueA).ConnectToSource(counter);
            conditionNode.ValueIn(Math_LtNode.IdValueB).SetValue(2);
            
            context.AddSequence(whileLoop.FlowOut(Flow_WhileNode.IdLoopBody),
                new FlowInRef[]
                {
                    bodyCheckFlowIn,
                    flowInToIncrease,
                });
            
            
            _bodyIterationCheck.SetupCheck(context, counter, out var bodyIterationCheckFlowIn, 2);
            
            _completedFlowCheck.SetupCheck(context, out var completedFlowCheckFlowIn);
            
            context.AddSequence(whileLoop.FlowOut(Flow_WhileNode.IdCompleted),
                new FlowInRef[]
                {
                    completedFlowCheckFlowIn,
                    bodyIterationCheckFlowIn,
                });
            
            var whileLoop2 = nodeCreator.CreateNode(new Flow_WhileNode());
            context.SetEntryPoint(whileLoop2.FlowIn(Flow_WhileNode.IdFlowIn), "While Loop flowIn (false Condition)");
            whileLoop2.ValueIn(Flow_WhileNode.IdCondition).SetValue(false);
            
            _bodyFlowChechWhenFalse.SetupNegateCheck(context, whileLoop2.FlowOut(Flow_WhileNode.IdLoopBody));
            _completedFlowCheckWhenFalse.SetupCheck(context, whileLoop2.FlowOut(Flow_WhileNode.IdCompleted));
            
        }
    }
}

