using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGLTF;

namespace Tests.Editor
{
    /// <summary>
    /// Exports <see cref="SampleModelDefinition"/> scenes into the folder layout the Khronos glTF
    /// Interactivity sample assets repository expects:
    /// <code>
    /// Models/&lt;Folder&gt;/glTF/&lt;File&gt;.gltf (+ .bin)
    /// Models/&lt;Folder&gt;/glTF-Binary/&lt;File&gt;.glb
    /// Models/&lt;Folder&gt;/screenshot/screenshot.png   (maintained by hand)
    /// </code>
    /// </summary>
    public static class SampleModelExporter
    {
        public const string GltfVariantFolder = "glTF";
        public const string GlbVariantFolder = "glTF-Binary";
        public const string ScreenshotRelativePath = "screenshot/screenshot.png";

        /// <summary>
        /// Exports the given definitions and then regenerates model-index.json, the showcase table and
        /// the per-model READMEs. This is the entry point every button uses, so that exporting a single
        /// model still leaves the repository consistent.
        /// </summary>
        public static void ExportAndRegenerate(SampleModelIndex index, IReadOnlyList<SampleModelDefinition> definitions)
        {
            if (!index)
            {
                Debug.LogError($"No {nameof(SampleModelIndex)} given.");
                return;
            }

            var modelsRoot = index.ResolveModelsRoot();
            if (string.IsNullOrEmpty(modelsRoot) || !Directory.Exists(modelsRoot))
            {
                Debug.LogError($"Models folder does not exist: {modelsRoot}\n" +
                               $"Fix \"{nameof(index.modelsRootPath)}\" on {index.name}.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var openScenePath = SceneManager.GetActiveScene().path;

            var exported = 0;
            var failed = 0;
            try
            {
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (!definition)
                        continue;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Exporting sample models",
                            $"{definition.ModelFolderName} ({i + 1}/{definitions.Count})",
                            (float) i / Mathf.Max(1, definitions.Count)))
                        break;

                    if (ExportModel(definition, modelsRoot))
                        exported++;
                    else
                        failed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            SampleModelIndexWriter.Regenerate(index);

            if (!string.IsNullOrEmpty(openScenePath) && File.Exists(openScenePath))
                EditorSceneManager.OpenScene(openScenePath, OpenSceneMode.Single);

            if (failed > 0)
                Debug.LogError($"Exported {exported} model(s) to {modelsRoot}, {failed} failed.");
            else
                Debug.Log($"<color=#00FF00><b>Exported {exported} model(s)</b></color> to {modelsRoot}");
        }

        /// <summary>Exports one model. Returns false and logs when the export did not fully succeed.</summary>
        public static bool ExportModel(SampleModelDefinition definition, string modelsRoot)
        {
            var error = definition.Validate();
            if (error != null)
            {
                Debug.LogError($"Cannot export {definition.name}: {error}", definition);
                return false;
            }

            var scenePath = definition.ScenePath;
            var modelFolder = Path.Combine(modelsRoot, definition.ModelFolderName);
            var fileName = definition.FileName;

            Debug.Log($"<b><color=#F69012>Exporting model</color> {definition.ModelFolderName}</b> from {scenePath}");

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not open scene {scenePath}: {e}", definition);
                return false;
            }

            var roots = scene.GetRootGameObjects();
            var transforms = Array.ConvertAll(roots, go => go.transform);

            // Same pre-pass as ExportAllScenes: lets scenes prepare themselves for export.
            foreach (var t in transforms)
            foreach (var exportScene in t.GetComponentsInChildren<IExportScene>(t))
                exportScene.OnBeforeExporting();

            var success = true;
            if (definition.exportGltf)
                success &= Export(transforms, false, fileName, Path.Combine(modelFolder, GltfVariantFolder));
            if (definition.exportGlb)
                success &= Export(transforms, true, fileName, Path.Combine(modelFolder, GlbVariantFolder));

            var screenshot = Path.Combine(modelFolder, ScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(screenshot))
                Debug.LogWarning($"{definition.ModelFolderName} has no screenshot at {screenshot}. " +
                                 "Screenshots are maintained by hand and need to be added manually.", definition);

            return success;
        }

        // Mirrors ExportAllScenes.Export so both flows produce comparable output.
        private static bool Export(Transform[] transforms, bool binary, string fileName, string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            try
            {
                var settings = GLTFSettings.GetOrCreateSettings();
                var exportContext = new ExportContext(settings)
                {
                    TexturePathRetriever = GLTFExportMenu.RetrieveTexturePath
                };

                // GLTFSceneExporter is stateful, so each variant needs its own instance.
                var exporter = new GLTFSceneExporter(transforms, exportContext);

                var resultFile = GLTFSceneExporter.GetFileName(path, fileName, binary ? ".glb" : ".gltf");
                if (binary)
                    exporter.SaveGLB(path, fileName);
                else
                    // The sample repo keeps the glTF variant to two files, so textures go into the
                    // .bin as buffer views instead of being written next to the .gltf.
                    exporter.SaveGLTFandBin(path, fileName, true, true);

                Debug.Log($"\t<color=#00FF00>Exported to</color> {resultFile}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"\tExport of {fileName} to {path} failed: {e}");
                return false;
            }
        }
    }
}
