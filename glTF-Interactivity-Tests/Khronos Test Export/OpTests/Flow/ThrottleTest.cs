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
        private CheckBox _errNaNCheckBox;
        private CheckBox _errInfCheckBox;
        private CheckBox _zeroDurationCheckBox;
        private CheckBox _remainingNaNInitialCheckBox;
        private CheckBox _remainingNaNAfterResetCheckBox;

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
            _outThrottleCheckBox = context.AddCheckBox("[out] flow");
            _lastRemainingTimeCheckBox = context.AddCheckBox("[lastRemainingTime]");
            _flowOutAfterDelayCheckBox = context.AddCheckBox("Flow Out After Delay", true);
            _setDelayCheckBox = context.AddCheckBox("SubTest: setDelay", true);
            _errFlowCheckBox = context.AddCheckBox("[err] Flow on -1 Duration");
            _errFlowOutCheckBox = context.AddCheckBox("Ignore [out] when error");
            _errFlowOutCheckBox.Negate();
            _resetCheckBox = context.AddCheckBox("[reset]");
            context.NewRow();
            _errNaNCheckBox = context.AddCheckBox("[err] Flow on NaN Duration");
            _errInfCheckBox = context.AddCheckBox("[err] Flow on +Inf Duration");
            _zeroDurationCheckBox = context.AddCheckBox("Duration 0: every [in] passes (3x)");
            _remainingNaNInitialCheckBox = context.AddCheckBox("[lastRemainingTime] NaN before first [in]");
            _remainingNaNAfterResetCheckBox = context.AddCheckBox("[lastRemainingTime] NaN after [reset]");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            // Basic test - only once flow out
            var throttleNode = nodeCreator.CreateNode<Flow_ThrottleNode>();
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
            
            var throttleNode2 = nodeCreator.CreateNode<Flow_ThrottleNode>();
            context.NewEntryPoint(_flowOutAfterDelayCheckBox.GetText(), 2f);
            
            var delayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
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
            var throttleNode3 = nodeCreator.CreateNode<Flow_ThrottleNode>();
            context.NewEntryPoint(throttleNode3.FlowIn(Flow_ThrottleNode.IdFlowIn), _errFlowCheckBox.GetText());
            throttleNode3.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(-1f);
            
            _errFlowCheckBox.SetupCheck(throttleNode3.FlowOut(Flow_ThrottleNode.IdFlowOutError));
            _errFlowOutCheckBox.SetupNegateCheck(throttleNode3.FlowOut(Flow_ThrottleNode.IdFlowOut));
            
            // Reset
            var throttleNode4 = nodeCreator.CreateNode<Flow_ThrottleNode>();
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

            // Error flow for NaN and +Inf durations (spec: negative, infinite or NaN -> err)
            void AddErrorCheck(CheckBox checkBox, float duration)
            {
                var errNode = nodeCreator.CreateNode<Flow_ThrottleNode>();
                context.NewEntryPoint(errNode.FlowIn(Flow_ThrottleNode.IdFlowIn), checkBox.GetText());
                errNode.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(duration);
                checkBox.SetupCheck(errNode.FlowOut(Flow_ThrottleNode.IdFlowOutError));
            }
            AddErrorCheck(_errNaNCheckBox, float.NaN);
            AddErrorCheck(_errInfCheckBox, float.PositiveInfinity);

            // Duration 0 is valid: the elapsed time is always >= 0, so every activation passes
            var throttleZeroNode = nodeCreator.CreateNode<Flow_ThrottleNode>();
            throttleZeroNode.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(0f);
            context.NewEntryPoint(_zeroDurationCheckBox.GetText());
            context.AddToCurrentEntrySequence(new []
            {
                throttleZeroNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleZeroNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleZeroNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
            });
            _zeroDurationCheckBox.SetupCheckFlowTimes(out var zeroDurationCheckFlow, 3);
            throttleZeroNode.FlowOut(Flow_ThrottleNode.IdFlowOut).ConnectToFlowDestination(zeroDurationCheckFlow);

            // lastRemainingTime is NaN until the first valid activation
            var throttleInitialNode = nodeCreator.CreateNode<Flow_ThrottleNode>();
            throttleInitialNode.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(1f);
            context.NewEntryPoint(_remainingNaNInitialCheckBox.GetText());
            _remainingNaNInitialCheckBox.SetupCheck(throttleInitialNode.ValueOut(Flow_ThrottleNode.IdOutElapsedTime),
                out var remainingNaNInitialCheckFlow, float.NaN);
            context.AddToCurrentEntrySequence(remainingNaNInitialCheckFlow);

            // ... and [reset] sets it back to NaN
            var throttleResetNaNNode = nodeCreator.CreateNode<Flow_ThrottleNode>();
            throttleResetNaNNode.ValueIn(Flow_ThrottleNode.IdInputDuration).SetValue(1f);
            context.NewEntryPoint(_remainingNaNAfterResetCheckBox.GetText());
            _remainingNaNAfterResetCheckBox.SetupCheck(throttleResetNaNNode.ValueOut(Flow_ThrottleNode.IdOutElapsedTime),
                out var remainingNaNAfterResetCheckFlow, float.NaN);
            context.AddToCurrentEntrySequence(new []
            {
                throttleResetNaNNode.FlowIn(Flow_ThrottleNode.IdFlowIn),
                throttleResetNaNNode.FlowIn(Flow_ThrottleNode.IdFlowReset),
                remainingNaNAfterResetCheckFlow,
            });
        }
    }
}