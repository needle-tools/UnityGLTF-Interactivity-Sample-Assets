using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: the animation state between animation/start and the first animation update. "isPlaying" isn't defined in
    /// terms of the animation state array, and playhead/virtualPlayhead "retain [their] last value until the animation is
    /// restarted" doesn't say whether the restart itself or its first update changes them. All reads happen in [out].
    ///
    /// Fresh start(0.5T, T) of an animation that never played:
    ///  isPlaying  A: true (entry exists)          B: false (until the first update)
    ///  playhead   A: 0 (no update yet)            B: 0.5T (set by the start)
    /// Restart(0.75T, T) from the [done] of a playback that ended at 0.5T:
    ///  playhead   A: 0.5T (old value retained)    B: 0.75T (set by the start)
    ///  virtualPlayhead likewise
    /// One-shot start(0.5T, 0.5T):
    ///  isPlaying  A: true (entry exists until the next update)   B: false
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationStateAfterStartTest : ITestCase, IDisposable
    {
        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _fresh, _restart, _oneShot;

        private CheckBox _freshIsPlayingTrueCheckBox, _freshIsPlayingFalseCheckBox;
        private CheckBox _freshPlayheadZeroCheckBox, _freshPlayheadStartCheckBox;
        private CheckBox _restartPlayheadOldCheckBox, _restartPlayheadNewCheckBox;
        private CheckBox _restartVirtualOldCheckBox, _restartVirtualNewCheckBox;
        private CheckBox _oneShotIsPlayingTrueCheckBox, _oneShotIsPlayingFalseCheckBox;

        public string GetTestName() => "animation/state right after start";

        public string GetTestDescription() =>
            "isPlaying, playhead and virtualPlayhead read in [out] of animation/start, before the first animation update. " +
            "A: the entry counts as playing, the playheads keep their old values until the first update. B: not playing yet, the playheads are set by the start.";

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, T);
                _objects.Add(obj);
                return obj;
            }
            _fresh = Create("UndefinedStateFreshTarget");
            _restart = Create("UndefinedStateRestartTarget");
            _oneShot = Create("UndefinedStateOneShotTarget");

            _freshIsPlayingTrueCheckBox = context.AddCheckBox("A: fresh start:\nisPlaying true");
            _freshIsPlayingFalseCheckBox = context.AddCheckBox("B: fresh start:\nisPlaying false");
            _freshPlayheadZeroCheckBox = context.AddCheckBox("A: fresh start:\nplayhead 0");
            _freshPlayheadStartCheckBox = context.AddCheckBox("B: fresh start:\nplayhead startTime");
            context.NewRow();
            _restartPlayheadOldCheckBox = context.AddCheckBox("A: restart:\nplayhead old value", true);
            _restartPlayheadNewCheckBox = context.AddCheckBox("B: restart:\nplayhead new startTime", true);
            _restartVirtualOldCheckBox = context.AddCheckBox("A: restart:\nvirtualPlayhead old value", true);
            _restartVirtualNewCheckBox = context.AddCheckBox("B: restart:\nvirtualPlayhead new startTime", true);
            context.NewRow();
            _oneShotIsPlayingTrueCheckBox = context.AddCheckBox("A: one-shot start:\nisPlaying true");
            _oneShotIsPlayingFalseCheckBox = context.AddCheckBox("B: one-shot start:\nisPlaying false");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── Fresh start ────────────────────────────────────────────────────────
            {
                var animationIndex = _fresh.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0.5f * T, T);
                context.NewEntryPoint(start.FlowIn(Animation_StartNode.IdFlowIn), _freshIsPlayingTrueCheckBox.GetText(), T + 0.5f);

                var isPlaying = State(context, animationIndex, "isPlaying", GltfTypes.Bool);
                var playhead = State(context, animationIndex, "playhead", GltfTypes.Float);
                _freshIsPlayingTrueCheckBox.SetupCheck(isPlaying, out var trueFlow, true);
                _freshIsPlayingFalseCheckBox.SetupCheck(isPlaying, out var falseFlow, false);
                _freshPlayheadZeroCheckBox.proximityCheckDistance = 0.001f;
                _freshPlayheadZeroCheckBox.SetupCheck(playhead, out var zeroFlow, 0f, true);
                _freshPlayheadStartCheckBox.proximityCheckDistance = 0.001f;
                _freshPlayheadStartCheckBox.SetupCheck(playhead, out var startTimeFlow, 0.5f * T, true);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowOut), trueFlow, falseFlow, zeroFlow, startTimeFlow);
            }

            // ── Restart from [done] ────────────────────────────────────────────────
            {
                var animationIndex = _restart.AnimationIndex(context);
                var firstPlayback = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, 0.5f * T);
                var restart = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0.75f * T, T);
                firstPlayback.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(restart.FlowIn(Animation_StartNode.IdFlowIn));
                context.NewEntryPoint(firstPlayback.FlowIn(Animation_StartNode.IdFlowIn), _restartPlayheadOldCheckBox.GetText(), T + 0.5f);

                var playhead = State(context, animationIndex, "playhead", GltfTypes.Float);
                var virtualPlayhead = State(context, animationIndex, "virtualPlayhead", GltfTypes.Float);
                _restartPlayheadOldCheckBox.proximityCheckDistance = 0.01f;
                _restartPlayheadOldCheckBox.SetupCheck(playhead, out var playheadOldFlow, 0.5f * T, true);
                _restartPlayheadNewCheckBox.proximityCheckDistance = 0.01f;
                _restartPlayheadNewCheckBox.SetupCheck(playhead, out var playheadNewFlow, 0.75f * T, true);
                _restartVirtualOldCheckBox.proximityCheckDistance = 0.01f;
                _restartVirtualOldCheckBox.SetupCheck(virtualPlayhead, out var virtualOldFlow, 0.5f * T, true);
                _restartVirtualNewCheckBox.proximityCheckDistance = 0.01f;
                _restartVirtualNewCheckBox.SetupCheck(virtualPlayhead, out var virtualNewFlow, 0.75f * T, true);
                context.AddSequence(restart.FlowOut(Animation_StartNode.IdFlowOut), playheadOldFlow, playheadNewFlow, virtualOldFlow, virtualNewFlow);
            }

            // ── One-shot start ─────────────────────────────────────────────────────
            {
                var animationIndex = _oneShot.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0.5f * T, 0.5f * T);
                context.NewEntryPoint(start.FlowIn(Animation_StartNode.IdFlowIn), _oneShotIsPlayingTrueCheckBox.GetText(), 1f);

                var isPlaying = State(context, animationIndex, "isPlaying", GltfTypes.Bool);
                _oneShotIsPlayingTrueCheckBox.SetupCheck(isPlaying, out var trueFlow, true);
                _oneShotIsPlayingFalseCheckBox.SetupCheck(isPlaying, out var falseFlow, false);
                context.AddSequence(start.FlowOut(Animation_StartNode.IdFlowOut), trueFlow, falseFlow);
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
