using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using static Khronos_Test_Export.InvalidGraphs.InvalidGraphBuilder;

namespace Khronos_Test_Export.InvalidGraphs
{
    /// <summary>
    /// All invalid-graph cases. Ids and grouping follow glTF-Interactivity-Tests/SpecInvalidGraphCases.md.
    /// Each case's <c>setup</c> must produce a valid graph; its <c>mutate</c> breaks exactly one rule.
    /// </summary>
    public static class InvalidGraphCases
    {
        // First node after the signal chain; subject nodes are S, S + 1, ...
        private const int S = FirstSubjectNode;

        private const string SecGraph = "Validation › Graph Object Validation";
        private const string SecExt = "Validation › Extension Object Validation";
        private const string SecTypes = "JSON Syntax › Types";
        private const string SecVars = "JSON Syntax › Variables";
        private const string SecEvents = "JSON Syntax › Events";
        private const string SecDecl = "JSON Syntax › Declarations";
        private const string SecNodes = "JSON Syntax › Nodes";
        private const string SecInline = "Validation › Inline Value Object Validation";
        private const string SecPtr = "Object Model Access › JSON Pointer Template Parsing";

        public static List<InvalidGraphCase> All()
        {
            var cases = new List<InvalidGraphCase>();
            Extension(cases);
            Types(cases);
            Variables(cases);
            Events(cases);
            Declarations(cases);
            Nodes(cases);
            Operations(cases);

            var duplicate = cases.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException($"Duplicate invalid graph case id {duplicate.Key}");
            return cases;
        }

        #region A – extension object

        private static void Extension(List<InvalidGraphCase> c)
        {
            const string g = "extension";
            Action<InvalidGraphBuilder> addInts = b => AddInts(b);

            c.Add(RejectExt("A1a", g, "graphs_empty", "graphs array is empty", SecExt, addInts, e => e["graphs"] = new JArray()));
            c.Add(RejectExt("A1b", g, "graphs_missing", "graphs property is missing", SecExt, addInts, e => e.Remove("graphs")));
            c.Add(RejectExt("A1c", g, "graphs_not_array", "graphs is an object instead of an array", SecExt, addInts,
                e => e["graphs"] = e["graphs"][0]));
            c.Add(RejectExt("A2a", g, "default_graph_negative", "graph (default graph index) is -1", SecExt, addInts, e => e["graph"] = -1));
            c.Add(RejectExt("A2b", g, "default_graph_fractional", "graph (default graph index) is 0.5", SecExt, addInts, e => e["graph"] = 0.5));
            c.Add(RejectExt("A2c", g, "default_graph_string", "graph (default graph index) is the string \"0\"", SecExt, addInts, e => e["graph"] = "0"));
            c.Add(RejectExt("A3", g, "default_graph_out_of_range", "graph (default graph index) is 1 with only one graph", SecExt, addInts,
                e => e["graph"] = 1));
            c.Add(RejectExt("A4", g, "default_graph_invalid",
                "default graph 1 is invalid (unknown operation); valid graph 0 must not run either", SecExt, addInts,
                e =>
                {
                    ((JArray)e["graphs"]).Add(InvalidGraphCase.BuildGraph(FailedEventId,
                        "FAILED [A4] graph 1 (invalid default graph) is running", AddInts,
                        graph => Decl(graph, "math/add")["op"] = "math/doesNotExist"));
                    e["graph"] = 1;
                }));
        }

        #endregion

        #region B – types

        private static void Types(List<InvalidGraphCase> c)
        {
            const string g = "types";
            Action<InvalidGraphBuilder> addFloats = b => AddFloats(b);

            c.Add(Reject("B1a", g, "unknown_signature", "types contains unknown signature \"float5\"", SecTypes, addFloats,
                gr => Arr(gr, "types").Add(new JObject { ["signature"] = "float5" })));
            c.Add(Reject("B1b", g, "signature_wrong_case", "type signature \"Float\" (signatures are case-sensitive)", SecTypes, addFloats,
                gr => Arr(gr, "types")[TypeIndex(gr, "float")]["signature"] = "Float"));
            c.Add(Reject("B2a", g, "duplicate_signature", "types contains \"float\" twice", SecTypes, addFloats,
                gr => Arr(gr, "types").Add(new JObject { ["signature"] = "float" })));
            c.Add(Reject("B2b", g, "duplicate_signature_with_extras", "types contains \"float\" twice, the second one with extras", SecTypes, addFloats,
                gr => Arr(gr, "types").Add(new JObject { ["signature"] = "float", ["extras"] = new JObject { ["note"] = "still a duplicate" } })));
            c.Add(Assert("B3a", g, "types_empty", "types array is empty", SecGraph, b => b.Node("math/E"),
                gr => gr["types"] = new JArray()));
            c.Add(Assert("B3b", g, "signature_missing", "types entry without signature", SecGraph, addFloats,
                gr => Arr(gr, "types").Add(new JObject())));
            c.Add(Assert("B3c", g, "signature_not_string", "types entry with numeric signature", SecGraph, addFloats,
                gr => Arr(gr, "types").Add(new JObject { ["signature"] = 3 })));
            c.Add(Assert("B3d", g, "type_not_object", "types entry is a string instead of an object", SecGraph, addFloats,
                gr => Arr(gr, "types").Add("float")));
        }

        #endregion

        #region C – variables

