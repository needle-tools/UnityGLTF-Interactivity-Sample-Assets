using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class SequenceTest : ITestCase
    {
        private CheckBox _sequenceOrderCheck;
        private CheckBox _sequenceOrderCheck2;
        private CheckBox _sequenceOrderCheck3;
        
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
            _sequenceOrderCheck3 = context.AddCheckBox("Sequence Order (b,B,a,A) > (A,B,a,b)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var sequenceNode = nodeCreator.CreateNode(new Flow_SequenceNode());
            context.NewEntryPoint(sequenceNode.FlowIn(Flow_SequenceNode.IdFlowIn), _sequenceOrderCheck.GetText());
            sequenceNode.FlowOut("0");
            sequenceNode.FlowOut("9");
            sequenceNode.FlowOut("10");
            
            _sequenceOrderCheck.SetupOrderFlowCheck(new[]
            {
                sequenceNode.FlowOut("0"),
                sequenceNode.FlowOut("10"),
                sequenceNode.FlowOut("9"),
            });         

            
            var sequenceNode2 = nodeCreator.CreateNode(new Flow_SequenceNode());
            context.NewEntryPoint(sequenceNode2.FlowIn(Flow_SequenceNode.IdFlowIn), _sequenceOrderCheck2.GetText());

            sequenceNode2.FlowOut("ccc");
            sequenceNode2.FlowOut("aaa");
            sequenceNode2.FlowOut("b");
            
            _sequenceOrderCheck2.SetupOrderFlowCheck(new[]
            {
                sequenceNode2.FlowOut("aaa"),
                sequenceNode2.FlowOut("b"),
                sequenceNode2.FlowOut("ccc"),
            });     
            
            var sequenceNode3 = nodeCreator.CreateNode(new Flow_SequenceNode());
            context.NewEntryPoint(sequenceNode3.FlowIn(Flow_SequenceNode.IdFlowIn), _sequenceOrderCheck3.GetText());

            sequenceNode3.FlowOut("b");
            sequenceNode3.FlowOut("B");
            sequenceNode3.FlowOut("a");
            sequenceNode3.FlowOut("A");
            
            _sequenceOrderCheck3.SetupOrderFlowCheck(new[]
            {
                sequenceNode3.FlowOut("A"),
                sequenceNode3.FlowOut("B"),
                sequenceNode3.FlowOut("a"),
                sequenceNode3.FlowOut("b"),
            });  

        }
    }
}