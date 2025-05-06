using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class SwitchTest : ITestCase
    {
        private CheckBox _selectionFlowCheck;
        private CheckBox _defaultFlowCheck;
        private CheckBox _noCasesDefaultFlowCheck;
        private CheckBox _negateCasesFlowCheck;
        private CheckBox _floatNumberCasesFlowCheck;
        public string GetTestName()
        {
            return "flow/switch";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _selectionFlowCheck = context.AddCheckBox("Selection flow");
            _defaultFlowCheck = context.AddCheckBox("Default flow");
            _noCasesDefaultFlowCheck = context.AddCheckBox("Empty cases default flow");
            _negateCasesFlowCheck = context.AddCheckBox("Negate cases flow");
            _floatNumberCasesFlowCheck = context.AddCheckBox("Float number cases flow");
        }

        public void CreateNodes(TestContext context)
        {
            var noteCreator = context.interactivityExportContext;
            
            var switchNode = noteCreator.CreateNode(new Flow_SwitchNode());
            
            context.NewEntryPoint(switchNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch selection flow");
            switchNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1, 4, 3};
            switchNode.FlowOut("1");
            switchNode.FlowOut("4");
            switchNode.FlowOut("3");
            switchNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(4);
            _selectionFlowCheck.SetupCheck(context, switchNode.FlowOut("4"));

            var switch2Node = noteCreator.CreateNode(new Flow_SwitchNode());
            context.NewEntryPoint(switch2Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch default flow");
            switch2Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1, 2, 3};
            switch2Node.FlowOut("1");
            switch2Node.FlowOut("2");
            switch2Node.FlowOut("3");
            switch2Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(5);
            _defaultFlowCheck.SetupCheck(context, switch2Node.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));

            var switch3Node = noteCreator.CreateNode(new Flow_SwitchNode());
            context.NewEntryPoint(switch3Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch empty-cases default flow");
            switch3Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {};
            switch3Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(5);
            _noCasesDefaultFlowCheck.SetupCheck(context, switch3Node.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
       
            var switch4Node = noteCreator.CreateNode(new Flow_SwitchNode());
            context.NewEntryPoint(switch4Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch negate cases flow");
            switch4Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {-1, -50, 3, 0};
            switch4Node.FlowOut("-1");
            switch4Node.FlowOut("-50");
            switch4Node.FlowOut("3");
            switch4Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(-50);
            _negateCasesFlowCheck.SetupCheck(context, switch4Node.FlowOut("-50"));
            
            // var switch5Node = noteCreator.CreateNode(new Flow_SwitchNode());
            // context.SetEntryPoint(switch5Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch float number cases flow");
            // switch5Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {0.1e1, 2, 3};
            // switch5Node.FlowOut("1.0");
            // switch5Node.FlowOut("2");
            // switch5Node.FlowOut("3.0");
            // switch5Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(2.3f);
            // _floatNumberCasesFlowCheck.SetupCheck(context, switch5Node.FlowOut("2"));
            
        }
    }
}