        private static void Variables(List<InvalidGraphCase> c)
        {
            const string g = "variables";

            c.Add(Reject("C1", g, "variable_type_out_of_range", "variable type index equals types length", SecVars, VarCase("int", 0),
                gr => Var(gr)["type"] = Arr(gr, "types").Count));

            c.Add(Reject("C2a", g, "float3_value_too_short", "float3 variable with 2 values", SecInline, VarCase("float3", 1.0, 2.0, 3.0),
                gr => Var(gr)["value"] = new JArray(1.0, 2.0)));
            c.Add(Reject("C2b", g, "float4x4_value_nine_elements", "float4x4 variable with 9 values", SecInline,
                VarCase("float4x4", Enumerable.Range(0, 16).Select(i => (object)(i % 5 == 0 ? 1.0 : 0.0)).ToArray()),
                gr => Var(gr)["value"] = new JArray(Enumerable.Range(0, 9).Select(i => (object)(i % 4 == 0 ? 1.0 : 0.0)).ToArray())));
            c.Add(Reject("C2c", g, "bool_value_two_elements", "bool variable with 2 values", SecInline, VarCase("bool", true),
                gr => Var(gr)["value"] = new JArray(true, false)));
            c.Add(Reject("C3a", g, "bool_value_number", "bool variable with value [1]", SecInline, VarCase("bool", true),
                gr => Var(gr)["value"] = new JArray(1)));
            c.Add(Reject("C3b", g, "bool_value_string", "bool variable with value [\"true\"]", SecInline, VarCase("bool", true),
                gr => Var(gr)["value"] = new JArray("true")));
            c.Add(Reject("C4a", g, "float_value_null", "float variable with value [null]", SecInline, VarCase("float", 1.0),
                gr => Var(gr)["value"] = new JArray(JValue.CreateNull())));
            c.Add(Reject("C4b", g, "float_value_string", "float variable with value [\"1.0\"]", SecInline, VarCase("float", 1.0),
                gr => Var(gr)["value"] = new JArray("1.0")));
            c.Add(Reject("C4c", g, "float2_value_bool_element", "float2 variable with value [1.0, true]", SecInline, VarCase("float2", 1.0, 2.0),
                gr => Var(gr)["value"] = new JArray(1.0, true)));
            c.Add(Reject("C5a", g, "int_value_fractional", "int variable with value [1.5]", SecInline, VarCase("int", 1),
                gr => Var(gr)["value"] = new JArray(1.5)));
            c.Add(Reject("C5b", g, "int_value_exceeds_int32", "int variable with value [2147483648]", SecInline, VarCase("int", 1),
                gr => Var(gr)["value"] = new JArray(2147483648L)));
            c.Add(Reject("C6a", g, "ref_value_missing_leading_slash", "ref variable with value [\"nodes/0\"]", SecInline, VarCase("ref", "/nodes/0"),
                gr => Var(gr)["value"] = new JArray("nodes/0")));
            c.Add(Reject("C6b", g, "ref_value_invalid_escape", "ref variable with value [\"/nodes/~2\"]", SecInline, VarCase("ref", "/nodes/0"),
                gr => Var(gr)["value"] = new JArray("/nodes/~2")));
            c.Add(Reject("C6c", g, "ref_value_number", "ref variable with value [0]", SecInline, VarCase("ref", "/nodes/0"),
                gr => Var(gr)["value"] = new JArray(0)));

            c.Add(Assert("C7a", g, "variables_without_types", "variables defined but the graph has no types array", SecGraph, VarCase("int", 0),
                gr => gr.Remove("types")));
            c.Add(Assert("C7b", g, "variable_value_empty", "variable with value []", SecGraph, VarCase("int", 0),
                gr => Var(gr)["value"] = new JArray()));
            c.Add(Assert("C7c", g, "variable_name_not_string", "variable name is a number", SecGraph, VarCase("int", 0),
                gr => Var(gr)["name"] = 5));
            c.Add(Assert("C7d", g, "variables_empty", "variables array is empty", SecGraph, b => b.Type("int"),
                gr => gr["variables"] = new JArray()));
            c.Add(Assert("C8a", g, "variable_type_missing", "variable without type", SecGraph, VarCase("int", 0),
                gr => Var(gr).Remove("type")));
            c.Add(Assert("C8b", g, "variable_type_negative", "variable type index -1", SecGraph, VarCase("int", 0),
                gr => Var(gr)["type"] = -1));
            c.Add(Assert("C8c", g, "variable_type_fractional", "variable type index 0.5", SecGraph, VarCase("int", 0),
                gr => Var(gr)["type"] = 0.5));
        }

        #endregion

        #region D – events

        private static void Events(List<InvalidGraphCase> c)
        {
            const string g = "events";
            // Event 0 is the signal event of the chain; custom events start at 1.
            Action<InvalidGraphBuilder> eventWithValue = b =>
                b.Event("custom/a", Values(("v", b.Inline("int", 1))));
            Func<JObject, JObject> ev1 = gr => (JObject)Arr(gr, "events")[1];
            Func<JObject, JObject> ev1Value = gr => (JObject)ev1(gr)["values"]["v"];

            c.Add(Reject("D1", g, "duplicate_event_id", "two events with the id \"custom/a\"", SecEvents,
                b => { b.Event("custom/a"); b.Event("custom/b"); },
                gr => Arr(gr, "events")[2]["id"] = "custom/a"));
            c.Add(Assert("D2", g, "event_value_named_event", "event value socket with the reserved id \"event\"", SecEvents, eventWithValue,
                gr => ev1(gr)["values"] = new JObject { ["event"] = ev1Value(gr).DeepClone() }));
            c.Add(Reject("D3a", g, "event_value_type_out_of_range", "event value type index equals types length", SecEvents, eventWithValue,
                gr => ev1Value(gr)["type"] = Arr(gr, "types").Count));
            c.Add(Assert("D3b", g, "event_value_type_missing", "event value without type", SecGraph, eventWithValue,
                gr => ev1Value(gr).Remove("type")));
            c.Add(Reject("D4a", g, "event_value_int_fractional", "int event value with initial value [1.5]", SecInline, eventWithValue,
                gr => ev1Value(gr)["value"] = new JArray(1.5)));
            c.Add(Reject("D4b", g, "event_value_wrong_length", "float3 event value with initial value [1, 2]", SecInline,
                b => b.Event("custom/a", Values(("v", b.Inline("float3", 1.0, 2.0, 3.0)))),
                gr => ev1Value(gr)["value"] = new JArray(1.0, 2.0)));
            c.Add(Assert("D5a", g, "event_id_not_string", "event id is a number", SecGraph, eventWithValue,
                gr => ev1(gr)["id"] = 5));
            c.Add(Assert("D5b", g, "event_values_empty", "event with values {}", SecGraph, eventWithValue,
                gr => ev1(gr)["values"] = new JObject()));
            c.Add(Assert("D5c", g, "event_name_not_string", "event name is a boolean", SecGraph, eventWithValue,
                gr => ev1(gr)["name"] = true));
        }

