#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Khronos_Test_Export.InvalidGraphs
{
    /// <summary>
    /// Menu entry that writes the invalid-graph test assets into &lt;test export path&gt;/invalid.
    /// These assets are generated as raw JSON on purpose: the UnityGLTF interactivity exporter validates graphs
    /// and could not produce them.
    /// </summary>
    public static class InvalidGraphExportMenu
    {
        // Same key TestExporter.ExportTest reads the destination folder from.
        private const string ExportPathPrefsKey = "GLTFTestExportPath";

        [MenuItem("Sample Scenes/Export Khronos Invalid Graph Tests")]
        public static void Export()
        {
            var path = EditorPrefs.GetString(ExportPathPrefsKey, "");
            if (string.IsNullOrEmpty(path))
            {
                path = EditorUtility.SaveFolderPanel("Khronos test export destination", "", "");
                if (string.IsNullOrEmpty(path))
                    return;
                EditorPrefs.SetString(ExportPathPrefsKey, path);
            }

            var folder = Path.Combine(path, InvalidGraphWriter.FolderName);
            var count = InvalidGraphWriter.Write(folder, InvalidGraphCases.All());
            Debug.Log($"<color=#00FF00><b>Exported {count} invalid graph test cases</b></color> to: {folder}");
            ReadmeCoverageWriter.Update(path);
        }

        [MenuItem("Sample Scenes/Update Khronos Test README Coverage")]
        public static void UpdateReadmeCoverage()
        {
            var path = EditorPrefs.GetString(ExportPathPrefsKey, "");
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("No Khronos test export path set yet. Export the tests first.");
                return;
            }
            ReadmeCoverageWriter.Update(path);
        }
    }
}
#endif
