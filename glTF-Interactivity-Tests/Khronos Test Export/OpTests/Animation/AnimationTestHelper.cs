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

        /// <summary>
        /// The Y component of the node's /nodes/{}/translation. Y is not affected by the X-negation
        /// of the Unity->glTF conversion, so it can be compared with the Unity-space clip values directly.
        /// </summary>
        public static ValueOutRef CreateTranslationY(GltfInteractivityExportNodes nodeCreator, int nodeIndex)
        {
            var pGet = CreateTranslationGet(nodeCreator, nodeIndex);
            var extract = nodeCreator.CreateNode<Math_Extract3Node>();
            extract.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));
            return extract.ValueOut(Math_Extract3Node.IdValueOutY);
        }

        /// <summary>
        /// Reads one of the animation state properties (isPlaying, minTime, maxTime, playhead,
        /// virtualPlayhead) of /animations/{}/extensions/KHR_interactivity/.
        /// </summary>
        public static ValueOutRef CreateAnimationStateGet(GltfInteractivityExportNodes nodeCreator, int animationIndex,
            string property, string gltfType)
        {
            var pGet = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pGet, PointersHelper.IdPointerAnimationIndex,
                "/animations/[" + PointersHelper.IdPointerAnimationIndex + "]/extensions/KHR_interactivity/" + property, gltfType);
            pGet.ValueIn(PointersHelper.IdPointerAnimationIndex).SetValue(animationIndex);
            return pGet.ValueOut(Pointer_GetNode.IdValue);
        }

        public static GltfInteractivityExportNode CreateStart(GltfInteractivityExportNodes nodeCreator, int animationIndex,
            float startTime, float endTime, float speed = 1f)
        {
            var startNode = nodeCreator.CreateNode<Animation_StartNode>();
            startNode.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            startNode.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(startTime);
            startNode.ValueIn(Animation_StartNode.IdValueEndtime).SetValue(endTime);
            startNode.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(speed);
            return startNode;
        }

        public static GltfInteractivityExportNode CreateStop(GltfInteractivityExportNodes nodeCreator, int animationIndex)
        {
            var stopNode = nodeCreator.CreateNode<Animation_StopNode>();
            stopNode.ValueIn(Animation_StopNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            return stopNode;
        }

        public static GltfInteractivityExportNode CreateStopAt(GltfInteractivityExportNodes nodeCreator, int animationIndex, float stopTime)
        {
            var stopAtNode = nodeCreator.CreateNode<Animation_StopAtNode>();
            stopAtNode.ValueIn(Animation_StopAtNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
            stopAtNode.ValueIn(Animation_StopAtNode.IdValueStopTime).SetValue(stopTime);
            return stopAtNode;
        }

        public static GltfInteractivityExportNode CreateDelay(GltfInteractivityExportNodes nodeCreator, float duration)
        {
            var delay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(duration);
            return delay;
        }
    }

    /// <summary>
    /// An animated test object and its clip, created in PrepareObjects and resolved to glTF indices in CreateNodes.
    /// </summary>
    public class AnimatedTestObject
    {
        public GameObject gameObject;
        public AnimationClip clip;

        public AnimatedTestObject(Transform parent, string name, Vector3 targetPosition, float duration)
        {
            gameObject = AnimationTestHelper.CreateAnimatedObject(parent, name, targetPosition, duration, out clip);
        }

        public int NodeIndex(TestContext context) => context.interactivityExportContext.Context.exporter.GetTransformIndex(gameObject.transform);

        public int AnimationIndex(TestContext context) => context.interactivityExportContext.Context.exporter.GetAnimationId(clip, gameObject.transform);

        public void Destroy()
        {
            if (gameObject != null)
                Object.DestroyImmediate(gameObject);
            if (clip != null)
                Object.DestroyImmediate(clip);
        }
    }
}