        #endregion

        #region E – declarations

        private static void Declarations(List<InvalidGraphCase> c)
        {
            const string g = "declarations";
            Action<InvalidGraphBuilder> addInts = b => AddInts(b);
            Action<InvalidGraphBuilder> extOp = b =>
            {
                var decl = b.AddDecl(new JObject
                {
                    ["op"] = "test/customOp",
                    ["extension"] = "EXT_interactivity_invalid_graph_test",
                    ["inputValueSockets"] = new JObject { ["a"] = new JObject { ["type"] = b.Type("int") } },
                    ["outputValueSockets"] = new JObject { ["result"] = new JObject { ["type"] = b.Type("int") } },
                });
                b.AddNode(decl, Values(("a", b.Inline("int", 1))));
            };
            Func<JObject, JObject> extDecl = gr => Decl(gr, "test/customOp");

            c.Add(Assert("E1a", g, "op_missing", "declaration without op", SecGraph, addInts,
                gr => Decl(gr, "math/add").Remove("op")));
            c.Add(Assert("E1b", g, "op_not_string", "declaration op is a number", SecGraph, addInts,
                gr => Decl(gr, "math/add")["op"] = 5));
            c.Add(Reject("E2a", g, "unknown_op_without_extension", "unknown op \"math/addd\" without extension", SecDecl, addInts,
                gr => Decl(gr, "math/add")["op"] = "math/addd"));
            c.Add(Reject("E2b", g, "op_wrong_case", "op \"Math/Add\" (ops are case-sensitive)", SecDecl, addInts,
                gr => Decl(gr, "math/add")["op"] = "Math/Add"));
            c.Add(Reject("E2c", g, "unused_unknown_declaration", "unknown op declared but not used by any node", SecDecl, addInts,
                gr => Arr(gr, "declarations").Add(new JObject { ["op"] = "math/doesNotExist" })));
            c.Add(Reject("E3a", g, "spec_op_with_input_value_sockets", "spec op math/add declares inputValueSockets", SecDecl, addInts,
                gr => Decl(gr, "math/add")["inputValueSockets"] = new JObject
                {
                    ["a"] = new JObject { ["type"] = TypeIndex(gr, "int") },
                    ["b"] = new JObject { ["type"] = TypeIndex(gr, "int") },
                }));
            c.Add(Reject("E3b", g, "spec_op_with_output_value_sockets", "spec op math/add declares outputValueSockets", SecDecl, addInts,
                gr => Decl(gr, "math/add")["outputValueSockets"] = new JObject { ["value"] = new JObject { ["type"] = TypeIndex(gr, "int") } }));
            c.Add(Reject("E4a", g, "ext_input_socket_type_out_of_range", "extension op input socket type index equals types length", SecDecl, extOp,
                gr => extDecl(gr)["inputValueSockets"]["a"]["type"] = Arr(gr, "types").Count));
            c.Add(Reject("E4b", g, "ext_output_socket_type_out_of_range", "extension op output socket type index equals types length", SecDecl, extOp,
                gr => extDecl(gr)["outputValueSockets"]["result"]["type"] = Arr(gr, "types").Count));
            c.Add(Assert("E4c", g, "ext_socket_type_missing", "extension op input socket without type", SecGraph, extOp,
                gr => extDecl(gr)["inputValueSockets"]["a"] = new JObject()));
            c.Add(Assert("E4d", g, "ext_input_sockets_empty", "extension op with inputValueSockets {}", SecGraph, extOp,
                gr => extDecl(gr)["inputValueSockets"] = new JObject()));
            c.Add(Reject("E5a", g, "duplicate_declaration", "math/add declared twice", SecDecl, addInts,
                gr => Arr(gr, "declarations").Add(new JObject { ["op"] = "math/add" })));
            c.Add(Reject("E5b", g, "duplicate_extension_declaration", "identical extension op declared twice", SecDecl, extOp,
                gr => Arr(gr, "declarations").Add(extDecl(gr).DeepClone())));
            c.Add(Reject("E5c", g, "duplicate_extension_declaration_other_outputs",
                "extension op declared twice, differing only in outputValueSockets (still equal)", SecDecl, extOp,
                gr =>
                {
                    var copy = (JObject)extDecl(gr).DeepClone();
                    copy["outputValueSockets"] = new JObject { ["other"] = new JObject { ["type"] = TypeIndex(gr, "int") } };
                    Arr(gr, "declarations").Add(copy);
                }));
            c.Add(Assert("E6", g, "extension_not_string", "declaration extension is a number", SecGraph, extOp,
                gr => extDecl(gr)["extension"] = 5));
        }

        #endregion

        #region F – nodes

