using System;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// The animation state pointers (/animations/{}/extensions/KHR_interactivity/...) and the state of
    /// animations that are never started.
    ///
    /// Checks:
    ///  - isPlaying, minTime, maxTime, playhead and virtualPlayhead before, during and after playback
    ///  - maxTime and minTime ignore animation samplers that no channel references: an unused sampler with a
    ///    longer input is added to the exported animation, maxTime stays T, and start(0, maxTime) plays to
    ///    the end with playhead == maxTime on [done]
    ///  - animations don't play without animation/start: isPlaying stays false and the node translation
    ///    doesn't change
    /// </summary>
    public class AnimationStateTest : ITestCase, IDisposable
    {
        private const float T = 2f;
        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);

        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _state;
        private AnimatedTestObject _unusedSampler;
        private AnimatedTestObject _notStarted;

        private CheckBox _isPlayingBeforeCheckBox;
        private CheckBox _playheadBeforeCheckBox;
        private CheckBox _virtualPlayheadBeforeCheckBox;
        private CheckBox _minTimeCheckBox;
        private CheckBox _maxTimeCheckBox;
        private CheckBox _isPlayingDuringCheckBox;
        private CheckBox _isPlayingDoneCheckBox;
        private CheckBox _playheadDoneCheckBox;
        private CheckBox _virtualPlayheadDoneCheckBox;

        private CheckBox _unusedSamplerMaxTimeCheckBox;
        private CheckBox _unusedSamplerMinTimeCheckBox;
        private CheckBox _unusedSamplerDoneCheckBox;
        private CheckBox _unusedSamplerPlayheadCheckBox;

        private CheckBox _notStartedIsPlayingCheckBox;
        private CheckBox _notStartedTranslationCheckBox;

        public string GetTestName()
        {
            return "animation/state";
        }

        public string GetTestDescription()
        {
            return "Animation state pointers before, during and after playback, maxTime/minTime with an unused sampler, and animations that are never started.";
        }

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, T);
                _objects.Add(obj);
                return obj;
            }
            _state = Create("AnimationStateTarget");
            _unusedSampler = Create("AnimationUnusedSamplerTarget");
            _notStarted = Create("AnimationNotStartedTarget");

            _isPlayingBeforeCheckBox = context.AddCheckBox("isPlaying false\nbefore start");
            _playheadBeforeCheckBox = context.AddCheckBox("playhead 0\nbefore start");
            _virtualPlayheadBeforeCheckBox = context.AddCheckBox("virtualPlayhead 0\nbefore start");
            _minTimeCheckBox = context.AddCheckBox("minTime 0");
            _maxTimeCheckBox = context.AddCheckBox("maxTime T");
            context.NewRow();
            _isPlayingDuringCheckBox = context.AddCheckBox("isPlaying true\nwhile playing", true);
            _isPlayingDoneCheckBox = context.AddCheckBox("isPlaying false\non [done]", true);
            _playheadDoneCheckBox = context.AddCheckBox("playhead T\non [done]", true);
            _virtualPlayheadDoneCheckBox = context.AddCheckBox("virtualPlayhead T\non [done]", true);
            context.NewRow();
            _unusedSamplerMaxTimeCheckBox = context.AddCheckBox("Unused sampler:\nmaxTime T");
            _unusedSamplerMinTimeCheckBox = context.AddCheckBox("Unused sampler:\nminTime 0");
            _unusedSamplerDoneCheckBox = context.AddCheckBox("Unused sampler:\nstart(0, maxTime) [done]", true);
            _unusedSamplerPlayheadCheckBox = context.AddCheckBox("Unused sampler:\nplayhead maxTime on [done]", true);
            context.NewRow();
            _notStartedIsPlayingCheckBox = context.AddCheckBox("Not started:\nisPlaying false", true);
            _notStartedTranslationCheckBox = context.AddCheckBox("Not started:\ntranslation unchanged", true);
        }

        /// <summary>
        /// Adds a sampler that no channel references, with an input accessor running to <paramref name="inputMax"/>.
        /// </summary>
        private static void AddUnusedSampler(GLTFSceneExporter exporter, int animationIndex, float inputMax)
        {
            var times = new[] { 0f, inputMax };
            var values = new[] { 0f, 0f, 0f, 0f, 0f, 0f };

            byte[] ToBytes(float[] floats)
            {
                var bytes = new byte[floats.Length * sizeof(float)];
                Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
                return bytes;
            }

            var input = exporter.ExportAccessor(ToBytes(times), (uint)times.Length, GLTFAccessorAttributeType.SCALAR,
                GLTFComponentType.Float, new List<double> { 0 }, new List<double> { inputMax });
            var output = exporter.ExportAccessor(ToBytes(values), (uint)times.Length, GLTFAccessorAttributeType.VEC3,
                GLTFComponentType.Float, null, null);

            exporter.GetRoot().Animations[animationIndex].Samplers.Add(new AnimationSampler
            {
                Input = input,
                Output = output,
                Interpolation = InterpolationType.LINEAR
            });
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── State before, during and after playback ────────────────────────────
            {
                var animationIndex = _state.AnimationIndex(context);
                ValueOutRef State(string property, string gltfType) =>
                    AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, property, gltfType);

                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                var midDelay = AnimationTestHelper.CreateDelay(nodeCreator, T * 0.5f);
                context.NewEntryPoint(_isPlayingBeforeCheckBox.GetText(), T + 1f);

                _isPlayingBeforeCheckBox.SetupCheck(State("isPlaying", GltfTypes.Bool), out var isPlayingBeforeFlow, false);
                _playheadBeforeCheckBox.SetupCheck(State("playhead", GltfTypes.Float), out var playheadBeforeFlow, 0f);
                _virtualPlayheadBeforeCheckBox.SetupCheck(State("virtualPlayhead", GltfTypes.Float), out var virtualPlayheadBeforeFlow, 0f);
                _minTimeCheckBox.proximityCheckDistance = 0.001f;
                _minTimeCheckBox.SetupCheck(State("minTime", GltfTypes.Float), out var minTimeFlow, 0f, true);
                _maxTimeCheckBox.proximityCheckDistance = 0.01f;
                _maxTimeCheckBox.SetupCheck(State("maxTime", GltfTypes.Float), out var maxTimeFlow, T, true);

                context.AddToCurrentEntrySequence(
                    isPlayingBeforeFlow,
                    playheadBeforeFlow,
                    virtualPlayheadBeforeFlow,
                    minTimeFlow,
                    maxTimeFlow,
                    start.FlowIn(Animation_StartNode.IdFlowIn),
                    midDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                _isPlayingDuringCheckBox.SetupCheck(State("isPlaying", GltfTypes.Bool), out var isPlayingDuringFlow, true);
                midDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(isPlayingDuringFlow);

                // The entry is removed from the animation state array before [done] is activated
                _isPlayingDoneCheckBox.SetupCheck(State("isPlaying", GltfTypes.Bool), out var isPlayingDoneFlow, false);
                _playheadDoneCheckBox.proximityCheckDistance = 0.01f;
                _playheadDoneCheckBox.SetupCheck(State("playhead", GltfTypes.Float), out var playheadDoneFlow, T, true);
                _virtualPlayheadDoneCheckBox.proximityCheckDistance = 0.01f;
                _virtualPlayheadDoneCheckBox.SetupCheck(State("virtualPlayhead", GltfTypes.Float), out var virtualPlayheadDoneFlow, T, true);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), isPlayingDoneFlow, playheadDoneFlow, virtualPlayheadDoneFlow);
            }

            // ── maxTime / minTime ignore unused samplers ───────────────────────────
            {
                var animationIndex = _unusedSampler.AnimationIndex(context);
                AddUnusedSampler(nodeCreator.Context.exporter, animationIndex, 2f * T);

                var maxTime = AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "maxTime", GltfTypes.Float);

                // endTime comes from the maxTime pointer
                var start = nodeCreator.CreateNode<Animation_StartNode>();
                start.ValueIn(Animation_StartNode.IdValueAnimationRef).SetValue(new StaticRefPointer($"/animations/{animationIndex}"));
                start.ValueIn(Animation_StartNode.IdValueStartTime).SetValue(0f);
                start.ValueIn(Animation_StartNode.IdValueEndtime).ConnectToSource(maxTime);
                start.ValueIn(Animation_StartNode.IdValueSpeed).SetValue(1f);

                context.NewEntryPoint(_unusedSamplerMaxTimeCheckBox.GetText(), T + 1f);
                _unusedSamplerMaxTimeCheckBox.proximityCheckDistance = 0.01f;
                _unusedSamplerMaxTimeCheckBox.SetupCheck(maxTime, out var maxTimeFlow, T, true);
                _unusedSamplerMinTimeCheckBox.proximityCheckDistance = 0.001f;
                _unusedSamplerMinTimeCheckBox.SetupCheck(AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "minTime", GltfTypes.Float),
                    out var minTimeFlow, 0f, true);
                context.AddToCurrentEntrySequence(maxTimeFlow, minTimeFlow, start.FlowIn(Animation_StartNode.IdFlowIn));

                _unusedSamplerDoneCheckBox.SetupCheck(out var doneFlow);
                _unusedSamplerPlayheadCheckBox.proximityCheckDistance = 0.01f;
                _unusedSamplerPlayheadCheckBox.SetupCheck(AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "playhead", GltfTypes.Float),
                    out var playheadFlow, T, true);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowDone), doneFlow, playheadFlow);
            }

            // ── Never started ──────────────────────────────────────────────────────
            // Nothing starts this animation, so after longer than the clip it's still not playing and the
            // node keeps its translation (the origin, which is also the clip's first keyframe).
            {
                var animationIndex = _notStarted.AnimationIndex(context);
                var delay = AnimationTestHelper.CreateDelay(nodeCreator, T + 0.5f);
                context.NewEntryPoint(_notStartedIsPlayingCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                _notStartedIsPlayingCheckBox.SetupCheck(AnimationTestHelper.CreateAnimationStateGet(nodeCreator, animationIndex, "isPlaying", GltfTypes.Bool),
                    out var isPlayingFlow, false);
                _notStartedTranslationCheckBox.proximityCheckDistance = 0.001f;
                _notStartedTranslationCheckBox.SetupCheck(AnimationTestHelper.CreateTranslationY(nodeCreator, _notStarted.NodeIndex(context)),
                    out var translationFlow, 0f, true);
                context.AddSequence(delay.FlowOut(Flow_SetDelayNode.IdFlowDone), isPlayingFlow, translationFlow);
            }
        }

        public void Dispose()
        {
            foreach (var obj in _objects)
                obj.Destroy();
            _objects.Clear();
        }
    }
}
