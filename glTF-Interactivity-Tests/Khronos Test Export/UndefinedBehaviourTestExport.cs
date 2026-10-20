#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Exports the <see cref="UndefinedBehaviourTestAttribute"/> test cases into their own folder, apart from the
    /// regular test assets: one glb + test-Json + md per case, an undefined-index.json and a README.md.
    /// Uses the TestExporter of the TestExporter scene, like <see cref="TestExportMenu"/>.
    /// </summary>
    public static class UndefinedBehaviourTestExport
    {
        private const string ExportPathPrefsKey = "GLTFUndefinedBehaviourTestExportPath";
        private const string RegularExportPathPrefsKey = "GLTFTestExportPath";
        private const string IndexFileName = "undefined-index";

        [MenuItem("Sample Scenes/Export Khronos Undefined Behaviour Tests")]
        public static void Export()
        {
            var scenePath = TestExportMenu.FindTestExporterScene();
            if (scenePath == null)
            {
                Debug.LogError("Could not find TestExporter.unity in the project.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var path = AskExportPath();
            if (path == null)
                return;

            var previousScenePath = SceneManager.GetActiveScene().path;
            var reopenPrevious = previousScenePath != scenePath && !string.IsNullOrEmpty(previousScenePath);
            var scene = SceneManager.GetActiveScene().path == scenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var exporter = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<TestExporter>(true))
                .FirstOrDefault();
            if (exporter == null)
            {
                Debug.LogError($"{scenePath} contains no {nameof(TestExporter)}.");
                return;
            }

            var cases = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(ITestCase).IsAssignableFrom(t)
                            && t.GetCustomAttribute<UndefinedBehaviourTestAttribute>() != null)
                .Select(t => (ITestCase)Activator.CreateInstance(t))
                .OrderBy(c => c.GetTestName(), StringComparer.Ordinal)
                .ToArray();

            // The README is written first: ExportTest disposes the cases
            WriteReadme(path, cases);
            exporter.ExportTest(cases, true, "UndefinedBehaviour", IndexFileName, path);

            if (reopenPrevious && File.Exists(previousScenePath))
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

            Debug.Log($"<color=#00FF00><b>Exported {cases.Length} undefined behaviour test(s) to {path}</b></color>");
        }

        /// <summary> Asks for the destination, suggesting the last one or a folder next to the regular test export. </summary>
        private static string AskExportPath()
        {
            var folder = EditorPrefs.GetString(ExportPathPrefsKey, "");
            var defaultName = "";
            if (string.IsNullOrEmpty(folder))
            {
                var regular = EditorPrefs.GetString(RegularExportPathPrefsKey, "");
                if (!string.IsNullOrEmpty(regular))
                    folder = Path.GetDirectoryName(Path.GetFullPath(regular).TrimEnd('/', '\\')) ?? "";
                defaultName = "Interactivity-UndefinedBehaviour";
            }

            var path = EditorUtility.SaveFolderPanel("Undefined behaviour test export destination", folder, defaultName);
            if (string.IsNullOrEmpty(path))
                return null;
            EditorPrefs.SetString(ExportPathPrefsKey, path);
            return path;
        }

        private static void WriteReadme(string path, ITestCase[] cases)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# KHR_interactivity – Undefined Behaviour Test Assets");
            sb.AppendLine();
            sb.AppendLine("Scenarios the [`KHR_interactivity` specification](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc)");
            sb.AppendLine("leaves undefined or describes contradictorily. They are **not conformance tests**: there is no single correct result.");
            sb.AppendLine("Their purpose is to compare what different engines do, as input for clarifying the specification.");
            sb.AppendLine();
            sb.AppendLine("> These files are generated (`Sample Scenes/Export Khronos Undefined Behaviour Tests` in the Unity sample project). Do not hand-edit them.");
            sb.AppendLine();
            sb.AppendLine("## How to read the results");
            sb.AppendLine();
            sb.AppendLine("Every observation has one check per possible reading, labelled `A:`, `B:`, ... Normally exactly one of them passes,");
            sb.AppendLine("and that one shows which reading the engine implements. If none passes, the engine does something else;");
            sb.AppendLine("the result variables (`resultVarName` in the test-Json) hold the observed values.");
            sb.AppendLine("Checks without a letter are spec-defined reference values; they make sure the scenario ran as intended.");
            sb.AppendLine();
            sb.AppendLine($"`{IndexFileName}.json` lists all cases in the same format as `test-index.json`.");
            sb.AppendLine();
            sb.AppendLine("| Test | Scenario |");
            sb.AppendLine("| --- | --- |");
            foreach (var testCase in cases)
            {
                var folder = testCase.GetTestName().Replace(" ", "_");
                sb.AppendLine($"| [`{testCase.GetTestName()}`]({folder}) | {testCase.GetTestDescription().Replace("|", "\\|")} |");
            }

            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "README.md"), sb.ToString());
        }
    }
}
#endif