        private static void Nodes(List<InvalidGraphCase> c)
        {
            const string g = "nodes";
            Action<InvalidGraphBuilder> addInts = b => { AddInts(b); b.Type("float"); };
            // S = math/E, S + 1 = math/add(a: S, b: 1.0)
            Action<InvalidGraphBuilder> refE = b =>
            {
                b.Node("math/E");
                b.Node("math/add", Values(("a", Ref(S)), ("b", b.Inline("float", 1.0))));
                b.Type("int");
            };
            // S = math/add(a: 1.0, b: 2.0), S + 1 = math/E
            Action<InvalidGraphBuilder> addThenE = b => { AddFloats(b); b.Node("math/E"); };
            // S = flow/multiGate, S + 1 = math/abs(a: S.lastIndex)
            Action<InvalidGraphBuilder> refMultiGate = b =>
            {
                b.Node("flow/multiGate");
                b.Node("math/abs", Values(("a", Ref(S, "lastIndex"))));
            };
            // S = flow/sequence(0 → S + 1), S + 1 = flow/sequence
            Action<InvalidGraphBuilder> sequences = b =>
            {
                b.Node("flow/sequence", flows: Flows(("0", S + 1, null)));
                b.Node("flow/sequence");
            };
            // S = flow/sequence(0 → S + 1), S + 1 = flow/branch(condition: true)
            Action<InvalidGraphBuilder> branch = b =>
            {
                b.Node("flow/sequence", flows: Flows(("0", S + 1, null)));
                b.Node("flow/branch", Values(("condition", b.Inline("bool", true))));
                b.Type("float");
            };
            Action<InvalidGraphBuilder> sine = b => { b.Node("math/sin", Values(("a", b.Inline("float", 1.0)))); b.Type("int"); };

            c.Add(Reject("F1a", g, "declaration_out_of_range", "node declaration index equals declarations length", SecNodes, addInts,
                gr => Node(gr, S)["declaration"] = Arr(gr, "declarations").Count));
            c.Add(Assert("F1b", g, "declaration_missing", "node without declaration", SecGraph, addInts,
                gr => Node(gr, S).Remove("declaration")));
            c.Add(Assert("F1c", g, "declaration_negative", "node declaration index -1", SecGraph, addInts,
                gr => Node(gr, S)["declaration"] = -1));
            c.Add(Assert("F1d", g, "nodes_without_declarations", "nodes defined but the graph has no declarations array", SecGraph, addInts,
                gr => gr.Remove("declarations")));

            c.Add(Assert("F2", g, "value_with_node_and_value", "input value socket defines both node and value", SecNodes, refE,
                gr => Value(gr, S + 1, "a")["value"] = new JArray(1.0)));

            c.Add(Reject("F3a", g, "value_ref_forward", "input value references a later node", SecNodes, addThenE,
                gr => Node(gr, S)["values"]["a"] = Ref(S + 1)));
            c.Add(Reject("F3b", g, "value_ref_self", "input value references its own node", SecNodes, addThenE,
                gr => Node(gr, S)["values"]["a"] = Ref(S)));
            c.Add(Assert("F3c", g, "value_ref_negative", "input value references node -1", SecGraph, addThenE,
                gr => Node(gr, S)["values"]["a"] = Ref(-1)));

            c.Add(Reject("F4a", g, "value_ref_unknown_socket", "input value references non-existent output socket \"result\"", SecNodes, refE,
                gr => Value(gr, S + 1, "a")["socket"] = "result"));
            c.Add(Reject("F4b", g, "value_ref_implicit_socket_missing",
                "input value omits socket, but flow/multiGate has no \"value\" output", SecNodes, refMultiGate,
                gr => Value(gr, S + 1, "a").Remove("socket")));
            c.Add(Reject("F4c", g, "value_ref_socket_wrong_case", "input value references socket \"LastIndex\" (case-sensitive)", SecNodes, refMultiGate,
                gr => Value(gr, S + 1, "a")["socket"] = "LastIndex"));
            c.Add(Reject("F5", g, "value_ref_type_mismatch", "input value references a float output but declares type int", SecNodes, refE,
                gr => Value(gr, S + 1, "a")["type"] = TypeIndex(gr, "int")));

            c.Add(Reject("F6a", g, "inline_type_out_of_range", "inline value type index equals types length", SecNodes, addInts,
                gr => Value(gr, S, "b")["type"] = Arr(gr, "types").Count));
            c.Add(Assert("F6b", g, "inline_type_missing", "inline value without type", SecGraph, addInts,
                gr => Value(gr, S, "b").Remove("type")));
            c.Add(Assert("F6c", g, "type_default_without_type", "input value socket {} (neither node, value nor type)", SecGraph, addInts,
                gr => Node(gr, S)["values"]["b"] = new JObject()));
            c.Add(Assert("F6d", g, "inline_type_negative", "inline value type index -1", SecGraph, addInts,
                gr => Value(gr, S, "b")["type"] = -1));

            c.Add(Reject("F7a", g, "inline_value_wrong_length", "float inline value with 2 elements", SecInline, b => AddFloats(b),
                gr => Value(gr, S, "b")["value"] = new JArray(1.0, 2.0)));
            c.Add(Reject("F7b", g, "inline_int_fractional", "int inline value [1.5]", SecInline, addInts,
                gr => Value(gr, S, "b")["value"] = new JArray(1.5)));
            c.Add(Reject("F7c", g, "inline_bool_number", "bool inline value [1]", SecInline, branch,
                gr => Value(gr, S + 1, "condition")["value"] = new JArray(1)));

            c.Add(Reject("F8a", g, "input_socket_missing", "math/add without input b", SecNodes, addInts,
                gr => ((JObject)Node(gr, S)["values"]).Remove("b")));
            c.Add(Reject("F8b", g, "input_socket_wrong_case", "math/add with input \"B\" instead of \"b\"", SecNodes, addInts,
                gr => RenameProperty((JObject)Node(gr, S)["values"], "b", "B")));
            c.Add(Reject("F8c", g, "values_missing", "math/add without values", SecNodes, addInts,
                gr => Node(gr, S).Remove("values")));

            c.Add(Reject("F9a", g, "mismatching_input_types", "math/add with int a and float b", SecNodes, addInts,
                gr => Node(gr, S)["values"]["b"] = InlineJson(gr, "float", 2.0)));
            c.Add(Reject("F9b", g, "branch_condition_float", "flow/branch with float condition", SecNodes, branch,
                gr => Node(gr, S + 1)["values"]["condition"] = InlineJson(gr, "float", 1.0)));
            c.Add(Reject("F9c", g, "unsupported_input_type", "math/sin with int input", SecNodes, sine,
                gr => Node(gr, S)["values"]["a"] = InlineJson(gr, "int", 1)));

            c.Add(Reject("F10a", g, "flow_backward", "output flow points to an earlier node", SecNodes, sequences,
                gr => Node(gr, S + 1)["flows"] = Flows(("0", S, null))));
            c.Add(Reject("F10b", g, "flow_self", "output flow points to its own node", SecNodes, sequences,
                gr => Node(gr, S)["flows"]["0"]["node"] = S));
            c.Add(Reject("F10c", g, "flow_out_of_range", "output flow points to node index equal to nodes length", SecNodes, sequences,
                gr => Node(gr, S)["flows"]["0"]["node"] = Arr(gr, "nodes").Count));
            c.Add(Reject("F10d", g, "flow_backward_into_signal_chain", "output flow points back to the debug/log node of the chain", SecNodes, sequences,
                gr => Node(gr, S)["flows"]["0"]["node"] = 1));
            c.Add(Assert("F11a", g, "flow_without_node", "output flow without node", SecGraph, sequences,
                gr => Node(gr, S)["flows"]["0"] = new JObject { ["socket"] = "in" }));
            c.Add(Assert("F11b", g, "flow_socket_not_string", "output flow socket is a number", SecGraph, sequences,
                gr => Node(gr, S)["flows"]["0"]["socket"] = 1));
            c.Add(Assert("F11c", g, "flow_node_negative", "output flow node -1", SecGraph, sequences,
                gr => Node(gr, S)["flows"]["0"]["node"] = -1));
            c.Add(Assert("F11d", g, "flows_empty_object", "node with flows {}", SecGraph, sequences,
                gr => Node(gr, S + 1)["flows"] = new JObject()));
            c.Add(Assert("F11e", g, "values_empty_object", "node with values {}", SecGraph, addThenE,
                gr => Node(gr, S + 1)["values"] = new JObject()));
            c.Add(Assert("F11f", g, "configuration_empty_object", "node with configuration {}", SecGraph, addThenE,
                gr => Node(gr, S + 1)["configuration"] = new JObject()));
            c.Add(Assert("F12a", g, "configuration_property_not_object", "configuration property is a plain number", SecGraph, VarCase("int", 0),
                gr => Node(gr, S)["configuration"]["variable"] = 0));
            c.Add(Assert("F12b", g, "configuration_value_empty", "configuration property with value []", SecGraph, VarCase("int", 0),
                gr => Node(gr, S)["configuration"]["variable"]["value"] = new JArray()));
            c.Add(Assert("F12c", g, "configuration_value_not_array", "configuration property with value 0 instead of [0]", SecGraph, VarCase("int", 0),
                gr => Node(gr, S)["configuration"]["variable"]["value"] = 0));

            c.Add(Reject("F13a", g, "extra_value_socket_invalid_type", "unused extra input value with type index out of range", SecNodes, addInts,
                gr => Node(gr, S)["values"]["c"] = new JObject { ["type"] = Arr(gr, "types").Count, ["value"] = new JArray(1) }));
            c.Add(Reject("F13b", g, "extra_value_socket_forward_ref", "unused extra input value referencing a later node", SecNodes, addThenE,
                gr => Node(gr, S)["values"]["c"] = Ref(S + 1)));
            c.Add(Reject("F13c", g, "extra_flow_backward", "unused extra output flow pointing to an earlier node", SecNodes, branch,
                gr => Node(gr, S + 1)["flows"] = Flows(("notAFlow", S, null))));
        }

