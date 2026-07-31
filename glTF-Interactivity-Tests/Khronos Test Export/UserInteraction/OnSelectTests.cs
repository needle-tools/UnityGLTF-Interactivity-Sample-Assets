using System;
using System.Collections.Generic;
using GLTF.Schema;
using TMPro;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Manual (user-interaction) test for the KHR_node_selectability extension and the
    /// event/onSelect node.
    ///
    /// Spec: https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_node_selectability
    ///
    /// The tester has to select (click/tap/point-and-trigger) the cubes while the test is
    /// running. All checks are evaluated once the interaction time window elapses:
    ///   - selecting the SELECTABLE cube must fire event/onSelect and report the correct
    ///     selectedNode ref, a valid controllerIndex and finite selectionPoint / selectionRayOrigin.
    ///   - the NON-SELECTABLE cube (selectable = false) must NOT fire event/onSelect.
    ///   - a cube whose ancestor is non-selectable must NOT fire either (selectability is
    ///     inherited: "a node is selectable if and only if there is no node at or above it in
    ///     the hierarchy with selectable = false").
    /// </summary>
    public class OnSelectTests : ITestCase, IUserInteractionTestCase, IDisposable
    {
        // The tester has this many seconds to perform the selections before the result is evaluated.
        private const float InteractionTimeWindow = 20f;

        private GameObject _selectableObj;
        private GameObject _nonSelectableObj;
        private GameObject _nonSelectableParent;
        private GameObject _childOfNonSelectable;

        private CheckBox _flowFiredCheckBox;
        private CheckBox _selectedNodeCheckBox;
        private CheckBox _controllerIndexCheckBox;
        private CheckBox _rayOriginFiniteCheckBox;
        private CheckBox _nonSelectableCheckBox;
        private CheckBox _inheritedCheckBox;

        private TextMeshPro _instructionLabel;
        private GameObject _arrow;
        private CheckBox _selectMarker;

        // Node indices resolved during CreateNodes, reused by GetRequiredInteractions().
        private int _selectableIndex;
        private int _nonSelectableIndex;
        private int _childOfNonSelectableIndex;

        public string GetTestName()
        {
            return "UserInteractions/eventOnSelect";
        }

        public string GetTestDescription()
        {
            return "KHR_node_selectability / event/onSelect. Select the LEFT cube (must fire event/onSelect). " +
                   "Do NOT select the MIDDLE cube (selectable=false) or the small cube on top of the RIGHT cube " +
                   "(inherited selectable=false) - no interaction with them is required, they only fail if you " +
                   "select them anyway.";
        }

        public void PrepareObjects(TestContext context)
        {
            _selectableObj = CreateCube("OnSelect_Selectable", context.Root, new Vector3(0f, 8f, 0f));
            _nonSelectableObj = CreateCube("OnSelect_NonSelectable", context.Root, new Vector3(10f, 8f, 0f));

            // Non-selectable parent with a (nominally selectable) child to verify inheritance.
            _nonSelectableParent = CreateCube("OnSelect_NonSelectableParent", context.Root, new Vector3(20f, 8f, 0f));
            _childOfNonSelectable = CreateCube("OnSelect_ChildOfNonSelectable", _nonSelectableParent.transform, new Vector3(0f, 3f, 0f));

            // Instruction label, placed above the interaction cubes. Hidden once the tester has
            // successfully selected the correct cube, so it's clear the interaction is done.
            _instructionLabel = context.AddLabel(
                "Select the cube under the arrow\n(click, tap, or point-and-trigger it).\nDo NOT select the other cubes.",
                new Vector3(10f, 18f, 0f));

            // Points straight at the cube the tester needs to select, and disappears once they do.
            _arrow = context.AddPointerArrow(new Vector3(0f, 11f, 0f), Color.cyan);

            // A small checkmark right on the cube itself, so success is visible where it happens -
            // not just in the checkbox list below.
            _selectMarker = context.AddObjectMarker(new Vector3(0f, 9.6f, 0f));

            _flowFiredCheckBox = context.AddCheckBox("onSelect: flow fired", asWaiting: true);
            _selectedNodeCheckBox = context.AddCheckBox("onSelect: selectedNode == target", asWaiting: true);
            _controllerIndexCheckBox = context.AddCheckBox("onSelect: controllerIndex >= 0", asWaiting: true);
            _rayOriginFiniteCheckBox = context.AddCheckBox("onSelect: selectionRayOrigin finite", asWaiting: true);

            // These show a checkmark (success) instantly and only flip to a failure mark if the
            // forbidden event actually fires - instead of showing a misleading fail state for the
            // whole interaction window while nothing has happened yet.
            _nonSelectableCheckBox = context.AddCheckBox("onSelect: selectable=false NOT fired");
            _nonSelectableCheckBox.Negate();
            _inheritedCheckBox = context.AddCheckBox("onSelect: inherited selectable=false NOT fired");
            _inheritedCheckBox.Negate();
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;

            var selectableIndex = _selectableIndex = exporter.GetTransformIndex(_selectableObj.transform);
            var nonSelectableIndex = _nonSelectableIndex = exporter.GetTransformIndex(_nonSelectableObj.transform);
            var parentIndex = exporter.GetTransformIndex(_nonSelectableParent.transform);
            var childIndex = _childOfNonSelectableIndex = exporter.GetTransformIndex(_childOfNonSelectable.transform);

            // Mark the negative-case nodes as non-selectable in the exported glTF.
            SetSelectable(context, nonSelectableIndex, false);
            SetSelectable(context, parentIndex, false);

            // A single onStart entry point anchors the timing: all checkboxes evaluate their
            // result after InteractionTimeWindow seconds. requiresUserInteraction tells the
            // viewer that the tester must interact during that window.
            context.NewEntryPoint("Select the cubes", InteractionTimeWindow, requiresUserInteraction: true);

            // ── Positive case: selecting the selectable cube ─────────────────────────
            var onSelect = nodeCreator.CreateNode<Event_OnSelectNode>();
            onSelect.Configuration[Event_OnSelectNode.IdConfigNodeIndex].Value = selectableIndex;

            _flowFiredCheckBox.SetupCheck(out var firedFlow);

            _selectedNodeCheckBox.SetupCheck(
                onSelect.ValueOut(Event_OnSelectNode.IdValueSelectedNodeRef),
                out var selectedNodeFlow,
                new StaticRefPointer("/nodes/"+selectableIndex));

            // controllerIndex >= 0
            var ge = nodeCreator.CreateNode<Math_GeNode>();
            ge.ValueIn(Math_GeNode.IdValueA).ConnectToSource(onSelect.ValueOut(Event_OnSelectNode.IdValueControllerIndex));
            ge.ValueIn(Math_GeNode.IdValueB).SetValue(0);
            _controllerIndexCheckBox.SetupCheck(ge.ValueOut(Math_GeNode.IdOut), out var controllerFlow, true);

            // selectionRayOrigin must be finite (not NaN). isNaN is scalar, so test each component.
            var rayNaN = AnyComponentIsNaN(nodeCreator, onSelect.ValueOut(Event_OnSelectNode.IdValueSelectionRayOrigin));
            _rayOriginFiniteCheckBox.SetupCheck(rayNaN, out var rayFiniteFlow, false);

            context.HideOnFlow(_instructionLabel.transform, out var hideLabelFlow);
            context.HideOnFlow(_arrow.transform, out var hideArrowFlow);
            _selectMarker.SetupCheck(out var markerFlow);

            // The negative-case checks already pass instantly (see .Negate() above), so once this
            // single onSelect event has fired, every check in this test is resolved - no need to
            // wait out the rest of the interaction window before reporting the result.
            context.AddEarlyCompletionTrigger(out var earlyCompletionFlow);

            context.AddSequence(onSelect.FlowOut(Event_OnSelectNode.IdFlowOut),
                firedFlow, selectedNodeFlow, controllerFlow, rayFiniteFlow, hideLabelFlow, hideArrowFlow, markerFlow,
                earlyCompletionFlow);

            // ── Negative case: non-selectable cube must never fire onSelect ──────────
            // Negated check: shows a checkmark immediately and only flips to failed the instant
            // onSelect actually fires, instead of waiting for the interaction window to end.
            var onSelectNonSelectable = nodeCreator.CreateNode<Event_OnSelectNode>();
            onSelectNonSelectable.Configuration[Event_OnSelectNode.IdConfigNodeIndex].Value = nonSelectableIndex;
            _nonSelectableCheckBox.SetupNegateCheck(out var nonSelectableFiredFlow);
            onSelectNonSelectable.FlowOut(Event_OnSelectNode.IdFlowOut).ConnectToFlowDestination(nonSelectableFiredFlow);

            // ── Negative case: inheritance - child of a non-selectable parent ────────
            var onSelectChild = nodeCreator.CreateNode<Event_OnSelectNode>();
            onSelectChild.Configuration[Event_OnSelectNode.IdConfigNodeIndex].Value = childIndex;
            _inheritedCheckBox.SetupNegateCheck(out var inheritedFiredFlow);
            onSelectChild.FlowOut(Event_OnSelectNode.IdFlowOut).ConnectToFlowDestination(inheritedFiredFlow);
        }

        /// <summary>
        /// Builds a bool that is true if any component of a float3 is NaN (via math/isNaN on
        /// each extracted component, OR-ed together).
        /// </summary>
        private static ValueOutRef AnyComponentIsNaN(GltfInteractivityExportNodes nodeCreator, ValueOutRef float3Value)
        {
            var extract = nodeCreator.CreateNode<Math_Extract3Node>();
            extract.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(float3Value);

            ValueOutRef anyNaN = null;
            for (int i = 0; i < 3; i++)
            {
                var isNaN = nodeCreator.CreateNode<Math_IsNaNNode>();
                isNaN.ValueIn(Math_IsNaNNode.IdValueA).ConnectToSource(extract.ValueOut(i.ToString()));
                if (anyNaN == null)
                {
                    anyNaN = isNaN.ValueOut(Math_IsNaNNode.IdOut);
                }
                else
                {
                    var or = nodeCreator.CreateNode<Math_OrNode>();
                    or.ValueIn(Math_OrNode.IdValueA).ConnectToSource(anyNaN);
                    or.ValueIn(Math_OrNode.IdValueB).ConnectToSource(isNaN.ValueOut(Math_IsNaNNode.IdOut));
                    anyNaN = or.ValueOut(Math_OrNode.IdOut);
                }
            }

            return anyNaN;
        }

        /// <summary>
        /// Structured description of the synthetic input(s) an automated runner must perform for
        /// this test to be meaningful: it cannot be derived from the graph alone since selecting is
        /// an external, engine-driven gesture.
        /// </summary>
        public IEnumerable<RequiredInteraction> GetRequiredInteractions()
        {
            yield return new RequiredInteraction
            {
                type = "select",
                expectation = "mustFire",
                targetNodeId = _selectableIndex,
                targetNodeName = _selectableObj.name,
                notes = "Select (click/tap/point-and-trigger) this node - event/onSelect must fire for it."
            };
            yield return new RequiredInteraction
            {
                type = "select",
                expectation = "mustNotFire",
                targetNodeId = _nonSelectableIndex,
                targetNodeName = _nonSelectableObj.name,
                notes = "selectable=false on this node - event/onSelect must never fire for it."
            };
            yield return new RequiredInteraction
            {
                type = "select",
                expectation = "mustNotFire",
                targetNodeId = _childOfNonSelectableIndex,
                targetNodeName = _childOfNonSelectable.name,
                notes = "Inherits selectable=false from its parent - event/onSelect must never fire for it."
            };
        }

        private static void SetSelectable(TestContext context, int nodeIndex, bool selectable)
        {
            context.interactivityExportContext.Context.AddSelectabilityExtensionToNode(nodeIndex);
            var extension = (KHR_node_selectability)context.interactivityExportContext.Context
                .ActiveGltfRoot.Nodes[nodeIndex].Extensions[KHR_node_selectability_Factory.EXTENSION_NAME];
            extension.selectable = selectable;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * 2f;
            return go;
        }

        public void Dispose()
        {
            if (_selectableObj != null) Object.DestroyImmediate(_selectableObj);
            if (_nonSelectableObj != null) Object.DestroyImmediate(_nonSelectableObj);
            if (_nonSelectableParent != null) Object.DestroyImmediate(_nonSelectableParent);
        }
    }
}
