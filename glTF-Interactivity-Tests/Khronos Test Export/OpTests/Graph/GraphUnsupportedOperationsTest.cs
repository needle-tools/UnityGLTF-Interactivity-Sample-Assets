using System.Linq;
using Khronos_Test_Export.InvalidGraphs;
using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Parts of the asset the execution environment can't run, which must not make it reject the test graph.
    /// The exporter can't write them, so they are added to the serialized extension after export:
    ///  - nodes whose declaration refers to an unsupported extension are no-ops: their output flows are never
    ///    activated and their output values are type-default (int 0)
    ///  - an invalid second graph next to the valid default graph 0; graph 1 would send test/onFailed if it ran
    /// </summary>
    public class GraphUnsupportedOperationsTest : ITestCase
    {
        private const string UnsupportedExtension = "EXT_interactivity_unsupported_test";

        private CheckBox _noOpFlowCheckBox;
        private CheckBox _noOpValueCheckBox;
        private CheckBox _invalidSecondGraphCheckBox;

        public string GetTestName()
        {
            return "graph/unsupported operations and graphs";
        }

        public string GetTestDescription()
        {
            return "Operations of an unsupported extension are no-ops, and an invalid graph that is not the default graph doesn't prevent the default graph from running.";
        }

        public void PrepareObjects(TestContext context)
        {
            _noOpFlowCheckBox = context.AddCheckBox("unsupported op:\noutput flow", true);
            _noOpFlowCheckBox.Negate();
            _noOpValueCheckBox = context.AddCheckBox("unsupported op:\ntype-default output");
            _invalidSecondGraphCheckBox = context.AddCheckBox("invalid graph 1:\ngraph 0 runs", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Placeholders that get declarations of an unsupported extension after export
            var flowPlaceholder = nodeCreator.CreateNode<Flow_SequenceNode>();
            var valuePlaceholder = nodeCreator.CreateNode<Math_AbsNode>();
            valuePlaceholder.ValueIn(Math_AbsNode.IdValueA).SetValue(-5);

            context.NewEntryPoint(_noOpValueCheckBox.GetText(), 0.5f);
            _noOpValueCheckBox.SetupCheck(valuePlaceholder.FirstValueOut(), out var valueCheckFlow, 0);
            _noOpFlowCheckBox.SetupNegateCheck(flowPlaceholder.FlowOut("0"));
            context.AddToCurrentEntrySequence(flowPlaceholder.FlowIn(Flow_SequenceNode.IdFlowIn), valueCheckFlow);

            context.PatchSerializedGraph(graph =>
            {
                var declarations = (JArray)graph["declarations"];
                var intType = TestContext.SerializedTypeIndex(graph, GltfTypes.Int);

                declarations.Add(new JObject { ["op"] = "test/unsupportedFlowOp", ["extension"] = UnsupportedExtension });
                TestContext.SerializedNode(graph, flowPlaceholder)["declaration"] = declarations.Count - 1;

                declarations.Add(new JObject
                {
                    ["op"] = "test/unsupportedValueOp",
                    ["extension"] = UnsupportedExtension,
                    ["inputValueSockets"] = new JObject { [Math_AbsNode.IdValueA] = new JObject { ["type"] = intType } },
                    ["outputValueSockets"] = new JObject { [Math_AbsNode.IdOut] = new JObject { ["type"] = intType } },
                });
                TestContext.SerializedNode(graph, valuePlaceholder)["declaration"] = declarations.Count - 1;
            });

            // Invalid graph 1: its math/add declaration becomes the undefined operation math/doesNotExist
            context.NewEntryPoint(_invalidSecondGraphCheckBox.GetText());
            _invalidSecondGraphCheckBox.SetupCheck(out var graphRunsFlow);
            context.AddToCurrentEntrySequence(graphRunsFlow);
            context.PatchSerializedExtension(extension =>
            {
                var invalidGraph = InvalidGraphCase.BuildGraph(InvalidGraphBuilder.FailedEventId,
                    "FAILED: graph 1 is invalid and not the default graph, it must not run",
                    b => b.Node("math/add", InvalidGraphBuilder.Values(("a", b.Inline("int", 1)), ("b", b.Inline("int", 2)))),
                    graph => ((JArray)graph["declarations"]).OfType<JObject>().First(d => (string)d["op"] == "math/add")["op"] = "math/doesNotExist");
                ((JArray)extension["graphs"]).Add(invalidGraph);
                extension["graph"] = 0;
            });
        }
    }
}
