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
        
        public string GetTestName()
        {
            return "flow/setDelay and cancelDelay";
        }

        public string GetTestDescription()
        {
            return "";
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
            var setStartTimeVar = VariablesHelpers.SetVariable(nodeCreator, startTimeVarId);
            setStartTimeVar.ValueIn(Variable_SetNode.IdInputValue).ConnectToSource(timeSinceStartValueRef);
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
            cancelDelayNode.ValueIn(Flow_CancelDelayNode.IdDelayIndex)
                .ConnectToSource(delayNode.ValueOut(Flow_SetDelayNode.IdOutLastDelayIndex));
            
            context.AddToCurrentEntrySequence(delayNode.FlowIn(), cancelDelayNode.FlowIn());
            _cancelOutFlowCheckBox.SetupCheck(cancelDelayNode.FlowOut());
            
            // Delay with Error
            context.NewEntryPoint("Error", 2f);
            var delayNode2 = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode2.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(-1f);
            context.AddToCurrentEntrySequence(delayNode2.FlowIn());
            _flowErrCheckBox.SetupCheck(delayNode2.FlowOut(Flow_SetDelayNode.IdFlowOutError));
            

        }
    }
}