        #endregion

        #region G / H – operation specific

        private static void Operations(List<InvalidGraphCase> c)
        {
            const string g = "operations";
            Func<JObject, JObject> cfg = gr => (JObject)Node(gr, S)["configuration"];
            Func<JObject, JObject> values = gr => (JObject)Node(gr, S)["values"];

            // variable/get
            const string secVarGet = "Operations › variable/get";
            var varGet = VarCase("int", 0);
            c.Add(Reject("G1a", g, "variable_get_config_missing", "variable/get without configuration", secVarGet, varGet,
                gr => Node(gr, S).Remove("configuration")));
            c.Add(Reject("G1b", g, "variable_get_index_out_of_range", "variable/get with variable index 1 (one variable)", secVarGet, varGet,
                gr => cfg(gr)["variable"] = Cv(1)));
            c.Add(Reject("G1c", g, "variable_get_index_negative", "variable/get with variable index -1", secVarGet, varGet,
                gr => cfg(gr)["variable"] = Cv(-1)));
            c.Add(Reject("G1d", g, "variable_get_index_fractional", "variable/get with variable index 0.5", secVarGet, varGet,
                gr => cfg(gr)["variable"] = Cv(0.5)));
            c.Add(Reject("G1e", g, "variable_get_index_string", "variable/get with variable index \"0\"", secVarGet, varGet,
                gr => cfg(gr)["variable"] = Cv("0")));
            c.Add(Reject("G1f", g, "variable_get_without_variables", "variable/get in a graph without variables", secVarGet, varGet,
                gr => gr.Remove("variables")));

            // variable/set
            const string secVarSet = "Operations › variable/set";
            Action<InvalidGraphBuilder> varSet = b =>
            {
                b.Variable("int", 0);
                b.Variable("int", 0);
                b.Node("variable/set", Values(("0", b.Inline("int", 5))), Config(("variables", new JArray(0))));
                b.Type("float");
            };
            c.Add(Reject("G2a", g, "variable_set_config_missing", "variable/set without configuration", secVarSet, varSet,
                gr => Node(gr, S).Remove("configuration")));
            c.Add(Reject("G2b", g, "variable_set_index_out_of_range", "variable/set with variables [2] (two variables)", secVarSet, varSet,
                gr => cfg(gr)["variables"] = Cv(2)));
            c.Add(Reject("G2c", g, "variable_set_index_negative", "variable/set with variables [-1]", secVarSet, varSet,
                gr => cfg(gr)["variables"] = Cv(-1)));
            c.Add(Reject("G2d", g, "variable_set_one_index_invalid", "variable/set with variables [0, 2] (two variables)", secVarSet, varSet,
                gr => { cfg(gr)["variables"] = new JObject { ["value"] = new JArray(0, 2) }; values(gr)["2"] = InlineJson(gr, "int", 1); }));
            c.Add(Reject("G2e", g, "variable_set_value_socket_missing", "variable/set without input value \"0\"", secVarSet, varSet,
                gr => Node(gr, S).Remove("values")));
            c.Add(Reject("G2f", g, "variable_set_value_socket_wrong_type", "variable/set with float value for an int variable", secVarSet, varSet,
                gr => values(gr)["0"] = InlineJson(gr, "float", 5.0)));

            // variable/interpolate
            const string secVarInterp = "Operations › variable/interpolate";
            Action<InvalidGraphBuilder> varInterp = b =>
            {
                b.Variable("float3", 0.0, 0.0, 0.0);
                b.Node("variable/interpolate", VarInterpolateValues(b, b.Inline("float3", 1.0, 1.0, 1.0)),
                    Config(("variable", 0), ("useSlerp", false)));
                b.Type("int");
                b.Type("bool");
                b.Type("float2");
            };
            c.Add(Reject("G3a", g, "variable_interpolate_int_variable", "variable/interpolate on an int variable", secVarInterp, varInterp,
                gr => { Var(gr)["type"] = TypeIndex(gr, "int"); Var(gr)["value"] = new JArray(0); values(gr)["value"] = InlineJson(gr, "int", 1); }));
            c.Add(Reject("G3b", g, "variable_interpolate_bool_variable", "variable/interpolate on a bool variable", secVarInterp, varInterp,
                gr => { Var(gr)["type"] = TypeIndex(gr, "bool"); Var(gr)["value"] = new JArray(false); values(gr)["value"] = InlineJson(gr, "bool", true); }));
            c.Add(Reject("G3c", g, "variable_interpolate_useSlerp_missing", "variable/interpolate without useSlerp", secVarInterp, varInterp,
                gr => cfg(gr).Remove("useSlerp")));
            c.Add(Reject("G3d", g, "variable_interpolate_useSlerp_on_float3", "variable/interpolate with useSlerp true on a float3 variable", secVarInterp, varInterp,
                gr => cfg(gr)["useSlerp"] = Cv(true)));
            c.Add(Reject("G3e", g, "variable_interpolate_useSlerp_not_bool", "variable/interpolate with useSlerp [0]", secVarInterp, varInterp,
                gr => cfg(gr)["useSlerp"] = Cv(0)));
            c.Add(Reject("G3f", g, "variable_interpolate_index_out_of_range", "variable/interpolate with variable index 1 (one variable)", secVarInterp, varInterp,
                gr => cfg(gr)["variable"] = Cv(1)));
            c.Add(Reject("G3g", g, "variable_interpolate_p1_missing", "variable/interpolate without input p1", secVarInterp, varInterp,
                gr => values(gr).Remove("p1")));
            c.Add(Reject("G3h", g, "variable_interpolate_value_wrong_type", "variable/interpolate with float2 value for a float3 variable", secVarInterp, varInterp,
                gr => values(gr)["value"] = InlineJson(gr, "float2", 1.0, 1.0)));

            // pointer/get
            const string secPtrGet = "Operations › pointer/get";
            Action<InvalidGraphBuilder> ptrGet = b =>
            {
                b.Node("pointer/get", config: Config(("pointer", "/nodes/0/translation"), ("type", b.Type("float3"))));
                b.Type("int");
            };
            Action<InvalidGraphBuilder> ptrGetParam = b =>
            {
                b.Node("pointer/get", Values(("nodeIndex", b.Inline("int", 0))),
                    Config(("pointer", "/nodes/[nodeIndex]/translation"), ("type", b.Type("float3"))));
                b.Type("float");
            };
            c.Add(Reject("G4a", g, "pointer_get_pointer_missing", "pointer/get without pointer", secPtrGet, ptrGet,
                gr => cfg(gr).Remove("pointer")));
            c.Add(Reject("G4b", g, "pointer_get_pointer_not_string", "pointer/get with pointer [5]", secPtrGet, ptrGet,
                gr => cfg(gr)["pointer"] = Cv(5)));
            c.Add(Reject("G4c", g, "pointer_get_type_missing", "pointer/get without type", secPtrGet, ptrGet,
                gr => cfg(gr).Remove("type")));
            c.Add(Reject("G4d", g, "pointer_get_type_out_of_range", "pointer/get with type index equal to types length", secPtrGet, ptrGet,
                gr => cfg(gr)["type"] = Cv(Arr(gr, "types").Count)));
            c.Add(Reject("G4e", g, "pointer_get_type_negative", "pointer/get with type index -1", secPtrGet, ptrGet,
                gr => cfg(gr)["type"] = Cv(-1)));
            c.Add(Reject("G4f", g, "pointer_get_type_fractional", "pointer/get with type index 0.5", secPtrGet, ptrGet,
                gr => cfg(gr)["type"] = Cv(0.5)));
            c.Add(Reject("G4g", g, "pointer_get_template_socket_missing", "pointer/get template [nodeIndex] without input value", secPtrGet, ptrGetParam,
                gr => Node(gr, S).Remove("values")));
            c.Add(Reject("G4h", g, "pointer_get_int_param_wrong_type", "pointer/get template [nodeIndex] with float input value", secPtrGet, ptrGetParam,
                gr => values(gr)["nodeIndex"] = InlineJson(gr, "float", 0.0)));
            c.Add(Reject("G4i", g, "pointer_get_ref_param_wrong_type", "pointer/get template {node} with int input value", secPtrGet, ptrGetParam,
                gr => { cfg(gr)["pointer"] = Cv("/nodes/{node}/translation"); RenameProperty(values(gr), "nodeIndex", "node"); }));

            // Invalid template syntax, mostly the examples of the spec.
            var templates = new (string name, string pointer)[]
            {
                ("invalid_escape", "/nodes/0/extras/~2"),
                ("duplicate_int_params", "/nodes/[index]/weights/[index]"),
                ("duplicate_mixed_params", "/nodes/{index}/weights/[index]"),
                ("lone_square_bracket", "/nodes/[/scale"),
                ("lone_curly_bracket", "/nodes/{/scale"),
                ("empty_square_param", "/nodes/[]/scale"),
                ("empty_curly_param", "/nodes/{}/scale"),
                ("unterminated_square_param", "/nodes/[index/scale"),
                ("unterminated_curly_param", "/nodes/{index/scale"),
                ("square_in_square_param", "/nodes/[i[ndex]/scale"),
                ("curly_in_square_param", "/nodes/[i{ndex]/scale"),
                ("square_in_curly_param", "/nodes/{i[ndex}/scale"),
                ("curly_in_curly_param", "/nodes/{i{ndex}/scale"),
                ("closing_square_in_square_param", "/nodes/[i]ndex]/scale"),
                ("closing_curly_in_square_param", "/nodes/[i}ndex]/scale"),
                ("closing_square_in_curly_param", "/nodes/{i]ndex}/scale"),
                ("closing_curly_in_curly_param", "/nodes/{i}ndex}/scale"),
                ("odd_literal_open_square", "/nodes/0/extras/[[i[ndex]]"),
                ("odd_literal_open_curly", "/nodes/0/extras/{{i{ndex}}"),
                ("odd_literal_close_square", "/nodes/0/extras/[[index]"),
                ("odd_literal_close_curly", "/nodes/0/extras/{{index}"),
                ("missing_leading_slash", "nodes/0/scale"),
            };
            for (int i = 0; i < templates.Length; i++)
            {
                var (name, pointer) = templates[i];
                c.Add(Reject($"G5-{i + 1:00}", g, "pointer_template_" + name, $"pointer/get with invalid template \"{pointer}\"", SecPtr, ptrGet,
                    gr => cfg(gr)["pointer"] = Cv(pointer)));
            }

            // pointer/set
            const string secPtrSet = "Operations › pointer/set";
            Action<InvalidGraphBuilder> ptrSet = b =>
            {
                b.Node("pointer/set", Values(("value", b.Inline("float3", 1.0, 2.0, 3.0))),
                    Config(("pointer", "/nodes/0/translation"), ("type", b.Type("float3"))));
                b.Type("float2");
            };
            c.Add(Reject("G6a", g, "pointer_set_template_int_param_value", "pointer/set template uses [value]", secPtrSet, ptrSet,
                gr => cfg(gr)["pointer"] = Cv("/nodes/[value]/translation")));
            c.Add(Reject("G6b", g, "pointer_set_template_ref_param_value", "pointer/set template uses {value}", secPtrSet, ptrSet,
                gr => cfg(gr)["pointer"] = Cv("/nodes/{value}/translation")));
            c.Add(Reject("G6c", g, "pointer_set_value_missing", "pointer/set without input value", secPtrSet, ptrSet,
                gr => Node(gr, S).Remove("values")));
            c.Add(Reject("G6d", g, "pointer_set_value_wrong_type", "pointer/set with float2 value for type float3", secPtrSet, ptrSet,
                gr => values(gr)["value"] = InlineJson(gr, "float2", 1.0, 2.0)));

            // pointer/interpolate
            const string secPtrInterp = "Operations › pointer/interpolate";
            Action<InvalidGraphBuilder> ptrInterp = b =>
            {
                b.Node("pointer/interpolate", VarInterpolateValues(b, b.Inline("float3", 1.0, 2.0, 3.0)),
                    Config(("pointer", "/nodes/0/translation"), ("type", b.Type("float3"))));
                b.Type("int");
                b.Type("bool");
            };
            foreach (var (id, param) in new[] { ("G7a", "[value]"), ("G7b", "[duration]"), ("G7c", "{p1}"), ("G7d", "[p2]") })
            {
                var paramName = param.Substring(1, param.Length - 2);
                c.Add(Reject(id, g, "pointer_interpolate_template_param_" + paramName, $"pointer/interpolate template uses {param}", secPtrInterp, ptrInterp,
                    gr => cfg(gr)["pointer"] = Cv($"/nodes/{param}/translation")));
            }
            c.Add(Reject("G7e", g, "pointer_interpolate_type_int", "pointer/interpolate with type int", secPtrInterp, ptrInterp,
                gr => { cfg(gr)["type"] = Cv(TypeIndex(gr, "int")); values(gr)["value"] = InlineJson(gr, "int", 1); }));
            c.Add(Reject("G7f", g, "pointer_interpolate_type_bool", "pointer/interpolate with type bool", secPtrInterp, ptrInterp,
                gr => { cfg(gr)["type"] = Cv(TypeIndex(gr, "bool")); values(gr)["value"] = InlineJson(gr, "bool", true); }));
            c.Add(Reject("G7g", g, "pointer_interpolate_duration_missing", "pointer/interpolate without input duration", secPtrInterp, ptrInterp,
                gr => values(gr).Remove("duration")));

            // event/receive, event/send (event 0 is the signal event, event 1 the custom one)
            const string secReceive = "Operations › event/receive";
            const string secSend = "Operations › event/send";
            Action<InvalidGraphBuilder> receive = b =>
            {
                b.Event("custom/a", Values(("v", b.Inline("int", 1))));
                b.Node("event/receive", config: Config(("event", 1)));
            };
            Action<InvalidGraphBuilder> send = b =>
            {
                b.Event("custom/a", Values(("v", b.Inline("int", 1))));
                b.Node("event/send", Values(("v", b.Inline("int", 5))), Config(("event", 1)));
                b.Type("float");
            };
            c.Add(Reject("G8a", g, "event_receive_config_missing", "event/receive without configuration", secReceive, receive,
                gr => Node(gr, S).Remove("configuration")));
            c.Add(Reject("G8b", g, "event_receive_index_out_of_range", "event/receive with event index equal to events length", secReceive, receive,
                gr => cfg(gr)["event"] = Cv(Arr(gr, "events").Count)));
            c.Add(Reject("G8c", g, "event_receive_index_negative", "event/receive with event index -1", secReceive, receive,
                gr => cfg(gr)["event"] = Cv(-1)));
            c.Add(Reject("G9a", g, "event_send_config_missing", "event/send without configuration", secSend, send,
                gr => Node(gr, S).Remove("configuration")));
            c.Add(Reject("G9b", g, "event_send_index_out_of_range", "event/send with event index equal to events length", secSend, send,
                gr => cfg(gr)["event"] = Cv(Arr(gr, "events").Count)));
            c.Add(Reject("G9c", g, "event_send_value_missing", "event/send without the event's value socket v", secSend, send,
                gr => Node(gr, S).Remove("values")));
            c.Add(Reject("G9d", g, "event_send_value_wrong_type", "event/send with float value for an int event socket", secSend, send,
                gr => values(gr)["v"] = InlineJson(gr, "float", 5.0)));

            // math/switch
            const string secMathSwitch = "Operations › math/switch";
            Action<InvalidGraphBuilder> mathSwitch = b =>
            {
                b.Node("math/switch", Values(
                        ("selection", b.Inline("int", 1)),
                        ("default", b.Inline("float", 0.0)),
                        ("1", b.Inline("float", 1.0)),
                        ("2", b.Inline("float", 2.0))),
                    Config(("cases", new JArray(1, 2))));
            };
            c.Add(Reject("H1a", g, "math_switch_selection_missing", "math/switch without selection", secMathSwitch, mathSwitch,
                gr => values(gr).Remove("selection")));
            c.Add(Reject("H1b", g, "math_switch_selection_float", "math/switch with float selection", secMathSwitch, mathSwitch,
                gr => values(gr)["selection"] = InlineJson(gr, "float", 1.0)));
            c.Add(Reject("H1c", g, "math_switch_default_missing", "math/switch without default", secMathSwitch, mathSwitch,
                gr => values(gr).Remove("default")));
            c.Add(Reject("H1d", g, "math_switch_case_socket_missing", "math/switch with cases [1, 2] but without input \"2\"", secMathSwitch, mathSwitch,
                gr => values(gr).Remove("2")));
            c.Add(Reject("H1e", g, "math_switch_case_type_differs", "math/switch with int case \"2\" but float default", secMathSwitch, mathSwitch,
                gr => values(gr)["2"] = InlineJson(gr, "int", 2)));

            // flow/switch
            const string secFlowSwitch = "Operations › flow/switch";
            Action<InvalidGraphBuilder> flowSwitch = b =>
            {
                b.Node("flow/switch", Values(("selection", b.Inline("int", 1))), Config(("cases", new JArray(1, 2))));
                b.Type("float");
            };
            c.Add(Reject("H2a", g, "flow_switch_selection_float", "flow/switch with float selection", secFlowSwitch, flowSwitch,
                gr => values(gr)["selection"] = InlineJson(gr, "float", 1.0)));
            c.Add(Reject("H2b", g, "flow_switch_selection_missing", "flow/switch without selection", secFlowSwitch, flowSwitch,
                gr => Node(gr, S).Remove("values")));

            // debug/log
            c.Add(Reject("H3", g, "debug_log_param_socket_missing", "debug/log message \"value = {a}\" without input a", "Operations › debug/log",
                b => b.Node("debug/log", Values(("a", b.Inline("int", 1))), Config(("severity", 0), ("message", "value = {a}"))),
                gr => Node(gr, S).Remove("values")));
        }

