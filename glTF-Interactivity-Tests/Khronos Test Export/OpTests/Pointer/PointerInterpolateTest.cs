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
    ///  - [err] fires for invalid inputs: negative duration, infinite duration, NaN p1, NaN p2
    ///
    /// The object starts at the origin so the start value is (0,0,0) in glTF space, which makes the
    /// expected values independent of Unity->glTF coordinate conversion.
    /// </summary>
    public class PointerInterpolateTest : ITestCase, IDisposable
    {
        private GameObject _target;

        private CheckBox _outOrderCheckBox;
        private CheckBox _valueAt50CheckBox;
        private CheckBox _flowDoneCheckBox;
        private CheckBox _valueAt100CheckBox;

        private CheckBox _errDurationNegCheckBox;
        private CheckBox _errDurationInfCheckBox;
        private CheckBox _errP1CheckBox;
        private CheckBox _errP2CheckBox;

        private static string Template =>
            "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation";

        private static readonly Vector3 StartPosition = Vector3.zero;
        private static readonly Vector3 TargetPosition = new Vector3(2f, 3f, 4f);
        private static readonly Vector2 P1 = new Vector2(1f, 1f);
        private static readonly Vector2 P2 = new Vector2(1f, 1f);
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
        }

        public void Dispose()
        {
            if (_target != null)
                Object.DestroyImmediate(_target);
        }
    }
}
