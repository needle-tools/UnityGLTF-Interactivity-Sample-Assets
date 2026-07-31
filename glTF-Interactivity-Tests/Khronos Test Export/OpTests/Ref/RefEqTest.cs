using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class RefEqTest : ITestCase, IDisposable
    {
        private CheckBox _checkSameMeshRef;
        private CheckBox _checkTwoNulls;
        private CheckBox _checkOneMeshOneNull;
        private CheckBox _checkMeshOneNode;
        private CheckBox _checkMeshVsStaticMesh;
        private CheckBox _checkMeshVsStaticNode;
        private CheckBox _checkChildVsStaticChild;

        private GameObject _meshObject;
        private GameObject _childObject;

        public string GetTestName()
        {
            return "ref/eq";
        }

        public string GetTestDescription()
        {
            return "Tests the ref/eq node with different inputs.";
        }

        public void PrepareObjects(TestContext context)
        {
            _checkSameMeshRef = context.AddCheckBox("mesh == mesh (same)");
            _checkTwoNulls = context.AddCheckBox("null == null");
            _checkOneMeshOneNull = context.AddCheckBox("mesh == null");
            _checkMeshOneNode = context.AddCheckBox("mesh == node");
            _checkMeshVsStaticMesh = context.AddCheckBox("mesh (from node) == mesh (static value)");
            _checkMeshVsStaticNode = context.AddCheckBox("mesh (from node) == node (static value)");
            _checkChildVsStaticChild = context.AddCheckBox("child node (from node) == child node (static value)");

            _meshObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _meshObject.name = "RefEqTestMesh";
            _meshObject.transform.SetParent(context.Root);
            _meshObject.transform.localPosition = new Vector3(0, -999, 0);
            _meshObject.transform.localScale = Vector3.zero;
            _meshObject.gameObject.SetActive(false);

            _childObject = new GameObject("RefEqTestChild");
            _childObject.transform.SetParent(_meshObject.transform);
            
        }

        public void CreateNodes(TestContext context)
        {
            var exporter = context.interactivityExportContext.Context.exporter;
            int nodeIndex = exporter.GetTransformIndex(_meshObject.transform);
            

            // Test 1: Same mesh ref
            context.NewEntryPoint("Same mesh ref in ref/eq");

            var pGetA = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetA, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetA.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var pGetB = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetB, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetB.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqSame = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqSame.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetA.FirstValueOut());
            refEqSame.ValueIn(Ref_EqNode.IdValueB).ConnectToSource(pGetB.FirstValueOut());

            _checkSameMeshRef.SetupCheck(refEqSame.FirstValueOut(), out var checkFlowSame, true);
            context.AddToCurrentEntrySequence(checkFlowSame);

            // Test 2: Two nulls
            context.NewEntryPoint("Two nulls in ref/eq");
            var refEqNulls = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqNulls.ValueIn(Ref_EqNode.IdValueA).SetValue(null);
            refEqNulls.ValueIn(Ref_EqNode.IdValueB).SetValue(null);
            
            _checkTwoNulls.SetupCheck(refEqNulls.ValueOut(Ref_EqNode.IdOutValue), out var checkFlowNulls, true);
            context.AddToCurrentEntrySequence(checkFlowNulls);

            // Test 3: One mesh and one null
            context.NewEntryPoint("One mesh and one null in ref/eq");
            var pGetMeshAndNull = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetMeshAndNull, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetMeshAndNull.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);
            
            var refEqMixed = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqMixed.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetMeshAndNull.FirstValueOut());
            refEqMixed.ValueIn(Ref_EqNode.IdValueB).SetValue(null);

            _checkOneMeshOneNull.SetupCheck(refEqMixed.ValueOut(Ref_EqNode.IdOutValue), out var checkFlowMixed, false);
            context.AddToCurrentEntrySequence(checkFlowMixed);

            // Test 4: One mesh and one node
            context.NewEntryPoint("One mesh and one node in ref/eq");
            var pGetMeshNodeA = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetMeshNodeA, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetMeshNodeA.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var pGetMeshNodeB = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetMeshNodeB, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/children/0", GltfTypes.Ref);
            pGetMeshNodeB.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqMeshNode = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqMeshNode.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetMeshNodeA.FirstValueOut());
            refEqMeshNode.ValueIn(Ref_EqNode.IdValueB).ConnectToSource(pGetMeshNodeB.FirstValueOut());

            _checkMeshOneNode.SetupCheck(refEqMeshNode.FirstValueOut(), out var checkFlowMeshNode, false);
            context.AddToCurrentEntrySequence(checkFlowMeshNode);

            // The following tests have input A connected to another node (pointer/get) and
            // input B set to a static ref value.
            var root = exporter.GetRoot();
            var meshIndex = root.Nodes[nodeIndex].Mesh.Id;
            var childNodeIndex = root.Nodes[nodeIndex].Children[0].Id;

            // Test 5: Connected mesh ref and the same mesh as static value
            context.NewEntryPoint("Connected mesh ref and same static mesh ref in ref/eq");
            var pGetMeshStatic = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetMeshStatic, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetMeshStatic.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqMeshStatic = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqMeshStatic.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetMeshStatic.FirstValueOut());
            refEqMeshStatic.ValueIn(Ref_EqNode.IdValueB).SetValue(new StaticRefPointer($"/meshes/{meshIndex}"));

            _checkMeshVsStaticMesh.SetupCheck(refEqMeshStatic.ValueOut(Ref_EqNode.IdOutValue), out var checkFlowMeshStatic, true);
            context.AddToCurrentEntrySequence(checkFlowMeshStatic);

            // Test 6: Connected mesh ref and a node as static value
            context.NewEntryPoint("Connected mesh ref and static node ref in ref/eq");
            var pGetMeshStaticNode = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetMeshStaticNode, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/mesh", GltfTypes.Ref);
            pGetMeshStaticNode.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqMeshStaticNode = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqMeshStaticNode.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetMeshStaticNode.FirstValueOut());
            refEqMeshStaticNode.ValueIn(Ref_EqNode.IdValueB).SetValue(new StaticRefPointer($"/nodes/{nodeIndex}"));

            _checkMeshVsStaticNode.SetupCheck(refEqMeshStaticNode.ValueOut(Ref_EqNode.IdOutValue), out var checkFlowMeshStaticNode, false);
            context.AddToCurrentEntrySequence(checkFlowMeshStaticNode);

            // Test 7: Connected child node ref and the same node as static value
            context.NewEntryPoint("Connected child node ref and same static node ref in ref/eq");
            var pGetChildStatic = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pGetChildStatic, "/nodes/["+PointersHelper.IdPointerNodeIndex+"]/children/0", GltfTypes.Ref);
            pGetChildStatic.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(nodeIndex);

            var refEqChildStatic = context.interactivityExportContext.CreateNode<Ref_EqNode>();
            refEqChildStatic.ValueIn(Ref_EqNode.IdValueA).ConnectToSource(pGetChildStatic.FirstValueOut());
            refEqChildStatic.ValueIn(Ref_EqNode.IdValueB).SetValue(new StaticRefPointer($"/nodes/{childNodeIndex}"));

            _checkChildVsStaticChild.SetupCheck(refEqChildStatic.ValueOut(Ref_EqNode.IdOutValue), out var checkFlowChildStatic, true);
            context.AddToCurrentEntrySequence(checkFlowChildStatic);
        }

        public void Dispose()
        {
            if (_meshObject != null)
                GameObject.DestroyImmediate(_meshObject);
        }
    }
}
