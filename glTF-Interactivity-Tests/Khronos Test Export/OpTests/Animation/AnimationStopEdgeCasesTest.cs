using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Edge cases of animation/stop and animation/stopAt.
    ///
    /// Checks:
    ///  - stop / stopAt on an animation that isn't playing: [out] fires, nothing else happens
    ///    (no [err], stopAt [done] never fires)
    ///  - stopAt with stopTime +Inf / -Inf is not an error (only NaN is)
    ///  - stopTime == endTime: the stop condition requires stopTime &lt; endTime, so the start node's
    ///    [done] fires and the stopAt [done] never does
    ///  - stopTime already passed: on the next update the pose is rewound to stopTime and the stopAt
    ///    [done] fires
    ///  - stopTime outside the start..end interval: [out] fires, the stopAt [done] never fires and
    ///    the animation plays to its end
    ///  - an animation stopped from another animation's [done] never fires its own [done]
    /// </summary>
    public class AnimationStopEdgeCasesTest : ITestCase, IDisposable
    {
        private const float T = 2f;
        private static readonly Vector3 TargetPosition = new Vector3(1f, 2f, 3f);
        private const float ShortDuration = 1f;
        private const float LongDuration = 3f;

        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _idle;
        private AnimatedTestObject _stopAtEnd;
        private AnimatedTestObject _stopAtPassed;
        private AnimatedTestObject _stopAtOutside;
        private AnimatedTestObject _stopFromDoneShort;
        private AnimatedTestObject _stopFromDoneLong;

        private CheckBox _stopIdleOutCheckBox;
        private CheckBox _stopIdleNoErrCheckBox;
        private CheckBox _stopAtIdleOutCheckBox;
        private CheckBox _stopAtIdleNoDoneCheckBox;
        private CheckBox _stopAtPosInfOutCheckBox;
        private CheckBox _stopAtNegInfOutCheckBox;
        private CheckBox _stopAtInfNoErrCheckBox;

        private CheckBox _stopAtEndStartDoneCheckBox;
        private CheckBox _stopAtEndStopAtNoDoneCheckBox;
        private CheckBox _stopAtPassedDoneCheckBox;
        private CheckBox _stopAtPassedPositionCheckBox;
        private CheckBox _stopAtPassedStartNoDoneCheckBox;
        private CheckBox _stopAtOutsideOutCheckBox;
        private CheckBox _stopAtOutsideNoDoneCheckBox;
        private CheckBox _stopAtOutsideStartDoneCheckBox;
        private CheckBox _stopFromDoneOutCheckBox;
        private CheckBox _stopFromDoneNoDoneCheckBox;

        public string GetTestName()
        {
            return "animation/stop and stopAt edge cases";
        }

        public string GetTestDescription()
        {
            return "stop/stopAt on an animation that isn't playing, infinite stopTime, stopTime at or past the end of the playback interval, and stopping an animation from another animation's [done].";
        }

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name, float duration = T)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, duration);
                _objects.Add(obj);
                return obj;
            }
            _idle = Create("AnimationStopIdleTarget");
            _stopAtEnd = Create("AnimationStopAtEndTimeTarget");
            _stopAtPassed = Create("AnimationStopAtPassedTarget");
            _stopAtOutside = Create("AnimationStopAtOutsideTarget");
            _stopFromDoneShort = Create("AnimationStopFromDoneShortTarget", ShortDuration);
            _stopFromDoneLong = Create("AnimationStopFromDoneLongTarget", LongDuration);

            _stopIdleOutCheckBox = context.AddCheckBox("stop, not playing:\n[out]");
            _stopIdleNoErrCheckBox = context.AddCheckBox("stop, not playing:\nno [err]");
            _stopIdleNoErrCheckBox.Negate();
            _stopAtIdleOutCheckBox = context.AddCheckBox("stopAt, not playing:\n[out]");
            _stopAtIdleNoDoneCheckBox = context.AddCheckBox("stopAt, not playing:\n[done] not fired", true);
            _stopAtIdleNoDoneCheckBox.Negate();
            context.NewRow();
            _stopAtPosInfOutCheckBox = context.AddCheckBox("stopAt +Inf:\n[out]");
            _stopAtNegInfOutCheckBox = context.AddCheckBox("stopAt -Inf:\n[out]");
            _stopAtInfNoErrCheckBox = context.AddCheckBox("stopAt +/-Inf:\nno [err]");
            _stopAtInfNoErrCheckBox.Negate();
            context.NewRow();
            _stopAtEndStartDoneCheckBox = context.AddCheckBox("stopTime == endTime:\nstart [done]", true);
            _stopAtEndStopAtNoDoneCheckBox = context.AddCheckBox("stopTime == endTime:\nstopAt [done] not fired", true);
            _stopAtEndStopAtNoDoneCheckBox.Negate();
            _stopAtPassedDoneCheckBox = context.AddCheckBox("stopTime passed:\nstopAt [done]", true);
            _stopAtPassedPositionCheckBox = context.AddCheckBox("stopTime passed:\npose rewound", true);
            _stopAtPassedStartNoDoneCheckBox = context.AddCheckBox("stopTime passed:\nstart [done] not fired", true);
            _stopAtPassedStartNoDoneCheckBox.Negate();
            context.NewRow();
            _stopAtOutsideOutCheckBox = context.AddCheckBox("stopTime > endTime:\nstopAt [out]");
            _stopAtOutsideNoDoneCheckBox = context.AddCheckBox("stopTime > endTime:\nstopAt [done] not fired", true);
            _stopAtOutsideNoDoneCheckBox.Negate();
            _stopAtOutsideStartDoneCheckBox = context.AddCheckBox("stopTime > endTime:\nstart [done]", true);
            context.NewRow();
            _stopFromDoneOutCheckBox = context.AddCheckBox("stop from another\n[done]: [out]", true);
            _stopFromDoneNoDoneCheckBox = context.AddCheckBox("stopped from another\n[done]: own [done] not fired", true);
            _stopFromDoneNoDoneCheckBox.Negate();
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── Not playing ────────────────────────────────────────────────────────
            var idleIndex = _idle.AnimationIndex(context);
            {
                var stop = AnimationTestHelper.CreateStop(nodeCreator, idleIndex);
                context.NewEntryPoint(stop.FlowIn(Animation_StopNode.IdFlowIn), _stopIdleOutCheckBox.GetText());
                _stopIdleOutCheckBox.SetupCheck(stop.FlowOut(Animation_StopNode.IdFlowOut));
                _stopIdleNoErrCheckBox.SetupNegateCheck(stop.FlowOut(Animation_StopNode.IdFlowError));
            }
            {
                var stopAt = AnimationTestHelper.CreateStopAt(nodeCreator, idleIndex, T * 0.1f);
                context.NewEntryPoint(stopAt.FlowIn(Animation_StopAtNode.IdFlowIn), _stopAtIdleOutCheckBox.GetText(), T * 0.1f + 1f);
                _stopAtIdleOutCheckBox.SetupCheck(stopAt.FlowOut(Animation_StopAtNode.IdFlowOut));
                _stopAtIdleNoDoneCheckBox.SetupNegateCheck(stopAt.FlowOut(Animation_StopAtNode.IdFlowDone));
            }

            // ── Infinite stopTime is valid ─────────────────────────────────────────
            {
                context.NewEntryPoint(_stopAtPosInfOutCheckBox.GetText());
                _stopAtInfNoErrCheckBox.SetupNegateCheck(out var infErrFlow);

                var stopAtPosInf = AnimationTestHelper.CreateStopAt(nodeCreator, idleIndex, float.PositiveInfinity);
                var stopAtNegInf = AnimationTestHelper.CreateStopAt(nodeCreator, idleIndex, float.NegativeInfinity);
                context.AddToCurrentEntrySequence(
                    stopAtPosInf.FlowIn(Animation_StopAtNode.IdFlowIn),
                    stopAtNegInf.FlowIn(Animation_StopAtNode.IdFlowIn));

                _stopAtPosInfOutCheckBox.SetupCheck(stopAtPosInf.FlowOut(Animation_StopAtNode.IdFlowOut));
                _stopAtNegInfOutCheckBox.SetupCheck(stopAtNegInf.FlowOut(Animation_StopAtNode.IdFlowOut));
                stopAtPosInf.FlowOut(Animation_StopAtNode.IdFlowError).ConnectToFlowDestination(infErrFlow);
                stopAtNegInf.FlowOut(Animation_StopAtNode.IdFlowError).ConnectToFlowDestination(infErrFlow);
            }

            // ── stopTime == endTime ────────────────────────────────────────────
            {
                var animationIndex = _stopAtEnd.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                var stopAt = AnimationTestHelper.CreateStopAt(nodeCreator, animationIndex, T);
                context.NewEntryPoint(_stopAtEndStartDoneCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), stopAt.FlowIn(Animation_StopAtNode.IdFlowIn));

                _stopAtEndStartDoneCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowDone));
                _stopAtEndStopAtNoDoneCheckBox.SetupNegateCheck(stopAt.FlowOut(Animation_StopAtNode.IdFlowDone));
            }

            // ── stopTime already passed ────────────────────────────────────────
            // At 0.75T a stop at 0.25T is scheduled: current >= stop, stop >= start and stop < end,
            // so the next update applies the pose at 0.25T and fires the stopAt [done].
            {
                var animationIndex = _stopAtPassed.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                var stopAt = AnimationTestHelper.CreateStopAt(nodeCreator, animationIndex, T * 0.25f);
                var delay = AnimationTestHelper.CreateDelay(nodeCreator, T * 0.75f);
                delay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(stopAt.FlowIn(Animation_StopAtNode.IdFlowIn));
                context.NewEntryPoint(_stopAtPassedDoneCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                _stopAtPassedDoneCheckBox.SetupCheck(out var doneFlow);
                _stopAtPassedPositionCheckBox.proximityCheckDistance = 0.01f;
                _stopAtPassedPositionCheckBox.SetupCheck(AnimationTestHelper.CreateTranslationY(nodeCreator, _stopAtPassed.NodeIndex(context)),
                    out var positionFlow, TargetPosition.y * 0.25f, true);
                context.AddSequence(stopAt.FlowOut(Animation_StopAtNode.IdFlowDone), doneFlow, positionFlow);
                _stopAtPassedStartNoDoneCheckBox.SetupNegateCheck(start.FlowOut(Animation_StartNode.IdFlowDone));
            }

            // ── stopTime outside the start..end interval ───────────────────────
            {
                var animationIndex = _stopAtOutside.AnimationIndex(context);
                var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
                var stopAt = AnimationTestHelper.CreateStopAt(nodeCreator, animationIndex, T * 1.5f);
                context.NewEntryPoint(_stopAtOutsideOutCheckBox.GetText(), T + 1f);
                context.AddToCurrentEntrySequence(start.FlowIn(Animation_StartNode.IdFlowIn), stopAt.FlowIn(Animation_StopAtNode.IdFlowIn));

                _stopAtOutsideOutCheckBox.SetupCheck(stopAt.FlowOut(Animation_StopAtNode.IdFlowOut));
                _stopAtOutsideNoDoneCheckBox.SetupNegateCheck(stopAt.FlowOut(Animation_StopAtNode.IdFlowDone));
                _stopAtOutsideStartDoneCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowDone));
            }

            // ── Stopped from another animation's [done] ────────────────────────────
            // The [done] of the short animation stops the long one, which then never fires its own [done].
            {
                var shortIndex = _stopFromDoneShort.AnimationIndex(context);
                var longIndex = _stopFromDoneLong.AnimationIndex(context);
                var shortStart = AnimationTestHelper.CreateStart(nodeCreator, shortIndex, 0f, ShortDuration);
                var longStart = AnimationTestHelper.CreateStart(nodeCreator, longIndex, 0f, LongDuration);
                var stopLong = AnimationTestHelper.CreateStop(nodeCreator, longIndex);
                shortStart.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(stopLong.FlowIn(Animation_StopNode.IdFlowIn));

                context.NewEntryPoint(_stopFromDoneOutCheckBox.GetText(), LongDuration + 1f);
                context.AddToCurrentEntrySequence(
                    longStart.FlowIn(Animation_StartNode.IdFlowIn),
                    shortStart.FlowIn(Animation_StartNode.IdFlowIn));

                _stopFromDoneOutCheckBox.SetupCheck(stopLong.FlowOut(Animation_StopNode.IdFlowOut));
                _stopFromDoneNoDoneCheckBox.SetupNegateCheck(longStart.FlowOut(Animation_StartNode.IdFlowDone));
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
