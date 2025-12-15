using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using UnityGLTF.Plugins;

namespace Khronos_Test_Export
{
    public class TestFileExporterPlugin : GLTFExportPlugin
    {

        // public override JToken AssetExtras 
        // { 
        //     get => new JObject(
        //         new JProperty("Spec.Version URL", "https://github.com/KhronosGroup/glTF/blob/d9bfdb08f0c09c125f588783921d9edceb7ee78c/extensions/2.0/Khronos/KHR_interactivity/Specification.adoc"),
        //         new JProperty("Spec.Version Date", "2025-03-10"));
        // }

        // Disabled by default until Gltf Interactivity spec is ratified
        public override bool EnabledByDefault => false;

        public override string DisplayName => GltfInteractivityExtension.ExtensionName + " (Test File Exporter)";
        public override string Description => "Exports interactivity test files";


        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            return new TestFileExportContext();
        }


        public class TestFileExportContext : InteractivityExportContext
        {
            private Texture2D _convertedTestSymbols;
            private Texture2D _fontTex;
            
            public override void BeforeTextureExport(GLTFSceneExporter exporter, ref GLTFSceneExporter.UniqueTexture texture, string textureSlot)
            {
                if (texture.Texture.name == "testsymbols")
                {
                    if (_convertedTestSymbols == null)
                    {
                        // Reduce resolution
                        var newTex = new Texture2D(1024 / 2, 128 / 2, TextureFormat.RGBA32, false);
                        Graphics.ConvertTexture(texture.Texture,  newTex);
                        _convertedTestSymbols = newTex;
                    }
                    texture.Texture = _convertedTestSymbols;
                }
                else if (texture.Texture.width == 2048 && texture.Texture.height == 2048)
                {
                    if (_fontTex == null)
                    {
                        // Reduce resolution
                        var newTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                        Graphics.ConvertTexture(texture.Texture,  newTex);
                        _fontTex = newTex;
                    }
                    texture.Texture = _fontTex;
                    
                }
            }

            public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
            {
                this.exporter = exporter;
                ActiveGltfRoot = gltfRoot;

                TriggerInterfaceExportCallbacks();
                RemoveUnconnectedNodes();
                
                // For Value Conversion, we need to presort the nodes, otherwise we might get wrong results
                TopologicalSort();
                CheckForImplicitValueConversions();

                CheckForCircularFlows();

                // Final Topological Sort
                TopologicalSort();

                CollectOpDeclarations();

                TriggerOnBeforeSerialization();
                ApplyInteractivityExtension();
            }
        }
    }
}