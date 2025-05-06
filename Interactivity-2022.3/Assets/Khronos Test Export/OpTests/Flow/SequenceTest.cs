using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class SequenceTest : ITestCase
    {
        private CheckBox _sequenceOrderCheck;
        private CheckBox _sequenceOrderCheck2;
        
        public string GetTestName()
        {
            return "flow/sequence";
        }

        public string GetTestDescription()
        {
            return "Tests the sequence order of the flow outputs.";
        }

        public void PrepareObjects(TestContext context)
        {
            _sequenceOrderCheck = context.AddCheckBox("Sequence Order (0,9,10) > (0,10,9)");
            _sequenceOrderCheck2 = context.AddCheckBox("Sequence Order (ccc,aaa,b) > (aaa,b,ccc)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var sequenceNode = nodeCreator.CreateNode(new Flow_SequenceNode());
            context.SetEntryPoint(sequenceNode.FlowIn(Flow_SequenceNode.IdFlowIn), _sequenceOrderCheck.GetText());

            _sequenceOrderCheck.SetupOrderFlowCheck(context, new[]
            {
                sequenceNode.FlowOut("0"),
                sequenceNode.FlowOut("10"),
                sequenceNode.FlowOut("9"),
            });         

            
            var sequenceNode2 = nodeCreator.CreateNode(new Flow_SequenceNode());
            context.SetEntryPoint(sequenceNode2.FlowIn(Flow_SequenceNode.IdFlowIn), _sequenceOrderCheck2.GetText());

            _sequenceOrderCheck2.SetupOrderFlowCheck(context, new[]
            {
                sequenceNode2.FlowOut("aaa"),
                sequenceNode2.FlowOut("b"),
                sequenceNode2.FlowOut("ccc"),
            });         

        }
    }
}