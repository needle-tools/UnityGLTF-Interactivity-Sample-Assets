using UnityEngine;

namespace GltfInteractivityScenes
{
    /// <summary>
    /// Per scene options for the KHR_interactivity export. Add it to any exported GameObject; for exports that
    /// include this object it overrides the settings of the Visual Scripting export plugin
    /// (applied by <see cref="InteractivityExportOptionsPlugin"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("glTF Interactivity/Interactivity Export Options")]
    public class InteractivityExportOptions : MonoBehaviour
    {
        [Tooltip("Run the clean up and optimization passes on the exported graph (deduplication, constant folding, " +
                 "removal of unused sequences, ...). Disable it to export the graph exactly as authored, " +
                 "e.g. for benchmark scenes.")]
        public bool cleanUpAndOptimizeExportedGraph = false;

        [Tooltip("Convert values between Unity and glTF space in the exported graph (mirrored X, camera looking " +
                 "along +Z). Disable it for graphs authored in glTF space, so the graph reads and writes the glTF " +
                 "values directly, e.g. to show the raw activeCamera pointer values.")]
        public bool addUnityGltfSpaceConversion = true;
    }
}
