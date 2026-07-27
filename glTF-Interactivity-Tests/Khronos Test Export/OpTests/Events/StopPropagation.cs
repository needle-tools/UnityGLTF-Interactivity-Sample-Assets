using System;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class StopPropagation : ITestCase
    {
        private CheckBox _immediateReceiverACheckbox;
        private CheckBox _immediateReceiverBCheckbox;
        private CheckBox _nonImmediateReceiverACheckbox;
        private CheckBox _nonImmediateReceiverBCheckbox;
        private CheckBox _invalidEventCheckbox;

        public string GetTestName() => "event/stopPropagation";

        public string GetTestDescription() =>
            "Verifies immediate and non-immediate propagation stopping, and handling of an invalid event reference.";

        public void PrepareObjects(TestContext context)
        {
            _immediateReceiverACheckbox = context.AddCheckBox("stopImmediate=true: Receiver A and out triggered", true);
            _immediateReceiverBCheckbox = context.AddCheckBox("stopImmediate=true: Receiver B not triggered", true);
            _nonImmediateReceiverACheckbox = context.AddCheckBox("stopImmediate=false: Receiver A and out triggered", true);
            _nonImmediateReceiverBCheckbox = context.AddCheckBox("stopImmediate=false: Receiver B triggered once", true);
            _invalidEventCheckbox = context.AddCheckBox("Invalid event ref: out triggered", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            CreateSameGraphHandlerCase(
                context,
                "stopImmediate=true",
                true,
                0,
                _immediateReceiverACheckbox,
                _immediateReceiverBCheckbox);

            CreateSameGraphHandlerCase(
                context,
                "stopImmediate=false",
                false,
                1,
                _nonImmediateReceiverACheckbox,
                _nonImmediateReceiverBCheckbox);

            // An invalid event reference is a no-op, but the out flow must still activate.
            context.NewEntryPoint("invalid event ref", 1f);

            var invalidEventStopNode = nodeCreator.CreateNode<Event_StopPropagationNode>();
            invalidEventStopNode.ValueIn(Event_StopPropagationNode.IdStopImmediate).SetValue(true);
            invalidEventStopNode.ValueIn(Event_StopPropagationNode.IdEvent).SetValue(null);

            context.AddToCurrentEntrySequence(
                invalidEventStopNode.FlowIn(Event_StopPropagationNode.IdFlowIn));

            _invalidEventCheckbox.SetupCheck(out var invalidEventCheckFlowIn);
            invalidEventStopNode.FlowOut(Event_StopPropagationNode.IdFlowOut)
                .ConnectToFlowDestination(invalidEventCheckFlowIn);
        }

        private static void CreateSameGraphHandlerCase(
            TestContext context,
            string caseName,
            bool stopImmediate,
            int expectedReceiverBCalls,
            CheckBox receiverACheckbox,
            CheckBox receiverBCheckbox)
        {
            var nodeCreator = context.interactivityExportContext;

            // Private (underscore-prefixed) event so it stays local to this GLB.
            var eventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                "_stopPropagationEvent_" + Guid.NewGuid());

            var sendNode = nodeCreator.CreateNode<Event_SendNode>();
            sendNode.Configuration[Event_SendNode.IdEvent].Value = eventId;

            context.NewEntryPoint(caseName, 1f);
            context.AddToCurrentEntrySequence(sendNode.FlowIn(Event_SendNode.IdFlowIn));

            // Receiver A appears first in JSON and is therefore activated first.
            var receiveA = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveA.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventId;

            var stopPropNode = nodeCreator.CreateNode<Event_StopPropagationNode>();
            stopPropNode.ValueIn(Event_StopPropagationNode.IdEvent)
                .ConnectToSource(receiveA.ValueOut(Event_ReceiveNode.IdEventOut));
            stopPropNode.ValueIn(Event_StopPropagationNode.IdStopImmediate).SetValue(stopImmediate);

            receiveA.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(stopPropNode.FlowIn(Event_StopPropagationNode.IdFlowIn));

            receiverACheckbox.SetupCheck(out var checkAFlowIn);
            stopPropNode.FlowOut(Event_StopPropagationNode.IdFlowOut)
                .ConnectToFlowDestination(checkAFlowIn);

            // With stopImmediate=true this pending same-graph handler is cancelled.
            // With stopImmediate=false it must still be activated.
            var receiveB = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveB.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventId;

            receiverBCheckbox.SetupCheckFlowTimes(
                out var receiverBCounterFlow,
                expectedReceiverBCalls);
            receiveB.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(receiverBCounterFlow);
        }
    }
}
