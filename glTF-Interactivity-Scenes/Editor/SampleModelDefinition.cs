using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor
{
    /// <summary>
    /// Describes one Unity scene that gets published as a model in the Khronos glTF Interactivity
    /// sample assets repository. One asset per model, stored next to the scene it exports.
    /// </summary>
    [CreateAssetMenu(fileName = "New Sample Model", menuName = "glTF Interactivity/Sample Model Definition")]
    public class SampleModelDefinition : ScriptableObject
    {
        [Tooltip("The scene that gets exported.")]
        public SceneAsset scene;

        [Tooltip("Folder name inside the sample repo's Models/ directory. Empty uses the scene name.")]
        public string modelFolderName = "";

        [Tooltip("Base name of the exported .glb / .gltf files. Empty uses the scene name.")]
        public string fileName = "";

        [Tooltip("Display name for model-index.json and the showcase table. Empty uses the folder name.")]
        public string label = "";

        [Tooltip("Short description of what the user can interact with.")]
        [TextArea(2, 5)]
        public string description = "";

        public string[] tags = { "interactivity", "showcase" };

        [Tooltip("Export <folder>/glTF-Binary/<file>.glb")]
        public bool exportGlb = true;

        [Tooltip("Export <folder>/glTF/<file>.gltf plus its .bin")]
        public bool exportGltf = true;

        [Tooltip("Include in model-index.json, the showcase table, and generate a README for this model.")]
        public bool includeInIndex = true;

        [Tooltip("Optional \"Source\" section of the generated README (Markdown), e.g. where third-party assets " +
                 "used by this model come from and what was changed. Leave empty to omit the section.")]
        [TextArea(2, 8)]
        public string source = "";

        [Tooltip("The \"License Information\" section of the generated README (Markdown), following the wording " +
                 "used by the Khronos glTF sample models. Credit the authors of any third-party assets here.")]
        [TextArea(2, 8)]
        public string licenseInformation = "Donated by Needle for glTF testing.";

        [Tooltip("License sentence appended to the \"License Information\" section. Leave empty to omit it.")]
        public string licenseStatement =
            "This model is licensed under a Creative Commons Attribution 4.0 International License.";

        // Kept up to date by OnValidate so a missing/moved scene can still be reported by name.
        [SerializeField, HideInInspector] private string cachedScenePath = "";

        public string ScenePath => scene ? AssetDatabase.GetAssetPath(scene) : cachedScenePath;

        public string SceneName
        {
            get
            {
                var path = ScenePath;
                return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
            }
        }

        public string ModelFolderName => string.IsNullOrWhiteSpace(modelFolderName) ? SceneName : modelFolderName.Trim();
        public string FileName => string.IsNullOrWhiteSpace(fileName) ? SceneName : fileName.Trim();
        public string Label => string.IsNullOrWhiteSpace(label) ? ModelFolderName : label.Trim();
        public string Description => (description ?? "").Trim();

        private void OnValidate()
        {
            if (scene)
                cachedScenePath = AssetDatabase.GetAssetPath(scene);
        }

        /// <summary>Returns null when this definition can be exported, otherwise the reason it can't.</summary>
        public string Validate()
        {
            if (!scene)
                return string.IsNullOrEmpty(cachedScenePath)
                    ? "No scene assigned."
                    : $"Scene is missing (was \"{cachedScenePath}\").";
            if (string.IsNullOrEmpty(ModelFolderName))
                return "Model folder name could not be determined.";
            if (string.IsNullOrEmpty(FileName))
                return "Export file name could not be determined.";
            if (!exportGlb && !exportGltf)
                return "Neither GLB nor glTF export is enabled.";
            return null;
        }

        [CustomEditor(typeof(SampleModelDefinition))]
        public class Inspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var definition = (SampleModelDefinition) target;

                EditorGUILayout.Space();
                var error = definition.Validate();
                if (error != null)
                    EditorGUILayout.HelpBox(error, MessageType.Warning);
                else
                    EditorGUILayout.HelpBox(
                        $"Models/{definition.ModelFolderName}/\n" +
                        (definition.exportGltf ? $"    glTF/{definition.FileName}.gltf (+ .bin)\n" : "") +
                        (definition.exportGlb ? $"    glTF-Binary/{definition.FileName}.glb\n" : "") +
                        "    screenshot/screenshot.png (maintained by hand)",
                        MessageType.None);

                var index = SampleModelIndex.Find();
                if (!index)
                {
                    EditorGUILayout.HelpBox(
                        "No SampleModelIndex asset found in the project. It is needed to resolve the output folder.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUI.DisabledScope(error != null))
                {
                    if (GUILayout.Button("Export This Model"))
                        SampleModelExporter.ExportAndRegenerate(index, new[] { definition });
                }
            }
        }
    }
}
