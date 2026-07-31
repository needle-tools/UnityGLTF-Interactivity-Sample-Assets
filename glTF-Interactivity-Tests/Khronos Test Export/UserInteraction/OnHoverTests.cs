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
    /// Manual (user-interaction) test for the KHR_node_hoverability extension and the
    /// event/onHoverIn and event/onHoverOut nodes.
    ///
    /// Spec: https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_node_hoverability
    ///
    /// The tester has to hover the cubes (move the pointer/controller onto them and away again)
    /// while the test is running. Results are evaluated after the interaction time window:
    ///   - hovering the HOVERABLE cube must fire event/onHoverIn (on enter) and event/onHoverOut
    ///     (on leave), both reporting the correct hoveredNode ref and a valid controllerIndex.
    ///   - the NON-HOVERABLE cube (hoverable = false) must NOT fire event/onHoverIn.
    ///   - a cube whose ancestor is non-hoverable must NOT fire either (hoverability is inherited:
    ///     "a node is hoverable if and only if there is no node at or above it in the hierarchy
    ///     with hoverable = false").
    /// </summary>
    public class OnHoverTests : ITestCase, IUserInteractionTestCase, IDisposable
    {
        // The tester has this many seconds to hover the cubes before the result is evaluated.
        private const float InteractionTimeWindow = 20f;

        private GameObject _hoverableObj;
        private GameObject _nonHoverableObj;
        private GameObject _nonHoverableParent;
        private GameObject _childOfNonHoverable;

        private CheckBox _hoverInFiredCheckBox;
        private CheckBox _hoverInNodeCheckBox;
        private CheckBox _hoverInControllerCheckBox;
        private CheckBox _hoverOutFiredCheckBox;
        private CheckBox _hoverOutNodeCheckBox;
        private CheckBox _nonHoverableCheckBox;
        private CheckBox _inheritedCheckBox;

        private TextMeshPro _instructionLabel;
        private GameObject _arrow;
        private CheckBox _hoverMarker;

        // Node indices resolved during CreateNodes, reused by GetRequiredInteractions().
        private int _hoverableIndex;
        private int _nonHoverableIndex;
        private int _childOfNonHoverableIndex;

        public string GetTestName()
        {
            return "UserInteractions/eventOnHover";
        }

        public string GetTestDescription()
        {
            return "KHR_node_hoverability / event/onHoverIn + event/onHoverOut. Hover the LEFT cube (move onto it " +
                   "and away again - must fire). Do NOT hover the MIDDLE cube (hoverable=false) or the small cube on " +
                   "top of the RIGHT cube (inherited hoverable=false) - no interaction with them is required, they " +
                   "only fail if you hover them anyway.";
        }

        public void PrepareObjects(TestContext context)
        {
            _hoverableObj = CreateCube("OnHover_Hoverable", context.Root, new Vector3(0f, 8f, 0f));
            _nonHoverableObj = CreateCube("OnHover_NonHoverable", context.Root, new Vector3(10f, 8f, 0f));

            // Non-hoverable parent with a (nominally hoverable) child to verify inheritance.
            _nonHoverableParent = CreateCube("OnHover_NonHoverableParent", context.Root, new Vector3(20f, 8f, 0f));
            _childOfNonHoverable = CreateCube("OnHover_ChildOfNonHoverable", _nonHoverableParent.transform, new Vector3(0f, 3f, 0f));

            // Instruction label, placed above the interaction cubes. Hidden once the tester has
            // successfully hovered the correct cube, so it's clear the interaction is done.
            _instructionLabel = context.AddLabel(
                "Hover the cube under the arrow\n(move onto it, then away again).\nDo NOT hover the other cubes.",
                new Vector3(10f, 18f, 0f));

            // Points straight at the cube the tester needs to hover, and disappears once they do.
            _arrow = context.AddPointerArrow(new Vector3(0f, 11f, 0f), Color.yellow);

            // A small checkmark right on the cube itself, so success is visible where it happens -
            // not just in the checkbox list below.
            _hoverMarker = context.AddObjectMarker(new Vector3(0f, 9.6f, 0f));

            _hoverInFiredCheckBox = context.AddCheckBox("onHoverIn: flow fired", asWaiting: true);
            _hoverInNodeCheckBox = context.AddCheckBox("onHoverIn: hoveredNode == target", asWaiting: true);
            _hoverInControllerCheckBox = context.AddCheckBox("onHoverIn: controllerIndex >= 0", asWaiting: true);
            _hoverOutFiredCheckBox = context.AddCheckBox("onHoverOut: flow fired", asWaiting: true);
            _hoverOutNodeCheckBox = context.AddCheckBox("onHoverOut: hoveredNode == target", asWaiting: true);

            // These show a checkmark (success) instantly and only flip to a failure mark if the
            // forbidden event actually fires - instead of showing a misleading fail state for the
            // whole interaction window while nothing has happened yet.
            _nonHoverableCheckBox = context.AddCheckBox("onHoverIn: hoverable=false NOT fired");
            _nonHoverableCheckBox.Negate();
            _inheritedCheckBox = context.AddCheckBox("onHoverIn: inherited hoverable=false NOT fired");
            _inheritedCheckBox.Negate();
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            var exporter = nodeCreator.Context.exporter;

            var hoverableIndex = _hoverableIndex = exporter.GetTransformIndex(_hoverableObj.transform);
            var nonHoverableIndex = _nonHoverableIndex = exporter.GetTransformIndex(_nonHoverableObj.transform);
            var parentIndex = exporter.GetTransformIndex(_nonHoverableParent.transform);
            var childIndex = _childOfNonHoverableIndex = exporter.GetTransformIndex(_childOfNonHoverable.transform);

            // Mark the negative-case nodes as non-hoverable in the exported glTF.
            SetHoverable(context, nonHoverableIndex, false);
            SetHoverable(context, parentIndex, false);

            // A single onStart entry point anchors the timing: all checkboxes evaluate their
            // result after InteractionTimeWindow seconds. requiresUserInteraction tells the
            // viewer that the tester must interact during that window.
            context.NewEntryPoint("Hover the cubes", InteractionTimeWindow, requiresUserInteraction: true);

            // ── Positive case: hovering in on the hoverable cube ─────────────────────
            var onHoverIn = nodeCreator.CreateNode<Event_OnHoverInNode>();
            onHoverIn.Configuration[Event_OnHoverInNode.IdConfigNodeIndex].Value = hoverableIndex;

            _hoverInFiredCheckBox.SetupCheck(out var hoverInFiredFlow);

            _hoverInNodeCheckBox.SetupCheck(
                onHoverIn.ValueOut(Event_OnHoverInNode.IdOutHoverNodeRef),
                out var hoverInNodeFlow,
                new StaticRefPointer("/nodes/"+hoverableIndex));

            var ge = nodeCreator.CreateNode<Math_GeNode>();
            ge.ValueIn(Math_GeNode.IdValueA).ConnectToSource(onHoverIn.ValueOut(Event_OnHoverInNode.IdOutControllerIndex));
            ge.ValueIn(Math_GeNode.IdValueB).SetValue(0);
            _hoverInControllerCheckBox.SetupCheck(ge.ValueOut(Math_GeNode.IdOut), out var hoverInControllerFlow, true);

            context.HideOnFlow(_instructionLabel.transform, out var hideLabelFlow);
            context.HideOnFlow(_arrow.transform, out var hideArrowFlow);
            _hoverMarker.SetupCheck(out var markerFlow);

            context.AddSequence(onHoverIn.FlowOut(Event_OnHoverInNode.IdFlowOut),
                hoverInFiredFlow, hoverInNodeFlow, hoverInControllerFlow, hideLabelFlow, hideArrowFlow, markerFlow);

            // ── Positive case: hovering out on the hoverable cube ────────────────────
            var onHoverOut = nodeCreator.CreateNode<Event_OnHoverOutNode>();
            onHoverOut.Configuration[Event_OnHoverOutNode.IdConfigNodeIndex].Value = hoverableIndex;

            _hoverOutFiredCheckBox.SetupCheck(out var hoverOutFiredFlow);

            _hoverOutNodeCheckBox.SetupCheck(
                onHoverOut.ValueOut(Event_OnHoverOutNode.IdOutHoverNodeRef),
                out var hoverOutNodeFlow,
                new StaticRefPointer("/nodes/"+hoverableIndex));

            // The negative-case checks already pass instantly (see .Negate() above), so once the
            // tester has both entered and left the hoverable cube, every check in this test is
            // resolved - no need to wait out the rest of the interaction window to report the result.
            context.AddEarlyCompletionTrigger(out var earlyCompletionFlow);

            context.AddSequence(onHoverOut.FlowOut(Event_OnHoverOutNode.IdFlowOut),
                hoverOutFiredFlow, hoverOutNodeFlow, earlyCompletionFlow);

            // ── Negative case: non-hoverable cube must never fire onHoverIn ──────────
            // Negated check: shows a checkmark immediately and only flips to failed the instant
            // onHoverIn actually fires, instead of waiting for the interaction window to end.
            var onHoverInNonHoverable = nodeCreator.CreateNode<Event_OnHoverInNode>();
            onHoverInNonHoverable.Configuration[Event_OnHoverInNode.IdConfigNodeIndex].Value = nonHoverableIndex;
            _nonHoverableCheckBox.SetupNegateCheck(out var nonHoverableFiredFlow);
            onHoverInNonHoverable.FlowOut(Event_OnHoverInNode.IdFlowOut).ConnectToFlowDestination(nonHoverableFiredFlow);

            // ── Negative case: inheritance - child of a non-hoverable parent ─────────
            var onHoverInChild = nodeCreator.CreateNode<Event_OnHoverInNode>();
            onHoverInChild.Configuration[Event_OnHoverInNode.IdConfigNodeIndex].Value = childIndex;
            _inheritedCheckBox.SetupNegateCheck(out var inheritedFiredFlow);
            onHoverInChild.FlowOut(Event_OnHoverInNode.IdFlowOut).ConnectToFlowDestination(inheritedFiredFlow);
        }

        /// <summary>
        /// Structured description of the synthetic input(s) an automated runner must perform for
        /// this test to be meaningful: it cannot be derived from the graph alone since hovering is
        /// an external, engine-driven gesture.
        /// </summary>
        public IEnumerable<RequiredInteraction> GetRequiredInteractions()
        {
            yield return new RequiredInteraction
            {
                type = "hover",
                expectation = "mustFire",
                targetNodeId = _hoverableIndex,
                targetNodeName = _hoverableObj.name,
                notes = "Move the pointer/controller onto this node, then off it again - both " +
                        "event/onHoverIn and event/onHoverOut must fire."
            };
            yield return new RequiredInteraction
            {
                type = "hover",
                expectation = "mustNotFire",
                targetNodeId = _nonHoverableIndex,
                targetNodeName = _nonHoverableObj.name,
                notes = "hoverable=false on this node - event/onHoverIn must never fire for it."
            };
            yield return new RequiredInteraction
            {
                type = "hover",
                expectation = "mustNotFire",
                targetNodeId = _childOfNonHoverableIndex,
                targetNodeName = _childOfNonHoverable.name,
                notes = "Inherits hoverable=false from its parent - event/onHoverIn must never fire for it."
            };
        }

        private static void SetHoverable(TestContext context, int nodeIndex, bool hoverable)
        {
            context.interactivityExportContext.Context.AddHoverabilityExtensionToNode(nodeIndex);
            var extension = (KHR_node_hoverability)context.interactivityExportContext.Context
                .ActiveGltfRoot.Nodes[nodeIndex].Extensions[KHR_node_hoverability_Factory.EXTENSION_NAME];
            extension.hoverable = hoverable;
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
            if (_hoverableObj != null) Object.DestroyImmediate(_hoverableObj);
            if (_nonHoverableObj != null) Object.DestroyImmediate(_nonHoverableObj);
            if (_nonHoverableParent != null) Object.DestroyImmediate(_nonHoverableParent);
        }
    }
}
