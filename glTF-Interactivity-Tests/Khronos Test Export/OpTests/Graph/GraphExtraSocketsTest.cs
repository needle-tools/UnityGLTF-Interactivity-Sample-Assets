using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Socket definitions that look wrong but are valid. The exporter can't write them, so the sockets are changed
    /// in the serialized graph after export:
    ///  - an output flow pointing to an input flow socket the target doesn't have is unconnected
    ///  - an additional input value socket (math/add input "c") is ignored
    ///  - an output flow id the operation doesn't define (flow/branch "notAFlow") is never activated
    ///  - an input value without "value" uses the type-default value (NaN for float)
    ///  - a node reference with an explicit matching "type"
    /// </summary>
    public class GraphExtraSocketsTest : ITestCase
    {
        private CheckBox _unknownFlowSocketNextCheckBox;
        private CheckBox _unknownFlowSocketTargetCheckBox;
        private CheckBox _extraValueSocketCheckBox;
        private CheckBox _extraFlowTrueCheckBox;
        private CheckBox _extraFlowTargetCheckBox;
        private CheckBox _typeDefaultCheckBox;
        private CheckBox _typedReferenceCheckBox;

        public string GetTestName()
        {
            return "graph/extra and unknown sockets";
        }

        public string GetTestDescription()
        {
            return "Flows to unknown input flow sockets, additional input value sockets and output flows, type-default input values and node references with an explicit type.";
        }

        public void PrepareObjects(TestContext context)
        {
            _unknownFlowSocketNextCheckBox = context.AddCheckBox("flow to unknown\nsocket: next output", true);
            _unknownFlowSocketTargetCheckBox = context.AddCheckBox("flow to unknown\nsocket: target not run", true);
            _unknownFlowSocketTargetCheckBox.Negate();
            _extraValueSocketCheckBox = context.AddCheckBox("math/add with\nextra input c");
            _extraFlowTrueCheckBox = context.AddCheckBox("flow/branch extra\nflow: [true]", true);
            _extraFlowTargetCheckBox = context.AddCheckBox("flow/branch extra\nflow: target not run", true);
            _extraFlowTargetCheckBox.Negate();
            context.NewRow();
            _typeDefaultCheckBox = context.AddCheckBox("type-default float\ninput is NaN");
            _typedReferenceCheckBox = context.AddCheckBox("node reference\nwith type");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Output flow "0" is redirected to the input flow socket "doesNotExist" of the target
            var sequence = nodeCreator.CreateNode<Flow_SequenceNode>();
            var unknownSocketTarget = nodeCreator.CreateNode<Flow_SequenceNode>();
            sequence.FlowOut("0").ConnectToFlowDestination(unknownSocketTarget.FlowIn(Flow_SequenceNode.IdFlowIn));
            context.NewEntryPoint(sequence.FlowIn(Flow_SequenceNode.IdFlowIn), _unknownFlowSocketNextCheckBox.GetText());
            _unknownFlowSocketNextCheckBox.SetupCheck(sequence.FlowOut("1"));
            _unknownFlowSocketTargetCheckBox.SetupNegateCheck(unknownSocketTarget.FlowOut("0"));
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, sequence)["flows"]["0"]["socket"] = "doesNotExist");

            // math/add with an additional input "c"
            var add = nodeCreator.CreateNode<Math_AddNode>();
            add.ValueIn(Math_AddNode.IdValueA).SetValue(1);
            add.ValueIn(Math_AddNode.IdValueB).SetValue(2);
            context.NewEntryPoint(_extraValueSocketCheckBox.GetText());
            _extraValueSocketCheckBox.SetupCheck(add.FirstValueOut(), out var addCheckFlow, 3);
            context.AddToCurrentEntrySequence(addCheckFlow);
            context.PatchSerializedGraph(graph =>
                ((JObject)TestContext.SerializedNode(graph, add)["values"])["c"] = new JObject
                {
                    ["type"] = TestContext.SerializedTypeIndex(graph, GltfTypes.Int),
                    ["value"] = new JArray(3),
                });

            // flow/branch whose "false" flow is renamed to "notAFlow"
            var branch = nodeCreator.CreateNode<Flow_BranchNode>();
            var extraFlowTarget = nodeCreator.CreateNode<Flow_SequenceNode>();
            branch.ValueIn(Flow_BranchNode.IdCondition).SetValue(true);
            branch.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(extraFlowTarget.FlowIn(Flow_SequenceNode.IdFlowIn));
            context.NewEntryPoint(branch.FlowIn(Flow_BranchNode.IdFlowIn), _extraFlowTrueCheckBox.GetText());
            _extraFlowTrueCheckBox.SetupCheck(branch.FlowOut(Flow_BranchNode.IdFlowOutTrue));
            _extraFlowTargetCheckBox.SetupNegateCheck(extraFlowTarget.FlowOut("0"));
            context.PatchSerializedGraph(graph =>
            {
                var flows = (JObject)TestContext.SerializedNode(graph, branch)["flows"];
                var flow = flows[Flow_BranchNode.IdFlowOutFalse];
                flows.Remove(Flow_BranchNode.IdFlowOutFalse);
                flows["notAFlow"] = flow;
            });

            // Type-default input value: { "type": float } without "value"
            var isNaN = nodeCreator.CreateNode<Math_IsNaNNode>();
            isNaN.ValueIn(Math_IsNaNNode.IdValueA).SetValue(0f);
            context.NewEntryPoint(_typeDefaultCheckBox.GetText());
            _typeDefaultCheckBox.SetupCheck(isNaN.FirstValueOut(), out var isNaNCheckFlow, true);
            context.AddToCurrentEntrySequence(isNaNCheckFlow);
            context.PatchSerializedGraph(graph =>
                ((JObject)TestContext.SerializedNode(graph, isNaN)["values"][Math_IsNaNNode.IdValueA]).Remove("value"));

            // Node reference with an explicit type: { "node": <math/E>, "type": float }
            var e = nodeCreator.CreateNode<Math_ENode>();
            var abs = nodeCreator.CreateNode<Math_AbsNode>();
            abs.ValueIn(Math_AbsNode.IdValueA).ConnectToSource(e.FirstValueOut());
            _typedReferenceCheckBox.SetupCheck(abs.FirstValueOut(), out var absCheckFlow, (float)System.Math.E, true);
            context.AddToCurrentEntrySequence(absCheckFlow);
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, abs)["values"][Math_AbsNode.IdValueA]["type"] = TestContext.SerializedTypeIndex(graph, GltfTypes.Float));
        }
    }
}
