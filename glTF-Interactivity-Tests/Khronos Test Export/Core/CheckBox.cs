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
        [SerializeField] private Transform waitForStart;
        [SerializeField] private Vector3 positionWhenValid;
        [SerializeField] private Vector2 size;

        // Used as the start of debug/log message templates: literal braces must be doubled, otherwise
        // e.g. "/nodes/{}/weights" makes the template invalid and the message falls back to empty
        public string logText => $"<{_testCase.CaseName} - {GetText()}>".Replace("{", "{{").Replace("}", "}}");
        public Vector2 CheckBoxSize => size;

        /// <summary>
        /// Shown until the test case of this check box starts. Only used by the all-in-one export,
        /// where the cases run one after another (see <see cref="TestContext.runCasesSequentially"/>).
        /// </summary>
        public Transform WaitForStart => waitForStart;

        /// <summary> Removes the <see cref="WaitForStart"/> object, so it doesn't get exported. </summary>
        public void RemoveWaitForStart()
        {
            if (waitForStart)
                DestroyImmediate(waitForStart.gameObject);
            waitForStart = null;
        }
        
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
        public bool flowOnce = false;

        /// <summary> Max characters per line of the displayed label, see <see cref="WrapLabelText"/>. </summary>
        public const int MaxLabelLineLength = 25;

        // Characters after which a long label line may be broken (the space itself is dropped at the break)
        private static readonly char[] LabelBreakCharacters = { ' ', '/', '\\', '_' };

        // The text as given to SetText. The displayed text is wrapped, names and json use this one.
        private string _rawText;

        /// <summary>
        /// When set, a Quaternion proximity check treats q and -q as equal (compares |dot| instead of
        /// dot), since both represent the same rotation. Required e.g. by math/matDecompose, whose spec
        /// permits the identity rotation to be either (0,0,0,1) or (0,0,0,-1).
        /// </summary>
        public bool quaternionSignAgnostic = false;

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
        
        /// <param name="text">Full text, used for entry point, variable and readme names (see <see cref="GetText"/>).</param>
        /// <param name="labelText">Optional shorter text shown on the 3D label only. Falls back to <paramref name="text"/>.</param>
        public void SetText(string text, string labelText = null)
        {
            _rawText = text;
            this.text.text = WrapLabelText(labelText ?? text, MaxLabelLineLength);
        }
        
        public string GetText()
        {
            return _rawText ?? text.text;
        }

        /// <summary>
        /// Breaks lines longer than <paramref name="maxLineLength"/>, since the TextMeshPro word wrapping
        /// only breaks at spaces and long words like pointer templates overflow the label. Breaks only
        /// after a space, '/', '\\' or '_', so words are never cut in half; a single word without such a
        /// character stays longer than the limit. Existing line breaks are kept.
        /// </summary>
        public static string WrapLabelText(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var wrappedLines = new System.Collections.Generic.List<string>();
            foreach (var line in text.Split('\n'))
            {
                var current = "";
                foreach (var token in SplitAfterBreakCharacters(line))
                {
                    if (current.Length > 0 && (current + token).TrimEnd().Length > maxLineLength)
                    {
                        wrappedLines.Add(current.TrimEnd());
                        current = token.TrimStart();
                    }
                    else
                        current += token;
                }
                wrappedLines.Add(current.TrimEnd());
            }
            return string.Join("\n", wrappedLines);
        }

        // "/nodes/{} abc" -> "/", "nodes/", "{} ", "abc"
        private static System.Collections.Generic.IEnumerable<string> SplitAfterBreakCharacters(string line)
        {
            var start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (Array.IndexOf(LabelBreakCharacters, line[i]) < 0)
                    continue;
                yield return line.Substring(start, i + 1 - start);
                start = i + 1;
            }
            if (start < line.Length)
                yield return line.Substring(start);
        }

        public string GetResultVariableName()
        {
            if (resultVarName != null)
                return resultVarName;
            
            var name = "TestResult_" + _testCase.CaseName + "_" + GetText();
            
            if (context.interactivityExportContext.Context.variables.Exists(v => v.Name == name))
            {
                var existingCount = context.interactivityExportContext.Context.variables.Count(v => v.Name.StartsWith(name));
                name += $" ({existingCount.ToString()})";
            }
            resultVarName = name;
            return name;
        }

        public string GetResultPassVariableName()
        {
            if (resultPassVarName != null)
                return resultPassVarName;
            
            var name = "TestResult_HasPassed_" + _testCase.CaseName + "_" + GetText();
            
            if (context.interactivityExportContext.Context.variables.Exists(v => v.Name == name))
            {
                var existingCount = context.interactivityExportContext.Context.variables.Count(v => v.Name.StartsWith(name));
                name += $" ({existingCount.ToString()})";
            }
            resultPassVarName = name;
            return name;
        }

        private void DeactivateWaiting(out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var waitingIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(waiting);
            
            var setPosition = context.interactivityExportContext.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(Vector3.zero);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(waitingIndex);  
            
            flowIn = setPosition.FlowIn(Pointer_SetNode.IdFlowIn); 
            flowOut = setPosition.FlowOut(Pointer_SetNode.IdFlowOut);
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
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), false, GltfTypes.Bool);
             
            VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, ResultValueVarId, true, out var setResultFlow, out var setResultFlowOut);
            flow.ConnectToFlowDestination(setResultFlow);

            if (isNegated)
            {
                if (ResultPassValueVarId == -1)
                    ResultPassValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultPassVariableName(), true, GltfTypes.Bool);

                VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, ResultPassValueVarId, false, out var setPassFlow, out _);
                setResultFlowOut.ConnectToFlowDestination(setPassFlow);
            }
            else
            {
                ResultPassValueVarId = ResultValueVarId;
            }
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
                if (context.interactivityExportContext.Context.variables.Exists(v => v.Name == resultVarName))
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
                if (context.interactivityExportContext.Context.variables.Exists(v => v.Name == resultVarName))
                    throw new Exception("Variable with the same name already exists: " + resultVarName);
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(resultVarName, initValue, gltfType);

            }
            VariablesHelpers.SetVariable(context.interactivityExportContext, ResultValueVarId, value, flow);
        }

        private void SetToForeground(int nodeIndex, out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var setPosition = context.interactivityExportContext.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setPosition, PointersHelper.IdPointerNodeIndex, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation", GltfTypes.Float3);
            setPosition.ValueIn(Pointer_SetNode.IdValue).SetValue(positionWhenValid);
            setPosition.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            flowIn = setPosition.FlowIn();
            flowOut = setPosition.FlowOut();

            // A waiting (hourglass) checkbox/marker otherwise only clears via the entry point's
            // timeout fallback in PostCheck, so it would keep showing "waiting" on top of the
            // valid/invalid mark for the whole interaction window even though the result is
            // already known. Clear it the instant the result is set instead.
            if (isWaiting)
            {
                DeactivateWaiting(out var waitFlowIn, out var waitFlowOut);
                flowOut.ConnectToFlowDestination(waitFlowIn);
                flowOut = waitFlowOut;
            }
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
        
        private void CreateFlowOnceGate(out FlowInRef gateIn, out FlowOutRef gateOut)
        {
            var varId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(
                "FlowOnce_" + System.Guid.NewGuid(), false, typeof(bool));

            VariablesHelpers.GetVariable(context.interactivityExportContext, varId, out var varRef);

            var branchNode = context.interactivityExportContext.CreateNode<Flow_BranchNode>();
            branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(varRef);

            // false branch: not yet triggered → mark done, then continue
            VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, varId, true, out var setFlowIn, out var setFlowOut);
            branchNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(setFlowIn);

            gateIn = branchNode.FlowIn(Flow_BranchNode.IdFlowIn);
            gateOut = setFlowOut;
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

            if (flowOnce)
            {
                CreateFlowOnceGate(out var gateIn, out var gateOut);
                gateOut.ConnectToFlowDestination(flow);
                flow = gateIn;
            }
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

            if (flowOnce)
            {
                CreateFlowOnceGate(out var gateIn, out var gateOut);
                gateOut.ConnectToFlowDestination(flow);
                flow = gateIn;
            }
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
        
        /// <summary>
        /// Builds a component-wise check for a vector (float2/3/4) whose expected value contains at
        /// least one NaN component. math/eq compares NaN as not-equal, so such a value can never be
        /// matched by the regular equality/proximity paths. Instead each component is extracted and
        /// checked individually: NaN components via math/isNaN, finite components via a tolerance
        /// (|actual - expected| &lt; proximityCheckDistance), and the per-component results are ANDed.
        /// Returns false (and leaves the outputs untouched) when the value is not a NaN-bearing vector.
        /// </summary>
        private bool TryBuildNaNVectorCheck(object valueToCompare, out GltfInteractivityExportNode resultNode, out ValueInRef inputValue)
        {
            resultNode = null;
            inputValue = null;

            GltfInteractivityExportNode extractNode;
            float[] components;

            if (valueToCompare is Vector2 v2 && (float.IsNaN(v2.x) || float.IsNaN(v2.y)))
            {
                extractNode = context.interactivityExportContext.CreateNode<Math_Extract2Node>();
                inputValue = extractNode.ValueIn(Math_Extract2Node.IdValueIn);
                components = new[] { v2.x, v2.y };
            }
            else if (valueToCompare is Vector3 v3 && (float.IsNaN(v3.x) || float.IsNaN(v3.y) || float.IsNaN(v3.z)))
            {
                extractNode = context.interactivityExportContext.CreateNode<Math_Extract3Node>();
                inputValue = extractNode.ValueIn(Math_Extract3Node.IdValueIn);
                components = new[] { v3.x, v3.y, v3.z };
            }
            else if (valueToCompare is Vector4 v4 && (float.IsNaN(v4.x) || float.IsNaN(v4.y) || float.IsNaN(v4.z) || float.IsNaN(v4.w)))
            {
                extractNode = context.interactivityExportContext.CreateNode<Math_Extract4Node>();
                inputValue = extractNode.ValueIn(Math_Extract4Node.IdValueIn);
                components = new[] { v4.x, v4.y, v4.z, v4.w };
            }
            else
            {
                return false;
            }

            ValueOutRef combined = null;
            for (int i = 0; i < components.Length; i++)
            {
                GltfInteractivityExportNode componentNode;
                if (float.IsNaN(components[i]))
                {
                    var isNaNNode = context.interactivityExportContext.CreateNode<Math_IsNaNNode>();
                    isNaNNode.ValueIn(Math_IsNaNNode.IdValueA).ConnectToSource(extractNode.ValueOut(i.ToString()));
                    componentNode = isNaNNode;
                }
                else
                {
                    var subNode = context.interactivityExportContext.CreateNode<Math_SubNode>();
                    subNode.ValueIn("a").ConnectToSource(extractNode.ValueOut(i.ToString()));
                    subNode.ValueIn("b").SetValue(components[i]);

                    var absNode = context.interactivityExportContext.CreateNode<Math_AbsNode>();
                    absNode.ValueIn("a").ConnectToSource(subNode.FirstValueOut());

                    var lessThanNode = context.interactivityExportContext.CreateNode<Math_LtNode>();
                    lessThanNode.ValueIn("a").ConnectToSource(absNode.FirstValueOut());
                    lessThanNode.SetValueInSocket("b", proximityCheckDistance);
                    componentNode = lessThanNode;
                }

                if (combined == null)
                {
                    combined = componentNode.FirstValueOut();
                    resultNode = componentNode;
                }
                else
                {
                    var andNode = context.interactivityExportContext.CreateNode<Math_AndNode>();
                    andNode.ValueIn("a").ConnectToSource(combined);
                    andNode.ValueIn("b").ConnectToSource(componentNode.FirstValueOut());
                    combined = andNode.FirstValueOut();
                    resultNode = andNode;
                }
            }

            return true;
        }

        public void SetupCheck(out ValueInRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            this.proximityCheck = proximityCheck;
            var compareValueType = GltfTypes.TypeIndex(valueToCompare.GetType());
        
            GltfInteractivityExportNode eqNode = null;
            if (TryBuildNaNVectorCheck(valueToCompare, out eqNode, out inputValue))
            {
                // A vector containing at least one NaN component (e.g. an "invalid" matDecompose
                // scale result). math/eq against NaN is always false, so we compare component-wise:
                // NaN components are verified with math/isNaN, finite components with a tolerance check.
            }
            else if (proximityCheck)
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

                    // For a sign-agnostic quaternion check, q and -q are the same rotation, so compare
                    // the absolute dot product against 1 rather than the signed dot.
                    ValueOutRef dotResult = dotNode.FirstValueOut();
                    if (quaternionSignAgnostic && valueToCompare is Quaternion)
                    {
                        var absDotNode = context.interactivityExportContext.CreateNode<Math_AbsNode>();
                        absDotNode.ValueIn("a").ConnectToSource(dotNode.FirstValueOut());
                        dotResult = absDotNode.FirstValueOut();
                    }

                    var gtNode = context.interactivityExportContext.CreateNode<Math_GtNode>();
                    gtNode.ValueIn(Math_GtNode.IdValueA).ConnectToSource(dotResult);
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
                else if (GltfTypes.GetTypeMapping(valueToCompare.GetType())?.GltfSignature == GltfTypes.Ref )
                {
                    eqNode = context.interactivityExportContext.CreateNode<Ref_EqNode>();
                    inputValue = eqNode.ValueIn(Ref_EqNode.IdValueA);
                    eqNode.ValueIn(Ref_EqNode.IdValueB).SetType(TypeRestriction.LimitToType(compareValueType)).SetValue(valueToCompare);
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

            // "Waiting" means "not evaluated yet", not "hasn't passed yet". SetPassed/SetToForeground
            // only clears the waiting indicator on the pass (True) branch below, so without this the
            // indicator would stay stuck until the entry point's full timeout on a failing comparison,
            // instead of resolving immediately once this check actually runs.
            if (isWaiting)
            {
                DeactivateWaiting(out var waitFlowIn, out var waitFlowOut);
                waitFlowOut.ConnectToFlowDestination(flow);
                flow = waitFlowIn;
            }

            validNode.FlowOut(Flow_BranchNode.IdFlowOutTrue)
                .ConnectToFlowDestination(setPosition);
            
            expectedValue = valueToCompare;
            context.AddLog(logText+ ": Value is {0}, should be {1} " + (proximityCheck ? $"(Proximity range: {TestValueFormat.ToStr(proximityCheckDistance)})" : ""), out var logFlowIn, out var logFlowOut, 2, out var logValueRef);
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

            if (flowOnce)
            {
                CreateFlowOnceGate(out var gateIn, out var gateOut);
                gateOut.ConnectToFlowDestination(flow);
                flow = gateIn;
            }
        }

        public void SetupCheck(ValueOutRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(out var inputValueRef, out flow, valueToCompare, proximityCheck);
            inputValueRef.ConnectToSource(inputValue);
        }
    }
}
