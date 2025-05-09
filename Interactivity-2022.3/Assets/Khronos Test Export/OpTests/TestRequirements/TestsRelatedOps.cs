using UnityGLTF.Interactivity.Export;

namespace Khronos_Test_Export.OpTests.TestRequirements
{
    public class TestsRelatedOps : ITestCase
    {
        private CheckBox _flowCheckBox;
        private CheckBox _valueCheckBox;
        private CheckBox _valueProximityCheckBox;
        private CheckBox _counterCheckBox;
        private CheckBox _multiFlowCheckBox;
        private CheckBox _delayedCheckBox;
        
        public string GetTestName()
        {
            return "Tests required operations";
        }

        public string GetTestDescription()
        {
            return "Testing required operations for proper test execution. This tests should be passed before testing all other tests.";
        }

        public void PrepareObjects(TestContext context)
        {
            _flowCheckBox = context.AddCheckBox("Flow Checks");
            _valueCheckBox = context.AddCheckBox("Value Checks");
            _valueProximityCheckBox = context.AddCheckBox("Value Proximity Checks");
            _counterCheckBox = context.AddCheckBox("Counter Checks");
            _multiFlowCheckBox = context.AddCheckBox("Multi Flow Checks");
            _delayedCheckBox = context.AddCheckBox("Delayed Checks", true);
        }

        public void CreateNodes(TestContext context)
        {
            context.NewEntryPoint("Entry");
           
            _flowCheckBox.SetupCheck(out var flowCheckFlowIn);
            _valueCheckBox.SetupCheck(out var valueCheckRef, out var flowValueCheckFlowIn, 1, false);
            valueCheckRef.SetValue(1);
            float proximityValue = 33.21145566622334233f;
            _valueProximityCheckBox.SetupCheck(out var valueProximityCheckRef, out var flowValueProximityCheckFlowIn, proximityValue, false);
            valueProximityCheckRef.SetValue(proximityValue);
            
            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            
            _counterCheckBox.SetupCheck(counter, out var counterCheckFlowIn, 2);
            _multiFlowCheckBox.SetupMultiFlowCheck(2, out var multiFlowCheckFlowIn);
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    flowCheckFlowIn,
                    flowValueCheckFlowIn,
                    flowValueProximityCheckFlowIn,
                    flowInToIncrease,
                    flowInToIncrease,
                    counterCheckFlowIn,
                    multiFlowCheckFlowIn[0],
                    multiFlowCheckFlowIn[1]
                });
            
            context.NewEntryPoint("Delayed Check", 1f);
            _delayedCheckBox.SetupCheck(out var delayedCheckFlow);
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    delayedCheckFlow,
                });
        }
    }
}