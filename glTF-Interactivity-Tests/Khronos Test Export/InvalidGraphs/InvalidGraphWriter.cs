using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Khronos_Test_Export.InvalidGraphs
{
    /// <summary>
    /// Writes one .gltf (JSON only, no buffers) per <see cref="InvalidGraphCase"/> into &lt;folder&gt;/&lt;group&gt;/,
    /// plus invalid-index.json and README.md for the whole set. Kept free of UnityEditor so it can run outside Unity.
    /// </summary>
    public static class InvalidGraphWriter
    {
        public const string FolderName = "invalid";
        public const string IndexFileName = "invalid-index.json";

        /// <returns>The number of written cases.</returns>
        public static int Write(string folder, IReadOnlyList<InvalidGraphCase> cases)
        {
            Directory.CreateDirectory(folder);

            // Remove previously generated assets so renamed or removed cases don't linger.
            foreach (var group in cases.Select(c => c.Group).Distinct())
            {
                var groupFolder = Path.Combine(folder, group);
                if (!Directory.Exists(groupFolder)) continue;
                foreach (var file in Directory.GetFiles(groupFolder, "*.gltf"))
                    File.Delete(file);
            }

            var index = new JArray();
            foreach (var c in cases)
            {
                var gltf = c.BuildGltf();
                var groupFolder = Path.Combine(folder, c.Group);
                Directory.CreateDirectory(groupFolder);
                WriteJson(Path.Combine(groupFolder, c.FileName), gltf);

                index.Add(new JObject
                {
                    ["label"] = $"{c.Id} {c.Name}",
                    ["name"] = $"{FolderName}/{c.Group}",
                    ["id"] = c.Id,
                    ["title"] = c.Title,
                    ["specSection"] = c.SpecSection,
                    ["expectedOutcome"] = c.ExpectedOutcomeString,
                    ["schemaAssert"] = c.SchemaAssert,
                    ["signalEvent"] = c.SignalEventId,
                    // From the unbroken graph, since some mutations remove the graphs or declarations.
                    ["tags"] = new JArray(UsedOps(c.BuildGltf(false)).Cast<object>().ToArray()),
                    ["variants"] = new JObject { ["glTF"] = c.FileName },
                });
            }

            WriteJson(Path.Combine(folder, IndexFileName), index);
            File.WriteAllText(Path.Combine(folder, "README.md"), CreateReadme(cases));
            return cases.Count;
        }

        private static IEnumerable<string> UsedOps(JObject gltf)
        {
            return gltf["extensions"]["KHR_interactivity"]["graphs"] is JArray graphs
                ? graphs.OfType<JObject>()
                    .SelectMany(g => g["declarations"] as JArray ?? new JArray())
                    .Select(d => d is JObject o ? o["op"] : null)
                    .Where(op => op != null && op.Type == JTokenType.String)
                    .Select(op => (string)op)
                    .Distinct()
                    .OrderBy(op => op)
                : Enumerable.Empty<string>();
        }

        private static void WriteJson(string path, JToken json)
        {
            File.WriteAllText(path, json.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        private static string CreateReadme(IReadOnlyList<InvalidGraphCase> cases)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# KHR_interactivity — Invalid Graph Test Assets");
            sb.AppendLine();
            sb.AppendLine("Each `.gltf` in this folder breaks exactly one validation rule of the");
            sb.AppendLine("[`KHR_interactivity` specification](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc).");
            sb.AppendLine("The files are plain JSON without buffers. Graphs that look suspicious but are valid are regular test assets (`graph/*`).");
            sb.AppendLine();
            sb.AppendLine("> These files are generated (`Sample Scenes/Export Khronos Invalid Graph Tests` in the Unity sample project). Do not hand-edit them.");
            sb.AppendLine();
            sb.AppendLine("## How a case reports its result");
            sb.AppendLine();
            sb.AppendLine("Every graph starts with `event/onStart → debug/log → event/send`:");
            sb.AppendLine();
            sb.AppendLine($"Every case (`rejectGraph`, `rejectExtension`) logs `FAILED [<id>] …` and sends the custom event `{InvalidGraphBuilder.FailedEventId}`.");
            sb.AppendLine("A conformant implementation rejects the graph, so nothing runs. **Pass = the event never arrives** (use a short timeout, one tick is enough).");
            sb.AppendLine();
            sb.AppendLine("An implementation that does not support `KHR_interactivity` at all passes every rejection case trivially;");
            sb.AppendLine("run the regular test assets (including `graph/*`) to make sure the results are meaningful.");
            sb.AppendLine();
            sb.AppendLine("`expectedOutcome` is `rejectExtension` for all structural \"assert\" rules of the spec's Validation section (`schemaAssert: true`).");
            sb.AppendLine("For some of them the normative text only requires rejecting the graph; either way no graph may run.");
            sb.AppendLine();
            sb.AppendLine($"`{IndexFileName}` lists all cases (same format as `test-index.json`, `name` is the folder under `Interactivity/`, `variants.glTF` the file in it).");
            sb.AppendLine("Each `.gltf` also carries its metadata in `asset.extras`.");
            sb.AppendLine();

            foreach (var group in cases.GroupBy(c => c.Group))
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                sb.AppendLine("| Id | File | Expected | Case | Spec |");
                sb.AppendLine("| --- | --- | --- | --- | --- |");
                foreach (var c in group)
                {
                    var expected = c.ExpectedOutcomeString + (c.SchemaAssert ? " †" : "");
                    sb.AppendLine($"| {c.Id} | [`{c.FileName}`]({group.Key}/{c.FileName}) | {expected} | {EscapeTable(c.Title)} | {c.SpecSection} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("† structural assert of the Validation section, see above.");
            return sb.ToString().Replace("\r\n", "\n");
        }

        private static string EscapeTable(string text) => text.Replace("|", "\\|");
    }
}
