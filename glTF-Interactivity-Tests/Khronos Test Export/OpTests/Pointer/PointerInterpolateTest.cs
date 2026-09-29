using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests pointer/interpolate by animating an empty node's local translation
    /// (/nodes/{}/translation) from the origin to a target position over a fixed duration.
    ///
    /// Checks:
    ///  - [out] fires as the *synchronous* continuation of [in]: a flow/sequence activates the
    ///    interpolation on output [0] (in -> out -> flowcheck-1) and fires flowcheck-2 on output [1];
    ///    a SetupOrderFlowCheck requires flowcheck-1 before flowcheck-2, so it fails for any impl that
    ///    defers [out] to completion ([done])
    ///  - the value read back at 50% of the duration matches the bezier-interpolated midpoint
    ///  - [done] fires when the interpolation completes
    ///  - the value read back on [done] equals the target
    ///  - [err] fires for invalid inputs: negative duration, infinite duration, NaN p1, NaN p2,
    ///    p1.x &lt; 0, p2.x &gt; 1, an infinite p1 component, an out-of-range index, a read-only
    ///    pointer and a type mismatch
    ///  - p1.y / p2.y outside [0, 1] (overshoot) is not an error
    ///  - duration 0: the target value is applied and [done] fires on the next tick
    ///  - starting a second interpolation of the same pointer replaces the first one: the first
    ///    [done] never fires, the second one does and the target is the second value
    ///  - a quaternion pointer (/nodes/{}/rotation) is interpolated with slerp
    ///
    /// The objects start at the origin so the start value is (0,0,0) in glTF space, which makes the
    /// expected values independent of Unity->glTF coordinate conversion. Every case that actually
    /// runs an interpolation uses its own object, since a second interpolation of the same pointer
    /// would replace the first.
    /// </summary>
    public class PointerInterpolateTest : ITestCase, IDisposable
    {
        private GameObject _target;
        private GameObject _zeroDurationTarget;
        private GameObject _retriggerTarget;
        private GameObject _rotationTarget;
        private GameObject _overshootTarget;

        private CheckBox _outOrderCheckBox;
        private CheckBox _valueAt50CheckBox;
        private CheckBox _flowDoneCheckBox;
        private CheckBox _valueAt100CheckBox;

        private CheckBox _errDurationNegCheckBox;
        private CheckBox _errDurationInfCheckBox;
        private CheckBox _errP1CheckBox;
        private CheckBox _errP2CheckBox;
        private CheckBox _errP1XNegativeCheckBox;
        private CheckBox _errP2XAboveOneCheckBox;
        private CheckBox _errP1InfCheckBox;
        private CheckBox _errOutOfRangeCheckBox;
        private CheckBox _errReadOnlyCheckBox;
        private CheckBox _errTypeMismatchCheckBox;

        private CheckBox _overshootOutCheckBox;
        private CheckBox _overshootNoErrCheckBox;
        private CheckBox _zeroDurationDoneCheckBox;
        private CheckBox _zeroDurationValueCheckBox;
        private CheckBox _retriggerFirstDoneCheckBox;
        private CheckBox _retriggerSecondDoneCheckBox;
        private CheckBox _retriggerValueCheckBox;
        private CheckBox _rotationSlerpCheckBox;

        private static string Template =>
            "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation";

        private static string RotationTemplate =>
            "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/rotation";

        // Control points on the diagonal give a linear easing curve (y == x)
        private static readonly Vector2 LinearP1 = new Vector2(0.25f, 0.25f);
        private static readonly Vector2 LinearP2 = new Vector2(0.75f, 0.75f);

        // 160 degrees around Y, in glTF space. At 25% slerp gives 40 degrees, while a normalized
        // linear interpolation (nlerp) gives about 34.5 degrees.
        private static readonly Quaternion RotationTarget = new Quaternion(0f, Mathf.Sin(80f * Mathf.Deg2Rad), 0f, Mathf.Cos(80f * Mathf.Deg2Rad));
        private const float RotationDuration = 4f;
        private const float RotationSampleFraction = 0.25f;

        private static readonly Vector3 StartPosition = Vector3.zero;
        private static readonly Vector3 TargetPosition = new Vector3(2f, 3f, 4f);
        private static readonly Vector2 P1 = new Vector2(0.25f, 0.1f);
        private static readonly Vector2 P2 = new Vector2(0.25f, 1f);
        private const float Duration = 4f;

        public string GetTestName()
        {
            return "pointer/interpolate";
        }

        public string GetTestDescription()
        {
            return "Interpolates a node's translation and checks the value at 50%/100% plus the error flows.";
        }

        public void PrepareObjects(TestContext context)
        {
            _target = new GameObject("PointerInterpolateTarget");
            _target.transform.SetParent(context.Root);
            _target.transform.localPosition = StartPosition;
            _target.transform.localRotation = Quaternion.identity;
            _target.transform.localScale = Vector3.one * 0.0001f;

            _outOrderCheckBox = context.AddCheckBox("[out] fired right after [in]");
            _valueAt50CheckBox = context.AddCheckBox("Value at 50%", true);
            _flowDoneCheckBox = context.AddCheckBox("Flow [done]", true);
            _valueAt100CheckBox = context.AddCheckBox("Value at 100%", true);
            context.NewRow();
            _errDurationNegCheckBox = context.AddCheckBox("[err] flow (duration -1)", false);
            _errDurationInfCheckBox = context.AddCheckBox("[err] flow (duration infinite)", false);
            _errP1CheckBox = context.AddCheckBox("[err] flow (p1 NaN)", false);
            _errP2CheckBox = context.AddCheckBox("[err] flow (p2 NaN)", false);
            context.NewRow();
            _errP1XNegativeCheckBox = context.AddCheckBox("[err] flow (p1.x -0.1)", false);
            _errP2XAboveOneCheckBox = context.AddCheckBox("[err] flow (p2.x 1.1)", false);
            _errP1InfCheckBox = context.AddCheckBox("[err] flow (p1.y +Inf)", false);
            _errOutOfRangeCheckBox = context.AddCheckBox("[err] flow (index out of range)", false);
            _errReadOnlyCheckBox = context.AddCheckBox("[err] flow (read-only globalMatrix)", false);
            _errTypeMismatchCheckBox = context.AddCheckBox("[err] flow (float on translation)", false);
            context.NewRow();
            _overshootOutCheckBox = context.AddCheckBox("p1.y/p2.y outside [0,1]: [out]", false);
            _overshootNoErrCheckBox = context.AddCheckBox("p1.y/p2.y outside [0,1]: no [err]", false);
            _overshootNoErrCheckBox.Negate();
            _zeroDurationDoneCheckBox = context.AddCheckBox("Duration 0: [done]", true);
            _zeroDurationValueCheckBox = context.AddCheckBox("Duration 0: value on [done]", true);
            context.NewRow();
            _retriggerFirstDoneCheckBox = context.AddCheckBox("Re-trigger: 1st [done] not fired", true);
            _retriggerFirstDoneCheckBox.Negate();
            _retriggerSecondDoneCheckBox = context.AddCheckBox("Re-trigger: 2nd [done]", true);
            _retriggerValueCheckBox = context.AddCheckBox("Re-trigger: 2nd target reached", true);
            _rotationSlerpCheckBox = context.AddCheckBox("Rotation uses slerp (value at 25%)", true);

            GameObject CreateTarget(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(context.Root);
                go.transform.localPosition = StartPosition;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.0001f;
                return go;
            }
            _zeroDurationTarget = CreateTarget("PointerInterpolateZeroDurationTarget");
            _retriggerTarget = CreateTarget("PointerInterpolateRetriggerTarget");
            _rotationTarget = CreateTarget("PointerInterpolateRotationTarget");
            _overshootTarget = CreateTarget("PointerInterpolateOvershootTarget");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var nodeIndex = nodeCreator.Context.exporter.GetTransformIndex(_target.transform);

            var expectedMid = (Vector3)InterpolateHelper.BezierInterpolate(P1, P2, StartPosition, TargetPosition, 0.5f);

            // ── Interpolate the translation ─────────────────────────────────────────
            var interpolateNode = nodeCreator.CreateNode<Pointer_InterpolateNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(interpolateNode, PointersHelper.IdPointerNodeIndex, Template, GltfTypes.Float3);
            interpolateNode.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(TargetPosition);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(Duration);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(P1);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(P2);

            // A single pointer/get read back at the two check times.
            var pGet = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pGet, PointersHelper.IdPointerNodeIndex, Template, GltfTypes.Float3);
            pGet.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var delayNode = nodeCreator.CreateNode<Flow_SetDelayNode>();
            delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(Duration / 2f);

            // A flow/sequence fires its outputs in order and only starts the next output once the
            // previous output's downstream has fully completed. We use that to assert [out] is the
            // *synchronous* continuation of [in]:
            //   Sequence[0] -> interpolate [in] -> interpolate [out] -> flowcheck-1
            //   Sequence[1] -> flowcheck-2
            //   Sequence[2] -> start the delay for the 50% / 100% value checks
            // SetupOrderFlowCheck passes only i f flowcheck-1 fires before flowcheck-2. A buggy impl
            // that defers [out] to completion fires Sequence[1] (flowcheck-2) first -> check fails.
            var startSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
            startSequence.FlowOut("0").ConnectToFlowDestination(interpolateNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
            startSequence.FlowOut("2").ConnectToFlowDestination(delayNode.FlowIn(Flow_SetDelayNode.IdFlowIn));

            context.NewEntryPoint("Interpolate translation", Duration + 0.5f);
            context.AddToCurrentEntrySequence(startSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
            
            _outOrderCheckBox.SetupOrderFlowCheck(new[]
            {
                interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOut), // flowcheck-1 (via [in] -> [out])
                startSequence.FlowOut("1"),                                 // flowcheck-2 (Sequence[1])
            });
            
            // Value at 50%: read back after half the duration
            _valueAt50CheckBox.proximityCheckDistance = 0.1f;
            _valueAt50CheckBox.SetupCheck(out var midValueRef, out var midFlowIn, expectedMid, true);
            midValueRef.ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));
            delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(midFlowIn);

            // Value at 100% + [done]
            _flowDoneCheckBox.SetupCheck(out var flowDoneCheckFlow);
            _valueAt100CheckBox.proximityCheckDistance = 0.001f;
            _valueAt100CheckBox.SetupCheck(out var endValueRef, out var endFlowIn, TargetPosition, true);
            endValueRef.ConnectToSource(pGet.ValueOut(Pointer_GetNode.IdValue));
            context.AddSequence(interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOutDone), new[]
            {
                flowDoneCheckFlow,
                endFlowIn
            });

            // ── Error flows ─────────────────────────────────────────────────────────
            void AddErrorFlowCheck(CheckBox checkBox, float duration, Vector2 p1, Vector2 p2)
            {
                context.NewEntryPoint(checkBox.GetText());
                var errNode = nodeCreator.CreateNode<Pointer_InterpolateNode>();
                PointersHelper.SetupPointerTemplateAndTargetInput(errNode, PointersHelper.IdPointerNodeIndex, Template, GltfTypes.Float3);
                errNode.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);
                errNode.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(TargetPosition);
                errNode.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(duration);
                errNode.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(p1);
                errNode.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(p2);
                context.AddToCurrentEntrySequence(errNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
                checkBox.SetupCheck(errNode.FlowOut(Pointer_InterpolateNode.IdFlowOutError));
            }

            AddErrorFlowCheck(_errDurationNegCheckBox, -1f, P1, P2);
            AddErrorFlowCheck(_errDurationInfCheckBox, float.PositiveInfinity, P1, P2);
            AddErrorFlowCheck(_errP1CheckBox, 1f, new Vector2(float.NaN, float.NaN), P2);
            AddErrorFlowCheck(_errP2CheckBox, 1f, P1, new Vector2(float.NaN, float.NaN));

            // The x coordinates of the control points are restricted to [0, 1]; any Inf/NaN component is invalid
            AddErrorFlowCheck(_errP1XNegativeCheckBox, 1f, new Vector2(-0.1f, 0.5f), P2);
            AddErrorFlowCheck(_errP2XAboveOneCheckBox, 1f, P1, new Vector2(1.1f, 0.5f));
            AddErrorFlowCheck(_errP1InfCheckBox, 1f, new Vector2(0.25f, float.PositiveInfinity), P2);

            // Pointers that can't be interpolated: unresolvable, read-only, type mismatch
            void AddPointerErrorCheck(CheckBox checkBox, string template, string gltfType, object value, int index)
            {
                context.NewEntryPoint(checkBox.GetText());
                var errNode = nodeCreator.CreateNode<Pointer_InterpolateNode>();
                PointersHelper.SetupPointerTemplateAndTargetInput(errNode, PointersHelper.IdPointerNodeIndex, template, gltfType);
                errNode.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(index);
                errNode.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(value);
                errNode.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(1f);
                errNode.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(P1);
                errNode.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(P2);
                context.AddToCurrentEntrySequence(errNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
                checkBox.SetupCheck(errNode.FlowOut(Pointer_InterpolateNode.IdFlowOutError));
            }

            AddPointerErrorCheck(_errOutOfRangeCheckBox, Template, GltfTypes.Float3, TargetPosition,
                nodeCreator.Context.exporter.GetRoot().Nodes.Count + 100);
            AddPointerErrorCheck(_errReadOnlyCheckBox, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/globalMatrix",
                GltfTypes.Float4x4, Matrix4x4.identity, nodeIndex);
            AddPointerErrorCheck(_errTypeMismatchCheckBox, Template, GltfTypes.Float, 1f, nodeIndex);

            // Helper for the cases below that run a real interpolation on their own object
            GltfInteractivityExportNode CreateInterpolation(int targetIndex, string template, string gltfType, object value,
                float duration, Vector2 p1, Vector2 p2)
            {
                var node = nodeCreator.CreateNode<Pointer_InterpolateNode>();
                PointersHelper.SetupPointerTemplateAndTargetInput(node, PointersHelper.IdPointerNodeIndex, template, gltfType);
                node.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(targetIndex);
                node.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(value);
                node.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(duration);
                node.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(p1);
                node.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(p2);
                return node;
            }

            ValueOutRef TranslationY(int targetIndex)
            {
                var get = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.SetupPointerTemplateAndTargetInput(get, PointersHelper.IdPointerNodeIndex, Template, GltfTypes.Float3);
                get.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(targetIndex);
                var extract = nodeCreator.CreateNode<Math_Extract3Node>();
                extract.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(get.ValueOut(Pointer_GetNode.IdValue));
                return extract.ValueOut(Math_Extract3Node.IdValueOutY);
            }

            // ── Overshooting control points (y outside [0, 1]) are valid ───────────
            var overshootIndex = nodeCreator.Context.exporter.GetTransformIndex(_overshootTarget.transform);
            context.NewEntryPoint(_overshootOutCheckBox.GetText(), 1f);
            var overshootNode = CreateInterpolation(overshootIndex, Template, GltfTypes.Float3, new Vector3(0f, 1f, 0f),
                0.5f, new Vector2(0.25f, -0.5f), new Vector2(0.75f, 1.5f));
            context.AddToCurrentEntrySequence(overshootNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
            _overshootOutCheckBox.SetupCheck(overshootNode.FlowOut(Pointer_InterpolateNode.IdFlowOut));
            _overshootNoErrCheckBox.SetupNegateCheck(overshootNode.FlowOut(Pointer_InterpolateNode.IdFlowOutError));

            // ── Duration 0: t is NaN or >= 1 on the next tick -> target applied, [done] ──
            var zeroDurationIndex = nodeCreator.Context.exporter.GetTransformIndex(_zeroDurationTarget.transform);
            const float zeroDurationTargetY = 2f;
            context.NewEntryPoint(_zeroDurationDoneCheckBox.GetText(), 1f);
            var zeroDurationNode = CreateInterpolation(zeroDurationIndex, Template, GltfTypes.Float3,
                new Vector3(0f, zeroDurationTargetY, 0f), 0f, LinearP1, LinearP2);
            context.AddToCurrentEntrySequence(zeroDurationNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
            _zeroDurationDoneCheckBox.SetupCheck(out var zeroDurationDoneFlow);
            _zeroDurationValueCheckBox.proximityCheckDistance = 0.001f;
            _zeroDurationValueCheckBox.SetupCheck(TranslationY(zeroDurationIndex), out var zeroDurationValueFlow, zeroDurationTargetY, true);
            context.AddSequence(zeroDurationNode.FlowOut(Pointer_InterpolateNode.IdFlowOutDone), zeroDurationDoneFlow, zeroDurationValueFlow);

            // ── Re-trigger: the second interpolation of the same pointer replaces the first ──
            var retriggerIndex = nodeCreator.Context.exporter.GetTransformIndex(_retriggerTarget.transform);
            const float retriggerFirstY = 5f;
            const float retriggerSecondY = 2f;
            const float retriggerDuration = 1f;
            context.NewEntryPoint(_retriggerFirstDoneCheckBox.GetText(), retriggerDuration + 1f);
            var retriggerFirst = CreateInterpolation(retriggerIndex, Template, GltfTypes.Float3,
                new Vector3(0f, retriggerFirstY, 0f), retriggerDuration, LinearP1, LinearP2);
            var retriggerSecond = CreateInterpolation(retriggerIndex, Template, GltfTypes.Float3,
                new Vector3(0f, retriggerSecondY, 0f), retriggerDuration, LinearP1, LinearP2);
            context.AddToCurrentEntrySequence(
                retriggerFirst.FlowIn(Pointer_InterpolateNode.IdFlowIn),
                retriggerSecond.FlowIn(Pointer_InterpolateNode.IdFlowIn));
            _retriggerFirstDoneCheckBox.SetupNegateCheck(retriggerFirst.FlowOut(Pointer_InterpolateNode.IdFlowOutDone));
            _retriggerSecondDoneCheckBox.SetupCheck(out var retriggerSecondDoneFlow);
            _retriggerValueCheckBox.proximityCheckDistance = 0.001f;
            _retriggerValueCheckBox.SetupCheck(TranslationY(retriggerIndex), out var retriggerValueFlow, retriggerSecondY, true);
            context.AddSequence(retriggerSecond.FlowOut(Pointer_InterpolateNode.IdFlowOutDone), retriggerSecondDoneFlow, retriggerValueFlow);

            // ── Quaternion pointers MUST use slerp ─────────────────────────────────
            // Linear easing, sampled at 25% of the duration. The check tolerance allows about 4 degrees
            // (~100 ms of timing error); the nlerp result is about 5.5 degrees off, so it fails.
            var rotationIndex = nodeCreator.Context.exporter.GetTransformIndex(_rotationTarget.transform);
            context.NewEntryPoint(_rotationSlerpCheckBox.GetText(), RotationDuration + 0.5f);
            var rotationNode = CreateInterpolation(rotationIndex, RotationTemplate, GltfTypes.Float4, RotationTarget,
                RotationDuration, LinearP1, LinearP2);
            var rotationDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            rotationDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(RotationDuration * RotationSampleFraction);
            context.AddToCurrentEntrySequence(
                rotationNode.FlowIn(Pointer_InterpolateNode.IdFlowIn),
                rotationDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            var rotationGet = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(rotationGet, PointersHelper.IdPointerNodeIndex, RotationTemplate, GltfTypes.Float4);
            rotationGet.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(rotationIndex);

            var expectedRotation = Quaternion.Slerp(Quaternion.identity, RotationTarget, RotationSampleFraction);
            _rotationSlerpCheckBox.quaternionSignAgnostic = true;
            _rotationSlerpCheckBox.proximityCheckDistance = 0.0006f;
            _rotationSlerpCheckBox.SetupCheck(rotationGet.ValueOut(Pointer_GetNode.IdValue), out var rotationCheckFlow, expectedRotation, true);
            rotationDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(rotationCheckFlow);
        }

        public void Dispose()
        {
            if (_target != null)
                Object.DestroyImmediate(_target);
            if (_zeroDurationTarget != null)
                Object.DestroyImmediate(_zeroDurationTarget);
            if (_retriggerTarget != null)
                Object.DestroyImmediate(_retriggerTarget);
            if (_rotationTarget != null)
                Object.DestroyImmediate(_rotationTarget);
            if (_overshootTarget != null)
                Object.DestroyImmediate(_overshootTarget);
        }
    }
}
