using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Configurations that are ignored or replaced by the default configuration instead of rejecting the graph.
    /// The exporter can't write them, so the configuration is changed in the serialized graph after export:
    ///  - debug/log with the syntactically invalid message "{" uses the default configuration and still activates [out]
    ///  - math/add is not configurable, a configuration on it is ignored
    ///  - flow/switch with an additional unknown configuration property still uses its cases
    /// </summary>
    public class GraphIgnoredConfigurationTest : ITestCase
    {
        private CheckBox _invalidLogMessageCheckBox;
        private CheckBox _nonConfigurableOpCheckBox;
        private CheckBox _unknownPropertyCaseCheckBox;
        private CheckBox _unknownPropertyDefaultCheckBox;

        public string GetTestName()
        {
            return "graph/ignored configuration";
        }

        public string GetTestDescription()
        {
            return "A debug/log message that is not valid falls back to the default configuration; a configuration on a non-configurable operation and unknown configuration properties are ignored.";
        }

        public void PrepareObjects(TestContext context)
        {
            _invalidLogMessageCheckBox = context.AddCheckBox("debug/log invalid\nmessage: [out]", true);
            _nonConfigurableOpCheckBox = context.AddCheckBox("math/add with\nconfiguration");
            _unknownPropertyCaseCheckBox = context.AddCheckBox("flow/switch unknown\nproperty: case [1]", true);
            _unknownPropertyDefaultCheckBox = context.AddCheckBox("flow/switch unknown\nproperty: [default]", true);
            _unknownPropertyDefaultCheckBox.Negate();
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // debug/log with message "{" (an unterminated parameter)
            var log = nodeCreator.AddLog(GltfInteractivityExportNodes.LogLevel.Info, "message replaced after export");
            context.NewEntryPoint(log.FlowIn(Debug_LogNode.IdFlowIn), _invalidLogMessageCheckBox.GetText());
            _invalidLogMessageCheckBox.SetupCheck(log.FlowOut(Debug_LogNode.IdFlowOut));
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, log)["configuration"][Debug_LogNode.IdConfigMessage] = new JObject { ["value"] = new JArray("{") });

            // math/add with a configuration
            var add = nodeCreator.CreateNode<Math_AddNode>();
            add.ValueIn(Math_AddNode.IdValueA).SetValue(1);
            add.ValueIn(Math_AddNode.IdValueB).SetValue(2);
            context.NewEntryPoint(_nonConfigurableOpCheckBox.GetText());
            _nonConfigurableOpCheckBox.SetupCheck(add.FirstValueOut(), out var addCheckFlow, 3);
            context.AddToCurrentEntrySequence(addCheckFlow);
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, add)["configuration"] = new JObject { ["cases"] = new JObject { ["value"] = new JArray(1, 2) } });

            // flow/switch with an unknown configuration property
            var switchNode = nodeCreator.CreateNode<Flow_SwitchNode>();
            switchNode.Configuration[Flow_SwitchNode.IdConfigurationCases].Value = new int[] { 1 };
            switchNode.ValueIn(Flow_SwitchNode.IdSelection).SetValue(1);
            context.NewEntryPoint(switchNode.FlowIn(Flow_SwitchNode.IdFlowIn), _unknownPropertyCaseCheckBox.GetText());
            _unknownPropertyCaseCheckBox.SetupCheck(switchNode.FlowOut("1"));
            _unknownPropertyDefaultCheckBox.SetupNegateCheck(switchNode.FlowOut(Flow_SwitchNode.IdFDefaultFlowOut));
            context.PatchSerializedGraph(graph =>
                ((JObject)TestContext.SerializedNode(graph, switchNode)["configuration"])["unknownProperty"] = new JObject { ["value"] = new JArray(true) });
        }
    }
}
