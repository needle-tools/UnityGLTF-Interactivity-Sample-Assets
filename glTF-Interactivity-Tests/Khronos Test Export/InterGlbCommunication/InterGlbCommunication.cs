using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export.InterGlbCommunication
{
    // Shared event/argument IDs used by both File A and File B
    internal static class InterGlbRefEchoEvents
    {
        // ── Original Ref-echo channel ────────────────────────────────────────────────
        // File A sends this event with a mesh Ref argument; File B receives it
        public const string RequestEventId  = "test/request";
        // File B sends this event back with the same Ref argument; File A receives it
        public const string ResponseEventId = "test/response";

        // ── Engine-event forward channel ─────────────────────────────────────────────
        // The "engine" (simulated by File A at start-up) sends this to File A with a Ref
        public const string EngineEventId          = "test/engineRefEvent";
        // File A forwards the Ref from the engine event to File B via this inter-GLB event
        public const string EngineForwardEventId   = "test/engineRefForward";
        // File B sends the Ref back to the engine via this event; File A verifies it
        public const string EngineCallbackEventId  = "test/engineRefCallback";

        // Name of the Ref argument carried by all events
        public const string RefArgName      = "meshRef";
    }

    /// <summary>
    /// File A: On start, gets a mesh Ref via pointer/get, sends it to File B via a custom event,
    /// then receives the echoed Ref from File B via another event and checks with ref/eq
    /// that the received Ref is identical to the original one.
    /// </summary>
    public class InterGlbCommunication : ITestCase, IDisposable
    {
        private CheckBox _refEchoCheckbox;
        private CheckBox _engineRefForwardCheckbox;
        private GameObject _meshObject;

        public string GetTestName() => "InterGlb/RefEcho_FileA";

        public string GetTestDescription() =>
            "Sends a mesh Ref to File B via a custom event. " +
            "File B echoes it back. File A checks with ref/eq that the ref is unchanged.";

        public void PrepareObjects(TestContext context)
        {
            _refEchoCheckbox          = context.AddCheckBox("Echoed Ref equals original", true);
            _engineRefForwardCheckbox = context.AddCheckBox("Engine Ref forwarded via File B", true);

            _meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _meshObject.name = "InterGlbRefEchoMesh";
            _meshObject.transform.SetParent(context.Root);
            // Hide it far away so it does not clutter the test scene visually
            _meshObject.transform.localPosition = new Vector3(0f, -999f, 0f);
            _meshObject.transform.localScale    = Vector3.zero;
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            int nodeIndex   = nodeCreator.Context.exporter.GetTransformIndex(_meshObject.transform);

            // Register the request event (sent by File A, received by File B)
            var requestEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer()
                    }
                }
            };
            var requestEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.RequestEventId, requestEventArgs);

            // Register the response event (sent by File B, received by File A)
            var responseEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer()
                    }
                }
            };
            var responseEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.ResponseEventId, responseEventArgs);

            // ── On-start: pointer/get mesh Ref → event/send request ──────────────────

            // Get the mesh Ref from this file's scene node
            var getMeshRef = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(getMeshRef,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/mesh", GltfTypes.Ref);
            getMeshRef.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            // Send the request event carrying the mesh Ref
            var sendRequestNode = nodeCreator.CreateNode<Event_SendNode>();
            sendRequestNode.Configuration[Event_SendNode.IdEvent].Value = requestEventId;
            sendRequestNode.ValueIn(InterGlbRefEchoEvents.RefArgName)
                .ConnectToSource(getMeshRef.ValueOut(Pointer_GetNode.IdValue));

            // Entry point: on start, send the request to File B
            // Delay of 2 s gives enough time for the event round-trip before the fallback check
            context.NewEntryPoint("Send mesh Ref to File B", 2f);
            context.AddToCurrentEntrySequence(sendRequestNode.FlowIn());

            // ── Event receive: compare echoed Ref with a fresh pointer/get ────────────

            // Listen for the response event from File B
            var receiveResponseNode = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveResponseNode.Configuration[Event_ReceiveNode.IdEventConfig].Value = responseEventId;

            // Get the mesh Ref again (for comparison – reads the same node in this scene)
            var getMeshRefForCompare = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(getMeshRefForCompare,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/mesh", GltfTypes.Ref);
            getMeshRefForCompare.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            // ref/eq: echoed Ref == local mesh Ref ?
            var refEqNode = nodeCreator.CreateNode<Ref_EqNode>();
            refEqNode.ValueIn(Ref_EqNode.IdValueA)
                .ConnectToSource(receiveResponseNode.ValueOut(InterGlbRefEchoEvents.RefArgName));
            refEqNode.ValueIn(Ref_EqNode.IdValueB)
                .ConnectToSource(getMeshRefForCompare.ValueOut(Pointer_GetNode.IdValue));

            // Checkbox: succeed if ref/eq is true; checked at the 2-second fallback
            _refEchoCheckbox.SetupCheck(refEqNode.FirstValueOut(), out var checkFlowIn, true);
            receiveResponseNode.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(checkFlowIn);

            // ══════════════════════════════════════════════════════════════════════════
            // Second test: Engine event → File A → File B → back to Engine
            //
            // Timeline (all near t=0):
            //   1. Start → File A sends "engineEventId" with mesh Ref  (simulates engine trigger)
            //   2. File A's receive node fires → extracts Ref → sends "engineForwardEventId" to File B
            //   3. File B's receive node fires → sends "engineCallbackEventId" back (to engine/File A)
            //   4. File A's second receive node fires → ref/eq → checkbox
            //   5. At 2-second fallback: verify checkbox result
            // ══════════════════════════════════════════════════════════════════════════

            var refEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer("/meshes/0/")
                    }
                }
            };

            var engineEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.EngineEventId, refEventArgs);
            var engineForwardEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.EngineForwardEventId, refEventArgs);
            var engineCallbackEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.EngineCallbackEventId, refEventArgs);

            // ── Step 1: On start, "the engine" fires engineEventId with the mesh Ref ──

            var getMeshRefForEngine = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(getMeshRefForEngine,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/mesh", GltfTypes.Ref);
            getMeshRefForEngine.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var sendEngineEvent = nodeCreator.CreateNode<Event_SendNode>();
            sendEngineEvent.Configuration[Event_SendNode.IdEvent].Value = engineEventId;
            sendEngineEvent.ValueIn(InterGlbRefEchoEvents.RefArgName)
                .ConnectToSource(getMeshRefForEngine.ValueOut(Pointer_GetNode.IdValue));

            // Reuse the existing entry point – engine event send happens on the same start node
            context.AddToCurrentEntrySequence(sendEngineEvent.FlowIn());

            // ── Step 2: File A receives the engine event → forwards Ref to File B ────

            var receiveEngineEvent = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveEngineEvent.Configuration[Event_ReceiveNode.IdEventConfig].Value = engineEventId;

            var sendForwardToB = nodeCreator.CreateNode<Event_SendNode>();
            sendForwardToB.Configuration[Event_SendNode.IdEvent].Value = engineForwardEventId;
            sendForwardToB.ValueIn(InterGlbRefEchoEvents.RefArgName)
                .ConnectToSource(receiveEngineEvent.ValueOut(InterGlbRefEchoEvents.RefArgName));

            receiveEngineEvent.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(sendForwardToB.FlowIn(Event_SendNode.IdFlowIn));

            // ── Step 4: File A receives File B's callback (engine receives result back) ─

            var receiveEngineCallback = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveEngineCallback.Configuration[Event_ReceiveNode.IdEventConfig].Value = engineCallbackEventId;

            // Compare the ref returned by File B against a fresh pointer/get (same mesh)
            var getMeshRefCallbackCompare = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(getMeshRefCallbackCompare,
                "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/mesh", GltfTypes.Ref);
            getMeshRefCallbackCompare.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqEngine = nodeCreator.CreateNode<Ref_EqNode>();
            refEqEngine.ValueIn(Ref_EqNode.IdValueA)
                .ConnectToSource(receiveEngineCallback.ValueOut(InterGlbRefEchoEvents.RefArgName));
            refEqEngine.ValueIn(Ref_EqNode.IdValueB)
                .ConnectToSource(getMeshRefCallbackCompare.ValueOut(Pointer_GetNode.IdValue));

            // Checkbox: succeed if the engine received back the original Ref unchanged
            _engineRefForwardCheckbox.SetupCheck(refEqEngine.FirstValueOut(), out var engineCheckFlowIn, true);
            receiveEngineCallback.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(engineCheckFlowIn);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_meshObject);
        }
    }

    /// <summary>
    /// File B: Receives the mesh Ref from File A via a custom event and immediately echoes
    /// it back unchanged via another custom event. Also marks its own checkbox when done.
    /// </summary>
    public class InterGlbCommunicationFileB : ITestCase
    {
        private CheckBox _receivedCheckbox;

        public string GetTestName() => "InterGlb/RefEcho_FileB";

        public string GetTestDescription() =>
            "Receives a mesh Ref from File A via a custom event and echoes it back via another event.";

        public void PrepareObjects(TestContext context)
        {
            _receivedCheckbox = context.AddCheckBox("Received and echoed Ref to File A", true);
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Register the request event (the one sent by File A)
            var requestEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer()
                    }
                }
            };
            var requestEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.RequestEventId, requestEventArgs);

            // Register the response event (sent back to File A)
            var responseEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer()
                    }
                }
            };
            var responseEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.ResponseEventId, responseEventArgs);

            // ── event/receive request → event/send response ──────────────────────────

            // Listen for the request event from File A
            var receiveRequestNode = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveRequestNode.Configuration[Event_ReceiveNode.IdEventConfig].Value = requestEventId;

            // Echo the Ref back via the response event
            var sendResponseNode = nodeCreator.CreateNode<Event_SendNode>();
            sendResponseNode.Configuration[Event_SendNode.IdEvent].Value = responseEventId;
            sendResponseNode.ValueIn(InterGlbRefEchoEvents.RefArgName)
                .ConnectToSource(receiveRequestNode.ValueOut(InterGlbRefEchoEvents.RefArgName));

            // Flow: event received → send response
            receiveRequestNode.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(sendResponseNode.FlowIn(Event_SendNode.IdFlowIn));

            // ── Engine-forward channel: receive Ref from File A, send back to the engine ──

            var engineForwardEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>
            {
                {
                    InterGlbRefEchoEvents.RefArgName,
                    new GltfInteractivityNode.EventValues
                    {
                        Type  = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Ref),
                        Value = new StaticRefPointer()
                    }
                }
            };

            var engineForwardEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.EngineForwardEventId, engineForwardEventArgs);
            var engineCallbackEventId = nodeCreator.Context.AddEventWithIdIfNeeded(
                InterGlbRefEchoEvents.EngineCallbackEventId, engineForwardEventArgs);

            // Receive the engine-forwarded Ref from File A
            var receiveEngineForward = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveEngineForward.Configuration[Event_ReceiveNode.IdEventConfig].Value = engineForwardEventId;

            // Send the Ref back to the engine
            var sendEngineCallback = nodeCreator.CreateNode<Event_SendNode>();
            sendEngineCallback.Configuration[Event_SendNode.IdEvent].Value = engineCallbackEventId;
            sendEngineCallback.ValueIn(InterGlbRefEchoEvents.RefArgName)
                .ConnectToSource(receiveEngineForward.ValueOut(InterGlbRefEchoEvents.RefArgName));

            receiveEngineForward.FlowOut(Event_ReceiveNode.IdFlowOut)
                .ConnectToFlowDestination(sendEngineCallback.FlowIn(Event_SendNode.IdFlowIn));

            // ── Entry point (dummy start log + delayed fallback check) ────────────────

            // Minimal start action so the entry point has a flow connected
            context.AddLog("File B: waiting for Ref echo request from File A",
                out var startLogFlowIn, out _);

            // Delay of 3 s – gives File A time to send the request and for this file to respond
            context.NewEntryPoint("Echo Ref back to File A", 3f);
            context.AddToCurrentEntrySequence(startLogFlowIn);

            // Checkbox: triggered when the response event has been sent
            _receivedCheckbox.SetupCheck(out var checkFlow);
            sendResponseNode.FlowOut(Event_SendNode.IdFlowOut)
                .ConnectToFlowDestination(checkFlow);
        }
    }
}