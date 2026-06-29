using System;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
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
        private CheckBox _eventPointerRefCheckBox;
        private CheckBox _onTickPointerRefCheckBox;
        private CheckBox _receivePointerRefCheckBox;

        public string GetTestName()
        {
            return "event/Event Refs";
        }

        public string GetTestDescription()
        {
            return "Verifies that event/onStart, event/onTick, and event/receive each output a valid (non-null) event ref.";
        }

        public void PrepareObjects(TestContext context)
        {
            _onStartRefCheckBox = context.AddCheckBox("event/onStart\nref not null", true);
            _onTickRefCheckBox  = context.AddCheckBox("event/onTick\nref not null", true, flowOnce: true);
            _receiveRefCheckBox = context.AddCheckBox("event/receive\nref not null", true);
            _onStartSameRefCheckBox = context.AddCheckBox("event/onStart\ntwo nodes same ref", true);
            _onTickSameRefCheckBox  = context.AddCheckBox("event/onTick\ntwo nodes same ref", true, flowOnce: true);
            _eventPointerRefCheckBox  = context.AddCheckBox("event/onStart\npointer/get isValid", true);
            _onTickPointerRefCheckBox  = context.AddCheckBox("event/onTick\npointer/get isValid", true, flowOnce: true);
            _receivePointerRefCheckBox = context.AddCheckBox("event/receive\npointer/get isValid", true);
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

            // ── Test 6: event ref validated via pointer/get (IdPointerTemplEventByRef) ──
            // Per spec §4.2.5: pointer/get with the event ref pointer sets isValid=true for any ref produced by an event operation.
            context.NewEntryPoint("event ref pointer/get", 0.5f);

            var onStartForRef = nodeCreator.CreateNode<Event_OnStartNode>();

            var pGetEvent = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetEvent, PointersHelper.IdPointerTemplEventByRef, GltfTypes.Ref);
            pGetEvent.ValueIn(PointersHelper.IdPointerEventRef).ConnectToSource(onStartForRef.ValueOut(Event_OnStartNode.IdEvent));

            // isValid must be true: the ref came from an event/onStart node, so it is an event reference
            _eventPointerRefCheckBox.SetupCheck(pGetEvent.ValueOut(Pointer_GetNode.IdIsValid), out var eventPointerCheckFlow, true);
            onStartForRef.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(eventPointerCheckFlow);

            // ── Test 7: event/onTick ref validated via pointer/get ─────────────────
            context.NewEntryPoint("onTick ref pointer/get", 1f);

            var onTickForRef = nodeCreator.CreateNode<Event_OnTickNode>();

            var pGetOnTick = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetOnTick, PointersHelper.IdPointerTemplEventByRef, GltfTypes.Ref);
            pGetOnTick.ValueIn(PointersHelper.IdPointerEventRef).ConnectToSource(onTickForRef.ValueOut(Event_OnTickNode.IdEvent));

            _onTickPointerRefCheckBox.SetupCheck(pGetOnTick.ValueOut(Pointer_GetNode.IdIsValid), out var onTickPointerCheckFlow, true);
            onTickForRef.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(onTickPointerCheckFlow);

            // ── Test 8: event/receive ref validated via pointer/get ────────────────
            var receivePointerEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                "_eventRefsTest_receive_" + Guid.NewGuid());

            var sendForReceivePointer = nodeCreator.CreateNode<Event_SendNode>();
            sendForReceivePointer.Configuration[Event_SendNode.IdEvent].Value = receivePointerEventId;

            context.NewEntryPoint("receive ref pointer/get", 1f);
            context.AddToCurrentEntrySequence(sendForReceivePointer.FlowIn());

            var receiveForPointer = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveForPointer.Configuration[Event_ReceiveNode.IdEventConfig].Value = receivePointerEventId;

            var pGetReceive = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetReceive, PointersHelper.IdPointerTemplEventByRef, GltfTypes.Ref);
            pGetReceive.ValueIn(PointersHelper.IdPointerEventRef).ConnectToSource(receiveForPointer.ValueOut(Event_ReceiveNode.IdEventOut));

            _receivePointerRefCheckBox.SetupCheck(pGetReceive.ValueOut(Pointer_GetNode.IdIsValid), out var receivePointerCheckFlow, true);
            receiveForPointer.FlowOut(Event_ReceiveNode.IdFlowOut).ConnectToFlowDestination(receivePointerCheckFlow);
        }
    }
}





