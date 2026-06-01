using System;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class StopPropagation : ITestCase
    {
        private CheckBox _receiverACheckbox;
        private CheckBox _receiverBCheckbox;

        public string GetTestName() => "event/stopPropagation";

        public string GetTestDescription() =>
            "Sends a custom event. Receiver A stops propagation; Receiver B must not be triggered.";

        public void PrepareObjects(TestContext context)
        {
            _receiverACheckbox = context.AddCheckBox("Receiver A: received event", true);
            _receiverBCheckbox = context.AddCheckBox("Receiver B: NOT triggered (propagation stopped)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Private (underscore-prefixed) event so it stays local to this GLB
            var eventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                "_stopPropagationEvent_" + Guid.NewGuid());

            // ── On start: send the event ─────────────────────────────────────────────
            var sendNode = nodeCreator.CreateNode<Event_SendNode>();
            sendNode.Configuration[Event_SendNode.IdEvent].Value = eventId;

            context.NewEntryPoint(GetTestName(), 1f);
            context.AddToCurrentEntrySequence(sendNode.FlowIn(Event_SendNode.IdFlowIn));

            // ── Receiver A (created first → processes the event before Receiver B) ───
            var receiveA = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveA.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventId;

            // Stop propagation: pass the event ref provided by the receive node
            var stopPropNode = nodeCreator.CreateNode<Event_StopPropagationNode>();
            stopPropNode.ValueIn(Event_StopPropagationNode.IdEvent)
                .ConnectToSource(receiveA.ValueOut(Event_ReceiveNode.IdEventOut));
            stopPropNode.ValueIn(Event_StopPropagationNode.IdStopImmediate).SetValue(false);

            // Flow: receiveA → stopPropagation → checkbox A passes
            receiveA.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(stopPropNode.FlowIn(Event_StopPropagationNode.IdFlowIn));

            _receiverACheckbox.SetupCheck(out var checkAFlowIn);
            stopPropNode.FlowOut(Event_StopPropagationNode.IdFlowOut)
                .ConnectToFlowDestination(checkAFlowIn);

            // ── Receiver B (created second → must NOT fire after stopPropagation) ────
            var receiveB = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveB.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventId;

            // Verify Receiver B fires exactly 0 times; passes at fallback if count == 0
            _receiverBCheckbox.SetupCheckFlowTimes(out var receiverBCounterFlow, 0);
            receiveB.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(receiverBCounterFlow);
        }
    }
}