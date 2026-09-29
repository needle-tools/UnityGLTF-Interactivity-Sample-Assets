using System;
using Newtonsoft.Json.Linq;

namespace Khronos_Test_Export.InvalidGraphs
{
    public enum ExpectedOutcome
    {
        /// <summary> The graph is invalid; no node of it may run. </summary>
        RejectGraph,
        /// <summary> The whole KHR_interactivity extension object is invalid; no graph may run. </summary>
        RejectExtension,
    }

    /// <summary>
    /// One test asset: a valid graph (<see cref="Setup"/>) with exactly one rule broken afterwards on JSON level
    /// (<see cref="Mutate"/> / <see cref="MutateExtension"/>). Ids match glTF-Interactivity-Tests/SpecInvalidGraphCases.md.
    ///
    /// Every case sends <c>test/onFailed</c> on start, so an implementation that wrongly runs the graph reports a failure.
    /// Valid graphs that look suspicious are regular test cases (OpTests/Graph), not part of this set.
    /// </summary>
    public class InvalidGraphCase
    {
        public string Id;
        public string Group;
        public string Name;
        public string Title;
        public string SpecSection;
        public ExpectedOutcome Expected;

        /// <summary>
        /// The rule is a structural "assert" of the spec's Validation (Informative) section, which rejects the
        /// extension, while the normative text for some of these only says the graph is rejected. Either way no graph may run.
        /// </summary>
        public bool SchemaAssert;

        public Action<InvalidGraphBuilder> Setup;
        public Action<JObject> Mutate;
        public Action<JObject> MutateExtension;

        public string FileName => $"{Id}_{Name}.gltf";

        public string SignalEventId => InvalidGraphBuilder.FailedEventId;

        public string LogMessage => $"FAILED [{Id}] {Title}: this graph MUST be rejected (spec: {SpecSection}).";

        /// <param name="applyMutation">false builds the unbroken base graph, used to verify the generator itself.</param>
        public JObject BuildGltf(bool applyMutation = true)
        {
            var graph = BuildGraph(SignalEventId, LogMessage, Setup, applyMutation ? Mutate : null);
            var extension = new JObject { ["graphs"] = new JArray(graph) };
            if (applyMutation)
                MutateExtension?.Invoke(extension);

            return new JObject
            {
                ["asset"] = new JObject
                {
                    ["version"] = "2.0",
                    ["generator"] = "Khronos_Test_Export InvalidGraphs",
                    ["extras"] = new JObject
                    {
                        ["caseId"] = Id,
                        ["title"] = Title,
                        ["specSection"] = SpecSection,
                        ["expectedOutcome"] = ExpectedOutcomeString,
                        ["schemaAssert"] = SchemaAssert,
                        ["signalEvent"] = SignalEventId,
                        ["pass"] = PassCondition,
                    },
                },
                ["extensionsUsed"] = new JArray("KHR_interactivity"),
                ["scene"] = 0,
                ["scenes"] = new JArray(new JObject { ["nodes"] = new JArray(0) }),
                // Target for pointer templates like /nodes/0/translation.
                ["nodes"] = new JArray(new JObject { ["name"] = "Target" }),
                ["extensions"] = new JObject { ["KHR_interactivity"] = extension },
            };
        }

        public string ExpectedOutcomeString => Expected == ExpectedOutcome.RejectGraph ? "rejectGraph" : "rejectExtension";

        public string PassCondition =>
            $"the graph is rejected: no node runs and the custom event '{InvalidGraphBuilder.FailedEventId}' is never sent";

        public static JObject BuildGraph(string signalEventId, string logMessage, Action<InvalidGraphBuilder> setup, Action<JObject> mutate)
        {
            var builder = new InvalidGraphBuilder(signalEventId, logMessage);
            setup?.Invoke(builder);
            var graph = builder.ToJson();
            mutate?.Invoke(graph);
            return graph;
        }
    }
}