        #endregion

        #region helpers

        private static InvalidGraphCase Make(string id, string group, string name, string title, string section, ExpectedOutcome expected,
            bool schemaAssert, Action<InvalidGraphBuilder> setup, Action<JObject> mutate)
        {
            return new InvalidGraphCase
            {
                Id = id, Group = group, Name = name, Title = title, SpecSection = section,
                Expected = expected, SchemaAssert = schemaAssert, Setup = setup, Mutate = mutate,
            };
        }

        private static InvalidGraphCase Reject(string id, string group, string name, string title, string section,
            Action<InvalidGraphBuilder> setup, Action<JObject> mutate)
            => Make(id, group, name, title, section, ExpectedOutcome.RejectGraph, false, setup, mutate);

        /// <summary> Structural assert of the Validation section, see <see cref="InvalidGraphCase.SchemaAssert"/>. </summary>
        private static InvalidGraphCase Assert(string id, string group, string name, string title, string section,
            Action<InvalidGraphBuilder> setup, Action<JObject> mutate)
            => Make(id, group, name, title, section, ExpectedOutcome.RejectExtension, true, setup, mutate);

        private static InvalidGraphCase RejectExt(string id, string group, string name, string title, string section,
            Action<InvalidGraphBuilder> setup, Action<JObject> mutateExtension)
        {
            var c = Make(id, group, name, title, section, ExpectedOutcome.RejectExtension, false, setup, null);
            c.MutateExtension = mutateExtension;
            return c;
        }

