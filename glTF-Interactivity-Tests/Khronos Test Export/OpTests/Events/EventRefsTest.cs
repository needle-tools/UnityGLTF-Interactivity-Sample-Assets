using System;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Verifies that common event nodes output a valid (non-null) event ref on activation.
    /// Tests: event/onStart, event/onTick, event/receive.
    /// </summary>
    public class EventRefsTest : ITestCase
    {
        private CheckBox _onStartRefCheckBox;
        private CheckBox _onTickRefCheckBox;
        private CheckBox _receiveRefCheckBox;
        private CheckBox _onStartSameRefCheckBox;
        private CheckBox _onTickSameRefCheckBox;

        public string GetTestName()
        {
            return "event refs";
        }

        public string GetTestDescription()
        {
            return "Verifies that event/onStart, event/onTick, and event/receive each output a valid (non-null) event ref.";
        }

        public void PrepareObjects(TestContext context)
        {
            _onStartRefCheckBox = context.AddCheckBox("event/onStart\nref not null", true);
            _onTickRefCheckBox  = context.AddCheckBox("event/onTick\nref not null", true);
            _receiveRefCheckBox = context.AddCheckBox("event/receive\nref not null", true);
            _onStartSameRefCheckBox = context.AddCheckBox("event/onStart\ntwo nodes same ref", true);
            _onTickSameRefCheckBox  = context.AddCheckBox("event/onTick\ntwo nodes same ref", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── Test 1: event/onStart – event ref must not be null ──────────────────
            context.NewEntryPoint("event/onStart ref", 0.5f);

            var onStartNode = nodeCreator.CreateNode<Event_OnStartNode>();

            var refEqOnStart = nodeCreator.CreateNode<Ref_EqNode>();
            refEqOnStart.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(onStartNode.ValueOut(Event_OnStartNode.IdEvent));
            refEqOnStart.ValueIn(Ref_EqNode.IdValueB).SetValue(null);

            // refEqOnStart outputs true when ref == null; we expect false (ref is valid)
            _onStartRefCheckBox.SetupCheck(refEqOnStart.FirstValueOut(), out var onStartCheckFlow, false);
            onStartNode.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(onStartCheckFlow);

            // ── Test 2: event/onTick – event ref must not be null ───────────────────
            context.NewEntryPoint("event/onTick ref", 1f);

            var onTickNode = nodeCreator.CreateNode<Event_OnTickNode>();

            var refEqOnTick = nodeCreator.CreateNode<Ref_EqNode>();
            refEqOnTick.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(onTickNode.ValueOut(Event_OnTickNode.IdEvent));
            refEqOnTick.ValueIn(Ref_EqNode.IdValueB).SetValue(null);

            // Same logic: expect false (ref is not null)
            _onTickRefCheckBox.SetupCheck(refEqOnTick.FirstValueOut(), out var onTickCheckFlow, false);
            onTickNode.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(onTickCheckFlow);

            // ── Test 3: event/receive – event ref must not be null ──────────────────
            var customEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                "_eventRefsTest_" + Guid.NewGuid());

            var sendNode = nodeCreator.CreateNode<Event_SendNode>();
            sendNode.Configuration[Event_SendNode.IdEvent].Value = customEventId;

            context.NewEntryPoint("event/receive ref", 1f);
            // Trigger the custom event from the onStart entry
            context.AddToCurrentEntrySequence(sendNode.FlowIn());

            var receiveNode = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveNode.Configuration[Event_ReceiveNode.IdEventConfig].Value = customEventId;

            var refEqReceive = nodeCreator.CreateNode<Ref_EqNode>();
            refEqReceive.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(receiveNode.ValueOut(Event_ReceiveNode.IdEventOut));
            refEqReceive.ValueIn(Ref_EqNode.IdValueB).SetValue(null);

            // Expect false: ref is not null when custom event is received
            _receiveRefCheckBox.SetupCheck(refEqReceive.FirstValueOut(), out var receiveCheckFlow, false);
            receiveNode.FlowOut(Event_ReceiveNode.IdFlowOut).ConnectToFlowDestination(receiveCheckFlow);

            // ── Test 4: Two event/onStart nodes must return the same ref ───────────
            context.NewEntryPoint("event/onStart same ref", 0.5f);

            var onStartNodeA = nodeCreator.CreateNode<Event_OnStartNode>();
            var onStartNodeB = nodeCreator.CreateNode<Event_OnStartNode>();

            var refEqOnStartSame = nodeCreator.CreateNode<Ref_EqNode>();
            refEqOnStartSame.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(onStartNodeA.ValueOut(Event_OnStartNode.IdEvent));
            refEqOnStartSame.ValueIn(Ref_EqNode.IdValueB).ConnectToSource(onStartNodeB.ValueOut(Event_OnStartNode.IdEvent));

            // Both onStart nodes must share the same event ref → expect true
            _onStartSameRefCheckBox.SetupCheck(refEqOnStartSame.ValueOut(Ref_EqNode.IdOutValue), out var onStartSameCheckFlow, true);
            onStartNodeA.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(onStartSameCheckFlow);

            // ── Test 5: Two event/onTick nodes must return the same ref ────────────
            context.NewEntryPoint("event/onTick same ref", 1f);

            var onTickNodeA = nodeCreator.CreateNode<Event_OnTickNode>();
            var onTickNodeB = nodeCreator.CreateNode<Event_OnTickNode>();

            var refEqOnTickSame = nodeCreator.CreateNode<Ref_EqNode>();
            refEqOnTickSame.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(onTickNodeA.ValueOut(Event_OnTickNode.IdEvent));
            refEqOnTickSame.ValueIn(Ref_EqNode.IdValueB).ConnectToSource(onTickNodeB.ValueOut(Event_OnTickNode.IdEvent));

            // Both onTick nodes must share the same event ref per tick → expect true
            _onTickSameRefCheckBox.SetupCheck(refEqOnTickSame.ValueOut(Ref_EqNode.IdOutValue), out var onTickSameCheckFlow, true);
            onTickNodeA.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(onTickSameCheckFlow);
        }
    }
}





