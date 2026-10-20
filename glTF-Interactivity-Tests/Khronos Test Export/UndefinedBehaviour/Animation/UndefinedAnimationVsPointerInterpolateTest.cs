using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: a property is undefined while "two or more active animations" target it, and pointer/set during an
    /// animation is explained, but a pointer/interpolate on a property an active animation drives is not covered.
    ///
    /// The animation moves Y 0 -> 2 over T; at the same time a linear pointer/interpolate moves the translation to
    /// Y -4 over T. Read at 0.5T and after both ended:
    ///  A: the animation wins (Y 1 at 0.5T, Y 2 at the end)
    ///  B: the interpolation wins (Y -2 at 0.5T, Y -4 at the end)
    /// Other values (e.g. alternating writes) fail both and are visible in the result variables.
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationVsPointerInterpolateTest : ITestCase, IDisposable
    {
        private const float Tolerance = 0.2f;
        private static readonly Vector3 InterpolateTarget = new Vector3(0f, -4f, 0f);

        private AnimatedTestObject _target;
        private CheckBox _midAnimationCheckBox, _midInterpolateCheckBox;
        private CheckBox _endAnimationCheckBox, _endInterpolateCheckBox;

        public string GetTestName() => "animation/vs pointer interpolate";

        public string GetTestDescription() =>
            "An animation (Y 0 -> 2) and a pointer/interpolate (Y 0 -> -4) drive the same translation over the same time. " +
            "A: the animation wins. B: the interpolation wins. Read at half time and after both ended.";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedVsInterpolateTarget", TargetPosition, T);
            _midAnimationCheckBox = context.AddCheckBox("A: at 0.5T:\nanimation pose (Y 1)", true);
            _midInterpolateCheckBox = context.AddCheckBox("B: at 0.5T:\ninterpolated (Y -2)", true);
            _endAnimationCheckBox = context.AddCheckBox("A: after end:\nanimation pose (Y 2)", true);
            _endInterpolateCheckBox = context.AddCheckBox("B: after end:\ninterpolated (Y -4)", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var nodeIndex = _target.NodeIndex(context);

            var start = AnimationTestHelper.CreateStart(nodeCreator, _target.AnimationIndex(context), 0f, T);

            var interpolate = nodeCreator.CreateNode<Pointer_InterpolateNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(interpolate, PointersHelper.IdPointerNodeIndex,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation", GltfTypes.Float3);
            interpolate.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);
            interpolate.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(InterpolateTarget);
            interpolate.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(T);
            // Control points on the diagonal: linear easing
            interpolate.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(new Vector2(0.25f, 0.25f));
            interpolate.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(new Vector2(0.75f, 0.75f));

            var midDelay = AnimationTestHelper.CreateDelay(nodeCreator, 0.5f * T);
            var endDelay = AnimationTestHelper.CreateDelay(nodeCreator, T + 0.5f);
            context.NewEntryPoint(_midAnimationCheckBox.GetText(), T + 1f);
            context.AddToCurrentEntrySequence(
                start.FlowIn(Animation_StartNode.IdFlowIn),
                interpolate.FlowIn(Pointer_InterpolateNode.IdFlowIn),
                midDelay.FlowIn(Flow_SetDelayNode.IdFlowIn),
                endDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            var y = AnimationTestHelper.CreateTranslationY(nodeCreator, nodeIndex);
            _midAnimationCheckBox.proximityCheckDistance = Tolerance;
            _midAnimationCheckBox.SetupCheck(y, out var midAnimationFlow, 0.5f * TargetPosition.y, true);
            _midInterpolateCheckBox.proximityCheckDistance = Tolerance;
            _midInterpolateCheckBox.SetupCheck(y, out var midInterpolateFlow, 0.5f * InterpolateTarget.y, true);
            context.AddSequence(midDelay.FlowOut(Flow_SetDelayNode.IdFlowDone), midAnimationFlow, midInterpolateFlow);

            _endAnimationCheckBox.proximityCheckDistance = Tolerance;
            _endAnimationCheckBox.SetupCheck(y, out var endAnimationFlow, TargetPosition.y, true);
            _endInterpolateCheckBox.proximityCheckDistance = Tolerance;
            _endInterpolateCheckBox.SetupCheck(y, out var endInterpolateFlow, InterpolateTarget.y, true);
            context.AddSequence(endDelay.FlowOut(Flow_SetDelayNode.IdFlowDone), endAnimationFlow, endInterpolateFlow);
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
