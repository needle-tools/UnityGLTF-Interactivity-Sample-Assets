using System;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;
using static Khronos_Test_Export.UndefinedAnimationHelper;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Spec gap: two notions of an invalid animation. animation/start activates [err] if the animation "is invalid as
    /// determined by the implementation"; minTime, maxTime and playhead MUST be NaN if it is invalid "as defined in the
    /// core glTF 2.0 specification". Whether a core-invalid animation can still be started is open.
    ///
    /// The animation's sampler input accessor has no min/max, which core glTF requires for animation inputs:
    ///  A: animation/start rejects it ([err])
    ///  B: animation/start plays it ([out]), the pose reaches the last key
    /// maxTime NaN is the spec-defined reference. A loader that rejects the whole asset is a result too (nothing runs).
    /// </summary>
    [UndefinedBehaviourTest]
    public class UndefinedAnimationCoreInvalidTest : ITestCase, IDisposable
    {
        private AnimatedTestObject _target;
        private CheckBox _maxTimeNaNCheckBox;
        private CheckBox _errCheckBox;
        private CheckBox _outCheckBox;
        private CheckBox _playsCheckBox;

        public string GetTestName() => "animation/core invalid animation";

        public string GetTestDescription() =>
            "animation/start fails for animations invalid \"as determined by the implementation\", minTime/maxTime/playhead are NaN for animations invalid by core glTF. " +
            "Here the sampler input accessor has no min/max (required by core glTF). A: start activates [err]. B: start activates [out] and the animation plays.";

        public void PrepareObjects(TestContext context)
        {
            _target = new AnimatedTestObject(context.Root, "UndefinedCoreInvalidTarget", TargetPosition, T);
            _maxTimeNaNCheckBox = context.AddCheckBox("maxTime NaN\n(spec reference)");
            context.NewRow();
            _errCheckBox = context.AddCheckBox("A: start [err]", true);
            _outCheckBox = context.AddCheckBox("B: start [out]", true);
            _playsCheckBox = context.AddCheckBox("B: plays:\npose at T on [done]", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var animationIndex = _target.AnimationIndex(context);

            // Remove min/max of the sampler input accessors
            var root = nodeCreator.Context.exporter.GetRoot();
            foreach (var sampler in root.Animations[animationIndex].Samplers)
            {
                var input = root.Accessors[sampler.Input.Id];
                input.Min = null;
                input.Max = null;
            }

            var start = AnimationTestHelper.CreateStart(nodeCreator, animationIndex, 0f, T);
            context.NewEntryPoint(_maxTimeNaNCheckBox.GetText(), T + 1f);
            _maxTimeNaNCheckBox.SetupCheck(IsNaN(context, State(context, animationIndex, "maxTime", GltfTypes.Float)), out var maxTimeFlow, true);
            context.AddToCurrentEntrySequence(maxTimeFlow, start.FlowIn(Animation_StartNode.IdFlowIn));

            _errCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowError));
            _outCheckBox.SetupCheck(start.FlowOut(Animation_StartNode.IdFlowOut));
            _playsCheckBox.proximityCheckDistance = 0.01f;
            _playsCheckBox.SetupCheck(AnimationTestHelper.CreateTranslationY(context.interactivityExportContext, _target.NodeIndex(context)),
                start.FlowOut(Animation_StartNode.IdFlowDone), TargetPosition.y, true);
        }

        public void Dispose()
        {
            _target?.Destroy();
            _target = null;
        }
    }
}
