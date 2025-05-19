using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    // TODO: more interpolate value tests with different bezier points
    
    public class VariableInterpolateTest : ITestCase
    {
        private CheckBox _valueAt50percentCheckBox;
        private CheckBox _valueAt100percentCheckBox;
        private CheckBox _flowOutCheckBox;
        private CheckBox _flowDoneCheckBox;
        private CheckBox _errorDurationCheckBox;
        private CheckBox _errorDurationInfCheckBox;
        private CheckBox _errorP1CheckBox;
        private CheckBox _errorP2CheckBox;
        
        public string GetTestName()
        {
            return "variable/interpolate";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _flowOutCheckBox = context.AddCheckBox("Flow [out]", false);
            _valueAt50percentCheckBox = context.AddCheckBox("Value at 50%", true);
            _flowDoneCheckBox = context.AddCheckBox("Flow [done]", true);
            _valueAt100percentCheckBox = context.AddCheckBox("Value at 100%", true);
            _errorDurationCheckBox = context.AddCheckBox("[Err] flow (duration -1f", false);
            _errorDurationInfCheckBox = context.AddCheckBox("[Err] flow (duration infinite", false);
            _errorP1CheckBox = context.AddCheckBox("[Err] flow (p1 NaN)", false);
            _errorP2CheckBox = context.AddCheckBox("[Err] flow (p2 NaN)", false);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var node = nodeCreator.CreateNode<Variable_InterpolateNode>();
            var currentValue = 0f;
            var var1Id = nodeCreator.Context.AddVariableWithIdIfNeeded("varInterpolate_" + Guid.NewGuid().ToString(),
                currentValue, typeof(float));
            
            node.Configuration[Variable_InterpolateNode.IdConfigUseSlerp].Value = false;
            node.Configuration[Variable_InterpolateNode.IdConfigVariable].Value = var1Id;
            var pointA = new Vector2(1f, 1f);
            var pointB = new Vector2(1f, 1f);
            var targetValue = 10f;
            var duration = 4f;
            node.ValueIn(Variable_InterpolateNode.IdPoint1).SetValue(pointA);
            node.ValueIn(Variable_InterpolateNode.IdPoint2).SetValue(pointB);
            node.ValueIn(Variable_InterpolateNode.IdValue).SetValue(targetValue);
            node.ValueIn(Variable_InterpolateNode.IdDuration).SetValue(duration);

            var t = 0.5f;
            var expectedValue = InterpolateHelper.BezierInterpolate(pointA, pointB, currentValue, targetValue, t);
            
            context.NewEntryPoint("Interpolate", duration+ 0.5f);
            context.AddToCurrentEntrySequence(node.FlowIn());
            
            _flowOutCheckBox.SetupCheck(node.FlowOut());
            _flowDoneCheckBox.SetupCheck(out var flowDoneCheckVarRef);
            
            var delayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(duration / 2f);
            context.AddToCurrentEntrySequence(delayNode.FlowIn());

            VariablesHelpers.GetVariable(nodeCreator, var1Id, out var var1ValueRef);

            _valueAt50percentCheckBox.proximityCheckDistance = 0.1f;
            _valueAt50percentCheckBox.SetupCheck(out var checkVarRef, out var flowInRef, expectedValue, true);
            delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(flowInRef);
            checkVarRef.ConnectToSource(var1ValueRef);

            _valueAt100percentCheckBox.SetupCheck(out var checkVarRef2, out var flowInRef2, targetValue, false);
            context.AddSequence(node.FlowOut(Variable_InterpolateNode.IdFlowOutDone), new []
            {
                flowDoneCheckVarRef,
                flowInRef2
            });
            checkVarRef2.ConnectToSource(var1ValueRef);
            
            // Error Flow
            void AddErrorFlowCheck(CheckBox checkBox, float duration, Vector2 p1, Vector2 p2)
            {
                context.NewEntryPoint(checkBox.GetText());
                var var2Id = nodeCreator.Context.AddVariableWithIdIfNeeded("varInterpolate_" + Guid.NewGuid().ToString(),
                    currentValue, typeof(float));

                var errInterpolateNode = nodeCreator.CreateNode<Variable_InterpolateNode>();
                errInterpolateNode.Configuration[Variable_InterpolateNode.IdConfigUseSlerp].Value = false;
                errInterpolateNode.Configuration[Variable_InterpolateNode.IdConfigVariable].Value = var2Id;
                errInterpolateNode.ValueIn(Variable_InterpolateNode.IdDuration).SetValue(duration);
                errInterpolateNode.ValueIn(Variable_InterpolateNode.IdPoint1).SetValue(p1);
                errInterpolateNode.ValueIn(Variable_InterpolateNode.IdPoint2).SetValue(p2);
                errInterpolateNode.ValueIn(Variable_InterpolateNode.IdValue).SetValue(14f);
                context.AddToCurrentEntrySequence(errInterpolateNode.FlowIn());
                checkBox.SetupCheck(errInterpolateNode.FlowOut(Variable_InterpolateNode.IdFlowOutError));
            }
            
            AddErrorFlowCheck(_errorDurationCheckBox, -1f, Vector2.one, Vector2.one);
            AddErrorFlowCheck(_errorDurationInfCheckBox, float.PositiveInfinity, Vector2.one, Vector2.one);
            AddErrorFlowCheck(_errorP1CheckBox, 1f, new Vector2(float.NaN, float.NaN), Vector2.one);
            AddErrorFlowCheck(_errorP2CheckBox, 1f, Vector2.one, new Vector2(float.NaN, float.NaN));
        }
    }
}