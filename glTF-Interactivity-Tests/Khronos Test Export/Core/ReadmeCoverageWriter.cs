using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Updates the coverage block (test file and sub-test counts) in &lt;tests root&gt;/README.md.
    /// The README is shared by several exports (test-index, mathtests-index, interglb-index, invalid graphs),
    /// so the counts are taken from the index and oracle files on disk, not from the current export.
    /// Only the text between <see cref="StartMarker"/> and <see cref="EndMarker"/> is replaced.
    /// </summary>
    public static class ReadmeCoverageWriter
    {
        public const string ReadmeFileName = "README.md";
        public const string StartMarker = "<!-- coverage:start -->";
        public const string EndMarker = "<!-- coverage:end -->";

        private const string InvalidIndexPath = "invalid/invalid-index.json";

        private class CoverageSet
        {
            public string label;
            public int testCases;
            public int subTests;
        }

        /// <returns>True if the README was found and is up to date (written or unchanged).</returns>
        public static bool Update(string testsRoot)
        {
            var readmePath = Path.Combine(testsRoot, ReadmeFileName);
            if (!File.Exists(readmePath))
            {
                Debug.LogWarning("README coverage not updated, file not found: " + readmePath);
                return false;
            }

            var bytes = File.ReadAllBytes(readmePath);
            var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var readme = new UTF8Encoding(hasBom).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));

            var start = readme.IndexOf(StartMarker, StringComparison.Ordinal);
            var end = readme.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start < 0 || end < start)
            {
                Debug.LogWarning($"README coverage not updated, markers {StartMarker} / {EndMarker} not found in: {readmePath}");
                return false;
            }

            var newLine = readme.Contains("\r\n") ? "\r\n" : "\n";
            var block = CreateCoverageBlock(testsRoot, newLine);
            var contentStart = start + StartMarker.Length;
            var updated = readme.Substring(0, contentStart) + newLine + block + newLine + readme.Substring(end);
            if (updated == readme)
                return true;

            File.WriteAllBytes(readmePath, (hasBom ? new byte[] { 0xEF, 0xBB, 0xBF } : Array.Empty<byte>())
                .Concat(new UTF8Encoding(false).GetBytes(updated)).ToArray());
            Debug.Log("README coverage updated: " + readmePath);
            return true;
        }

        private static string CreateCoverageBlock(string testsRoot, string newLine)
        {
            var sets = new List<CoverageSet>();

            var testIndex = ReadIndex(testsRoot, "test-index.json");
            if (testIndex != null)
                sets.Add(Count(testsRoot, $"`test-index.json` ({string.Join(", ", Categories(testIndex))})", testIndex));

            var mathIndex = ReadIndex(testsRoot, "mathtests-index.json");
            if (mathIndex != null)
                sets.Add(Count(testsRoot, "`mathtests-index.json`", mathIndex));

            // interglb-index.json holds both the user interaction and the multi-file tests
            var interGlbIndex = ReadIndex(testsRoot, "interglb-index.json");
            if (interGlbIndex != null)
            {
                sets.Add(Count(testsRoot, "`UserInteractions/` (need a simulated hover/select)",
                    interGlbIndex.Where(e => Category(e) == "UserInteractions").ToList()));
                sets.Add(Count(testsRoot, "`InterGlb/` (two files loaded together)",
                    interGlbIndex.Where(e => Category(e) == "InterGlb").ToList()));
            }

            var invalidCount = ReadIndex(testsRoot, InvalidIndexPath)?.Count ?? 0;

            var sb = new StringBuilder();
            sb.Append($"> **Current coverage:** {N(sets.Sum(s => s.testCases))} test files · {N(sets.Sum(s => s.subTests))} sub-tests, plus {N(invalidCount)} invalid-graph cases.").Append(newLine);
            sb.Append(">").Append(newLine);
            sb.Append("> | Set | Test cases | Sub-tests |").Append(newLine);
            sb.Append("> | --- | ---: | ---: |").Append(newLine);
            foreach (var set in sets)
                sb.Append($"> | {set.label} | {N(set.testCases)} | {N(set.subTests)} |").Append(newLine);
            sb.Append($"> | [`invalid/`](#invalid-graphs-invalid) (must be rejected, no sub-tests) | {N(invalidCount)} | — |");
            return sb.ToString();
        }

        private static List<JToken> ReadIndex(string testsRoot, string indexPath)
        {
            var fullPath = Path.Combine(testsRoot, indexPath);
            return File.Exists(fullPath) ? JArray.Parse(File.ReadAllText(fullPath)).ToList() : null;
        }

        private static CoverageSet Count(string testsRoot, string label, List<JToken> entries)
        {
            var set = new CoverageSet { label = label, testCases = entries.Count };
            foreach (var entry in entries)
            {
                var oraclePath = Path.Combine(testsRoot, (string)entry["name"], "test-Json", (string)entry["variants"]["test-Json"]);
                if (!File.Exists(oraclePath))
                {
                    Debug.LogWarning("README coverage: oracle file not found: " + oraclePath);
                    continue;
                }
                var oracle = JObject.Parse(File.ReadAllText(oraclePath));
                set.subTests += oracle["tests"]?.Sum(t => (t["subTests"] as JArray)?.Count ?? 0) ?? 0;
            }
            return set;
        }

        private static string Category(JToken entry) => ((string)entry["name"]).Split('/')[0];

        private static IEnumerable<string> Categories(IEnumerable<JToken> entries) =>
            entries.Select(Category).Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
