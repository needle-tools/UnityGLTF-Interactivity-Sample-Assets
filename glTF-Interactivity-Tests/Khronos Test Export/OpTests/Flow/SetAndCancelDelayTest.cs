using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class SetAndCancelDelayTest : ITestCase
    {
        private CheckBox _flowOutCheckBox;
        private CheckBox _flowDoneInCorrectTimeCheckBox;
        private CheckBox _flowDoneCheckBox;
        private CheckBox _flowErrCheckBox;
        private CheckBox _setDelayCancelCheckBox;
        private CheckBox _cancelCheckBox;
        private CheckBox _cancelOutFlowCheckBox;
        private CheckBox _delayRefCheckBox;
        private CheckBox _flowErrNaNCheckBox;
        private CheckBox _flowErrInfCheckBox;
        private CheckBox _concurrentOrderCheckBox;
        private CheckBox _multiplePendingCheckBox;
        private CheckBox _cancelAllCheckBox;
        private CheckBox _cancelLastDelayNullCheckBox;
        private CheckBox _cancelDelayNullRefCheckBox;
        private CheckBox _cancelDelayFiredRefCheckBox;

        public string GetTestName()
        {
            return "flow/setDelay and cancelDelay";
        }

        public string GetTestDescription()
        {
            return "Verifies setDelay and cancelDelay flow behavior, including resolution of the delay ref through its canonical object-model pointer.";
        }

        public void PrepareObjects(TestContext context)
        {
            _flowOutCheckBox = context.AddCheckBox("Flow [out]", true);
            _flowDoneCheckBox = context.AddCheckBox("Flow [done]", true);
            _flowDoneInCorrectTimeCheckBox = context.AddCheckBox("Flow [done] \nin correct delay", true);
             _flowErrCheckBox = context.AddCheckBox("Flow [err]");
            _setDelayCancelCheckBox = context.AddCheckBox("setDelay [cancel]", true);
            _setDelayCancelCheckBox.Negate();
            
            _cancelCheckBox = context.AddCheckBox("cancelDelay triggered", true);
            _cancelCheckBox.Negate();
            _cancelOutFlowCheckBox = context.AddCheckBox("cancelDelay \nFlow [out]");
            _delayRefCheckBox      = context.AddCheckBox("lastDelay\nref isValid", true);
            context.NewRow();
            _flowErrNaNCheckBox = context.AddCheckBox("Flow [err]\n(NaN)");
            _flowErrInfCheckBox = context.AddCheckBox("Flow [err]\n(+Inf)");
            _concurrentOrderCheckBox = context.AddCheckBox("Concurrent delays\n[done] in time order", true);
            _multiplePendingCheckBox = context.AddCheckBox("One node, 3 pending\n[done] 3x", true);
            _cancelAllCheckBox = context.AddCheckBox("[cancel] cancels all\npending delays", true);
            _cancelAllCheckBox.Negate();
            _cancelLastDelayNullCheckBox = context.AddCheckBox("[cancel] sets\nlastDelay null");
            _cancelDelayNullRefCheckBox = context.AddCheckBox("cancelDelay null ref\nFlow [out]");
            _cancelDelayFiredRefCheckBox = context.AddCheckBox("cancelDelay fired ref\nFlow [out]", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            context.NewEntryPoint("Set Delay", 2f);
            
            var setDelayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();

            setDelayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(1f);
            TimeHelpers.AddTickNode(nodeCreator, TimeHelpers.GetTimeValueOption.TimeSinceStartup, out var timeSinceStartValueRef);

            var startTimeVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(
                "startTime_" + System.Guid.NewGuid().ToString(), 0, GltfTypes.Float);
            var setStartTimeVar = VariablesHelpers.SetVariable(nodeCreator, startTimeVarId, out var setStartTimeVarSocket, out _, out _);
            setStartTimeVarSocket.ConnectToSource(timeSinceStartValueRef);
            context.AddToCurrentEntrySequence(setStartTimeVar.FlowIn(Variable_SetNode.IdFlowIn));
            context.AddToCurrentEntrySequence(setDelayNode.FlowIn(Flow_SetDelayNode.IdFlowIn));
            
            VariablesHelpers.GetVariable(nodeCreator, startTimeVarId, out var startTimeVarRef);
            var subtractNode = nodeCreator.CreateNode<Math_SubNode>();
            subtractNode.ValueIn(Math_SubNode.IdValueA).ConnectToSource(timeSinceStartValueRef);
            subtractNode.ValueIn(Math_SubNode.IdValueB).ConnectToSource(startTimeVarRef);
            
            _flowDoneCheckBox.SetupCheck(out var flowDoneCheckFlow);
            _flowDoneInCorrectTimeCheckBox.proximityCheckDistance = 0.1f; // time Tolerance
            _flowDoneInCorrectTimeCheckBox.SetupCheck(subtractNode.FirstValueOut(), out var startTimeVarCheckFlow, 1f, true);
            _flowOutCheckBox.SetupCheckFlowTimes(out var flowOutCheckFlow, 1);
            setDelayNode.FlowOut(Flow_SetDelayNode.IdFlowOut).ConnectToFlowDestination(flowOutCheckFlow);
           
            context.AddSequence(setDelayNode.FlowOut(Flow_SetDelayNode.IdFlowDone), new FlowInRef[]
                {
                    flowDoneCheckFlow,
                    startTimeVarCheckFlow,
                });
            
            // setDelay Cancel
            context.NewEntryPoint(_setDelayCancelCheckBox.GetText(), 2f);
            var setDelayNode2 = nodeCreator.CreateNode<Flow_SetDelayNode>();
            setDelayNode2.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(1f);
            context.AddToCurrentEntrySequence(setDelayNode2.FlowIn());
            context.AddToCurrentEntrySequence(setDelayNode2.FlowIn(Flow_SetDelayNode.IdFlowInCancel));
            _setDelayCancelCheckBox.SetupNegateCheck(setDelayNode2.FlowOut(Flow_SetDelayNode.IdFlowDone));
            
            
            // Cancel Delay

            context.NewEntryPoint("Cancel Delay", 2f);
            var delayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(1f);
            
            _cancelCheckBox.SetupNegateCheck(delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone));
            
            var cancelDelayNode = nodeCreator.CreateNode<Flow_CancelDelayNode>();
            cancelDelayNode.ValueIn(Flow_CancelDelayNode.IdDelay)
                .ConnectToSource(delayNode.ValueOut(Flow_SetDelayNode.IdOutLastDelay));
            
            context.AddToCurrentEntrySequence(delayNode.FlowIn(), cancelDelayNode.FlowIn());
            _cancelOutFlowCheckBox.SetupCheck(cancelDelayNode.FlowOut());
            
            // Delay with Error
            context.NewEntryPoint("Error", 2f);
            var delayNode2 = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode2.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(-1f);
            context.AddToCurrentEntrySequence(delayNode2.FlowIn());
            _flowErrCheckBox.SetupCheck(delayNode2.FlowOut(Flow_SetDelayNode.IdFlowOutError));

            // Delay Ref valid check via pointer/get (IdPointerTemplDelayByRef, spec §4.2.4)
            context.NewEntryPoint("Delay Ref", 0.5f);
            var setDelayRef = nodeCreator.CreateNode<Flow_SetDelayNode>();
            setDelayRef.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(2f);
            context.AddToCurrentEntrySequence(setDelayRef.FlowIn(Flow_SetDelayNode.IdFlowIn));

            var pGetDelay = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetDelay, PointersHelper.IdPointerTemplDelayByRef, GltfTypes.Ref);
            pGetDelay.ValueIn(PointersHelper.IdPointerDelayRef).ConnectToSource(setDelayRef.ValueOut(Flow_SetDelayNode.IdOutLastDelay));

            // isValid is true while the delay is still scheduled (checked immediately after setDelay.out)
            _delayRefCheckBox.SetupCheck(pGetDelay.ValueOut(Pointer_GetNode.IdIsValid), out var delayRefCheckFlow, true);
            setDelayRef.FlowOut(Flow_SetDelayNode.IdFlowOut).ConnectToFlowDestination(delayRefCheckFlow);

            // NaN and +Inf durations -> err
            void AddErrorCheck(CheckBox checkBox, float duration)
            {
                context.NewEntryPoint(checkBox.GetText());
                var errNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
                errNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(duration);
                context.AddToCurrentEntrySequence(errNode.FlowIn());
                checkBox.SetupCheck(errNode.FlowOut(Flow_SetDelayNode.IdFlowOutError));
            }
            AddErrorCheck(_flowErrNaNCheckBox, float.NaN);
            AddErrorCheck(_flowErrInfCheckBox, float.PositiveInfinity);

            // Concurrent delays of different nodes fire in the order of their activation times,
            // not in the order they were scheduled: 0.6s, 0.2s, 0.4s -> done order 0.2s, 0.4s, 0.6s
            context.NewEntryPoint(_concurrentOrderCheckBox.GetText(), 1.5f);
            var delaySlow = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delaySlow.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.6f);
            var delayFast = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayFast.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.2f);
            var delayMid = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayMid.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.4f);
            context.AddToCurrentEntrySequence(delaySlow.FlowIn(), delayFast.FlowIn(), delayMid.FlowIn());
            _concurrentOrderCheckBox.SetupOrderFlowCheck(new[]
            {
                delayFast.FlowOut(Flow_SetDelayNode.IdFlowDone),
                delayMid.FlowOut(Flow_SetDelayNode.IdFlowDone),
                delaySlow.FlowOut(Flow_SetDelayNode.IdFlowDone),
            });

            // Every activation of the same node schedules its own delay
            context.NewEntryPoint(_multiplePendingCheckBox.GetText(), 1f);
            var multiDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            multiDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.3f);
            context.AddToCurrentEntrySequence(multiDelay.FlowIn(), multiDelay.FlowIn(), multiDelay.FlowIn());
            _multiplePendingCheckBox.SetupCheckFlowTimes(out var multiDoneFlow, 3);
            multiDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(multiDoneFlow);

            // [cancel] cancels every pending delay of the node and sets lastDelay to null
            context.NewEntryPoint(_cancelAllCheckBox.GetText(), 1f);
            var cancelAllDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            cancelAllDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.3f);

            var lastDelayIsNull = nodeCreator.CreateNode<Ref_EqNode>();
            lastDelayIsNull.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(cancelAllDelay.ValueOut(Flow_SetDelayNode.IdOutLastDelay));
            lastDelayIsNull.ValueIn(Ref_EqNode.IdValueB).SetValue(null);
            _cancelLastDelayNullCheckBox.SetupCheck(lastDelayIsNull.ValueOut(Ref_EqNode.IdOutValue), out var lastDelayNullCheckFlow, true);

            context.AddToCurrentEntrySequence(
                cancelAllDelay.FlowIn(),
                cancelAllDelay.FlowIn(),
                cancelAllDelay.FlowIn(),
                cancelAllDelay.FlowIn(Flow_SetDelayNode.IdFlowInCancel),
                lastDelayNullCheckFlow);
            _cancelAllCheckBox.SetupNegateCheck(cancelAllDelay.FlowOut(Flow_SetDelayNode.IdFlowDone));

            // cancelDelay with a null ref must not cause an error, [out] still fires
            context.NewEntryPoint(_cancelDelayNullRefCheckBox.GetText());
            var cancelNullRef = nodeCreator.CreateNode<Flow_CancelDelayNode>();
            cancelNullRef.ValueIn(Flow_CancelDelayNode.IdDelay).SetValue(null);
            context.AddToCurrentEntrySequence(cancelNullRef.FlowIn());
            _cancelDelayNullRefCheckBox.SetupCheck(cancelNullRef.FlowOut());

            // cancelDelay with a ref whose delay already fired: nothing to cancel, [out] still fires
            context.NewEntryPoint(_cancelDelayFiredRefCheckBox.GetText(), 1f);
            var firedDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            firedDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(0.1f);
            var cancelFiredRef = nodeCreator.CreateNode<Flow_CancelDelayNode>();
            cancelFiredRef.ValueIn(Flow_CancelDelayNode.IdDelay).ConnectToSource(firedDelay.ValueOut(Flow_SetDelayNode.IdOutLastDelay));
            context.AddToCurrentEntrySequence(firedDelay.FlowIn());
            firedDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(cancelFiredRef.FlowIn());
            _cancelDelayFiredRefCheckBox.SetupCheck(cancelFiredRef.FlowOut());
        }
    }
}