using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ThrottleTest : ITestCase
    {
        private CheckBox _outThrottleCheckBox;
        private CheckBox _lastRemainingTimeCheckBox;
        private CheckBox _flowOutAfterDelayCheckBox;
        private CheckBox _setDelayCheckBox;
        private CheckBox _errFlowCheckBox;
        private CheckBox _errFlowOutCheckBox;
        private CheckBox _resetCheckBox;
        
        public string GetTestName()
        {
            return "flow/throttle";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _outThrottleCheckBox = context.AddCheckBox("Out Throttle");
            _lastRemainingTimeCheckBox = context.AddCheckBox("Last Remaining Time");
            _flowOutAfterDelayCheckBox = context.AddCheckBox("Flow Out After Delay", true);
            _setDelayCheckBox = context.AddCheckBox("SubTest: setDelay", true);
            _errFlowCheckBox = context.AddCheckBox("Error Flow on -1 Duration");
            _errFlowOutCheckBox = context.AddCheckBox("Ignore Flow Out when Error");
            _errFlowOutCheckBox.Negate();
            _resetCheckBox = context.AddCheckBox("Reset");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            // Basic test - only once flow out
            var throttleNode = nodeCreator.CreateNode(new Flow_ThrottleNode());
            context.NewEntryPoint(_outThrottleCheckBox.GetText());
            
            _lastRemainingTimeCheckBox.proximityCheckDistance = 0.01f;
            _lastRemainingTimeCheckBox.SetupCheck(throttleNode.ValueOut(Flow_ThrottleNode.IdOutElapsedTime),
                out var lastRemainingTimeCheckFlow, 1f, true);

            
            context.AddToCurrentEntrySequence(new []
            {
                throttleNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
                lastRemainingTimeCheckFlow,
            });
            _outThrottleCheckBox.SetupCheckFlowTimes(out var outThrottleCheckFlow, 1);
            throttleNode.FlowOut(Flow_ThrottleNode.IdFlowOut).ConnectToFlowDestination(outThrottleCheckFlow);
            throttleNode.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(1f);
        
            // Wait for delay
            
            var throttleNode2 = nodeCreator.CreateNode(new Flow_ThrottleNode());
            context.NewEntryPoint(_flowOutAfterDelayCheckBox.GetText(), 2f);
            
            var delayNode = nodeCreator.CreateNode(new Flow_SetDelayNode());
            delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(1.5f);

            _setDelayCheckBox.SetupCheck(out var setDelayCheckFlow);
            context.AddSequence(delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone),
                new []
                {
                    setDelayCheckFlow,
                    throttleNode2.FlowIn(Flow_ThrottleNode.IdFlowIn)
                });
            
            context.AddToCurrentEntrySequence(new []
            {
                throttleNode2.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode2.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode2.FlowIn(Flow_ThrottleNode.IdFlowIn),
                delayNode.FlowIn(Flow_SetDelayNode.IdFlowIn)
            });

            throttleNode2.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(1f);

            _flowOutAfterDelayCheckBox.SetupCheckFlowTimes(out var flowOutAfterDelayCheckFlow, 2);
            throttleNode2.FlowOut(Flow_ThrottleNode.IdFlowOut).ConnectToFlowDestination(flowOutAfterDelayCheckFlow);
            
            // Error flow
            var throttleNode3 = nodeCreator.CreateNode(new Flow_ThrottleNode());
            context.NewEntryPoint(throttleNode3.FlowIn(Flow_ThrottleNode.IdFlowIn), _errFlowCheckBox.GetText());
            throttleNode3.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(-1);
            
            _errFlowCheckBox.SetupCheck(throttleNode3.FlowOut(Flow_ThrottleNode.IdFlowOutError));
            _errFlowOutCheckBox.SetupNegateCheck(throttleNode3.FlowOut(Flow_ThrottleNode.IdFlowOut));
            
            // Reset
            var throttleNode4 = nodeCreator.CreateNode(new Flow_ThrottleNode());
            context.NewEntryPoint(_resetCheckBox.GetText());
            throttleNode4.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(2f);
            context.AddToCurrentEntrySequence(new []
            {
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowReset),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowReset),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleNode4.FlowIn(Flow_ThrottleNode.IdFlowIn),
            });
            _resetCheckBox.SetupCheckFlowTimes(out var resetCheckFlow, 3);
            throttleNode4.FlowOut(Flow_ThrottleNode.IdFlowOut).ConnectToFlowDestination(resetCheckFlow);
        }
    }
}