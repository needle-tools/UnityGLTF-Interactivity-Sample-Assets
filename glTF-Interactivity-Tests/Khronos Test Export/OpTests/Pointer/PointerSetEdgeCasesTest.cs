using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Error and edge cases of pointer/set (and the matching pointer/get isValid cases).
    ///
    /// Checks:
    ///  - [err] for a negative template index, an out-of-range index, a type mismatch between the
    ///    declared type and the property, a read-only property and /nodes/{}/weights; [out] never fires for these
    ///  - a value that is invalid for the property (metallicFactor 2) is not an error: [out] fires, [err] doesn't
    ///  - pointer/set on a property with a running pointer/interpolate removes the interpolation:
    ///    its [done] never fires and the property keeps the set value
    ///  - pointer/get with a negative or out-of-range index returns isValid false
    /// </summary>
    public class PointerSetEdgeCasesTest : ITestCase, IDisposable
    {
        private GameObject _target;
        private GameObject _interpolationTarget;
        private GameObject _materialCube;
        private Material _material;

        private CheckBox _errNegativeIndexCheckBox;
        private CheckBox _errOutOfRangeCheckBox;
        private CheckBox _errTypeMismatchCheckBox;
        private CheckBox _errReadOnlyCheckBox;
        private CheckBox _errWeightsCheckBox;
        private CheckBox _noOutOnErrorCheckBox;

        private CheckBox _invalidValueOutCheckBox;
        private CheckBox _invalidValueNoErrCheckBox;

        private CheckBox _interpolationDoneNotFiredCheckBox;
        private CheckBox _interpolationStoppedValueCheckBox;

        private CheckBox _getNegativeIndexCheckBox;
        private CheckBox _getOutOfRangeCheckBox;

        private static string TranslationTemplate =>
            "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation";

        private const float InterpolationDuration = 1f;
        private const float InterpolationTargetY = 5f;
        private const float SetValueY = 1f;

        public string GetTestName()
        {
            return "pointer/set edge cases";
        }

        public string GetTestDescription()
        {
            return "pointer/set error flows (invalid index, type mismatch, read-only, weights), invalid values, stopping a running interpolation, and pointer/get isValid for invalid indices.";
        }

        public void PrepareObjects(TestContext context)
        {
            _target = new GameObject("PointerSetEdgeCasesTarget");
            _target.transform.SetParent(context.Root);
            _target.transform.localPosition = Vector3.zero;
            _target.transform.localScale = Vector3.one * 0.0001f;

            _interpolationTarget = new GameObject("PointerSetStopsInterpolationTarget");
            _interpolationTarget.transform.SetParent(context.Root);
            _interpolationTarget.transform.localPosition = Vector3.zero;
            _interpolationTarget.transform.localScale = Vector3.one * 0.0001f;

            _material = new Material(Shader.Find("UnityGLTF/PBRGraph"));
            _material.name = "PointerSetInvalidValueMaterial";
            _materialCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _materialCube.name = "PointerSetInvalidValueCube";
            _materialCube.transform.SetParent(context.Root);
            _materialCube.transform.localPosition = Vector3.zero;
            _materialCube.transform.localScale = Vector3.zero;
            _materialCube.GetComponent<MeshRenderer>().sharedMaterial = _material;

            _errNegativeIndexCheckBox = context.AddCheckBox("[err] index -1");
            _errOutOfRangeCheckBox = context.AddCheckBox("[err] index out of range");
            _errTypeMismatchCheckBox = context.AddCheckBox("[err] type mismatch\n(float on translation)");
            _errReadOnlyCheckBox = context.AddCheckBox("[err] read-only\n(/nodes.length)");
            _errWeightsCheckBox = context.AddCheckBox("[err] /nodes/{}/weights");
            _noOutOnErrorCheckBox = context.AddCheckBox("No [out] on\nthe [err] cases");
            _noOutOnErrorCheckBox.Negate();
            context.NewRow();
            _invalidValueOutCheckBox = context.AddCheckBox("Invalid value\n(metallic 2): [out]");
            _invalidValueNoErrCheckBox = context.AddCheckBox("Invalid value\n(metallic 2): no [err]");
            _invalidValueNoErrCheckBox.Negate();
            _interpolationDoneNotFiredCheckBox = context.AddCheckBox("set stops interpolate:\n[done] not fired", true);
            _interpolationDoneNotFiredCheckBox.Negate();
            _interpolationStoppedValueCheckBox = context.AddCheckBox("set stops interpolate:\nkeeps set value", true);
            context.NewRow();
            _getNegativeIndexCheckBox = context.AddCheckBox("pointer/get index -1:\nisValid false");
            _getOutOfRangeCheckBox = context.AddCheckBox("pointer/get index out\nof range: isValid false");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;
            var nodeIndex = exporter.GetTransformIndex(_target.transform);
            var outOfRangeIndex = exporter.GetRoot().Nodes.Count + 100;

            // ── [err] cases ────────────────────────────────────────────────────────
            // The [out] flows of all error cases below feed this one negated check. Its own entry point
            // only evaluates it; the delay lets the error cases run first.
            context.NewEntryPoint(_noOutOnErrorCheckBox.GetText(), 0.2f);
            _noOutOnErrorCheckBox.SetupNegateCheck(out var unexpectedOutFlowIn);

            // The exporter's Validator reports a literal nodeIndex of -1 as an export error, since that's
            // what GetTransformIndex returns for an object that wasn't exported. Here -1 is intended, so it
            // comes from a variable instead of a literal (the Validator only checks unconnected sockets).
            void SetNodeIndex(GltfInteractivityExportNode node, int index)
            {
                if (index != -1)
                {
                    node.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(index);
                    return;
                }
                var negativeIndexVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("NegativeNodeIndex_" + Guid.NewGuid(), -1, GltfTypes.Int);
                VariablesHelpers.GetVariable(nodeCreator, negativeIndexVarId, out var negativeIndex);
                node.ValueIn(PointersHelper.IdPointerNodeIndex).ConnectToSource(negativeIndex);
            }

            void AddErrorCheck(CheckBox checkBox, string template, int type, object value, int? index)
            {
                context.NewEntryPoint(checkBox.GetText());
                var setNode = nodeCreator.CreateNode<Pointer_SetNode>();
                if (index.HasValue)
                {
                    PointersHelper.SetupPointerTemplateAndTargetInput(setNode, PointersHelper.IdPointerNodeIndex, template, type);
                    SetNodeIndex(setNode, index.Value);
                }
                else
                    PointersHelper.AddPointerConfig(setNode, template, type);
                setNode.ValueIn(Pointer_SetNode.IdValue).SetValue(value);
                context.AddToCurrentEntrySequence(setNode.FlowIn(Pointer_SetNode.IdFlowIn));
                checkBox.SetupCheck(setNode.FlowOut(Pointer_SetNode.IdFlowOutError));
                setNode.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(unexpectedOutFlowIn);
            }

            var float3Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float3);
            var floatType = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float);
            var intType = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Int);

            AddErrorCheck(_errNegativeIndexCheckBox, TranslationTemplate, float3Type, Vector3.one, -1);
            AddErrorCheck(_errOutOfRangeCheckBox, TranslationTemplate, float3Type, Vector3.one, outOfRangeIndex);
            AddErrorCheck(_errTypeMismatchCheckBox, TranslationTemplate, floatType, 1f, nodeIndex);
            AddErrorCheck(_errReadOnlyCheckBox, "/nodes.length", intType, 1, null);
            // weights has the float[] type, which the extension doesn't support: always [err]
            AddErrorCheck(_errWeightsCheckBox, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/weights", floatType, 1f, nodeIndex);

            // ── Invalid value for the property: not an error ───────────────────────
            var materialIndex = exporter.GetMaterialIndex(_material);
            context.NewEntryPoint(_invalidValueOutCheckBox.GetText());
            var setMetallic = nodeCreator.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setMetallic, PointersHelper.IdPointerMaterialIndex,
                "/materials/[" + PointersHelper.IdPointerMaterialIndex + "]/pbrMetallicRoughness/metallicFactor", GltfTypes.Float);
            setMetallic.ValueIn(PointersHelper.IdPointerMaterialIndex).SetValue(materialIndex);
            setMetallic.ValueIn(Pointer_SetNode.IdValue).SetValue(2f);
            context.AddToCurrentEntrySequence(setMetallic.FlowIn(Pointer_SetNode.IdFlowIn));
            _invalidValueOutCheckBox.SetupCheck(setMetallic.FlowOut(Pointer_SetNode.IdFlowOut));
            _invalidValueNoErrCheckBox.SetupNegateCheck(setMetallic.FlowOut(Pointer_SetNode.IdFlowOutError));

            // ── pointer/set removes a running interpolation of the same property ───
            var interpolationNodeIndex = exporter.GetTransformIndex(_interpolationTarget.transform);
            context.NewEntryPoint(_interpolationDoneNotFiredCheckBox.GetText(), InterpolationDuration + 1f);

            var interpolateNode = nodeCreator.CreateNode<Pointer_InterpolateNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(interpolateNode, PointersHelper.IdPointerNodeIndex, TranslationTemplate, GltfTypes.Float3);
            interpolateNode.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(interpolationNodeIndex);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(new Vector3(0f, InterpolationTargetY, 0f));
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(InterpolationDuration);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(new Vector2(0.25f, 0.25f));
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(new Vector2(0.75f, 0.75f));

            var setTranslation = nodeCreator.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setTranslation, PointersHelper.IdPointerNodeIndex, TranslationTemplate, GltfTypes.Float3);
            setTranslation.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(interpolationNodeIndex);
            setTranslation.ValueIn(Pointer_SetNode.IdValue).SetValue(new Vector3(0f, SetValueY, 0f));

            var getTranslation = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(getTranslation, PointersHelper.IdPointerNodeIndex, TranslationTemplate, GltfTypes.Float3);
            getTranslation.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(interpolationNodeIndex);
            var extractY = nodeCreator.CreateNode<Math_Extract3Node>();
            extractY.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(getTranslation.ValueOut(Pointer_GetNode.IdValue));

            // Read back after the interpolation would have finished
            var checkDelay = nodeCreator.CreateNode<Flow_SetDelayNode>();
            checkDelay.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(InterpolationDuration + 0.5f);

            context.AddToCurrentEntrySequence(
                interpolateNode.FlowIn(Pointer_InterpolateNode.IdFlowIn),
                setTranslation.FlowIn(Pointer_SetNode.IdFlowIn),
                checkDelay.FlowIn(Flow_SetDelayNode.IdFlowIn));

            _interpolationDoneNotFiredCheckBox.SetupNegateCheck(interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOutDone));
            _interpolationStoppedValueCheckBox.proximityCheckDistance = 0.001f;
            _interpolationStoppedValueCheckBox.SetupCheck(extractY.ValueOut(Math_Extract3Node.IdValueOutY), out var stoppedValueFlowIn, SetValueY, true);
            checkDelay.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(stoppedValueFlowIn);

            // ── pointer/get isValid for invalid indices ────────────────────────────
            void AddGetInvalidCheck(CheckBox checkBox, int index)
            {
                context.NewEntryPoint(checkBox.GetText());
                var getNode = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.SetupPointerTemplateAndTargetInput(getNode, PointersHelper.IdPointerNodeIndex, TranslationTemplate, GltfTypes.Float3);
                SetNodeIndex(getNode, index);
                checkBox.SetupCheck(getNode.ValueOut(Pointer_GetNode.IdIsValid), out var checkFlow, false);
                context.AddToCurrentEntrySequence(checkFlow);
            }
            AddGetInvalidCheck(_getNegativeIndexCheckBox, -1);
            AddGetInvalidCheck(_getOutOfRangeCheckBox, outOfRangeIndex);
        }

        public void Dispose()
        {
            if (_target != null)
                Object.DestroyImmediate(_target);
            if (_interpolationTarget != null)
                Object.DestroyImmediate(_interpolationTarget);
            if (_materialCube != null)
                Object.DestroyImmediate(_materialCube);
            if (_material != null)
                Object.DestroyImmediate(_material);
        }
    }
}
