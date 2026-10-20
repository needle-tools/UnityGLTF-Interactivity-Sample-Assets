using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Declaration equality compares op, extension and the ids and type indices of the input value sockets.
    /// Two declarations of the same extension op that differ only in the type of an input value socket are
    /// different declarations, so the graph is valid (equal declarations would reject it, see invalid E5).
    /// The exporter can't write them, so they are added to the serialized graph after export:
    ///  - test/overloadedOp with input a:int / output value:int and with input a:float / output value:float
    ///  - the extension is unsupported, so both nodes are no-ops with type-default outputs (int 0, float NaN)
    /// If an implementation treats the two declarations as equal, it rejects the graph and no check passes.
    /// </summary>
    public class GraphDeclarationEqualityTest : ITestCase
    {
        private const string UnsupportedExtension = "EXT_interactivity_unsupported_test";
        private const string OverloadedOp = "test/overloadedOp";

        private CheckBox _intDeclarationCheckBox;
        private CheckBox _floatDeclarationCheckBox;

        public string GetTestName()
        {
            return "graph/declaration equality";
        }

        public string GetTestDescription()
        {
            return "Two declarations of the same extension operation that differ only in an input value socket type are different declarations, so the graph is valid.";
        }

        public void PrepareObjects(TestContext context)
        {
            _intDeclarationCheckBox = context.AddCheckBox("a:int declaration:\ntype-default 0");
            _floatDeclarationCheckBox = context.AddCheckBox("a:float declaration:\nisNaN");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Placeholders that get the two test/overloadedOp declarations after export
            var intPlaceholder = nodeCreator.CreateNode<Math_AbsNode>();
            intPlaceholder.ValueIn(Math_AbsNode.IdValueA).SetValue(-5);
            var floatPlaceholder = nodeCreator.CreateNode<Math_AbsNode>();
            floatPlaceholder.ValueIn(Math_AbsNode.IdValueA).SetValue(-5f);

            context.NewEntryPoint(_intDeclarationCheckBox.GetText());
            _intDeclarationCheckBox.SetupCheck(intPlaceholder.FirstValueOut(), out var intCheckFlow, 0);
            context.AddToCurrentEntrySequence(intCheckFlow);

            var isNaN = nodeCreator.CreateNode<Math_IsNaNNode>();
            isNaN.ValueIn(Math_IsNaNNode.IdValueA).ConnectToSource(floatPlaceholder.FirstValueOut());
            context.NewEntryPoint(_floatDeclarationCheckBox.GetText());
            _floatDeclarationCheckBox.SetupCheck(isNaN.FirstValueOut(), out var floatCheckFlow, true);
            context.AddToCurrentEntrySequence(floatCheckFlow);

            context.PatchSerializedGraph(graph =>
            {
                var declarations = (JArray)graph["declarations"];
                foreach (var (placeholder, signature) in new[] { (intPlaceholder, GltfTypes.Int), (floatPlaceholder, GltfTypes.Float) })
                {
                    var type = TestContext.SerializedTypeIndex(graph, signature);
                    declarations.Add(new JObject
                    {
                        ["op"] = OverloadedOp,
                        ["extension"] = UnsupportedExtension,
                        ["inputValueSockets"] = new JObject { [Math_AbsNode.IdValueA] = new JObject { ["type"] = type } },
                        ["outputValueSockets"] = new JObject { [Math_AbsNode.IdOut] = new JObject { ["type"] = type } },
                    });
                    TestContext.SerializedNode(graph, placeholder)["declaration"] = declarations.Count - 1;
                }
            });
        }
    }
}