        /// <summary> S = math/add(a: 1, b: 2) </summary>
        private static void AddInts(InvalidGraphBuilder b)
            => b.Node("math/add", Values(("a", b.Inline("int", 1)), ("b", b.Inline("int", 2))));

        /// <summary> S = math/add(a: 1.0, b: 2.0) </summary>
        private static void AddFloats(InvalidGraphBuilder b)
            => b.Node("math/add", Values(("a", b.Inline("float", 1.0)), ("b", b.Inline("float", 2.0))));

        /// <summary> Variable 0 with the given type/value, S = variable/get(variable: 0) </summary>
        private static Action<InvalidGraphBuilder> VarCase(string signature, params object[] value) => b =>
        {
            b.Variable(signature, value);
            b.Node("variable/get", config: Config(("variable", 0)));
        };

        private static JObject VarInterpolateValues(InvalidGraphBuilder b, JObject value) => Values(
            ("value", value),
            ("duration", b.Inline("float", 1.0)),
            ("p1", b.Inline("float2", 0.25, 0.1)),
            ("p2", b.Inline("float2", 0.25, 1.0)));

        private static JArray Arr(JObject graph, string property) => (JArray)graph[property];
        private static JObject Node(JObject graph, int index) => (JObject)graph["nodes"][index];
        private static JObject Value(JObject graph, int node, string socket) => (JObject)Node(graph, node)["values"][socket];
        private static JObject Var(JObject graph) => (JObject)graph["variables"][0];

        private static JObject Decl(JObject graph, string op)
            => Arr(graph, "declarations").OfType<JObject>().First(d => (string)d["op"] == op);

        private static int TypeIndex(JObject graph, string signature)
        {
            var types = Arr(graph, "types");
            for (int i = 0; i < types.Count; i++)
                if ((string)types[i]["signature"] == signature)
                    return i;
            throw new InvalidOperationException($"Type {signature} must be created in the case setup");
        }

        private static JObject InlineJson(JObject graph, string signature, params object[] value)
            => new JObject { ["type"] = TypeIndex(graph, signature), ["value"] = new JArray(value) };

        /// <summary> Single configuration value, e.g. { "value": [ 1 ] }. </summary>
        private static JObject Cv(object value) => new JObject { ["value"] = new JArray(value) };

        private static void RenameProperty(JObject o, string from, string to)
        {
            var value = o[from];
            o.Remove(from);
            o[to] = value;
        }

        #endregion
    }
}
