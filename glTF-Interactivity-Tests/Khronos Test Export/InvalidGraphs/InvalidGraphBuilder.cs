using System.Linq;
using Newtonsoft.Json.Linq;

namespace Khronos_Test_Export.InvalidGraphs
{
    /// <summary>
    /// Minimal, spec-conform KHR_interactivity graph writer working directly on JSON. Unlike the UnityGLTF
    /// interactivity exporter it performs no validation, so a case can take the valid graph and break exactly
    /// one rule afterwards (see <see cref="InvalidGraphCase.Mutate"/>).
    ///
    /// Every graph starts with the result signal chain (nodes 0..2, event 0), which uses no types:
    /// <c>event/onStart → debug/log → event/send(test/onFailed)</c>.
    /// Subject nodes are appended afterwards, so mutations of node order or types never touch the chain.
    /// Empty arrays and objects are never emitted, as required by the spec.
    /// </summary>
    public class InvalidGraphBuilder
    {
        public const string FailedEventId = "test/onFailed";

        public readonly JArray Types = new JArray();
        public readonly JArray Variables = new JArray();
        public readonly JArray Events = new JArray();
        public readonly JArray Declarations = new JArray();
        public readonly JArray Nodes = new JArray();

        /// <summary> Index of the first node after the signal chain. </summary>
        public const int FirstSubjectNode = 3;

        public InvalidGraphBuilder(string signalEventId, string logMessage)
        {
            Event(signalEventId);
            Node("event/onStart", flows: Flows(("out", 1, null)));
            Node("debug/log",
                config: Config(("severity", 0), ("message", EscapeLogMessage(logMessage))),
                flows: Flows(("out", 2, null)));
            Node("event/send", config: Config(("event", 0)));
        }

        public int Type(string signature)
        {
            for (int i = 0; i < Types.Count; i++)
                if ((string)Types[i]["signature"] == signature)
                    return i;
            Types.Add(new JObject { ["signature"] = signature });
            return Types.Count - 1;
        }

        /// <summary> Returns the declaration of a spec operation, adding it if needed. </summary>
        public int Decl(string op)
        {
            for (int i = 0; i < Declarations.Count; i++)
                if ((string)Declarations[i]["op"] == op && Declarations[i]["extension"] == null)
                    return i;
            Declarations.Add(new JObject { ["op"] = op });
            return Declarations.Count - 1;
        }

        /// <summary> Always adds the declaration, e.g. for extension operations. </summary>
        public int AddDecl(JObject declaration)
        {
            Declarations.Add(declaration);
            return Declarations.Count - 1;
        }

        public int Node(string op, JObject values = null, JObject config = null, JObject flows = null)
        {
            return AddNode(Decl(op), values, config, flows);
        }

        public int AddNode(int declaration, JObject values = null, JObject config = null, JObject flows = null)
        {
            var node = new JObject { ["declaration"] = declaration };
            if (config != null && config.Count > 0) node["configuration"] = config;
            if (values != null && values.Count > 0) node["values"] = values;
            if (flows != null && flows.Count > 0) node["flows"] = flows;
            Nodes.Add(node);
            return Nodes.Count - 1;
        }

        public int Variable(string signature, params object[] value)
        {
            var variable = new JObject { ["type"] = Type(signature) };
            if (value.Length > 0) variable["value"] = new JArray(value);
            Variables.Add(variable);
            return Variables.Count - 1;
        }

        public int Event(string id = null, JObject values = null)
        {
            var ev = new JObject();
            if (id != null) ev["id"] = id;
            if (values != null && values.Count > 0) ev["values"] = values;
            Events.Add(ev);
            return Events.Count - 1;
        }

        /// <summary> Inline constant input value (or typed event/variable value). </summary>
        public JObject Inline(string signature, params object[] value)
        {
            return new JObject { ["type"] = Type(signature), ["value"] = new JArray(value) };
        }

        /// <summary> Type-default input value. </summary>
        public JObject TypeDefault(string signature)
        {
            return new JObject { ["type"] = Type(signature) };
        }

        /// <summary> Reference to another node's output value socket. </summary>
        public static JObject Ref(int node, string socket = null)
        {
            var r = new JObject { ["node"] = node };
            if (socket != null) r["socket"] = socket;
            return r;
        }

        public static JObject Values(params (string id, JObject source)[] values)
        {
            var o = new JObject();
            foreach (var (id, source) in values) o[id] = source;
            return o;
        }

        /// <summary> Configuration object; scalar values are wrapped into a one-element array, arrays are used as-is. </summary>
        public static JObject Config(params (string id, object value)[] properties)
        {
            var o = new JObject();
            foreach (var (id, value) in properties)
                o[id] = new JObject { ["value"] = value is JArray array ? array : new JArray(value) };
            return o;
        }

        public static JObject Flows(params (string id, int node, string socket)[] flows)
        {
            var o = new JObject();
            foreach (var (id, node, socket) in flows)
            {
                var f = new JObject { ["node"] = node };
                if (socket != null) f["socket"] = socket;
                o[id] = f;
            }
            return o;
        }

        public JObject ToJson()
        {
            var graph = new JObject();
            if (Types.Count > 0) graph["types"] = Types;
            if (Variables.Count > 0) graph["variables"] = Variables;
            if (Events.Count > 0) graph["events"] = Events;
            if (Declarations.Count > 0) graph["declarations"] = Declarations;
            if (Nodes.Count > 0) graph["nodes"] = Nodes;
            return graph;
        }

        /// <summary>
        /// Curly brackets in debug/log messages define value sockets; literal brackets must be doubled,
        /// otherwise e.g. a pointer template in the message would add a required socket to the log node.
        /// </summary>
        public static string EscapeLogMessage(string message)
        {
            return string.Concat(message.Select(c => c == '{' ? "{{" : c == '}' ? "}}" : c.ToString()));
        }
    }
}
