using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor
{
    /// <summary>
    /// Lets you pick which of the Khronos sample models to re-export. Regenerating the index and markdown
    /// always covers all models, so a partial export still leaves the repository consistent.
    /// </summary>
    public class SampleModelExportWindow : EditorWindow
    {
        private const string SelectionPrefsKey = "khronosSampleModelsExportSelection";

        private SampleModelIndex index;
        private readonly HashSet<string> deselected = new HashSet<string>();
        private Vector2 scroll;

        [MenuItem("Sample Scenes/Export Khronos Sample Models")]
        public static void Open()
        {
            var window = GetWindow<SampleModelExportWindow>();
            window.titleContent = new GUIContent("Khronos Sample Models");
            window.minSize = new Vector2(420, 320);
            window.Show();
        }

        private void OnEnable()
        {
            if (!index)
                index = SampleModelIndex.Find();

            deselected.Clear();
            foreach (var name in EditorPrefs.GetString(SelectionPrefsKey, "").Split('\n'))
                if (!string.IsNullOrEmpty(name))
                    deselected.Add(name);
        }

        private static GUIContent WithTooltip(string iconName, string tooltip)
        {
            var content = new GUIContent(EditorGUIUtility.IconContent(iconName));
            content.tooltip = tooltip;
            return content;
        }

        private void SaveSelection() =>
            EditorPrefs.SetString(SelectionPrefsKey, string.Join("\n", deselected));

        private void OnGUI()
        {
            index = (SampleModelIndex) EditorGUILayout.ObjectField("Index", index, typeof(SampleModelIndex), false);
            if (!index)
            {
                EditorGUILayout.HelpBox(
                    $"No {nameof(SampleModelIndex)} asset assigned or found in the project.\n" +
                    "Create one via Assets > Create > glTF Interactivity > Sample Model Index.",
                    MessageType.Warning);
                return;
            }

            var root = index.ResolveModelsRoot();
            var rootExists = !string.IsNullOrEmpty(root) && Directory.Exists(root);
            EditorGUILayout.HelpBox(
                rootExists ? $"Output: {root}" : $"Models folder does not exist:\n{root}",
                rootExists ? MessageType.None : MessageType.Error);

            var models = index.ValidModels.ToList();
            if (models.Count == 0)
            {
                EditorGUILayout.HelpBox($"{index.name} has no models assigned.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All", EditorStyles.miniButtonLeft))
                {
                    deselected.Clear();
                    SaveSelection();
                }

                if (GUILayout.Button("None", EditorStyles.miniButtonRight))
                {
                    foreach (var model in models)
                        deselected.Add(model.ModelFolderName);
                    SaveSelection();
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var model in models)
            {
                var name = model.ModelFolderName;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var selected = !deselected.Contains(name);
                    var newSelected = EditorGUILayout.ToggleLeft(
                        new GUIContent(model.Label, model.ScenePath), selected, GUILayout.ExpandWidth(true));
                    if (newSelected != selected)
                    {
                        if (newSelected) deselected.Remove(name);
                        else deselected.Add(name);
                        SaveSelection();
                    }

                    var error = model.Validate();
                    if (error != null)
                        GUILayout.Label(WithTooltip("console.erroricon.sml", error), GUILayout.Width(20));
                    else if (rootExists && !File.Exists(Path.Combine(root, name, SampleModelExporter
                                 .ScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar))))
                        GUILayout.Label(WithTooltip("console.warnicon.sml", "No screenshot yet"), GUILayout.Width(20));
                    else
                        GUILayout.Space(24);

                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55)))
                        Selection.activeObject = model;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            var selectedModels = models.Where(m => !deselected.Contains(m.ModelFolderName)).ToArray();
            using (new EditorGUI.DisabledScope(!rootExists))
            {
                using (new EditorGUI.DisabledScope(selectedModels.Length == 0))
                {
                    if (GUILayout.Button($"Export Selected ({selectedModels.Length})", GUILayout.Height(28)))
                        SampleModelExporter.ExportAndRegenerate(index, selectedModels);
                }

                if (GUILayout.Button($"Export All ({models.Count})", GUILayout.Height(28)))
                    SampleModelExporter.ExportAndRegenerate(index, models);

                if (GUILayout.Button("Regenerate JSON / Markdown Only"))
                    SampleModelIndexWriter.Regenerate(index);
            }

            if (rootExists && GUILayout.Button("Reveal Output Folder"))
                EditorUtility.RevealInFinder(root);
        }
    }
}
