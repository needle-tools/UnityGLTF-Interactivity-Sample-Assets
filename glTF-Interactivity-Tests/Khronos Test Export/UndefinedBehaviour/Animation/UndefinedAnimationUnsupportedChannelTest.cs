using System;
using System.Collections.Generic;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityGLTF.Extensions;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: what animation/start does with an animation that is valid glTF but has a channel the engine can't
    /// apply is open: play the rest, play nothing, or "invalid as determined by the implementation" ([err]).
    ///
    /// The animation has its translation channel plus a KHR_animation_pointer channel targeting
    /// /nodes/{n}/extensions/EXT_interactivity_unsupported_test/value, a property of an unknown (not required) node extension:
    ///  A: animation/start activates [err]
    ///  B: [out], the translation channel plays (pose at T on [done])
    ///  C: [out], nothing plays (translation stays at the origin)
    /// maxTime == T is the reference: the animation is valid glTF.
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationUnsupportedChannelTest : ITestCase, IDisposable
    {
        private const string UnknownExtension = "EXT_interactivity_unsupported_test";

        private AnimatedTestObject _target;
        private CheckBox _maxTimeCheckBox;
        private CheckBox _errCheckBox;
        private CheckBox _outCheckBox;
        private CheckBox _partialCheckBox;
        private CheckBox _nothingCheckBox;

        public string GetTestName() => "animation/unsupported channel";

        public string GetTestDescription() =>
            "An animation with a translation channel and a KHR_animation_pointer channel to a property of an unknown node extension. " +
            "A: start activates [err]. B: [out] and the translation channel plays. C: [out] but nothing plays.";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedUnsupportedChannelTarget", TargetPosition, T);
            _maxTimeCheckBox = context.AddCheckBox("maxTime == T\n(spec reference)");
            context.NewRow();
            _errCheckBox = context.AddCheckBox("A: start [err]", true);
            _outCheckBox = context.AddCheckBox("B/C: start [out]", true);
            _partialCheckBox = context.AddCheckBox("B: translation plays\n(pose at T after T)", true);
            _nothingCheckBox = context.AddCheckBox("C: nothing plays\n(pose 0 after T)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;
            var animationIndex = _target.AnimationIndex(context);
            var nodeIndex = _target.NodeIndex(context);

            // The unknown node extension with an animatable-looking float property
            var root = exporter.GetRoot();
            var node = root.Nodes[nodeIndex];
            if (node.Extensions == null)
                node.Extensions = new Dictionary<string, IExtension>();
            node.Extensions[UnknownExtension] = new JsonExtension(UnknownExtension, new JObject { ["value"] = 0.0 });
            exporter.DeclareExtensionUsage(UnknownExtension, false);
            exporter.DeclareExtensionUsage(KHR_animation_pointer.EXTENSION_NAME, false);

            // A pointer channel animating it 0 -> 1 over T
            var animation = root.Animations[animationIndex];
            animation.Samplers.Add(new AnimationSampler
            {
                Input = ExportFloatAccessor(exporter, new[] { 0f, T }, true),
                Output = ExportFloatAccessor(exporter, new[] { 0f, 1f }, false),
                Interpolation = InterpolationType.LINEAR,
            });
            animation.Channels.Add(new AnimationChannel
            {
                Sampler = new AnimationSamplerId { Id = animation.Samplers.Count - 1, GLTFAnimation = animation, Root = root },
                Target = new AnimationChannelTarget
                {
                    Path = "pointer",
                    Extensions = new Dictionary<string, IExtension>
                    {
                        [KHR_animation_pointer.EXTENSION_NAME] = new KHR_animation_pointer { path = $"/nodes/{nodeIndex}/extensions/{UnknownExtension}/value" },
                    },
                },
            });

            var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
            var delay = AnimationTestHelper.CreateDelay(nodeCreator, T + 0.25f);
            context.NewEntryPoint(_maxTimeCheckBox.GetText(), T + 1f);
            _maxTimeCheckBox.proximityCheckDistance = 0.01f;
            _maxTimeCheckBox.SetupCheck(State(context, animationIndex, "maxTime", GltfTypes.Float), out var maxTimeFlow, T, true);
            context.AddToCurrentEntrySequence(maxTimeFlow, start.FlowIn(Animation_StartNode.IdFlowIn), delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            _errCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowError));
            _outCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowOut));

            var y = AnimationTestHelper.CreateTranslationY(nodeCreator, nodeIndex);
            _partialCheckBox.proximityCheckDistance = 0.01f;
            _partialCheckBox.SetupCheck(y, out var partialFlow, TargetPosition.y, true);
            _nothingCheckBox.proximityCheckDistance = 0.01f;
            _nothingCheckBox.SetupCheck(y, out var nothingFlow, 0f, true);
            context.AddSequence(delay.FlowOut(Flow_SetDelayNode.IdFlowDone), partialFlow, nothingFlow);
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
