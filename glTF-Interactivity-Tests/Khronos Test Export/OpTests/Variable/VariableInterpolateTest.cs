using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
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
        private CheckBox _slerpCheckBox;
        private CheckBox _easeInCheckBox;
        private CheckBox _overshootValueCheckBox;
        private CheckBox _overshootNoErrCheckBox;
        private CheckBox _errorP1XNegativeCheckBox;
        private CheckBox _errorP2XAboveOneCheckBox;
        private CheckBox _errorP1InfCheckBox;
        private CheckBox _zeroDurationDoneCheckBox;
        private CheckBox _zeroDurationValueCheckBox;
        private CheckBox _retriggerFirstDoneCheckBox;
        private CheckBox _retriggerSecondDoneCheckBox;
        private CheckBox _retriggerValueCheckBox;
        private CheckBox _setStopsDoneCheckBox;
        private CheckBox _setStopsValueCheckBox;

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
            _slerpCheckBox = context.AddCheckBox("useSlerp on float4, value at 100%", true);
            context.NewRow();
            _easeInCheckBox = context.AddCheckBox("Ease-in curve, value at 50%", true);
            _overshootValueCheckBox = context.AddCheckBox("Overshoot curve (p1.y -1), value below start", true);
            _overshootNoErrCheckBox = context.AddCheckBox("Overshoot curve: no [err]", false);
            _overshootNoErrCheckBox.Negate();
            _errorP1XNegativeCheckBox = context.AddCheckBox("[Err] flow (p1.x -0.1)", false);
            _errorP2XAboveOneCheckBox = context.AddCheckBox("[Err] flow (p2.x 1.1)", false);
            _errorP1InfCheckBox = context.AddCheckBox("[Err] flow (p1.y +Inf)", false);
            context.NewRow();
            _zeroDurationDoneCheckBox = context.AddCheckBox("Duration 0: [done]", true);
            _zeroDurationValueCheckBox = context.AddCheckBox("Duration 0: value on [done]", true);
            _retriggerFirstDoneCheckBox = context.AddCheckBox("Re-trigger: 1st [done] not fired", true);
            _retriggerFirstDoneCheckBox.Negate();
            _retriggerSecondDoneCheckBox = context.AddCheckBox("Re-trigger: 2nd [done]", true);
            _retriggerValueCheckBox = context.AddCheckBox("Re-trigger: 2nd target reached", true);
            _setStopsDoneCheckBox = context.AddCheckBox("variable/set stops it: [done] not fired", true);
            _setStopsDoneCheckBox.Negate();
            _setStopsValueCheckBox = context.AddCheckBox("variable/set stops it: keeps set value", true);
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
            var pointA = new Vector2(0.25f, 0.1f);
            var pointB = new Vector2(0.25f, 1f);
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

            // Spherical interpolation: useSlerp is only valid for float4 variables
            var slerpDuration = 1f;
            context.NewEntryPoint(_slerpCheckBox.GetText(), slerpDuration + 0.5f);
            var slerpVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("varInterpolateSlerp_" + Guid.NewGuid().ToString(),
                Quaternion.identity, typeof(Quaternion));
            var slerpTarget = Quaternion.Euler(0f, 90f, 0f);

            var slerpNode = nodeCreator.CreateNode<Variable_InterpolateNode>();
            slerpNode.Configuration[Variable_InterpolateNode.IdConfigUseSlerp].Value = true;
            slerpNode.Configuration[Variable_InterpolateNode.IdConfigVariable].Value = slerpVarId;
            slerpNode.ValueIn(Variable_InterpolateNode.IdValue).SetValue(slerpTarget);
            slerpNode.ValueIn(Variable_InterpolateNode.IdDuration).SetValue(slerpDuration);
            slerpNode.ValueIn(Variable_InterpolateNode.IdPoint1).SetValue(pointA);
            slerpNode.ValueIn(Variable_InterpolateNode.IdPoint2).SetValue(pointB);
            context.AddToCurrentEntrySequence(slerpNode.FlowIn());

            VariablesHelpers.GetVariable(nodeCreator, slerpVarId, out var slerpValueRef);
            _slerpCheckBox.quaternionSignAgnostic = true;
            _slerpCheckBox.SetupCheck(out var slerpCheckValueRef, out var slerpCheckFlowIn, slerpTarget, true);
            slerpCheckValueRef.ConnectToSource(slerpValueRef);
            slerpNode.FlowOut(Variable_InterpolateNode.IdFlowOutDone).ConnectToFlowDestination(slerpCheckFlowIn);

            AddErrorFlowCheck(_errorP1XNegativeCheckBox, 1f, new Vector2(-0.1f, 0.5f), Vector2.one);
            AddErrorFlowCheck(_errorP2XAboveOneCheckBox, 1f, Vector2.one, new Vector2(1.1f, 0.5f));
            AddErrorFlowCheck(_errorP1InfCheckBox, 1f, new Vector2(0.5f, float.PositiveInfinity), Vector2.one);

            // Creates a float variable (start value 0) and an interpolate node for it
            GltfInteractivityExportNode CreateFloatInterpolation(string name, float target, float nodeDuration, Vector2 p1, Vector2 p2, out int varId)
            {
                varId = nodeCreator.Context.AddVariableWithIdIfNeeded(name + "_" + Guid.NewGuid().ToString(), 0f, typeof(float));
                var interpolate = nodeCreator.CreateNode<Variable_InterpolateNode>();
                interpolate.Configuration[Variable_InterpolateNode.IdConfigUseSlerp].Value = false;
                interpolate.Configuration[Variable_InterpolateNode.IdConfigVariable].Value = varId;
                interpolate.ValueIn(Variable_InterpolateNode.IdValue).SetValue(target);
                interpolate.ValueIn(Variable_InterpolateNode.IdDuration).SetValue(nodeDuration);
                interpolate.ValueIn(Variable_InterpolateNode.IdPoint1).SetValue(p1);
                interpolate.ValueIn(Variable_InterpolateNode.IdPoint2).SetValue(p2);
                return interpolate;
            }

            // Reads the variable at the given fraction of the duration and compares it with the eased
            // value. LerpUnclamped, because eased progress values outside [0, 1] are valid.
            void AddCurveValueCheck(CheckBox checkBox, string name, Vector2 p1, Vector2 p2, float fraction, float tolerance,
                out GltfInteractivityExportNode interpolate)
            {
                const float curveTarget = 10f;
                const float curveDuration = 4f;
                interpolate = CreateFloatInterpolation(name, curveTarget, curveDuration, p1, p2, out var curveVarId);
                var curveDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
                curveDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(curveDuration * fraction);

                context.NewEntryPoint(checkBox.GetText(), curveDuration + 0.5f);
                context.AddToCurrentEntrySequence(interpolate.FlowIn(), curveDelay.FlowIn());

                var expected = Mathf.LerpUnclamped(0f, curveTarget, InterpolateHelper.EvaluateEasing(p1, p2, fraction));
                VariablesHelpers.GetVariable(nodeCreator, curveVarId, out var curveValue);
                checkBox.proximityCheckDistance = tolerance;
                checkBox.SetupCheck(curveValue, out var curveCheckFlow, expected, true);
                curveDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(curveCheckFlow);
            }

            // CSS "ease-in": 50% time -> ~31.5% progress
            AddCurveValueCheck(_easeInCheckBox, "varInterpolateEaseIn", new Vector2(0.42f, 0f), new Vector2(1f, 1f), 0.5f, 0.2f, out _);

            // Overshooting curve: the progress dips to about -0.207 at 19% of the duration, where the curve
            // is flat, so timing jitter barely matters. An implementation that clamps the progress reads 0.
            AddCurveValueCheck(_overshootValueCheckBox, "varInterpolateOvershoot", new Vector2(0.5f, -1f), new Vector2(0.5f, 2f), 0.19f, 0.2f,
                out var overshootNode);
            _overshootNoErrCheckBox.SetupNegateCheck(overshootNode.FlowOut(Variable_InterpolateNode.IdFlowOutError));

            // Duration 0: t is NaN or >= 1 on the next tick -> target value and [done]
            var zeroDurationNode = CreateFloatInterpolation("varInterpolateZeroDuration", 3f, 0f, pointA, pointB, out var zeroDurationVarId);
            context.NewEntryPoint(_zeroDurationDoneCheckBox.GetText(), 1f);
            context.AddToCurrentEntrySequence(zeroDurationNode.FlowIn());
            VariablesHelpers.GetVariable(nodeCreator, zeroDurationVarId, out var zeroDurationValue);
            _zeroDurationDoneCheckBox.SetupCheck(out var zeroDurationDoneFlow);
            _zeroDurationValueCheckBox.SetupCheck(zeroDurationValue, out var zeroDurationValueFlow, 3f, false);
            context.AddSequence(zeroDurationNode.FlowOut(Variable_InterpolateNode.IdFlowOutDone), zeroDurationDoneFlow, zeroDurationValueFlow);

            // Re-trigger: a second interpolation of the same variable replaces the first entry
            const float retriggerDuration = 1f;
            var retriggerVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("varInterpolateRetrigger_" + Guid.NewGuid().ToString(), 0f, typeof(float));
            GltfInteractivityExportNode CreateRetrigger(float target)
            {
                var interpolate = nodeCreator.CreateNode<Variable_InterpolateNode>();
                interpolate.Configuration[Variable_InterpolateNode.IdConfigUseSlerp].Value = false;
                interpolate.Configuration[Variable_InterpolateNode.IdConfigVariable].Value = retriggerVarId;
                interpolate.ValueIn(Variable_InterpolateNode.IdValue).SetValue(target);
                interpolate.ValueIn(Variable_InterpolateNode.IdDuration).SetValue(retriggerDuration);
                interpolate.ValueIn(Variable_InterpolateNode.IdPoint1).SetValue(pointA);
                interpolate.ValueIn(Variable_InterpolateNode.IdPoint2).SetValue(pointB);
                return interpolate;
            }
            var retriggerFirst = CreateRetrigger(10f);
            var retriggerSecond = CreateRetrigger(20f);
            context.NewEntryPoint(_retriggerFirstDoneCheckBox.GetText(), retriggerDuration + 1f);
            context.AddToCurrentEntrySequence(retriggerFirst.FlowIn(), retriggerSecond.FlowIn());
            VariablesHelpers.GetVariable(nodeCreator, retriggerVarId, out var retriggerValue);
            _retriggerFirstDoneCheckBox.SetupNegateCheck(retriggerFirst.FlowOut(Variable_InterpolateNode.IdFlowOutDone));
            _retriggerSecondDoneCheckBox.SetupCheck(out var retriggerSecondDoneFlow);
            _retriggerValueCheckBox.SetupCheck(retriggerValue, out var retriggerValueFlow, 20f, false);
            context.AddSequence(retriggerSecond.FlowOut(Variable_InterpolateNode.IdFlowOutDone), retriggerSecondDoneFlow, retriggerValueFlow);

            // variable/set removes a running interpolation of the variable
            const float setStopsDuration = 1f;
            var setStopsNode = CreateFloatInterpolation("varInterpolateSetStops", 10f, setStopsDuration, pointA, pointB, out var setStopsVarId);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, setStopsVarId, 4f, out var setStopsSetFlowIn, out _);
            var setStopsDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            setStopsDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(setStopsDuration + 0.5f);
            context.NewEntryPoint(_setStopsDoneCheckBox.GetText(), setStopsDuration + 1f);
            context.AddToCurrentEntrySequence(setStopsNode.FlowIn(), setStopsSetFlowIn, setStopsDelay.FlowIn());
            VariablesHelpers.GetVariable(nodeCreator, setStopsVarId, out var setStopsValue);
            _setStopsDoneCheckBox.SetupNegateCheck(setStopsNode.FlowOut(Variable_InterpolateNode.IdFlowOutDone));
            _setStopsValueCheckBox.SetupCheck(setStopsValue, out var setStopsValueFlow, 4f, false);
            setStopsDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(setStopsValueFlow);
        }
    }
}