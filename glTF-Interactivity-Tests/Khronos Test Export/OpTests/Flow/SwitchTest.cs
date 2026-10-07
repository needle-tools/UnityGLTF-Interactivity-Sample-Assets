using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class SwitchTest : ITestCase
    {
        private CheckBox _selectionFlowCheck;
        private CheckBox _defaultFlowCheck;
        private CheckBox _noCasesDefaultFlowCheck;
        private CheckBox _negateCasesFlowCheck;
        private CheckBox _fractionalCasesDefaultConfigCheck;
        private CheckBox _outOfRangeCasesDefaultConfigCheck;
        private CheckBox _fractionalCasesNoCaseFlowCheck;
        private CheckBox _outOfRangeCasesNoCaseFlowCheck;
        private CheckBox _duplicateCasesFlowCheck;
        private CheckBox _duplicateCasesNoOtherFlowCheck;
        private CheckBox _selectionNotInCasesDefaultFlowCheck;
        private CheckBox _selectionNotInCasesNoExtraFlowCheck;
        //private CheckBox _floatNumberCasesFlowCheck;
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
            _fractionalCasesDefaultConfigCheck = context.AddCheckBox("Cases [0.5, 1] use default configuration");
            _fractionalCasesNoCaseFlowCheck = context.AddCheckBox("Cases [0.5, 1], selection 1: [1] not activated");
            _fractionalCasesNoCaseFlowCheck.Negate();
            context.NewRow();
            _outOfRangeCasesDefaultConfigCheck = context.AddCheckBox("Cases [-2147483649, 0] use default configuration");
            _outOfRangeCasesNoCaseFlowCheck = context.AddCheckBox("Cases [-2147483649, 0], selection 0: [0] and [1] not activated");
            _outOfRangeCasesNoCaseFlowCheck.Negate();
            _duplicateCasesFlowCheck = context.AddCheckBox("Duplicate cases [1, 2, 2]");
            _duplicateCasesNoOtherFlowCheck = context.AddCheckBox("Duplicate cases [1, 2, 2], selection 2: [1] and [3] not activated");
            _duplicateCasesNoOtherFlowCheck.Negate();
            context.NewRow();
            _selectionNotInCasesDefaultFlowCheck = context.AddCheckBox("Selection 2 not in cases [1]: [default] activated");
            _selectionNotInCasesNoExtraFlowCheck = context.AddCheckBox("Selection 2 not in cases [1]: [1] and extra output [2] not activated");
            _selectionNotInCasesNoExtraFlowCheck.Negate();
            //_floatNumberCasesFlowCheck = context.AddCheckBox("Float number cases flow");
        }

        public void CreateNodes(TestContext context)
        {
            var noteCreator = context.interactivityExportContext;
            
            var switchNode = noteCreator.CreateNode<Flow_SwitchNode>();
            
            context.NewEntryPoint(switchNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch selection flow");
            switchNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1, 4, 3};
            switchNode.FlowOut("1");
            switchNode.FlowOut("4");
            switchNode.FlowOut("3");
            switchNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(4);
            _selectionFlowCheck.SetupCheck(switchNode.FlowOut("4"));

            var switch2Node = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switch2Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch default flow");
            switch2Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1, 2, 3};
            switch2Node.FlowOut("1");
            switch2Node.FlowOut("2");
            switch2Node.FlowOut("3");
            switch2Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(5);
            _defaultFlowCheck.SetupCheck(switch2Node.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));

            var switch3Node = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switch3Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch empty-cases default flow");
            // Empty arrays are not allowed as configuration values; omitting cases selects the default configuration (no cases).
            switch3Node.Configuration.Remove(Flow_SwitchNode.IdConfigurationCases);
            switch3Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(5);
            _noCasesDefaultFlowCheck.SetupCheck(switch3Node.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
       
            var switch4Node = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switch4Node.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch negate cases flow");
            switch4Node.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {-1, -50, 3, 0};
            switch4Node.FlowOut("-1");
            switch4Node.FlowOut("-50");
            switch4Node.FlowOut("3");
            switch4Node.ValueIn(Flow_SwitchNode.IdSelection).SetValue(-50);
            _negateCasesFlowCheck.SetupCheck(switch4Node.FlowOut("-50"));

            // A case that is not exactly representable as a 32-bit integer invalidates the whole cases array,
            // so the default configuration without cases is used and every selection goes to the default flow.
            var switchFractionalNode = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switchFractionalNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch fractional cases default configuration");
            switchFractionalNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new object[] {0.5, 1};
            switchFractionalNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(1);
            _fractionalCasesDefaultConfigCheck.SetupCheck(switchFractionalNode.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
            // Output "1" is connected, so an engine that ignores the invalid cases and routes by the selection fails
            _fractionalCasesNoCaseFlowCheck.SetupNegateCheck(switchFractionalNode.FlowOut("1"));

            var switchOutOfRangeNode = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switchOutOfRangeNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch out of int32 range cases default configuration");
            switchOutOfRangeNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new object[] {-2147483649L, 0};
            switchOutOfRangeNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(0);
            _outOfRangeCasesDefaultConfigCheck.SetupCheck(switchOutOfRangeNode.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
            _outOfRangeCasesNoCaseFlowCheck.SetupNegateCheck(out var outOfRangeNoCaseFlowIn);
            switchOutOfRangeNode.FlowOut("0").ConnectToFlowDestination(outOfRangeNoCaseFlowIn);
            switchOutOfRangeNode.FlowOut("1").ConnectToFlowDestination(outOfRangeNoCaseFlowIn);

            // Duplicate cases are ignored.
            var switchDuplicateNode = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switchDuplicateNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch duplicate cases flow");
            switchDuplicateNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1, 2, 2};
            switchDuplicateNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(2);
            _duplicateCasesFlowCheck.SetupCheck(switchDuplicateNode.FlowOut("2"));
            // "1" is another case, "3" is an extra output that is not in cases: neither may be activated
            _duplicateCasesNoOtherFlowCheck.SetupNegateCheck(out var duplicateNoOtherFlowIn);
            switchDuplicateNode.FlowOut("1").ConnectToFlowDestination(duplicateNoOtherFlowIn);
            switchDuplicateNode.FlowOut("3").ConnectToFlowDestination(duplicateNoOtherFlowIn);

            // Spec: the case output is only used if the cases array contains the selection. Output "2" exists and is
            // connected, but 2 is not in cases, so the default output must be activated.
            var switchNotInCasesNode = noteCreator.CreateNode<Flow_SwitchNode>();
            context.NewEntryPoint(switchNotInCasesNode.FlowIn(Flow_SwitchNode.IdFlowIn), "Switch selection not in cases");
            switchNotInCasesNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] {1};
            switchNotInCasesNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(2);
            _selectionNotInCasesDefaultFlowCheck.SetupCheck(switchNotInCasesNode.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
            _selectionNotInCasesNoExtraFlowCheck.SetupNegateCheck(out var notInCasesNoExtraFlowIn);
            switchNotInCasesNode.FlowOut("1").ConnectToFlowDestination(notInCasesNoExtraFlowIn);
            switchNotInCasesNode.FlowOut("2").ConnectToFlowDestination(notInCasesNoExtraFlowIn);

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