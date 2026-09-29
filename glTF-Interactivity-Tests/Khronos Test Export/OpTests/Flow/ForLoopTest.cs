using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ForLoopTest : ITestCase
    {
        private CheckBox _bodyCheck;
        private CheckBox _loopRangeCheck;
        private CheckBox _completeCheck;
        private CheckBox _initialIndexCheck;
        private CheckBox _completedIndexCheck;
        private CheckBox _emptyRangeBodyCheck;
        private CheckBox _emptyRangeCompletedCheck;
        private CheckBox _emptyRangeIndexCheck;
        private CheckBox _negativeRangeCheck;
        private CheckBox _negativeRangeIndexCheck;
        private CheckBox _endIndexReevaluatedCheck;

        public string GetTestName()
        {
            return "flow/for";
        }

        public string GetTestDescription()
        {
            return "";
        }
        
        public void PrepareObjects(TestContext context)
        {
            _bodyCheck = context.AddCheckBox("[body] flow");
            _loopRangeCheck = context.AddCheckBox("Loop range (0..10)");
            _completeCheck = context.AddCheckBox("[completed] flow");
            _initialIndexCheck = context.AddCheckBox("Initial index");
            _completedIndexCheck = context.AddCheckBox("[index] when completed");
            context.NewRow();
            _emptyRangeBodyCheck = context.AddCheckBox("startIndex > endIndex (5..2): no [loopBody]");
            _emptyRangeBodyCheck.Negate();
            _emptyRangeCompletedCheck = context.AddCheckBox("startIndex > endIndex (5..2): [completed]");
            _emptyRangeIndexCheck = context.AddCheckBox("startIndex > endIndex (5..2): [index] 5");
            _negativeRangeCheck = context.AddCheckBox("Negative range (-3..0): 3 iterations");
            _negativeRangeIndexCheck = context.AddCheckBox("Negative range (-3..0): [index] 0 when completed");
            _endIndexReevaluatedCheck = context.AddCheckBox("[endIndex] re-evaluated (10 -> 3 in body): 3 iterations");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var forLoop = nodeCreator.CreateNode<Flow_ForLoopNode>();
            context.NewEntryPoint("Loop Entry");

            forLoop.Configuration[Flow_ForLoopNode.IdConfigInitialIndex].Value = 1;
            forLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            forLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(10);
            
            _initialIndexCheck.SetupCheck(forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var initialIndexCheckFlow, 1);
            context.AddToCurrentEntrySequence(
                new FlowInRef[]
                {
                    initialIndexCheckFlow,
                    forLoop.FlowIn(Flow_ForLoopNode.IdFlowIn),
                });
            
            _bodyCheck.SetupCheck(out var bodyCheckFlowIn);
            
            context.AddPlusOneCounter(out var loopRangeCounter, out var flowInToIncrease);
            _loopRangeCheck.SetupCheck(loopRangeCounter, out var loopRangeCheckFlowIn, 10);

            _completeCheck.SetupCheck(out var completeCheckFlowIn);
            
            _completedIndexCheck.SetupCheck(forLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var completedIndexCheckFlowIn, 10);
            
            context.AddSequence( forLoop.FlowOut(Flow_ForLoopNode.IdCompleted),
                new FlowInRef[]
                {
                    completedIndexCheckFlowIn,
                    loopRangeCheckFlowIn,
                    completeCheckFlowIn
                });
            
            context.AddSequence(forLoop.FlowOut(Flow_ForLoopNode.IdLoopBody), new FlowInRef[]
            {
                bodyCheckFlowIn,
                flowInToIncrease
            });

            // startIndex > endIndex: index is set to startIndex, the body never runs and [completed] fires
            var emptyLoop = nodeCreator.CreateNode<Flow_ForLoopNode>();
            emptyLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(5);
            emptyLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(2);
            context.NewEntryPoint(emptyLoop.FlowIn(Flow_ForLoopNode.IdFlowIn), "Empty range");

            _emptyRangeBodyCheck.SetupNegateCheck(emptyLoop.FlowOut(Flow_ForLoopNode.IdLoopBody));
            _emptyRangeCompletedCheck.SetupCheck(out var emptyCompletedFlowIn);
            _emptyRangeIndexCheck.SetupCheck(emptyLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var emptyIndexFlowIn, 5);
            context.AddSequence(emptyLoop.FlowOut(Flow_ForLoopNode.IdCompleted), emptyCompletedFlowIn, emptyIndexFlowIn);

            // Negative indices: -3, -2, -1
            var negativeLoop = nodeCreator.CreateNode<Flow_ForLoopNode>();
            negativeLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(-3);
            negativeLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(0);
            context.NewEntryPoint(negativeLoop.FlowIn(Flow_ForLoopNode.IdFlowIn), "Negative range");

            context.AddPlusOneCounter(out var negativeCounter, out var negativeIncreaseFlowIn);
            negativeLoop.FlowOut(Flow_ForLoopNode.IdLoopBody).ConnectToFlowDestination(negativeIncreaseFlowIn);
            _negativeRangeCheck.SetupCheck(negativeCounter, out var negativeCountFlowIn, 3);
            _negativeRangeIndexCheck.SetupCheck(negativeLoop.ValueOut(Flow_ForLoopNode.IdIndex), out var negativeIndexFlowIn, 0);
            context.AddSequence(negativeLoop.FlowOut(Flow_ForLoopNode.IdCompleted), negativeCountFlowIn, negativeIndexFlowIn);

            // endIndex is evaluated before every iteration: the body lowers it from 10 to 3
            var endIndexVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("ForLoop_endIndex_" + System.Guid.NewGuid(), 10, GltfTypes.Int);
            VariablesHelpers.GetVariable(nodeCreator, endIndexVarId, out var endIndexVarRef);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, endIndexVarId, 3, out var setEndIndexFlowIn, out _);

            var reevaluatedLoop = nodeCreator.CreateNode<Flow_ForLoopNode>();
            reevaluatedLoop.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
            reevaluatedLoop.ValueIn(Flow_ForLoopNode.IdEndIndex).ConnectToSource(endIndexVarRef);
            context.NewEntryPoint(reevaluatedLoop.FlowIn(Flow_ForLoopNode.IdFlowIn), "endIndex re-evaluation");

            context.AddPlusOneCounter(out var reevaluatedCounter, out var reevaluatedIncreaseFlowIn);
            context.AddSequence(reevaluatedLoop.FlowOut(Flow_ForLoopNode.IdLoopBody), setEndIndexFlowIn, reevaluatedIncreaseFlowIn);
            _endIndexReevaluatedCheck.SetupCheck(reevaluatedCounter, out var reevaluatedCountFlowIn, 3);
            reevaluatedLoop.FlowOut(Flow_ForLoopNode.IdCompleted).ConnectToFlowDestination(reevaluatedCountFlowIn);
        }
    }
}