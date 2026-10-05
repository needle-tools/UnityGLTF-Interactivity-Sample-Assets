using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Activation order and output values of the lifecycle and custom event nodes.
    ///
    /// Checks:
    ///  - several event/onStart nodes are activated in the order they appear in JSON
    ///  - several event/receive nodes for the same event are activated in JSON order and all get the sent value
    ///  - the very first tick has timeSinceStart == 0 and timeSinceLastTick NaN
    ///  - the first tick happens after the event/onStart nodes were activated
    ///  - several event/onTick nodes are activated in JSON order and have the same values within one tick
    ///  - timeSinceStart never decreases between ticks
    ///
    /// JSON order is the node creation order here: the exporter's final sort keeps the relative order of
    /// nodes without incoming connections, which includes all event nodes.
    /// </summary>
    public class EventOrderAndTickTest : ITestCase
    {
        private CheckBox _onStartOrderCheckBox;
        private CheckBox _receiveOrderCheckBox;
        private CheckBox _receiveValuesCheckBox;
        private CheckBox _firstTickTimeSinceStartCheckBox;
        private CheckBox _firstTickTimeSinceLastTickCheckBox;
        private CheckBox _firstTickAfterOnStartCheckBox;
        private CheckBox _tickOrderCheckBox;
        private CheckBox _tickSameValuesCheckBox;
        private CheckBox _tickNonDecreasingCheckBox;

        private const int SentValue = 7;

        public string GetTestName()
        {
            return "event/activation order and onTick";
        }

        public string GetTestDescription()
        {
            return "Checks that onStart, onTick and receive nodes are activated in JSON order, and the onTick output values (first tick, same values within a tick).";
        }

        public void PrepareObjects(TestContext context)
        {
            _onStartOrderCheckBox = context.AddCheckBox("onStart:\nJSON order");
            _receiveOrderCheckBox = context.AddCheckBox("receive:\nJSON order", true);
            _receiveValuesCheckBox = context.AddCheckBox("receive: all get\nthe sent value", true);
            _receiveValuesCheckBox.Negate();
            context.NewRow();
            _firstTickTimeSinceStartCheckBox = context.AddCheckBox("1st tick:\ntimeSinceStart 0", true);
            _firstTickTimeSinceLastTickCheckBox = context.AddCheckBox("1st tick:\ntimeSinceLastTick NaN", true);
            _firstTickAfterOnStartCheckBox = context.AddCheckBox("1st tick\nafter onStart", true);
            _tickOrderCheckBox = context.AddCheckBox("onTick:\nJSON order", true);
            _tickSameValuesCheckBox = context.AddCheckBox("onTick: same\nvalues in a tick", true);
            _tickSameValuesCheckBox.Negate();
            _tickNonDecreasingCheckBox = context.AddCheckBox("timeSinceStart\nnon-decreasing", true);
            _tickNonDecreasingCheckBox.Negate();
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // ── onStart: JSON order ────────────────────────────────────────────────
            // The entry point's own onStart node comes first in JSON, so its fallback check would run
            // before the three onStart nodes below. The delay moves the fallback after them.
            context.NewEntryPoint(_onStartOrderCheckBox.GetText(), 0.2f);
            var onStartA = nodeCreator.CreateNode<Event_OnStartNode>();
            var onStartB = nodeCreator.CreateNode<Event_OnStartNode>();
            var onStartC = nodeCreator.CreateNode<Event_OnStartNode>();
            _onStartOrderCheckBox.SetupOrderFlowCheck(new[]
            {
                onStartA.FlowOut(Event_OnStartNode.IdFlowOut),
                onStartB.FlowOut(Event_OnStartNode.IdFlowOut),
                onStartC.FlowOut(Event_OnStartNode.IdFlowOut),
            });

            // ── receive: JSON order and identical values ───────────────────────────
            var eventParameters = new Dictionary<string, GltfInteractivityNode.EventValues>();
            eventParameters.Add("value", new GltfInteractivityNode.EventValues
            {
                Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Int),
                Value = 0
            });
            var customEventId = nodeCreator.Context.AddEventWithIdIfNeeded("_eventOrderTest_" + Guid.NewGuid(), eventParameters);

            var sendNode = nodeCreator.CreateNode<Event_SendNode>();
            sendNode.Configuration[Event_SendNode.IdEvent].Value = customEventId;
            sendNode.ValueIn("value").SetValue(SentValue);

            context.NewEntryPoint(_receiveOrderCheckBox.GetText(), 0.5f);
            context.AddToCurrentEntrySequence(sendNode.FlowIn());

            // Each receiver: [0] -> order check, [1] -> value check (a mismatch fires the negated check)
            _receiveValuesCheckBox.SetupNegateCheck(out var wrongValueFlowIn);
            var receiverOrderFlows = new FlowOutRef[3];
            for (int i = 0; i < receiverOrderFlows.Length; i++)
            {
                var receiveNode = nodeCreator.CreateNode<Event_ReceiveNode>();
                receiveNode.Configuration[Event_ReceiveNode.IdEventConfig].Value = customEventId;

                var receiverSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
                receiveNode.FlowOut(Event_ReceiveNode.IdFlowOut).ConnectToFlowDestination(receiverSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
                receiverOrderFlows[i] = receiverSequence.FlowOut("0");

                var valueEq = nodeCreator.CreateNode<Math_EqNode>();
                valueEq.ValueIn(Math_EqNode.IdValueA).ConnectToSource(receiveNode.ValueOut("value"));
                valueEq.ValueIn(Math_EqNode.IdValueB).SetValue(SentValue);
                var valueBranch = nodeCreator.CreateNode<Flow_BranchNode>();
                valueBranch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(valueEq.FirstValueOut());
                receiverSequence.FlowOut("1").ConnectToFlowDestination(valueBranch.FlowIn(Flow_BranchNode.IdFlowIn));
                valueBranch.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(wrongValueFlowIn);
            }
            _receiveOrderCheckBox.SetupOrderFlowCheck(receiverOrderFlows);

            // ── onTick ─────────────────────────────────────────────────────────────
            context.NewEntryPoint(_firstTickTimeSinceStartCheckBox.GetText(), 1f);

            // Set by an own onStart node (not the entry point, which may start later in the all-in-one
            // export); must already be true on the very first tick.
            var onStartDoneVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("OnStartBeforeTick_" + Guid.NewGuid(), false, GltfTypes.Bool);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, onStartDoneVarId, true, out var setOnStartDoneFlowIn, out _);
            var onStartForTick = nodeCreator.CreateNode<Event_OnStartNode>();
            onStartForTick.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(setOnStartDoneFlowIn);
            VariablesHelpers.GetVariable(nodeCreator, onStartDoneVarId, out var onStartDoneValue);

            // timeSinceStart of the first onTick node in the current tick, read by the second one.
            var tickTimeVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("TickTimeSinceStart_" + Guid.NewGuid(), 0f, GltfTypes.Float);
            VariablesHelpers.GetVariable(nodeCreator, tickTimeVarId, out var tickTimeValue);

            var onTickA = nodeCreator.CreateNode<Event_OnTickNode>();
            var onTickB = nodeCreator.CreateNode<Event_OnTickNode>();
            var onTickC = nodeCreator.CreateNode<Event_OnTickNode>();

            // flow/doN with n = 1 lets each onTick node pass only on its first activation
            FlowOutRef FirstActivationOnly(FlowOutRef flow)
            {
                var doOnce = nodeCreator.CreateNode<Flow_DoNNode>();
                doOnce.ValueIn(Flow_DoNNode.IdN).SetValue(1);
                flow.ConnectToFlowDestination(doOnce.FlowIn(Flow_DoNNode.IdFlowIn));
                return doOnce.FlowOut(Flow_DoNNode.IdOut);
            }

            // onTick A: [0] first-tick checks, [1] order, [2] non-decreasing check, [3] store timeSinceStart
            var tickASequence = nodeCreator.CreateNode<Flow_SequenceNode>();
            onTickA.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(tickASequence.FlowIn(Flow_SequenceNode.IdFlowIn));

            _firstTickTimeSinceStartCheckBox.SetupCheck(onTickA.ValueOut(Event_OnTickNode.IdOutTimeSinceStart), out var firstTickTimeSinceStartFlow, 0f);
            _firstTickTimeSinceLastTickCheckBox.SetupCheck(onTickA.ValueOut(Event_OnTickNode.IdOutTimeSinceLastTick), out var firstTickTimeSinceLastTickFlow, float.NaN);
            _firstTickAfterOnStartCheckBox.SetupCheck(onStartDoneValue, out var firstTickAfterOnStartFlow, true);
            context.AddSequence(FirstActivationOnly(tickASequence.FlowOut("0")),
                firstTickTimeSinceStartFlow,
                firstTickTimeSinceLastTickFlow,
                firstTickAfterOnStartFlow);

            var tickAOrderFlow = FirstActivationOnly(tickASequence.FlowOut("1"));

            var decreasedNode = nodeCreator.CreateNode<Math_LtNode>();
            decreasedNode.ValueIn(Math_LtNode.IdValueA).ConnectToSource(onTickA.ValueOut(Event_OnTickNode.IdOutTimeSinceStart));
            decreasedNode.ValueIn(Math_LtNode.IdValueB).ConnectToSource(tickTimeValue);
            var decreasedBranch = nodeCreator.CreateNode<Flow_BranchNode>();
            decreasedBranch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(decreasedNode.FirstValueOut());
            tickASequence.FlowOut("2").ConnectToFlowDestination(decreasedBranch.FlowIn(Flow_BranchNode.IdFlowIn));
            _tickNonDecreasingCheckBox.SetupNegateCheck(decreasedBranch.FlowOut(Flow_BranchNode.IdFlowOutTrue));

            VariablesHelpers.SetVariable(nodeCreator, tickTimeVarId, onTickA.ValueOut(Event_OnTickNode.IdOutTimeSinceStart), tickASequence.FlowOut("3"));

            // onTick B: [0] order, [1] same timeSinceStart as onTick A in this tick
            var tickBSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
            onTickB.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(tickBSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
            var tickBOrderFlow = FirstActivationOnly(tickBSequence.FlowOut("0"));

            var sameValueNode = nodeCreator.CreateNode<Math_EqNode>();
            sameValueNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(onTickB.ValueOut(Event_OnTickNode.IdOutTimeSinceStart));
            sameValueNode.ValueIn(Math_EqNode.IdValueB).ConnectToSource(tickTimeValue);
            var sameValueBranch = nodeCreator.CreateNode<Flow_BranchNode>();
            sameValueBranch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(sameValueNode.FirstValueOut());
            tickBSequence.FlowOut("1").ConnectToFlowDestination(sameValueBranch.FlowIn(Flow_BranchNode.IdFlowIn));
            _tickSameValuesCheckBox.SetupNegateCheck(sameValueBranch.FlowOut(Flow_BranchNode.IdFlowOutFalse));

            // onTick C: order only
            var tickCOrderFlow = FirstActivationOnly(onTickC.FlowOut(Event_OnTickNode.IdFlowOut));

            _tickOrderCheckBox.SetupOrderFlowCheck(new[] { tickAOrderFlow, tickBOrderFlow, tickCOrderFlow });
        }
    }
}
