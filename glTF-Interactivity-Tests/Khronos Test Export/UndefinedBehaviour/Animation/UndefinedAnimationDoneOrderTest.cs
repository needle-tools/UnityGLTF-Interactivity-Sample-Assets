using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: animations that end in the same update activate their [done] in the order of the
    /// "implementation-defined animation state dynamic array"; where a restarted entry goes is not specified either.
    /// Both animations are one-shot (startTime == endTime), so they end in the same update.
    ///
    /// Started P, then Q:
    ///  A: [done] in start order (P first)
    ///  B: reverse order (Q first)
    ///
    /// Started P, Q, then P again (restart removes P's entry and adds a new one):
    ///  A: the new entry is appended (Q first)
    ///  B: the entry keeps its place (P first)
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationDoneOrderTest : ITestCase, IDisposable
    {
        private const int P = 1;
        private const int Q = 2;

        private readonly List<AnimatedTestObject> _objects = new List<AnimatedTestObject>();
        private AnimatedTestObject _orderP, _orderQ, _restartP, _restartQ;

        private CheckBox _startOrderCheckBox;
        private CheckBox _reverseOrderCheckBox;
        private CheckBox _restartAppendedCheckBox;
        private CheckBox _restartKeepsPlaceCheckBox;

        public string GetTestName() => "animation/done order";

        public string GetTestDescription() =>
            "Animations ending in the same update fire [done] in the order of the implementation-defined animation state array. " +
            "Started P, Q: A: start order (P first). B: Q first. " +
            "Started P, Q, P again: A: restarted entry is appended (Q first). B: it keeps its place (P first).";

        public void PrepareObjects(TestContext context)
        {
            AnimatedTestObject Create(string name)
            {
                var obj = new AnimatedTestObject(context.Root, name, TargetPosition, T);
                _objects.Add(obj);
                return obj;
            }
            _orderP = Create("UndefinedDoneOrderPTarget");
            _orderQ = Create("UndefinedDoneOrderQTarget");
            _restartP = Create("UndefinedDoneOrderRestartPTarget");
            _restartQ = Create("UndefinedDoneOrderRestartQTarget");

            _startOrderCheckBox = context.AddCheckBox("A: start P, Q:\nP [done] first", true);
            _reverseOrderCheckBox = context.AddCheckBox("B: start P, Q:\nQ [done] first", true);
            context.NewRow();
            _restartAppendedCheckBox = context.AddCheckBox("A: start P, Q, P:\nQ [done] first (appended)", true);
            _restartKeepsPlaceCheckBox = context.AddCheckBox("B: start P, Q, P:\nP [done] first (kept place)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── Start order ────────────────────────────────────────────────────────
            {
                var firstVar = AddVariable(context, "UndefinedDoneOrder_first", 0, GltfTypes.Int);
                var startP = AnimationTestHelper.CreateStart(nodeCreator, _orderP.AnimationIndex(context), 0.5f * T, 0.5f * T);
                var startQ = AnimationTestHelper.CreateStart(nodeCreator, _orderQ.AnimationIndex(context), 0.5f * T, 0.5f * T);
                startP.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(RecordFirst(context, firstVar, P));
                startQ.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(RecordFirst(context, firstVar, Q));

                var delay = AnimationTestHelper.CreateDelay(nodeCreator, 1f);
                context.NewEntryPoint(_startOrderCheckBox.GetText(), 1.5f);
                context.AddToCurrentEntrySequence(
                    startP.FlowIn(Animation_StartNode.IdFlowIn),
                    startQ.FlowIn(Animation_StartNode.IdFlowIn),
                    delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                var first = Get(context, firstVar);
                _startOrderCheckBox.SetupCheck(first, out var startOrderFlow, P);
                _reverseOrderCheckBox.SetupCheck(first, out var reverseOrderFlow, Q);
                context.AddSequence(delay.FlowOut(Flow_SetDelayNode.IdFlowDone), startOrderFlow, reverseOrderFlow);
            }

            // ── Restart ────────────────────────────────────────────────────────────
            {
                var firstVar = AddVariable(context, "UndefinedDoneOrder_restartFirst", 0, GltfTypes.Int);
                var pIndex = _restartP.AnimationIndex(context);
                var startP = AnimationTestHelper.CreateStart(nodeCreator, pIndex, 0.5f * T, 0.5f * T);
                var startQ = AnimationTestHelper.CreateStart(nodeCreator, _restartQ.AnimationIndex(context), 0.5f * T, 0.5f * T);
                var restartP = AnimationTestHelper.CreateStart(nodeCreator, pIndex, 0.5f * T, 0.5f * T);
                // startP's [done] must not fire, the restart replaces its entry
                restartP.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(RecordFirst(context, firstVar, P));
                startQ.FlowOut(Animation_StartNode.IdFlowDone).ConnectToFlowDestination(RecordFirst(context, firstVar, Q));

                var delay = AnimationTestHelper.CreateDelay(nodeCreator, 1f);
                context.NewEntryPoint(_restartAppendedCheckBox.GetText(), 1.5f);
                context.AddToCurrentEntrySequence(
                    startP.FlowIn(Animation_StartNode.IdFlowIn),
                    startQ.FlowIn(Animation_StartNode.IdFlowIn),
                    restartP.FlowIn(Animation_StartNode.IdFlowIn),
                    delay.FlowIn(Flow_SetDelayNode.IdFlowIn));

                var first = Get(context, firstVar);
                _restartAppendedCheckBox.SetupCheck(first, out var appendedFlow, Q);
                _restartKeepsPlaceCheckBox.SetupCheck(first, out var keptPlaceFlow, P);
                context.AddSequence(delay.FlowOut(Flow_SetDelayNode.IdFlowDone), appendedFlow, keptPlaceFlow);
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
