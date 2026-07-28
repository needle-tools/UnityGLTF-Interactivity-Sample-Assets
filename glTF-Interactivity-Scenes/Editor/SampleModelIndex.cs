using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor
{
    /// <summary>
    /// The ordered list of models we publish to the Khronos sample assets repository, plus where to
    /// write them. The order of <see cref="models"/> is the order of entries in model-index.json.
    /// </summary>
    [CreateAssetMenu(fileName = "SampleModelIndex", menuName = "glTF Interactivity/Sample Model Index")]
    public class SampleModelIndex : ScriptableObject
    {
        [Tooltip("Path to the sample repo's Models folder. Relative paths are resolved against the Unity project folder.")]
        public string modelsRootPath = "../glTF-Test-Assets-Interactivity/Models";

        [Tooltip("Base URL the generated markdown links to, without a trailing slash. Must point at the Models folder.")]
        public string rawContentBaseUrl =
            "https://raw.GithubUserContent.com/KhronosGroup/glTF-Interactivity-Sample-Assets/main/Models";

        [Tooltip("Viewer URL used for the \"Show\" links. The model URL is appended as ?model=...")]
        public string viewerUrl = "https://gltf-interactivity.needle.tools";

        [Tooltip("Models we export from Unity. Other models in the repo are left untouched by the generator.")]
        public List<SampleModelDefinition> models = new List<SampleModelDefinition>();

        public static string ProjectFolder => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>Absolute path of the sample repo's Models folder.</summary>
        public string ResolveModelsRoot()
        {
            if (string.IsNullOrWhiteSpace(modelsRootPath))
                return null;
            var path = modelsRootPath.Trim();
            if (!Path.IsPathRooted(path))
                path = Path.Combine(ProjectFolder, path);
            return Path.GetFullPath(path);
        }

        public IEnumerable<SampleModelDefinition> ValidModels => models.Where(m => m);

        /// <summary>The single index asset in the project, or null if there is none.</summary>
        public static SampleModelIndex Find()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(SampleModelIndex));
            if (guids.Length == 0)
                return null;
            if (guids.Length > 1)
                Debug.LogWarning($"Found {guids.Length} {nameof(SampleModelIndex)} assets, using the first one.");
            return AssetDatabase.LoadAssetAtPath<SampleModelIndex>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [CustomEditor(typeof(SampleModelIndex))]
        public class Inspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();

                var index = (SampleModelIndex) target;

                EditorGUILayout.Space();
                var root = index.ResolveModelsRoot();
                if (string.IsNullOrEmpty(root))
                    EditorGUILayout.HelpBox("No models root path set.", MessageType.Error);
                else if (!Directory.Exists(root))
                    EditorGUILayout.HelpBox($"Models folder does not exist:\n{root}", MessageType.Error);
                else
                    EditorGUILayout.HelpBox(root, MessageType.None);

                if (GUILayout.Button("Open Export Window"))
                    SampleModelExportWindow.Open();

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(root) || !Directory.Exists(root)))
                {
                    if (GUILayout.Button("Export All Models"))
                        SampleModelExporter.ExportAndRegenerate(index, index.ValidModels.ToArray());

                    if (GUILayout.Button("Regenerate JSON / Markdown Only"))
                        SampleModelIndexWriter.Regenerate(index);
                }
            }
        }
    }
}
