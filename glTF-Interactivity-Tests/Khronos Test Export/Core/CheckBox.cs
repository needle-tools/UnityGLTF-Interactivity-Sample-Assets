using System;
using System.Linq;
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
        [SerializeField] private Transform waiting;
        [SerializeField] private Vector3 positionWhenValid;
        [SerializeField] private Vector2 size;

        public string logText => $"<{_testCase.CaseName} - {text.text}>";
        public Vector2 CheckBoxSize => size;
        
        private int validIndex;
        public object expectedValue = null;

        public int ResultValueVarId { get; private set; } = -1;
        public int ResultPassValueVarId { get; private set; } = -1;

        public float proximityCheckDistance = 0.0001f;

        private TestContext.Case _testCase;
        private bool isNegated = false;
        private bool isWaiting = false;
        public TestContext context;
        private bool proximityCheck = false;

        private string resultVarName = null;
        private string resultPassVarName = null;
        
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

        public void Waiting()
        {
            waiting.localPosition = positionWhenValid;
            isWaiting = true;
        }
        
        /// <summary>
        /// Setup the checkbox to show the Check-Symbol by default, and the Failed-Symbol when the flow is triggered
        /// </summary>
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
            if (resultVarName != null)
                return resultVarName;
            
            var name = "TestResult_" + _testCase.CaseName + "_" + text.text;
            
            if (context.interactivityExportContext.Context.variables.Exists(v => v.Id == name))
            {
                var existingCount = context.interactivityExportContext.Context.variables.Count(v => v.Id.StartsWith(name));
                name += $" ({existingCount.ToString()})";
            }
            resultVarName = name;
            return name;
        }

        public string GetResultPassVariableName()
        {
            if (resultPassVarName != null)
                return resultPassVarName;
            
            var name = "TestResult_HasPassed_" + _testCase.CaseName + "_" + text.text;
            
            if (context.interactivityExportContext.Context.variables.Exists(v => v.Id == name))
            {
                var existingCount = context.interactivityExportContext.Context.variables.Count(v => v.Id.StartsWith(name));
                name += $" ({existingCount.ToString()})";
            }
            resultPassVarName = name;
            return name;
        }

        private void DeactivateWaiting(out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var waitingIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(waiting);
            
            var setPosition = context.interactivityExportContext.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(Vector3.zero);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(waitingIndex);  
            
            flowIn = setPosition.FlowIn(Pointer_SetNode.IdFlowIn); 
            flowOut = setPosition.FlowOut(Pointer_SetNode.IdFlowOut);
        }

        private string ExpectedValueToString()
        {
            var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
            
            if (expectedValue is float floatValue)
                return floatValue.ToString(invariantCulture);

            if (expectedValue is double doubleValue)
                return doubleValue.ToString(invariantCulture);
            
            if (expectedValue is bool boolValue)
                return boolValue.ToString(invariantCulture);
            
            return expectedValue.ToString();
        }
        
        private void SavePassResult(out ValueInRef boolValue, out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            if (ResultPassValueVarId == -1)
                ResultPassValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultPassVariableName(), false, GltfTypes.Bool);
            
            VariablesHelpers.SetVariable(context.interactivityExportContext, ResultPassValueVarId, out boolValue, out flowIn, out flowOut);
        }
        
        private void SaveResult(FlowOutRef flow)
        {
            if (ResultValueVarId == -1)
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), isNegated, GltfTypes.Bool);
            
            VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, ResultValueVarId, true, out var setFlow, out _);
            flow.ConnectToFlowDestination(setFlow);
            ResultPassValueVarId = ResultValueVarId;
        }
        
        private object GetDefaultValue(Type type)
        {
            const float defaultFloat = -0.0142f;
            var gltfType = GltfTypes.GetTypeMapping(type).GltfSignature;
            switch (gltfType)
            {
                case GltfTypes.Bool:
                    return false;
                case GltfTypes.Int:
                    return -1;
                case GltfTypes.Float:
                    return defaultFloat;
                case GltfTypes.Float2:
                    return new Vector2(defaultFloat, defaultFloat);
                case GltfTypes.Float3:
                    return new Vector3(defaultFloat, defaultFloat, defaultFloat);
                case GltfTypes.Float4:
                    return new Vector4(defaultFloat, defaultFloat, defaultFloat);
                case GltfTypes.Float4x4:
                    return new Matrix4x4();
                default:
                    return null;
            }
        }
        
        private void SaveResult(out ValueInRef value, FlowOutRef flow, Type type)
        {
            if (ResultValueVarId == -1)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var initValue = GetDefaultValue(type);
                
                var resultVarName = GetResultVariableName();
                if (context.interactivityExportContext.Context.variables.Exists(v => v.Id == resultVarName))
                    throw new Exception("Variable with the same name already exists: " + resultVarName);
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(resultVarName, initValue, gltfType);
            }

            var setVar = VariablesHelpers.SetVariable(context.interactivityExportContext, ResultValueVarId, out value, out _, out _);
            flow.ConnectToFlowDestination(setVar.FlowIn(Variable_SetNode.IdFlowIn));
        }
        
        private void SaveResult(ValueOutRef value, FlowOutRef flow, Type type)
        {
            if (ResultValueVarId == -1)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var initValue = GetDefaultValue(type);
                
                var resultVarName = GetResultVariableName();
                if (context.interactivityExportContext.Context.variables.Exists(v => v.Id == resultVarName))
                    throw new Exception("Variable with the same name already exists: " + resultVarName);
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(resultVarName, initValue, gltfType);

            }
            VariablesHelpers.SetVariable(context.interactivityExportContext, ResultValueVarId, value, flow);
        }

        private void SetToForeground(int nodeIndex, out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var setPosition = context.interactivityExportContext.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/{" + PointersHelper.IdPointerNodeIndex + "}/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);  
            
            flowIn = setPosition.FlowIn(); 
            flowOut = setPosition.FlowOut();
        }
        
        private void SetPassed(out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            SetToForeground(validIndex, out flowIn, out flowOut);
        }
        
        private void SetFailed(out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var invalid = context.interactivityExportContext.Context.exporter.GetTransformIndex(this.invalid);
            SetToForeground(invalid, out flowIn, out flowOut);
        }
        
        private void PostCheck(Func<FlowInRef> fallbackFlowCheck, bool withResultCheck = true)
        {
            if (withResultCheck)
            {
                VariablesHelpers.GetVariable(context.interactivityExportContext, ResultValueVarId,
                    out var resultVarRef);

                //GltfInteractivityExportNode eqNode = null;
                // if (proximityCheck)
                // {
                //     var subtractNode = context.interactivityExportContext.CreateNode<Math_SubNode>();
                //     subtractNode.ValueIn(Math_SubNode.IdValueA).ConnectToSource(resultVarRef);
                //     subtractNode.ValueIn("b").SetValue(expectedValue);
                //
                //     var absNode = context.interactivityExportContext.CreateNode<Math_AbsNode>();
                //     absNode.ValueIn("a").ConnectToSource(subtractNode.FirstValueOut());
                //
                //     var lessThanNode = context.interactivityExportContext.CreateNode<Math_LtNode>();
                //     lessThanNode.ValueIn("a").ConnectToSource(absNode.FirstValueOut());
                //     lessThanNode.SetValueInSocket("b", proximityCheckDistance);
                //     eqNode = lessThanNode;
                // }
                // else
                // {
                //     eqNode = context.interactivityExportContext.CreateNode<Math_EqNode>();
                //     if (ResultPassValueVarId != -1)
                //         eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(ResultPassValueVarId);
                //     else
                //         eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(resultVarRef);
                //     eqNode.ValueIn(Math_EqNode.IdValueB).SetValue(expectedValue);
                // }

                VariablesHelpers.GetVariable(context.interactivityExportContext, ResultPassValueVarId,
                    out var passValueRef);
                
                var branchNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
                branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(passValueRef);
                if (isNegated)
                    branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(fallbackFlowCheck());
                else
                    branchNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(fallbackFlowCheck());

                if (isWaiting)
                {
                    DeactivateWaiting(out var flowIn, out var flowOut);
                    flowOut.ConnectToFlowDestination(branchNode.FlowIn(Flow_BranchNode.IdFlowIn));
                    context.AddFallbackToLastEntryPoint(flowIn);
                }
                else
                    context.AddFallbackToLastEntryPoint(branchNode.FlowIn(Flow_BranchNode.IdFlowIn));
            }
            else
            {
                if (isWaiting)
                {
                    DeactivateWaiting(out var flowIn, out var flowOut);
                    context.AddFallbackToLastEntryPoint(flowIn);
                    flowOut.ConnectToFlowDestination(fallbackFlowCheck());
                }
                else
                    context.AddFallbackToLastEntryPoint(fallbackFlowCheck());
            }
        }
        
        public void SetupMultiFlowCheck(int count, out FlowInRef[] flows, string[] flowNames = null)
        {
            flows = new FlowInRef[count];

            var branchNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
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
                var andNode = context.interactivityExportContext.CreateNode<Math_AndNode>();
                andNode.ValueIn(Math_AndNode.IdValueA).ConnectToSource(triggeredVarRef);
                
                if (lastAndNode != null)
                    andNode.ValueIn(Math_AndNode.IdValueB).ConnectToSource(lastAndNode.FirstValueOut());
                else
                    andNode.ValueIn(Math_AndNode.IdValueB).SetValue(true);
                
                lastAndNode = andNode;
            }
            
            branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(lastAndNode.FirstValueOut());
            
            SetPassed(out var flowSetValid, out var flowOutSetValid);
                
            branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(flowSetValid);
            
            expectedValue = true;
            context.AddLog(logText+ $": All Flows triggered (Number: {count})", out var logFlowIn, out var logFlowOut);
            flowOutSetValid.ConnectToFlowDestination(logFlowIn);
            SaveResult(logFlowOut);
        
            PostCheck(() =>
            {
                context.AddLog("ERROR! "+logText+ ": Not all flows got triggered! This should not happened!", out var logFlowInFallback, out var nextFlowOut);
                FlowInRef flowIn = null;
                FlowOutRef flowOut = null;
                for (int i = 0; i < count; i++)
                {
                    if (flowNames != null)
                        context.AddLog("   State "+i.ToString() + $" {flowNames[i]}: " + " {0}", out flowIn, out flowOut, stateValues[i]);
                    else
                        context.AddLog("   State "+i.ToString() + " {0}", out flowIn, out flowOut, stateValues[i]);
                    nextFlowOut.ConnectToFlowDestination(flowIn);
                    nextFlowOut = flowOut;

                }
                return logFlowInFallback;
            });
            
        }

        public void SetupOrderFlowCheck(FlowOutRef[] flows)
        {
            var nodeCreator = context.interactivityExportContext;
            var countVar = nodeCreator.Context.AddVariableWithIdIfNeeded("FlowSequenceCount_"+System.Guid.NewGuid().ToString(), 0, GltfTypes.Int);
            
            VariablesHelpers.GetVariable(nodeCreator, countVar, out var countVarRef);
            var addCount = nodeCreator.CreateNode<Math_AddNode>();
            addCount.ValueIn(Math_AddNode.IdValueA).ConnectToSource(countVarRef);
            addCount.ValueIn(Math_AddNode.IdValueB).SetValue(1);
            
            int index = 0;
            FlowOutRef lastFlow = null;
            foreach (var flow in flows)
            {
                index++;
                var eqNode = nodeCreator.CreateNode<Math_EqNode>();
                eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(countVarRef);
                eqNode.ValueIn(Math_EqNode.IdValueB).SetValue(index);

                var setVarNode = VariablesHelpers.SetVariable(nodeCreator, countVar, addCount.FirstValueOut(), flow);
                
                var checkBranch = nodeCreator.CreateNode<Flow_BranchNode>();
                checkBranch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());
                setVarNode.FlowOut(Variable_SetNode.IdFlowOut).ConnectToFlowDestination(checkBranch.FlowIn(Flow_BranchNode.IdFlowIn));
                
                context.AddLog("ERROR! "+logText+ ": Incorrect flow order triggered! Expected Socket Id: "+flow.socket.Key, out var invalidLogFlowIn, out var invalidLogFlowOut);
                checkBranch.FlowOut(Flow_BranchNode.IdFlowOutFalse)
                    .ConnectToFlowDestination(invalidLogFlowIn);
                
                
                var setInvalidVar = VariablesHelpers.SetVariable(nodeCreator, countVar, out var setInvalidVarValue, out _, out _);
                setInvalidVarValue.SetValue(-1000);
                invalidLogFlowOut.ConnectToFlowDestination(setInvalidVar.FlowIn(Variable_SetNode.IdFlowIn));
                
                
                lastFlow = checkBranch.FlowOut(Flow_BranchNode.IdFlowOutTrue);
            }
            
            SetPassed(out var flowSetValid, out var flowOutSetValid);
            lastFlow.ConnectToFlowDestination(flowSetValid);
            expectedValue = true;
            context.AddLog(logText+ ": Correct flow order triggered", out var logFlowIn, out var logFlowOut);
            flowOutSetValid.ConnectToFlowDestination(logFlowIn);
            SaveResult(logFlowOut);
            
            PostCheck(() =>
            {
                context.AddLog("ERROR! "+logText+ ": Correct flow order not triggered! This should not happened!", out var logFlowInFallback, out var _);
                return logFlowInFallback;
            });
        }
        
        public void SetupCheckFlowTimes(out FlowInRef flow, int callTimes)
        {
            SetPassed(out var flowSetValid, out var flowOutSetValid);

            context.AddPlusOneCounter(out var counter, out var flowInToIncrease);
            flow = flowInToIncrease;
            
            expectedValue = callTimes;
            
            PostCheck(() =>
            {
                var eq = context.interactivityExportContext.CreateNode<Math_EqNode>();
                eq.ValueIn(Math_EqNode.IdValueA).ConnectToSource(counter);
                eq.ValueIn(Math_EqNode.IdValueB).SetValue(callTimes);
                
                var branchNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
                branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eq.FirstValueOut());
                
                branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(flowSetValid);
                
                SavePassResult(out var passValue, out var flowInPass, out var flowOutPass);
                flowOutPass.ConnectToFlowDestination(branchNode.FlowIn());
                
                passValue.ConnectToSource(eq.FirstValueOut());
                
                context.AddLog(logText+ ": Flow got triggered correct amount", out var logFlowIn, out var logFlowOut);
                flowOutSetValid.ConnectToFlowDestination(logFlowIn);
                
                SaveResult( counter, logFlowOut, typeof(int));
                
                context.AddLog("ERROR! "+logText+ ": Flow got triggered {0} times from "+callTimes.ToString()+ ". This should not happened!", out var logFlowInFallback, out var _, counter);
                branchNode.FlowOut(Flow_BranchNode.IdFlowOutFalse)
                    .ConnectToFlowDestination(logFlowInFallback);

                return flowInPass;;
            }, false);
        }
        
        public void SetupCheck(out FlowInRef flow)
        {
            SetPassed(out var flowSetValid, out var flowOutSetValid);

            flow =  flowSetValid;
            expectedValue = true;
            context.AddLog(logText+ ": Flow triggered", out var logFlowIn, out var logFlowOut);
            flowOutSetValid.ConnectToFlowDestination(logFlowIn);
            SaveResult(logFlowOut);

            PostCheck(() =>
            {
                context.AddLog("ERROR! "+logText+ ": Flow not triggered! This should not happened!", out var logFlowInFallback, out var _);
                return logFlowInFallback;
            });
        }

        public void SetupCheck(FlowOutRef flow)
        {
            SetupCheck(out var flowIn);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void SetupNegateCheck(out FlowInRef flow)
        {
            SetFailed(out var flowSetValid, out var flowOutSetValid);

            flow = flowSetValid;
            expectedValue = false;
            context.AddLog("ERROR! "+logText+ ": Flow triggered! This should not happened!", out var logFlowIn, out var logFlowOut);
            flowOutSetValid.ConnectToFlowDestination(logFlowIn);
            SaveResult(logFlowOut);
            
            PostCheck(() =>
            {
                context.AddLog(logText+ ": Test Successful", out var logSuccessFlowIn, out _);
                return logSuccessFlowIn;
            });
        }

        public void SetupNegateCheck(FlowOutRef flow)
        {
            SetupNegateCheck(out var flowIn);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void SetupCheck(ValueOutRef inputValue, FlowOutRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(inputValue, out var flowIn, valueToCompare, proximityCheck);
            flow.ConnectToFlowDestination(flowIn);
        }

        private bool RequiresDotForApproximationCheck(object valueToCompare)
        {
            if (valueToCompare is bool || valueToCompare is int ||  valueToCompare is float || valueToCompare is double)
                return false;
            return valueToCompare is Vector2 || valueToCompare is Vector3 || valueToCompare is Vector4 ||
                   valueToCompare is Quaternion || valueToCompare is Matrix4x4;
        }

        public void SetupCheckValueDiffers(out ValueInRef valueA, out ValueInRef valueB, out FlowInRef flow)
        {
            var eqNode = context.interactivityExportContext.CreateNode<Math_EqNode>();
            valueA = eqNode.ValueIn(Math_EqNode.IdValueA);
            valueB =  eqNode.ValueIn(Math_EqNode.IdValueB);

            var validNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
            validNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());

            SetPassed(out var setPosition, out var flowOutSetValid);
            
            validNode.FlowOut(Flow_BranchNode.IdFlowOutFalse)
                .ConnectToFlowDestination(setPosition);
            
            expectedValue = true;
            context.AddLog(logText+ ": Value A is {0} and Value B is {1}. Should be not-equal.", out var logFlowIn, out var logFlowOut, 2, out var logValueRef);
            flow = logFlowIn;
            logFlowOut.ConnectToFlowDestination(validNode.FlowIn());
            
            valueA = valueA.Link(logValueRef[0]);
            valueB = valueB.Link(logValueRef[1]);
            
            context.AddLog(logText+ ": Test Successful", out var logSuccesFlowIn, out var logSuccessFlowOut);
            flowOutSetValid.ConnectToFlowDestination(logSuccesFlowIn);
            
            SaveResult(logSuccessFlowOut);
            
            PostCheck(() =>
            {
                context.AddLog("ERROR! "+logText+ ": Test Failed", out var logFailedFlowIn, out _);
                return logFailedFlowIn;
            });
        }
        
        public void SetupCheck(out ValueInRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            this.proximityCheck = proximityCheck;
            var compareValueType = GltfTypes.TypeIndex(valueToCompare.GetType());
        
            GltfInteractivityExportNode eqNode = null;
            if (proximityCheck)
            {
                if (valueToCompare is Matrix4x4 vtcMat)
                {
                    var resultDec = context.interactivityExportContext.CreateNode<Math_Extract4x4Node>();
                    inputValue = resultDec.ValueIn(Math_Extract4x4Node.IdValueIn);
                    
                    ValueOutRef lastAddResult = null;
                    
                    //var compareDec = context.interactivityExportContext.CreateNode<Math_Extract4x4Node>();
                    for (int i = 0; i < 16; i++)
                    {
                        var subtractNode = context.interactivityExportContext.CreateNode<Math_SubNode>();
                        subtractNode.ValueIn("a").ConnectToSource(resultDec.ValueOut(i.ToString()));
                        subtractNode.ValueIn("b").SetValue(vtcMat[i]);
                
                        var absNode = context.interactivityExportContext.CreateNode<Math_AbsNode>();
                        absNode.ValueIn("a").ConnectToSource(subtractNode.FirstValueOut());

                        var lessThanNode = context.interactivityExportContext.CreateNode<Math_LtNode>();
                        lessThanNode.ValueIn("a").ConnectToSource(absNode.FirstValueOut());
                        lessThanNode.SetValueInSocket("b", proximityCheckDistance);

                        if (lastAddResult == null)
                        {
                            lastAddResult = lessThanNode.FirstValueOut();
                            eqNode = lessThanNode;
                        }
                        else
                        {
                            var andNode = context.interactivityExportContext.CreateNode<Math_AndNode>();
                            andNode.ValueIn("a").ConnectToSource(lastAddResult);
                            andNode.ValueIn("b").ConnectToSource(lessThanNode.FirstValueOut());
                            lastAddResult = andNode.FirstValueOut();
                            eqNode = andNode;
                        }
       
                    }
                    
                }
                else
                if (RequiresDotForApproximationCheck(valueToCompare))
                {
                    var dotNode = context.interactivityExportContext.CreateNode<Math_DotNode>();
                    var normalizeNode = context.interactivityExportContext.CreateNode<Math_NormalizeNode>();
                    inputValue = normalizeNode.ValueIn(Math_NormalizeNode.IdValueA);
                    
                    dotNode.ValueIn(Math_DotNode.IdValueA).ConnectToSource(normalizeNode.FirstValueOut());
                    object valueToCompareNorm = null;
                    float valueToCompareLength = 0f;
                    if (valueToCompare is Vector2 v2)
                    {
                        valueToCompareLength = v2.magnitude;
                        valueToCompareNorm = v2.normalized;
                    }
                    else if (valueToCompare is Vector3 v3)
                    {
                        valueToCompareLength = v3.magnitude;
                        valueToCompareNorm = v3.normalized;
                    }
                    else if (valueToCompare is Vector4 v4)
                    {
                        valueToCompareLength = v4.magnitude;
                        valueToCompareNorm = v4.normalized;
                    }
                    else if (valueToCompare is Quaternion q)
                    {
                        valueToCompareLength = new Vector4(q.x, q.y, q.z, q.w).magnitude;
                        valueToCompareNorm = q.normalized;
                    }
                    
                    dotNode.ValueIn(Math_DotNode.IdValueB).SetValue(valueToCompareNorm);
                    
                    var gtNode = context.interactivityExportContext.CreateNode<Math_GtNode>();
                    gtNode.ValueIn(Math_GtNode.IdValueA).ConnectToSource(dotNode.FirstValueOut());
                    gtNode.SetValueInSocket("b", 1f-proximityCheckDistance);

                    var lengthNode = context.interactivityExportContext.CreateNode<Math_LengthNode>();
                    inputValue = inputValue.Link(lengthNode.ValueIn(Math_LengthNode.IdValueA));
                    
                    var gtLengthNode = context.interactivityExportContext.CreateNode<Math_GtNode>();
                    gtLengthNode.ValueIn(Math_GtNode.IdValueA).ConnectToSource(lengthNode.FirstValueOut());
                    gtLengthNode.SetValueInSocket("b", valueToCompareLength-proximityCheckDistance);
                    
                    var andNode = context.interactivityExportContext.CreateNode<Math_AndNode>();
                    andNode.ValueIn(Math_AndNode.IdValueA).ConnectToSource(gtLengthNode.FirstValueOut());
                    andNode.ValueIn(Math_AndNode.IdValueB).ConnectToSource(gtNode.FirstValueOut());
                    
                    eqNode = andNode;

                    // context.AddLog($"{valueToCompare}   Value={{0}}  ", out var valueInLogFlowIn, out _, 1, out var lvOut);
                    // inputValue = inputValue.Link(lvOut[0]);
                    // context.AddToCurrentEntrySequence(valueInLogFlowIn);
                    // context.AddLog($"{valueToCompare}   Normalized={{0}}  ", out var normLogFlowIn, out _, normalizeNode.FirstValueOut());
                    // context.AddToCurrentEntrySequence(normLogFlowIn);
                    //
                    //  context.AddLog($"{valueToCompare}   Length={{0}}  ", out var lengthLogFlowIn, out _, lengthNode.FirstValueOut());
                    //  context.AddToCurrentEntrySequence(lengthLogFlowIn);
                    //  context.AddLog($"{valueToCompare}   Dot={{0}}  ", out var dotLogFlowIn, out _, dotNode.FirstValueOut());
                    //  context.AddToCurrentEntrySequence(dotLogFlowIn);
         
                    // expectedValue = valueToCompare;
                    // return;
                }
                else
                {
                    var subtractNode = context.interactivityExportContext.CreateNode<Math_SubNode>();
                    inputValue = subtractNode.ValueIn("a");
                    subtractNode.ValueIn("b").SetValue(valueToCompare);
                
                    var absNode = context.interactivityExportContext.CreateNode<Math_AbsNode>();
                    absNode.ValueIn("a").ConnectToSource(subtractNode.FirstValueOut());

                    var lessThanNode = context.interactivityExportContext.CreateNode<Math_LtNode>();
                    lessThanNode.ValueIn("a").ConnectToSource(absNode.FirstValueOut());
                    lessThanNode.SetValueInSocket("b", proximityCheckDistance);
                    eqNode = lessThanNode; 
                }
            }
            else
            {
                if (valueToCompare is float f && float.IsNaN(f))
                {
                    var isNaNNode = context.interactivityExportContext.CreateNode<Math_IsNaNNode>();
                    inputValue = isNaNNode.ValueIn(Math_IsNaNNode.IdValueA);
                    eqNode = isNaNNode;
                }
                else
                {
                    eqNode = context.interactivityExportContext.CreateNode<Math_EqNode>();
                    inputValue = eqNode.ValueIn(Math_EqNode.IdValueA);
                    eqNode.ValueIn(Math_EqNode.IdValueB).SetType(TypeRestriction.LimitToType(compareValueType)).SetValue(valueToCompare);
                }
            }
            
            var validNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
            validNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(eqNode.FirstValueOut());

            SetPassed(out var setPosition, out var flowOutSetValid);
           
            flow = validNode.FlowIn(Flow_BranchNode.IdFlowIn);
            
            validNode.FlowOut(Flow_BranchNode.IdFlowOutTrue)
                .ConnectToFlowDestination(setPosition);
            
            expectedValue = valueToCompare;
            context.AddLog(logText+ ": Value is {0}, should be {1} " + (proximityCheck ? $"(Proximity range: {proximityCheckDistance})" : ""), out var logFlowIn, out var logFlowOut, 2, out var logValueRef);
            inputValue = inputValue.Link(logValueRef[0]);
            logValueRef[1].SetValue(expectedValue);
            validNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(logFlowIn);
            
            context.AddLog(logText+ ": Test Successful", out var logSuccesFlowIn, out var logSuccessFlowOut);

            SavePassResult(out var passValue, out var flowInPass, out var flowOutPass);
            passValue.ConnectToSource(eqNode.FirstValueOut());
            flowOutSetValid.ConnectToFlowDestination(flowInPass);
            flowOutPass.ConnectToFlowDestination(logSuccesFlowIn);
            logSuccessFlowOut.ConnectToFlowDestination(logFlowIn);
            
            SaveResult(out var saveResultInputValue, logFlowOut, valueToCompare.GetType());
            inputValue = inputValue.Link(saveResultInputValue);
            
            PostCheck(() =>
            {
                context.AddLog("ERROR! "+logText+ ": Test Failed", out var logFailedFlowIn, out _);
                return logFailedFlowIn;
            });
        }

        public void SetupCheck(ValueOutRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(out var inputValueRef, out flow, valueToCompare, proximityCheck);
            inputValueRef.ConnectToSource(inputValue);
        }
    }
}