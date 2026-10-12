using System;
using TMPro;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// The read-only /extensions/KHR_interactivity/activeCamera/... pointers.
    ///
    /// The values depend on the viewer's camera, so no exact values are checked. Per pointer:
    ///  - pointer/get isValid is true
    ///  - two pointer/get nodes read in the same flow return the same value (NaN counts as equal to NaN)
    ///  - pointer/set fires [err], and a pointer/get afterwards doesn't return the set value
    /// Sanity checks, assuming the viewer renders with an active camera (without one, the spec returns NaN
    /// for all values, so these checks fail):
    ///  - position and rotation contain no NaN, rotation has unit length
    ///  - the camera is either perspective or orthographic: exactly one of perspective/yfov and orthographic/xmag is NaN
    ///  - perspective: 0 &lt; yfov &lt; pi, aspectRatio &gt; 0, 0 &lt; znear &lt; zfar (zfar may be infinite)
    ///  - orthographic: xmag != 0, ymag != 0, 0 &lt;= znear &lt; zfar
    /// pointer/interpolate on activeCamera/position fires [err]. [out] never fires for any of the pointer/set
    /// or pointer/interpolate nodes, [done] never fires for the pointer/interpolate node.
    ///
    /// Visual sign only (not a checkbox, a flipped rotation is still a valid unit quaternion): a red error plate is
    /// placed every tick at activeCamera position + rotation * (0, 0, +d). glTF cameras look along -Z, so with a correct
    /// rotation the plate is behind the camera. It's only visible when the rotation is turned by 180 degrees
    /// (camera looking along +Z), e.g. a left-handed engine that doesn't flip the camera's look direction.
    /// </summary>
    public class ActiveCameraPointersTest : ITestCase, IDisposable
    {
        private const string Prefix = "/extensions/KHR_interactivity/activeCamera/";

        private class CameraPointer
        {
            public string name;
            public string gltfType;
            // Written by pointer/set; implausible for a real camera, so reading it back means the set wasn't rejected
            public object setValue;

            public CheckBox isValidCheckBox;
            public CheckBox consistentCheckBox;
            public CheckBox setErrCheckBox;
            public CheckBox unchangedCheckBox;

            public string Pointer => Prefix + name;
        }

        private readonly CameraPointer[] _pointers =
        {
            new CameraPointer { name = "position", gltfType = GltfTypes.Float3, setValue = new Vector3(1234.5f, -2345.5f, 3456.5f) },
            new CameraPointer { name = "rotation", gltfType = GltfTypes.Float4, setValue = new Quaternion(0.1f, 0.2f, 0.3f, 4f) },
            new CameraPointer { name = "perspective/aspectRatio", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "perspective/yfov", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "perspective/znear", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "perspective/zfar", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "orthographic/xmag", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "orthographic/ymag", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "orthographic/znear", gltfType = GltfTypes.Float, setValue = 12345.5f },
            new CameraPointer { name = "orthographic/zfar", gltfType = GltfTypes.Float, setValue = 12345.5f },
        };

        private CheckBox _positionNoNaNCheckBox;
        private CheckBox _rotationNoNaNCheckBox;
        private CheckBox _rotationUnitLengthCheckBox;
        private CheckBox _oneProjectionCheckBox;
        private CheckBox _perspectiveValuesCheckBox;
        private CheckBox _orthographicValuesCheckBox;

        private CheckBox _interpolateErrCheckBox;
        private CheckBox _noOutCheckBox;

        private const float InterpolationDuration = 0.1f;

        private const float FlippedPlateDistance = 4f;
        private const float FlippedPlateScale = 0.5f;
        private GameObject _flippedPlate;
        private Material _flippedPlateMaterial;
        private TextMeshPro _flippedPlateLabel; // owned (and destroyed) by the TestContext

        public string GetTestName()
        {
            return "pointer/activeCamera";
        }

        public string GetTestDescription()
        {
            return "Read-only activeCamera pointers: pointer/get isValid, two reads in the same flow return the same value, " +
                   "pointer/set and pointer/interpolate fire [err] and don't change the value, and sanity checks of the values " +
                   "(assumes the viewer renders with an active camera, otherwise all values are NaN).";
        }

        public void PrepareObjects(TestContext context)
        {
            foreach (var p in _pointers)
            {
                p.isValidCheckBox = context.AddCheckBox(p.name + "\nisValid");
                p.consistentCheckBox = context.AddCheckBox(p.name + "\ntwo reads equal");
                p.setErrCheckBox = context.AddCheckBox(p.name + "\npointer/set [err]");
                p.unchangedCheckBox = context.AddCheckBox(p.name + "\nunchanged after set");
                context.NewRow();
            }

            _positionNoNaNCheckBox = context.AddCheckBox("position\nhas no NaN");
            _rotationNoNaNCheckBox = context.AddCheckBox("rotation\nhas no NaN");
            _rotationUnitLengthCheckBox = context.AddCheckBox("rotation\nhas unit length");
            context.NewRow();
            _oneProjectionCheckBox = context.AddCheckBox("either perspective\nor orthographic");
            _perspectiveValuesCheckBox = context.AddCheckBox("perspective values\nvalid (if perspective)");
            _orthographicValuesCheckBox = context.AddCheckBox("orthographic values\nvalid (if orthographic)");
            context.NewRow();
            _interpolateErrCheckBox = context.AddCheckBox("position\npointer/interpolate [err]");
            _noOutCheckBox = context.AddCheckBox("No [out]/[done] on\nset and interpolate");
            _noOutCheckBox.Negate();

            CreateFlippedPlate(context);
        }

        private void CreateFlippedPlate(TestContext context)
        {
            // Hidden (scale 0) until the graph places it
            _flippedPlate = new GameObject("ActiveCamera_RotationFlippedError");
            _flippedPlate.transform.SetParent(context.Root, false);
            _flippedPlate.transform.localScale = Vector3.zero;

            _flippedPlateMaterial = new Material(Shader.Find("UnityGLTF/PBRGraph"));
            _flippedPlateMaterial.name = "ActiveCameraRotationFlipped";
            _flippedPlateMaterial.SetColor("baseColorFactor", new Color(0.85f, 0.1f, 0.1f));
            _flippedPlateMaterial.SetColor("emissiveFactor", new Color(0.6f, 0.05f, 0.05f));

            // Quad and text face local -Z, i.e. the camera when the plate gets the camera's (flipped) rotation
            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Background";
            UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());
            background.GetComponent<MeshRenderer>().sharedMaterial = _flippedPlateMaterial;
            background.transform.SetParent(_flippedPlate.transform, false);
            background.transform.localScale = new Vector3(3.4f, 0.55f, 1f);

            var label = _flippedPlateLabel = context.AddLabel("ERROR: activeCamera/rotation is flipped by 180°\nthe view direction must be the rotated -Z axis, not +Z", Vector3.zero);
            label.transform.SetParent(_flippedPlate.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            label.transform.localRotation = Quaternion.identity;
            label.transform.localScale = Vector3.one;
            label.enableAutoSizing = false;
            label.fontSize = 1f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(3.3f, 0.5f);
        }

        /// <summary>
        /// Every tick: plate pose = activeCamera matrix * translation (0, 0, +d), converted to the local space of the
        /// plate's parent.
        /// </summary>
        private void CreateFlippedPlateNodes(TestContext context, Func<string, string, ValueOutRef> getCameraPointer)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;

            var cameraMatrix = nodeCreator.CreateNode<Math_MatComposeNode>();
            cameraMatrix.ValueIn(Math_MatComposeNode.IdInputTranslation).ConnectToSource(getCameraPointer("position", GltfTypes.Float3));
            cameraMatrix.ValueIn(Math_MatComposeNode.IdInputRotation).ConnectToSource(getCameraPointer("rotation", GltfTypes.Float4));
            cameraMatrix.ValueIn(Math_MatComposeNode.IdInputScale).SetValue(Vector3.one);

            var offset = nodeCreator.CreateNode<Math_MatComposeNode>();
            offset.ValueIn(Math_MatComposeNode.IdInputTranslation).SetValue(new Vector3(0f, 0f, FlippedPlateDistance));
            offset.ValueIn(Math_MatComposeNode.IdInputRotation).SetValue(Quaternion.identity);
            offset.ValueIn(Math_MatComposeNode.IdInputScale).SetValue(Vector3.one);

            var world = nodeCreator.CreateNode<Math_MatMulNode>();
            world.ValueIn(Math_MatMulNode.IdValueA).ConnectToSource(cameraMatrix.FirstValueOut());
            world.ValueIn(Math_MatMulNode.IdValueB).ConnectToSource(offset.FirstValueOut());
            var localMatrix = world.FirstValueOut();

            var parentIndex = exporter.GetTransformIndex(context.Root);
            if (parentIndex >= 0)
            {
                var parentMatrix = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.AddPointerConfig(parentMatrix, $"/nodes/{parentIndex}/globalMatrix", GltfTypes.Float4x4);
                var inverse = nodeCreator.CreateNode<Math_InverseNode>();
                inverse.ValueIn(Math_InverseNode.IdValueA).ConnectToSource(parentMatrix.ValueOut(Pointer_GetNode.IdValue));
                var toLocal = nodeCreator.CreateNode<Math_MatMulNode>();
                toLocal.ValueIn(Math_MatMulNode.IdValueA).ConnectToSource(inverse.FirstValueOut());
                toLocal.ValueIn(Math_MatMulNode.IdValueB).ConnectToSource(world.FirstValueOut());
                localMatrix = toLocal.FirstValueOut();
            }

            var decompose = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            decompose.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(localMatrix);

            var plateIndex = exporter.GetTransformIndex(_flippedPlate.transform);
            GltfInteractivityExportNode Set(string property, string type, ValueOutRef value, object constant = null)
            {
                var setNode = nodeCreator.CreateNode<Pointer_SetNode>();
                PointersHelper.AddPointerConfig(setNode, $"/nodes/{plateIndex}/{property}", type);
                if (value != null)
                    setNode.ValueIn(Pointer_SetNode.IdValue).ConnectToSource(value);
                else
                    setNode.ValueIn(Pointer_SetNode.IdValue).SetValue(constant);
                return setNode;
            }

            var setTranslation = Set("translation", GltfTypes.Float3, decompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation));
            var setRotation = Set("rotation", GltfTypes.Float4, decompose.ValueOut(Math_MatDecomposeNode.IdOutputRotation));
            var setScale = Set("scale", GltfTypes.Float3, null, Vector3.one * FlippedPlateScale);

            var onTick = nodeCreator.CreateNode<Event_OnTickNode>();
            onTick.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(setTranslation.FlowIn(Pointer_SetNode.IdFlowIn));
            setTranslation.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(setRotation.FlowIn(Pointer_SetNode.IdFlowIn));
            setRotation.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(setScale.FlowIn(Pointer_SetNode.IdFlowIn));
        }

        public void Dispose()
        {
            if (_flippedPlateLabel != null)
                _flippedPlateLabel.transform.SetParent(null, false);
            if (_flippedPlate != null)
                UnityEngine.Object.DestroyImmediate(_flippedPlate);
            if (_flippedPlateMaterial != null)
                UnityEngine.Object.DestroyImmediate(_flippedPlateMaterial);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            ValueOutRef Get(CameraPointer p)
            {
                var getNode = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.AddPointerConfig(getNode, p.Pointer, p.gltfType);
                return getNode.ValueOut(Pointer_GetNode.IdValue).ExpectedType(ExpectedType.GtlfType(p.gltfType));
            }

            ValueOutRef Op<TSchema>(ValueOutRef a, object b = null) where TSchema : GltfInteractivityNodeSchema, new()
            {
                var node = nodeCreator.CreateNode<TSchema>();
                node.ValueIn("a").ConnectToSource(a);
                if (b is ValueOutRef bRef)
                    node.ValueIn("b").ConnectToSource(bRef);
                else if (b != null)
                    node.ValueIn("b").SetValue(b);
                return node.FirstValueOut();
            }

            ValueOutRef And(params ValueOutRef[] values)
            {
                var result = values[0];
                for (int i = 1; i < values.Length; i++)
                    result = Op<Math_AndNode>(result, values[i]);
                return result;
            }

            ValueOutRef Or(params ValueOutRef[] values)
            {
                var result = values[0];
                for (int i = 1; i < values.Length; i++)
                    result = Op<Math_OrNode>(result, values[i]);
                return result;
            }

            ValueOutRef IsNaN(ValueOutRef value) => Op<Math_IsNaNNode>(value);

            ValueOutRef AnyComponentNaN<TExtract>(ValueOutRef vector, int count) where TExtract : GltfInteractivityNodeSchema, new()
            {
                var extract = nodeCreator.CreateNode<TExtract>();
                extract.ValueIn("a").ConnectToSource(vector);
                var components = new ValueOutRef[count];
                for (int i = 0; i < count; i++)
                    components[i] = IsNaN(extract.ValueOut(i.ToString()));
                return Or(components);
            }

            // ── [out] of set/interpolate and [done] of interpolate must never fire ──
            // Own entry point, delayed so the set/interpolate cases (and a wrongly accepted interpolation) run first
            context.NewEntryPoint(_noOutCheckBox.GetText(), InterpolationDuration + 0.4f);
            _noOutCheckBox.SetupNegateCheck(out var unexpectedOutFlowIn);

            // ── Per pointer: isValid, consistency, read-only ───────────────────────
            foreach (var p in _pointers)
            {
                context.NewEntryPoint(p.isValidCheckBox.GetText());

                var getNode = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.AddPointerConfig(getNode, p.Pointer, p.gltfType);
                p.isValidCheckBox.SetupCheck(getNode.ValueOut(Pointer_GetNode.IdIsValid), out var isValidFlow, true);

                // Two separate pointer/get nodes, read in the same flow
                var readA = Get(p);
                var readB = Get(p);
                var equal = Op<Math_EqNode>(readA, readB);
                // math/eq is false for NaN, which is the expected value of the projection pointers that don't
                // match the camera type. math/isNaN only takes floats, so this only applies to the scalar pointers.
                if (p.gltfType == GltfTypes.Float)
                    equal = Or(equal, And(IsNaN(readA), IsNaN(readB)));
                p.consistentCheckBox.SetupCheck(equal, out var consistentFlow, true);

                var setNode = nodeCreator.CreateNode<Pointer_SetNode>();
                PointersHelper.AddPointerConfig(setNode, p.Pointer, p.gltfType);
                setNode.ValueIn(Pointer_SetNode.IdValue).SetValue(p.setValue);
                p.setErrCheckBox.SetupCheck(setNode.FlowOut(Pointer_SetNode.IdFlowOutError));
                setNode.FlowOut(Pointer_SetNode.IdFlowOut).ConnectToFlowDestination(unexpectedOutFlowIn);

                var isSetValue = Op<Math_EqNode>(Get(p), p.setValue);
                p.unchangedCheckBox.SetupCheck(isSetValue, out var unchangedFlow, false);

                context.AddToCurrentEntrySequence(isValidFlow, consistentFlow,
                    setNode.FlowIn(Pointer_SetNode.IdFlowIn), unchangedFlow);
            }

            CameraPointer Find(string name) => Array.Find(_pointers, p => p.name == name);

            // ── Sanity checks of the values ────────────────────────────────────────
            context.NewEntryPoint(_positionNoNaNCheckBox.GetText());

            var position = Get(Find("position"));
            var rotation = Get(Find("rotation"));

            _positionNoNaNCheckBox.SetupCheck(Op<Math_NotNode>(AnyComponentNaN<Math_Extract3Node>(position, 3)), out var positionNoNaNFlow, true);
            _rotationNoNaNCheckBox.SetupCheck(Op<Math_NotNode>(AnyComponentNaN<Math_Extract4Node>(rotation, 4)), out var rotationNoNaNFlow, true);

            var rotationLengthError = Op<Math_AbsNode>(Op<Math_SubNode>(Op<Math_LengthNode>(rotation), 1f));
            _rotationUnitLengthCheckBox.SetupCheck(Op<Math_LtNode>(rotationLengthError, 0.001f), out var rotationUnitLengthFlow, true);

            var aspectRatio = Get(Find("perspective/aspectRatio"));
            var yfov = Get(Find("perspective/yfov"));
            var perspectiveZNear = Get(Find("perspective/znear"));
            var perspectiveZFar = Get(Find("perspective/zfar"));
            var xmag = Get(Find("orthographic/xmag"));
            var ymag = Get(Find("orthographic/ymag"));
            var orthographicZNear = Get(Find("orthographic/znear"));
            var orthographicZFar = Get(Find("orthographic/zfar"));

            var notPerspective = IsNaN(yfov);
            var notOrthographic = IsNaN(xmag);
            _oneProjectionCheckBox.SetupCheck(Op<Math_XorNode>(notPerspective, notOrthographic), out var oneProjectionFlow, true);

            // Comparisons with NaN are false, so a NaN value of the matching projection type fails these checks
            var perspectiveValid = And(
                Op<Math_GtNode>(yfov, 0f),
                Op<Math_LtNode>(yfov, Mathf.PI),
                Op<Math_GtNode>(aspectRatio, 0f),
                Op<Math_GtNode>(perspectiveZNear, 0f),
                Op<Math_GtNode>(perspectiveZFar, perspectiveZNear));
            _perspectiveValuesCheckBox.SetupCheck(Or(notPerspective, perspectiveValid), out var perspectiveValuesFlow, true);

            var orthographicValid = And(
                Op<Math_NotNode>(Op<Math_EqNode>(xmag, 0f)),
                Op<Math_NotNode>(Op<Math_EqNode>(ymag, 0f)),
                Op<Math_GeNode>(orthographicZNear, 0f),
                Op<Math_GtNode>(orthographicZFar, orthographicZNear));
            _orthographicValuesCheckBox.SetupCheck(Or(notOrthographic, orthographicValid), out var orthographicValuesFlow, true);

            context.AddToCurrentEntrySequence(positionNoNaNFlow, rotationNoNaNFlow, rotationUnitLengthFlow,
                oneProjectionFlow, perspectiveValuesFlow, orthographicValuesFlow);

            // ── pointer/interpolate on a read-only pointer ─────────────────────────
            context.NewEntryPoint(_interpolateErrCheckBox.GetText());
            var interpolateNode = nodeCreator.CreateNode<Pointer_InterpolateNode>();
            PointersHelper.AddPointerConfig(interpolateNode, Find("position").Pointer, GltfTypes.Float3);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdValue).SetValue(Find("position").setValue);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdDuration).SetValue(InterpolationDuration);
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint1).SetValue(new Vector2(0.25f, 0.25f));
            interpolateNode.ValueIn(Pointer_InterpolateNode.IdPoint2).SetValue(new Vector2(0.75f, 0.75f));
            context.AddToCurrentEntrySequence(interpolateNode.FlowIn(Pointer_InterpolateNode.IdFlowIn));
            _interpolateErrCheckBox.SetupCheck(interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOutError));
            interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOut).ConnectToFlowDestination(unexpectedOutFlowIn);
            interpolateNode.FlowOut(Pointer_InterpolateNode.IdFlowOutDone).ConnectToFlowDestination(unexpectedOutFlowIn);

            // ── Visual sign for a flipped camera rotation ──────────────────────────
            CreateFlippedPlateNodes(context, (name, type) =>
            {
                var getNode = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.AddPointerConfig(getNode, Prefix + name, type);
                return getNode.ValueOut(Pointer_GetNode.IdValue).ExpectedType(ExpectedType.GtlfType(type));
            });
        }
    }
}
