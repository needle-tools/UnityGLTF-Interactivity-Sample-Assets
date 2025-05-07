using System;
using TMPro;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class CheckBox : MonoBehaviour
    {
        [SerializeField] private TextMeshPro text;
        [SerializeField] private Transform valid;
        [SerializeField] private Transform invalid;
        [SerializeField] private Vector3 positionWhenValid;
        [SerializeField] private Vector2 size;
        
        public Vector2 CheckBoxSize => size;
        
        private int validIndex;
        private int invalidIndex;
        public object expectedValue = null;

        public int ResultValueVarId { get; private set; } = -1;
        
        private TestContext.Case _testCase;
        private bool isNegated = false;
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(valid.position, size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(invalid.position, size);
        }

        
        public void SetCase(TestContext.Case testCase)
        {
            _testCase = testCase;
        }

        public void Negate()
        {
            var vPos = valid.localPosition;
            var invPos = invalid.localPosition;
            
            valid.localPosition = invPos;
            invalid.localPosition = vPos;
            isNegated = true;
        }
        
        public void SetText(string text)
        {
            this.text.text = text;
        }
        
        public string GetText()
        {
            return text.text;
        }

        public string GetResultVariableName()
        {
            return "TestResult_" + _testCase.CaseName + "_" + text.text;
        }
        
        private void SaveResult(TestContext context, FlowOutRef flow)
        {
            if (ResultValueVarId == -1)
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), isNegated, GltfTypes.Bool);
            
            VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, ResultValueVarId, !isNegated, out var setFlow, out _);
            flow.ConnectToFlowDestination(setFlow);
        }

        private void SaveResult(TestContext context, out ValueInRef value, FlowOutRef flow, Type type)
        {
            if (ResultValueVarId == -1)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var initValue = GltfTypes.GetNullByType(gltfType);
                
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), initValue, gltfType);
            }

            var setVar = VariablesHelpers.SetVariable(context.interactivityExportContext, ResultValueVarId);
            value = setVar.ValueIn(Variable_SetNode.IdInputValue);
            flow.ConnectToFlowDestination(setVar.FlowIn(Variable_SetNode.IdFlowIn));
        }
        

        private void SaveResult(TestContext context, ValueOutRef value, FlowOutRef flow, Type type)
        {
            if (ResultValueVarId == -1)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var initValue = GltfTypes.GetNullByType(gltfType);
                
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), initValue, gltfType);
            }
            VariablesHelpers.SetVariable(context.interactivityExportContext, ResultValueVarId, value, flow);
        }

        private void AddFallbackFlowCheck(TestContext context, Func<TestContext, FlowInRef> fallbackFlowCheck, bool withResultCheck = true)
        {
            if (withResultCheck)
            {
                VariablesHelpers.GetVariable(context.interactivityExportContext, ResultValueVarId, out var resultVarRef);
                var eqNode = context.interactivityExportContext.CreateNode(new Math_EqNode());
                eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(resultVarRef);
                eqNode.ValueIn(Math_EqNode.IdValueB).SetValue(expectedValue);
                var branchNode = context.interactivityExportContext.CreateNode(new Flow_BranchNode());
                branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());
                if (isNegated)
                    branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(fallbackFlowCheck(context));
                else
                    branchNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(fallbackFlowCheck(context));
                context.AddFallbackToLastEntryPoint(branchNode.FlowIn(Flow_BranchNode.IdFlowIn));
            }
            else
            {
                context.AddFallbackToLastEntryPoint(fallbackFlowCheck(context));
            }
        }
        
        public void SetupMultiFlowCheck(TestContext context, int count, out FlowInRef[] flows, string[] flowNames = null)
        {
            flows = new FlowInRef[count];

            var branchNode = context.interactivityExportContext.CreateNode(new Flow_BranchNode());
            GltfInteractivityExportNode lastAndNode = null;

            var stateValues = new ValueOutRef[count];
            for (int i = 0; i < count; i++)
            {
                var triggeredVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(
                    "FlowTrigger_" + System.Guid.NewGuid().ToString(), false, typeof(bool));
                VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, triggeredVarId, true, out var setFlow, out var outFlowSet);
                outFlowSet.ConnectToFlowDestination(branchNode.FlowIn(Flow_BranchNode.IdFlowIn));
                flows[i] = setFlow;
            
                VariablesHelpers.GetVariable(context.interactivityExportContext, triggeredVarId, out var triggeredVarRef);
                stateValues[i] = triggeredVarRef;
                var andNode = context.interactivityExportContext.CreateNode(new Math_AndNode());
                andNode.ValueIn(Math_AndNode.IdValueA).ConnectToSource(triggeredVarRef);
                
                if (lastAndNode != null)
                    andNode.ValueIn(Math_AndNode.IdValueB).ConnectToSource(lastAndNode.FirstValueOut());
                else
                    andNode.ValueIn(Math_AndNode.IdValueB).SetValue(true);
                
                lastAndNode = andNode;
            }
            
            branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(lastAndNode.FirstValueOut());
            
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);
            
            var setPosition = context.interactivityExportContext.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);
                
            branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(setPosition.FlowIn(Pointer_SetNode.IdFlowIn));
            
            expectedValue = true;
            context.AddLog(text.text+ $": All Flows triggered (Number: {count})", out var logFlowIn, out var logFlowOut);
            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
            SaveResult(context, logFlowOut);
            
            AddFallbackFlowCheck(context, testContext =>
            {
                testContext.AddLog(text.text+ ": Not all flows got triggered! This should not happened!", out var logFlowInFallback, out var nextFlowOut);
                FlowInRef flowIn = null;
                FlowOutRef flowOut = null;
                for (int i = 0; i < count; i++)
                {
                    if (flowNames != null)
                        testContext.AddLog("   State "+i.ToString() + $" {flowNames[i]}: " + " {0}", out flowIn, out flowOut, stateValues[i]);
                    else
                        testContext.AddLog("   State "+i.ToString() + " {0}", out flowIn, out flowOut, stateValues[i]);
                    nextFlowOut.ConnectToFlowDestination(flowIn);
                    nextFlowOut = flowOut;

                }
                return logFlowInFallback;
            });
            
        }

        public void SetupOrderFlowCheck(TestContext context, FlowOutRef[] flows)
        {
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);

            var nodeCreator = context.interactivityExportContext;
            var countVar = nodeCreator.Context.AddVariableWithIdIfNeeded("FlowSequenceCount_"+System.Guid.NewGuid().ToString(), 0, GltfTypes.Int);
            
            VariablesHelpers.GetVariable(nodeCreator, countVar, out var countVarRef);
            var addCount = nodeCreator.CreateNode(new Math_AddNode());
            addCount.ValueIn(Math_AddNode.IdValueA).ConnectToSource(countVarRef);
            addCount.ValueIn(Math_AddNode.IdValueB).SetValue(1);
            
            int index = 0;
            FlowOutRef lastFlow = null;
            foreach (var flow in flows)
            {
                index++;
                var eqNode = nodeCreator.CreateNode(new Math_EqNode());
                eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(countVarRef);
                eqNode.ValueIn(Math_EqNode.IdValueB).SetValue(index);

                var setVarNode = VariablesHelpers.SetVariable(nodeCreator, countVar, addCount.FirstValueOut(), flow);
                
                var checkBranch = nodeCreator.CreateNode(new Flow_BranchNode());
                checkBranch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());
                setVarNode.FlowOut(Variable_SetNode.IdFlowOut).ConnectToFlowDestination(checkBranch.FlowIn(Flow_BranchNode.IdFlowIn));
                
                context.AddLog(text.text+ ": Incorrect flow order triggered! Expected Socket Id: "+flow.socket.Key, out var invalidLogFlowIn, out var invalidLogFlowOut);
                checkBranch.FlowOut(Flow_BranchNode.IdFlowOutFalse)
                    .ConnectToFlowDestination(invalidLogFlowIn);
                
                
                var setInvalidVar = VariablesHelpers.SetVariable(nodeCreator, countVar);
                setInvalidVar.ValueIn(Variable_SetNode.IdInputValue).SetValue(-1000);
                invalidLogFlowOut.ConnectToFlowDestination(setInvalidVar.FlowIn(Variable_SetNode.IdFlowIn));
                
                
                lastFlow = checkBranch.FlowOut(Flow_BranchNode.IdFlowOutTrue);
            }
            
            
            var setPosition = nodeCreator.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);

            lastFlow.ConnectToFlowDestination(setPosition.FlowIn(Pointer_SetNode.IdFlowIn));
            expectedValue = true;
            context.AddLog(text.text+ ": Correct flow order triggered", out var logFlowIn, out var logFlowOut);
            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
            SaveResult(context, logFlowOut);
            
            AddFallbackFlowCheck(context, testContext =>
            {
                testContext.AddLog(text.text+ ": Correct flow order not triggered! This should not happened!", out var logFlowInFallback, out var _);
                return logFlowInFallback;
            });
        }
        
        public void SetupCheckFlowTimes(TestContext context, out FlowInRef flow, int callTimes)
        {
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);
            
            var setPosition = context.interactivityExportContext.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);

            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            flow = flowInToIncrease;
            
            expectedValue = callTimes;
            
            AddFallbackFlowCheck(context, testContext =>
            {
                var eq = testContext.interactivityExportContext.CreateNode(new Math_EqNode());
                eq.ValueIn(Math_EqNode.IdValueA).ConnectToSource(counter);
                eq.ValueIn(Math_EqNode.IdValueB).SetValue(callTimes);
                
                var branchNode = testContext.interactivityExportContext.CreateNode(new Flow_BranchNode());
                branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eq.FirstValueOut());
                
                branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(setPosition.FlowIn(Pointer_SetNode.IdFlowIn));
                context.AddLog(text.text+ ": Flow got triggered correct amount", out var logFlowIn, out var logFlowOut);
                setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
                SaveResult(context, counter, logFlowOut, typeof(int));
                
                
                testContext.AddLog(text.text+ ": Flow got triggered {0} times from "+callTimes.ToString()+ ". This should not happened!", out var logFlowInFallback, out var _, counter);
                branchNode.FlowOut(Flow_BranchNode.IdFlowOutFalse)
                    .ConnectToFlowDestination(logFlowInFallback);
                
                return branchNode.FlowIn(Flow_BranchNode.IdFlowIn);
            }, false);
        }
        
        public void SetupCheck(TestContext context, out FlowInRef flow)
        {
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);
            
            var setPosition = context.interactivityExportContext.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);

            flow = setPosition.FlowIn(Pointer_SetNode.IdFlowIn);
            expectedValue = true;
            context.AddLog(text.text+ ": Flow triggered", out var logFlowIn, out var logFlowOut);
            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
            SaveResult(context, logFlowOut);

            AddFallbackFlowCheck(context, testContext =>
            {
                testContext.AddLog(text.text+ ": Flow not triggered! This should not happened!", out var logFlowInFallback, out var _);
                return logFlowInFallback;
            });
        }

        public void SetupCheck(TestContext context, FlowOutRef flow)
        {
            SetupCheck(context, out var flowIn);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void SetupNegateCheck(TestContext context, out FlowInRef flow)
        {
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);
            
            var setPosition = context.interactivityExportContext.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);

            flow = setPosition.FlowIn(Pointer_SetNode.IdFlowIn);
            expectedValue = false;
            context.AddLog(text.text+ ": Flow triggered! This should not happened!", out var logFlowIn, out var logFlowOut);
            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
            SaveResult(context, logFlowOut);
            
            AddFallbackFlowCheck(context, testContext =>
            {
                context.AddLog(text.text+ ": Test Successful", out var logSuccessFlowIn, out _);
                return logSuccessFlowIn;
            });
        }

        public void SetupNegateCheck(TestContext context, FlowOutRef flow)
        {
            SetupNegateCheck(context, out var flowIn);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void SetupCheck(TestContext context, ValueOutRef inputValue, FlowOutRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(context, inputValue, out var flowIn, valueToCompare, proximityCheck);
            flow.ConnectToFlowDestination(flowIn);
        }

        public void SetupCheck(TestContext context, out ValueInRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
              var compareValueType = GltfTypes.TypeIndex(valueToCompare.GetType());
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);

            GltfInteractivityExportNode eqNode;
            if (proximityCheck)
            {
                var subtractNode = context.interactivityExportContext.CreateNode(new Math_SubNode());
                inputValue = subtractNode.ValueIn("a");
                subtractNode.ValueIn("b").SetValue(valueToCompare);
            
                var absNode = context.interactivityExportContext.CreateNode(new Math_AbsNode());
                absNode.ValueIn("a").ConnectToSource(subtractNode.FirstValueOut());

                var lessThanNode = context.interactivityExportContext.CreateNode(new Math_LtNode());
                lessThanNode.ValueIn("a").ConnectToSource(absNode.FirstValueOut());
                lessThanNode.SetValueInSocket("b", 0.0001f);
                eqNode = lessThanNode; 
            }
            else
            {
                eqNode = context.interactivityExportContext.CreateNode(new Math_EqNode());
                inputValue = eqNode.ValueIn(Math_EqNode.IdValueA);
                eqNode.ValueIn(Math_EqNode.IdValueB).SetType(TypeRestriction.LimitToType(compareValueType)).SetValue(valueToCompare);
            }
            
            var validNode = context.interactivityExportContext.CreateNode(new Flow_BranchNode());
            validNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());

            var setPosition = context.interactivityExportContext.CreateNode(new Pointer_SetNode());
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(validIndex);

            flow = validNode.FlowIn(Flow_BranchNode.IdFlowIn);
            
            validNode.FlowOut(Flow_BranchNode.IdFlowOutTrue)
                .ConnectToFlowDestination(setPosition.FlowIn(Pointer_SetNode.IdFlowIn));
            
            context.AddLog(text.text+ ": Value is {0}, should be "+valueToCompare.ToString(), out var logFlowIn, out var logFlowOut, 1, out var logValueRef);
            inputValue = inputValue.Link(logValueRef[0]);
            validNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(logFlowIn);
            
            context.AddLog(text.text+ ": Test Successful", out var logSuccesFlowIn, out var logSuccessFlowOut);

            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logSuccesFlowIn);
            logSuccessFlowOut.ConnectToFlowDestination(logFlowIn);
            
            expectedValue = valueToCompare;
            SaveResult(context, out var saveResultInputValue, logFlowOut, valueToCompare.GetType());
            inputValue = inputValue.Link(saveResultInputValue);
            
            AddFallbackFlowCheck(context, testContext =>
            {
                context.AddLog(text.text+ ": Test Failed", out var logFailedFlowIn, out _);
                return logFailedFlowIn;
            });
        }

        public void SetupCheck(TestContext context, ValueOutRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(context, out var inputValueRef, out flow, valueToCompare, proximityCheck);
            inputValueRef.ConnectToSource(inputValue);
        }
    }
}