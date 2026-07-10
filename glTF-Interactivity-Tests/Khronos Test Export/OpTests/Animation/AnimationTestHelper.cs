using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Shared helpers for the animation/start, animation/stop and animation/stopAt test cases.
    ///
    /// The animation tests all follow the same idea: create a custom legacy AnimationClip via the
    /// Unity API that linearly moves a simple (near-invisible) empty node's localPosition from the
    /// origin to a target position over a known duration, export it, drive it with the animation
    /// node under test and read the node translation back with a pointer/get to assert the object
    /// actually moved to the expected position.
    /// </summary>
    public static class AnimationTestHelper
    {
        /// <summary>
        /// Creates a GameObject carrying a legacy Animation component whose single clip linearly
        /// animates its own localPosition from (0,0,0) to <paramref name="targetPosition"/> over
        /// <paramref name="duration"/> seconds. The object is scaled down so it does not clutter
        /// the test scene visually.
        /// </summary>
        public static GameObject CreateAnimatedObject(Transform parent, string name, Vector3 targetPosition,
            float duration, out AnimationClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.0001f;

            clip = new AnimationClip { legacy = true, wrapMode = WrapMode.Once, name = name + "Clip" };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, duration, targetPosition.x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 0f, duration, targetPosition.y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", AnimationCurve.Linear(0f, 0f, duration, targetPosition.z));

            var animation = go.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = false;
            return go;
        }

        /// <summary>
        /// The glTF-space translation for a Unity-space local position. UnityGLTF negates the X axis
        /// of translations on export (see SchemaExtensions.CoordinateSpaceConversionScale and the
        /// translation handling in ExporterAnimationPointer), so a pointer/get on
        /// /nodes/{}/translation returns the position with X flipped.
        /// </summary>
        public static Vector3 ToGltf(Vector3 unityLocalPosition)
        {
            return new Vector3(-unityLocalPosition.x, unityLocalPosition.y, unityLocalPosition.z);
        }

        /// <summary>
        /// Creates a pointer/get reading the /nodes/{}/translation of the given node index.
        /// </summary>
        public static GltfInteractivityExportNode CreateTranslationGet(GltfInteractivityExportNodes nodeCreator, int nodeIndex)
        {
            var pGet = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pGet, PointersHelper.IdPointerNodeIndex,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation", GltfTypes.Float3);
            pGet.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);
            return pGet;
        }
    }
}
