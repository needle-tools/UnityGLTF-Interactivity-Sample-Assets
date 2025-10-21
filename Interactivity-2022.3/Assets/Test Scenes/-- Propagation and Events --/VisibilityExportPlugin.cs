using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;

namespace EventSamples
{
    public class VisibilityExportPlugin : GLTFExportPlugin
    {
        public override string DisplayName
        {
            get => "Force Visibility Export";
        }
        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            return new VisibilityExportPluginContext(context);
            
        }
    }
    
    public class VisibilityExportPluginContext : GLTFExportPluginContext
    {
        private ExportContext _context; 
        public VisibilityExportPluginContext(ExportContext context) 
        {
            _context = context;
        }

        public override void AfterNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            var fv = transform.GetComponent<ForceVisibility>();
            if (fv == null)
                return;

            if (node.Extensions == null || !node.Extensions.TryGetValue(KHR_node_visibility_Factory.EXTENSION_NAME, out var ext))
            {
                return;
            }
            if (ext is KHR_node_visibility visibilityExt)
            {
                visibilityExt.visible = fv.Visible;
            }
            else
            {
                node.AddExtension(KHR_node_visibility_Factory.EXTENSION_NAME, new KHR_node_visibility() { visible = fv.Visible});
            }
            
            

        }
    }
}