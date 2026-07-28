#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Menu entry that exports the Khronos interactivity tests: opens the TestExporter scene and runs
    /// every <see cref="TestCreator"/> in it (the overview tests, the math tests and the inter-glb tests),
    /// each with its own ExportAllInOne / ExportIndividual settings, then restores the previously open scene.
    /// </summary>
    public static class TestExportMenu
    {
        private const string SceneName = "TestExporter";

        // Same key TestExporter.ExportTest reads the destination folder from.
        private const string ExportPathPrefsKey = "GLTFTestExportPath";

        [MenuItem("Sample Scenes/Export Khronos Tests")]
        public static void ExportAll()
        {
            var scenePath = FindTestExporterScene();
            if (scenePath == null)
            {
                Debug.LogError($"Could not find {SceneName}.unity in the project.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var previousScenePath = SceneManager.GetActiveScene().path;
            var reopenPrevious = previousScenePath != scenePath && !string.IsNullOrEmpty(previousScenePath);

            var scene = SceneManager.GetActiveScene().path == scenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Include inactive objects so a disabled exporter hierarchy still exports.
            var creators = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<TestCreator>(true))
                .ToArray();

            if (creators.Length == 0)
            {
                Debug.LogError($"{scenePath} contains no {nameof(TestCreator)} components.");
                return;
            }

            // TestExporter asks for the destination folder whenever the pref is empty, which would pop up
            // once per exporter. Ask once here instead so a menu export runs unattended.
            if (!EnsureExportPath(scene))
                return;

            Debug.Log($"<b><color=#F69012>Exporting Khronos tests</color></b> from {scenePath} " +
                      $"({creators.Length} exporter(s))");

            foreach (var creator in creators)
                creator.ExportTests(creator.ExportAllInOne, creator.ExportIndividual);

            if (reopenPrevious && System.IO.File.Exists(previousScenePath))
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

            Debug.Log($"<color=#00FF00><b>Exported {creators.Length} test exporter(s)</b></color>");
        }

        /// <summary>
        /// Makes sure the destination folder pref TestExporter reads is set, asking once if it isn't.
        /// Returns false when the user cancelled the folder dialog.
        /// </summary>
        private static bool EnsureExportPath(Scene scene)
        {
            if (!string.IsNullOrEmpty(EditorPrefs.GetString(ExportPathPrefsKey, "")))
                return true;

            var exporter = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<TestExporter>(true))
                .FirstOrDefault();

            // testExportPath is stored relative to the Assets folder (that is what the inspector writes).
            var suggestion = "";
            if (exporter && !string.IsNullOrEmpty(exporter.testExportPath))
                suggestion = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, exporter.testExportPath));

            var path = EditorUtility.SaveFolderPanel("Khronos test export destination", suggestion, "");
            if (string.IsNullOrEmpty(path))
                return false;

            EditorPrefs.SetString(ExportPathPrefsKey, path);
            return true;
        }

        private static string FindTestExporterScene()
        {
            return AssetDatabase.FindAssets($"{SceneName} t:SceneAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == SceneName);
        }
    }
}
#endif
