using System.Reflection;
using GLTF.Schema;
using UnityGLTF;
using UnityGLTF.Plugins;

namespace GltfInteractivityScenes
{
    /// <summary>
    /// Applies <see cref="InteractivityExportOptions"/> found in the exported objects to the other export plugins.
    /// UnityGLTF registers export plugins automatically, so nothing has to be set up.
    /// </summary>
    public class InteractivityExportOptionsPlugin : GLTFExportPlugin
    {
        public override string DisplayName => "Interactivity Export Options (scene)";
        public override string Description =>
            "Lets an InteractivityExportOptions component in the exported scene override interactivity export settings, " +
            "e.g. disable the graph clean up for benchmark scenes.";

        public override GLTFExportPluginContext CreateInstance(ExportContext context) => new Context();

        private class Context : GLTFExportPluginContext
        {
            // Fields of the Visual Scripting export context. Set via reflection: that context lives in an editor-only
            // UnityGLTF assembly this package does not reference.
            private const string CleanUpField = "cleanUpAndOptimizeExportedGraph";
            private const string SpaceConversionField = "addUnityGltfSpaceConversion";

            public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
            {
                InteractivityExportOptions options = null;
                foreach (var root in exporter.RootTransforms)
                {
                    if (!root) continue;
                    options = root.GetComponentInChildren<InteractivityExportOptions>(true);
                    if (options) break;
                }
                if (!options)
                    return;

                foreach (var plugin in exporter.Plugins)
                {
                    SetBool(plugin, CleanUpField, options.cleanUpAndOptimizeExportedGraph);
                    SetBool(plugin, SpaceConversionField, options.addUnityGltfSpaceConversion);
                }
            }

            private static void SetBool(object plugin, string fieldName, bool value)
            {
                var field = plugin?.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(bool))
                    field.SetValue(plugin, value);
            }
        }
    }
}
