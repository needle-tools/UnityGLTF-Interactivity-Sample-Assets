using System;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Pointers that only exist for some objects of an array: the spot pointers of KHR_lights_punctual
    /// (/extensions/KHR_lights_punctual/lights/{}/spot/innerConeAngle and outerConeAngle) only exist for spot lights.
    ///
    /// The lights are exported as point, spot, directional, point, so there are point lights with a lower and a higher
    /// index than the spot light (implementations that register pointers per array instead of per object may resolve
    /// the spot pointers of every light once any spot light exists).
    ///
    /// Checks:
    ///  - pointer/get of spot/outerConeAngle and spot/innerConeAngle on point and directional lights: isValid false
    ///    (by index and by ref)
    ///  - pointer/get of spot/outerConeAngle on the spot light: isValid true and the exported value
    ///  - pointer/set of spot/outerConeAngle on a point light: [err], no [out]
    /// </summary>
    public class PointerLightTypePointersTest : ITestCase, IDisposable
    {
        private const float SpotAngle = 60f;

        private readonly List<GameObject> _lightObjects = new List<GameObject>();
        private Light _pointBefore, _spot, _directional, _pointAfter;

        private readonly List<(CheckBox checkBox, Light light, string property, bool byRef)> _isValidFalseChecks = new();
        private CheckBox _spotIsValidCheckBox;
        private CheckBox _spotValueCheckBox;
        private CheckBox _setErrCheckBox;
        private CheckBox _setNoOutCheckBox;

        private const string OuterConeAngle = "spot/outerConeAngle";
        private const string InnerConeAngle = "spot/innerConeAngle";

        public string GetTestName()
        {
            return "pointer/get light type pointers";
        }

        public string GetTestDescription()
        {
            return "The spot pointers of KHR_lights_punctual only exist for spot lights: pointer/get on point and directional lights returns isValid false, pointer/set triggers [err].";
        }

        private Light CreateLight(TestContext context, string name, LightType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(context.Root);
            go.transform.localPosition = Vector3.zero;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.intensity = 0.01f;
            if (type == LightType.Spot)
                light.spotAngle = SpotAngle;
            _lightObjects.Add(go);
            return light;
        }

        public void PrepareObjects(TestContext context)
        {
            _isValidFalseChecks.Clear();

            // export order: point, spot, directional, point
            _pointBefore = CreateLight(context, "LightTypePointers_Point_0", LightType.Point);
            _spot = CreateLight(context, "LightTypePointers_Spot", LightType.Spot);
            _directional = CreateLight(context, "LightTypePointers_Directional", LightType.Directional);
            _pointAfter = CreateLight(context, "LightTypePointers_Point_1", LightType.Point);

            void AddIsValidFalse(string label, Light light, string property, bool byRef)
            {
                var checkBox = context.AddCheckBox($"{label}\n{property}\nisValid false");
                _isValidFalseChecks.Add((checkBox, light, property, byRef));
            }

            AddIsValidFalse("point (before spot)", _pointBefore, OuterConeAngle, false);
            AddIsValidFalse("point (before spot)", _pointBefore, InnerConeAngle, false);
            AddIsValidFalse("point (after spot)", _pointAfter, OuterConeAngle, false);
            AddIsValidFalse("point (after spot)", _pointAfter, InnerConeAngle, false);
            context.NewRow();
            AddIsValidFalse("directional", _directional, OuterConeAngle, false);
            AddIsValidFalse("directional", _directional, InnerConeAngle, false);
            AddIsValidFalse("point (before spot)\nby ref", _pointBefore, OuterConeAngle, true);
            AddIsValidFalse("directional\nby ref", _directional, OuterConeAngle, true);
            context.NewRow();
            _spotIsValidCheckBox = context.AddCheckBox($"spot\n{OuterConeAngle}\nisValid true");
            _spotValueCheckBox = context.AddCheckBox($"spot\n{OuterConeAngle}\nvalue");
            _setErrCheckBox = context.AddCheckBox($"set point\n{OuterConeAngle}\n[err]");
            _setNoOutCheckBox = context.AddCheckBox($"set point\n{OuterConeAngle}\nno [out]");
            _setNoOutCheckBox.Negate();
        }

        private static GltfInteractivityExportNode CreateLightPointerGet(GltfInteractivityExportNodes nodeCreator, int lightIndex, string property, bool byRef)
        {
            var getNode = nodeCreator.CreateNode<Pointer_GetNode>();
            if (byRef)
            {
                PointersHelper.SetupPointerTemplateAndTargetInput(getNode, PointersHelper.IdPointerLightRef,
                    PointersHelper.IdPointerTemplLightByRef + "/" + property, GltfTypes.Float);
                getNode.ValueIn(PointersHelper.IdPointerLightRef).SetValue(new StaticRefPointer($"/extensions/KHR_lights_punctual/lights/{lightIndex}"));
            }
            else
            {
                PointersHelper.SetupPointerTemplateAndTargetInput(getNode, PointersHelper.IdPointerLightIndex,
                    PointersHelper.IdPointerTemplLightByIndex + "/" + property, GltfTypes.Float);
                getNode.ValueIn(PointersHelper.IdPointerLightIndex).SetValue(lightIndex);
            }
            return getNode;
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;

            foreach (var (checkBox, light, property, byRef) in _isValidFalseChecks)
            {
                context.NewEntryPoint(checkBox.GetText());
                var getNode = CreateLightPointerGet(nodeCreator, exporter.GetLightIndex(light), property, byRef);
                checkBox.SetupCheck(getNode.ValueOut(Pointer_GetNode.IdIsValid), out var checkFlow, false);
                context.AddToCurrentEntrySequence(checkFlow);
            }

            // ── the spot light has the spot pointers ─────────────────────────────
            var spotIndex = exporter.GetLightIndex(_spot);
            var exportedOuterConeAngle = ((GLTFSpotLight)exporter.GetRoot().Lights[spotIndex]).outerConeAngle;
            context.NewEntryPoint(_spotIsValidCheckBox.GetText());
            var getSpot = CreateLightPointerGet(nodeCreator, spotIndex, OuterConeAngle, false);
            _spotIsValidCheckBox.SetupCheck(getSpot.ValueOut(Pointer_GetNode.IdIsValid), out var spotIsValidFlow, true);
            _spotValueCheckBox.proximityCheckDistance = 0.0001f;
            _spotValueCheckBox.SetupCheck(getSpot.ValueOut(Pointer_GetNode.IdValue), out var spotValueFlow, exportedOuterConeAngle, true);
            context.AddToCurrentEntrySequence(spotIsValidFlow, spotValueFlow);

            // ── pointer/set of a spot pointer on a point light ─────────────────────
            context.NewEntryPoint(_setErrCheckBox.GetText());
            var setNode = nodeCreator.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setNode, PointersHelper.IdPointerLightIndex,
                PointersHelper.IdPointerTemplLightByIndex + "/" + OuterConeAngle, GltfTypes.Float);
            setNode.ValueIn(PointersHelper.IdPointerLightIndex).SetValue(exporter.GetLightIndex(_pointBefore));
            setNode.ValueIn(Pointer_SetNode.IdValue).SetValue(0.3f);
            context.AddToCurrentEntrySequence(setNode.FlowIn(Pointer_SetNode.IdFlowIn));
            _setErrCheckBox.SetupCheck(setNode.FlowOut(Pointer_SetNode.IdFlowOutError));
            _setNoOutCheckBox.SetupNegateCheck(setNode.FlowOut(Pointer_SetNode.IdFlowOut));
        }

        public void Dispose()
        {
            foreach (var go in _lightObjects)
                if (go != null)
                    Object.DestroyImmediate(go);
            _lightObjects.Clear();
            _isValidFalseChecks.Clear();
        }
    }
}
