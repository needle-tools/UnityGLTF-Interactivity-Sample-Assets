using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class BranchTest : ITestCase
    {
        private CheckBox _condTrueFlowTrueCheck;
        private CheckBox _condFalseFlowFalseCheck;
        private CheckBox _condFalseFlowTrueCheck;
        private CheckBox _condTrueFlowFalseCheck;
        
        public string GetTestName()
        {
            return "flow/branch";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _condTrueFlowTrueCheck = context.AddCheckBox("True-Condition true-flow");
            _condTrueFlowFalseCheck = context.AddCheckBox("True-Condition false-flow");
            _condTrueFlowFalseCheck.Negate();
            
            _condFalseFlowTrueCheck = context.AddCheckBox("False-Condition true-flow");
            _condFalseFlowTrueCheck.Negate();
            _condFalseFlowFalseCheck = context.AddCheckBox("False-Condition false-flow");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var branchTrueNode = nodeCreator.CreateNode(new Flow_BranchNode());
            context.NewEntryPoint(branchTrueNode.FlowIn(Flow_BranchNode.IdFlowIn), "Branch Condition True");
            branchTrueNode.ValueIn(Flow_BranchNode.IdCondition).SetValue(true);
            _condTrueFlowTrueCheck.SetupCheck(context, branchTrueNode.FlowOut(Flow_BranchNode.IdFlowOutTrue));
            _condTrueFlowFalseCheck.SetupNegateCheck(context, branchTrueNode.FlowOut(Flow_BranchNode.IdFlowOutFalse));
            
            var branchFalseNode = nodeCreator.CreateNode(new Flow_BranchNode());
            context.NewEntryPoint(branchFalseNode.FlowIn(Flow_BranchNode.IdFlowIn), "Branch Condition False");
            branchFalseNode.ValueIn(Flow_BranchNode.IdCondition).SetValue(false);
            _condFalseFlowTrueCheck.SetupNegateCheck(context, branchFalseNode.FlowOut(Flow_BranchNode.IdFlowOutTrue));
            _condFalseFlowFalseCheck.SetupCheck(context, branchFalseNode.FlowOut(Flow_BranchNode.IdFlowOutFalse));

        }
    }
}