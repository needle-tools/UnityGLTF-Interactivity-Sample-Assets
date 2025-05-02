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
                ResultValueVarId = context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(GetResultVariableName(), false, GltfTypes.Bool);
            
            VariablesHelpers.SetVariableStaticValue(context.interactivityExportContext, ResultValueVarId, true, out var setFlow, out _);
            flow.ConnectToFlowDestination(setFlow);
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
        }

        public void SetupCheck(TestContext context, FlowOutRef flow)
        {
            SetupCheck(context, out var flowIn);
            flow.ConnectToFlowDestination(flowIn);
        }

        public void SetupCheck(TestContext context, ValueOutRef inputValue, FlowOutRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            SetupCheck(context, inputValue, out var flowIn, valueToCompare, proximityCheck);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void SetupCheck(TestContext context, ValueOutRef inputValue, out FlowInRef flow, object valueToCompare,
            bool proximityCheck = false)
        {
            var compareValueType = GltfTypes.TypeIndex(valueToCompare.GetType());
            validIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(valid);
            invalidIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(invalid);

            GltfInteractivityExportNode eqNode;
            if (proximityCheck)
            {
                var subtractNode = context.interactivityExportContext.CreateNode(new Math_SubNode());
                subtractNode.ValueIn("a").ConnectToSource(inputValue);
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
                eqNode.ValueIn(Math_EqNode.IdValueA).ConnectToSource(inputValue);
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
            
            context.AddLog(text.text+ ": Value is {0}, should be "+valueToCompare.ToString(), out var logFlowIn, out var logFlowOut, inputValue);
            validNode.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(logFlowIn);
            
            setPosition.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(logFlowIn);
            
            expectedValue = valueToCompare;
            SaveResult(context, inputValue, logFlowOut, valueToCompare.GetType());
            
        }
    }
}