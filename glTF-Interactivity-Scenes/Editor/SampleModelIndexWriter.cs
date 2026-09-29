using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor
{
    /// <summary>
    /// Regenerates the derived files of the sample assets repository from the <see cref="SampleModelIndex"/>.
    ///
    /// IMPORTANT: the repository can contain models that are not exported from Unity by us. Everything in
    /// here therefore merges instead of overwriting: index entries without a matching
    /// <see cref="SampleModelDefinition"/> are never modified, reordered or removed, and no README is
    /// generated for them.
    /// </summary>
    public static class SampleModelIndexWriter
    {
        public const string IndexFileName = "model-index.json";
        public const string ShowcaseFileName = "Models-showcase.md";
        private const string GeneratedBeginMarker = "<!-- BEGIN GENERATED MODELS -->";
        private const string GeneratedEndMarker = "<!-- END GENERATED MODELS -->";

        public static void Regenerate(SampleModelIndex index)
        {
            if (!index)
            {
                Debug.LogError($"No {nameof(SampleModelIndex)} given.");
                return;
            }

            var root = index.ResolveModelsRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                Debug.LogError($"Models folder does not exist: {root}");
                return;
            }

            var definitions = index.ValidModels.Where(m => m.includeInIndex).ToList();

            var entries = UpdateModelIndex(root, definitions);
            WriteShowcase(index, root, entries);

            foreach (var definition in definitions)
                WriteModelReadme(root, definition);

            UpdateRootReadme(index, root, entries);

            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- model-index.json

        /// <summary>
        /// Merges our definitions into the existing index and returns the merged array. Uses JObject/JArray
        /// rather than a typed model so unknown properties on foreign entries survive round-tripping.
        /// </summary>
        private static JArray UpdateModelIndex(string root, IEnumerable<SampleModelDefinition> definitions)
        {
            var file = Path.Combine(root, IndexFileName);

            JArray entries;
            if (File.Exists(file))
            {
                try
                {
                    entries = JArray.Parse(File.ReadAllText(file));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not parse {file}, aborting to avoid data loss: {e}");
                    return new JArray();
                }
            }
            else
            {
                entries = new JArray();
            }

            var added = 0;
            foreach (var definition in definitions)
            {
                var name = definition.ModelFolderName;
                if (string.IsNullOrEmpty(name))
                    continue;

                var entry = FindEntry(entries, name);
                if (entry == null)
                {
                    entry = new JObject();
                    entries.Add(entry);
                    added++;
                }

                // Assigning to an existing property keeps its position; new properties are appended,
                // so entries that already exist keep their hand-authored key order.
                entry["label"] = definition.Label;
                entry["name"] = name;
                entry["screenshot"] = SampleModelExporter.ScreenshotRelativePath;

                if (string.IsNullOrEmpty(definition.Description))
                    entry.Remove("description");
                else
                    entry["description"] = definition.Description;

                entry["tags"] = new JArray(definition.tags ?? Array.Empty<string>());

                var variants = new JObject();
                if (definition.exportGltf)
                    variants[SampleModelExporter.GltfVariantFolder] = definition.FileName + ".gltf";
                if (definition.exportGlb)
                    variants[SampleModelExporter.GlbVariantFolder] = definition.FileName + ".glb";
                entry["variants"] = variants;
            }

            File.WriteAllText(file, entries.ToString(Formatting.Indented) + Environment.NewLine);

            var foreign = entries.Count - definitions.Count();
            Debug.Log($"Updated {IndexFileName}: {entries.Count} entries ({added} added, " +
                      $"{foreign} not managed by this project and left untouched).");
            return entries;
        }

        private static JObject FindEntry(JArray entries, string name) =>
            entries.OfType<JObject>().FirstOrDefault(e =>
                string.Equals(e["name"]?.ToString(), name, StringComparison.Ordinal));

        // ---------------------------------------------------------------- Models-showcase.md

        /// <summary>
        /// Built from the merged index rather than from our definitions, so models contributed by others
        /// keep their row in the table.
        /// </summary>
        private static void WriteShowcase(SampleModelIndex index, string root, JArray entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# glTF 2.0 Sample Assets");
            sb.AppendLine();
            sb.AppendLine("## Models tagged with **interactivity** and **showcase**");
            sb.AppendLine();
            sb.AppendLine("Models that demonstrate interactive features of glTF.");
            sb.AppendLine();
            sb.AppendLine("| Model   | Description |");
            sb.AppendLine("|---------|-------------|");

            foreach (var entry in entries.OfType<JObject>())
            {
                var name = entry["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    continue;

                var label = entry["label"]?.ToString() ?? name;
                var screenshot = entry["screenshot"]?.ToString() ?? SampleModelExporter.ScreenshotRelativePath;
                var description = EscapeTableCell(entry["description"]?.ToString() ?? "");

                var cell = $"[{label}]({name}/README.md)<br>" +
                           $"[![{label}]({name}/{screenshot})]({name}/README.md)";

                var glb = entry["variants"]?[SampleModelExporter.GlbVariantFolder]?.ToString();
                if (!string.IsNullOrEmpty(glb))
                {
                    var url = GlbUrl(index, name, glb);
                    cell += $"<br>[Show]({index.viewerUrl}?model={url}) – [Download GLB]({url})";
                }

                sb.AppendLine($"| {cell} | {description} |");
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("### Copyright");
            sb.AppendLine();
            sb.AppendLine("&copy; 2025, The Khronos Group and Needle.");
            sb.AppendLine();
            sb.AppendLine("**License:** [Creative Commons Attribtution 4.0 International](https://creativecommons.org/licenses/by/4.0/legalcode)");

            var file = Path.Combine(root, ShowcaseFileName);
            File.WriteAllText(file, sb.ToString());
            Debug.Log($"Updated {ShowcaseFileName}.");
        }

        // ---------------------------------------------------------------- Models/<Name>/README.md

        /// <summary>
        /// Only written for models we own. These are linked from the showcase table and the root README,
        /// so every model needs one. The layout follows the per-model READMEs of the Khronos glTF sample
        /// models (https://github.com/KhronosGroup/glTF-Sample-Models/tree/main/2.0):
        /// title, Screenshot, Description, (Source,) License Information.
        /// </summary>
        private static void WriteModelReadme(string root, SampleModelDefinition definition)
        {
            var name = definition.ModelFolderName;
            var folder = Path.Combine(root, name);
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"Skipping README for {name}: {folder} does not exist (has it been exported yet?).",
                    definition);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# {definition.Label}");
            sb.AppendLine();
            sb.AppendLine("## Screenshot");
            sb.AppendLine();
            sb.AppendLine($"![screenshot]({SampleModelExporter.ScreenshotRelativePath})");
            sb.AppendLine();
            sb.AppendLine("## Description");
            sb.AppendLine();
            sb.AppendLine(string.IsNullOrEmpty(definition.Description)
                ? $"{definition.Label}, an interactive glTF sample using KHR_interactivity."
                : definition.Description);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(definition.source))
            {
                sb.AppendLine("## Source");
                sb.AppendLine();
                sb.AppendLine(definition.source.Trim());
                sb.AppendLine();
            }
            sb.AppendLine("## License Information");
            sb.AppendLine();
            sb.AppendLine(definition.licenseInformation);
            if (!string.IsNullOrWhiteSpace(definition.licenseStatement))
            {
                sb.AppendLine();
                sb.AppendLine(definition.licenseStatement);
            }

            File.WriteAllText(Path.Combine(folder, "README.md"), sb.ToString());
        }

        // ---------------------------------------------------------------- root README.md

        /// <summary>
        /// The root README's model table is hand-written, so we only touch it when it explicitly opts in
        /// via the generated-section markers.
        /// </summary>
        private static void UpdateRootReadme(SampleModelIndex index, string root, JArray entries)
        {
            var readme = Path.Combine(Path.GetDirectoryName(root) ?? root, "README.md");
            if (!File.Exists(readme))
                return;

            var text = File.ReadAllText(readme);
            var begin = text.IndexOf(GeneratedBeginMarker, StringComparison.Ordinal);
            var end = text.IndexOf(GeneratedEndMarker, StringComparison.Ordinal);
            if (begin < 0 || end < begin)
                return;

            var modelsFolder = Path.GetFileName(root);
            var sb = new StringBuilder();
            sb.AppendLine(GeneratedBeginMarker);
            sb.AppendLine();
            sb.AppendLine("| Model | Description |");
            sb.AppendLine("|-------|-------------|");
            foreach (var entry in entries.OfType<JObject>())
            {
                var name = entry["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    continue;
                var label = entry["label"]?.ToString() ?? name;
                var screenshot = entry["screenshot"]?.ToString() ?? SampleModelExporter.ScreenshotRelativePath;
                var description = EscapeTableCell(entry["description"]?.ToString() ?? "");
                sb.AppendLine($"| [![{label}]({modelsFolder}/{name}/{screenshot})]({modelsFolder}/{name}/README.md)" +
                              $"<br>[{label}]({modelsFolder}/{name}/README.md) | {description} |");
            }

            sb.AppendLine();

            var updated = text.Substring(0, begin) + sb + text.Substring(end);
            if (updated != text)
            {
                File.WriteAllText(readme, updated);
                Debug.Log("Updated the generated model section in the repository README.md.");
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string GlbUrl(SampleModelIndex index, string name, string glbFileName) =>
            $"{index.rawContentBaseUrl}/{name}/{SampleModelExporter.GlbVariantFolder}/{glbFileName}";

        private static string EscapeTableCell(string value) =>
            value.Replace("|", "\\|").Replace("\r\n", "<br>").Replace("\n", "<br>").Trim();
    }
}
