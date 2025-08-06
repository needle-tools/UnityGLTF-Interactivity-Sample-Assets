using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export.ExtraTests
{
    public class MatrixUpdatesTests : ITestCase, IDisposable
    {
        private GameObject _gameObject;
        private GameObject _childGameObject;
        private GameObject _child2GameObject;
        private GameObject _child3GameObject;
        private CheckBox _globalMatrixCheckBox;
        private CheckBox _localMatrixCheckBox;

        private CheckBox _childGlobalMatrixCheckBox;
        private CheckBox _child2GlobalMatrixCheckBox;
        private CheckBox _child3GlobalMatrixCheckBox;
        
        public string GetTestName()
        {
            return "Extras/Matrix Updates";
        }

        public string GetTestDescription()
        {
            return "Testing if globalMatrix and matrix will be updated in current frame/flow processing";
        }

        public void PrepareObjects(TestContext context)
        {
            _gameObject = new GameObject("MatrixTest");
            _gameObject.transform.SetParent(context.Root);
            _gameObject.transform.localScale = Vector3.one * 0.0001f;
            _gameObject.transform.localPosition = Vector3.zero;

            _childGameObject = new GameObject("child1");
            _childGameObject.transform.SetParent(_gameObject.transform);
            _childGameObject.transform.localPosition = Vector3.zero;
            
            _child2GameObject = new GameObject("child2");
            _child2GameObject.transform.SetParent(_gameObject.transform);
            _child2GameObject.transform.localPosition = Vector3.zero;
            
            _child3GameObject = new GameObject("child3");
            _child3GameObject.transform.SetParent(_gameObject.transform);
            _child3GameObject.transform.localPosition = Vector3.zero;
            
            _localMatrixCheckBox = context.AddCheckBox("matrix");
            _globalMatrixCheckBox = context.AddCheckBox("globalMatrix");
            _childGlobalMatrixCheckBox = context.AddCheckBox("globalMatrix from Child 1");
            _child2GlobalMatrixCheckBox = context.AddCheckBox("globalMatrix from Child 2");
            _child3GlobalMatrixCheckBox = context.AddCheckBox("globalMatrix from Child 3");
        }

        public void CreateNodes(TestContext context)
        {
            var transformIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(_gameObject.transform);
            var childTransformIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(_childGameObject.transform);
            var child2TransformIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(_child2GameObject.transform);
            var child3TransformIndex = context.interactivityExportContext.Context.exporter.GetTransformIndex(_child3GameObject.transform);
            var nodeCreator = context.interactivityExportContext;

            context.NewEntryPoint("Matrix Update");

            context.interactivityExportContext.Context.addUnityGltfSpaceConversion = false;
            TransformHelpers.SetWorldPosition(nodeCreator, out var targetRef, out var posRef, out var flowInRef, out var flowOutRef);
            targetRef.SetValue(transformIndex);
            posRef.SetValue(new Vector3(10f, 20f, 30f));

            TransformHelpers.SetWorldPosition(nodeCreator, out var targetRef2, out var posRef2, out var flowInRef2, out var flowOutRef2);
            targetRef2.SetValue(transformIndex);
            var newPos = new Vector3(1f, 2f, 3f);
            posRef2.SetValue(newPos);
            flowOutRef.ConnectToFlowDestination(flowInRef2);
            
            context.AddToCurrentEntrySequence(flowInRef);

            var localMatrix = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(localMatrix, $"/nodes/{transformIndex}/matrix", GltfTypes.Float4x4);
            var localDecompose = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            localDecompose.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(localMatrix.FirstValueOut());
            _localMatrixCheckBox.SetupCheck(localDecompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var localCheckFlowIn, newPos, true);
            
            var globalMatrix = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(globalMatrix, $"/nodes/{transformIndex}/globalMatrix", GltfTypes.Float4x4);
            var globalDecompose = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            globalDecompose.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(globalMatrix.FirstValueOut());
            _globalMatrixCheckBox.SetupCheck(globalDecompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var globalCheckFlowIn, newPos, true);
            
            var childGlobalMatrix = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(childGlobalMatrix, $"/nodes/{childTransformIndex}/globalMatrix", GltfTypes.Float4x4);
            var childGlobalDecompose = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            childGlobalDecompose.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(childGlobalMatrix.FirstValueOut());
            _childGlobalMatrixCheckBox.SetupCheck(childGlobalDecompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var childGlobalCheckFlowIn, newPos, true);
            
            var sub = nodeCreator.CreateNode<Math_SubNode>();
            sub.ValueIn("a").ConnectToSource(childGlobalDecompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation));
            sub.ValueIn("b").SetValue(new Vector3(1, 1, 1));
            TransformHelpers.SetWorldPosition(nodeCreator, out var targetChild2Ref, out var pos2Ref, out var flowIn2Ref, out var flowOut2Ref);
            targetChild2Ref.SetValue(child2TransformIndex);
            pos2Ref.ConnectToSource(sub.FirstValueOut());
      
            var child2GlobalMatrix = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(child2GlobalMatrix, $"/nodes/{child2TransformIndex}/globalMatrix", GltfTypes.Float4x4);
            var child2GlobalDecompose = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            child2GlobalDecompose.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(child2GlobalMatrix.FirstValueOut());
            _child2GlobalMatrixCheckBox.SetupCheck(child2GlobalDecompose.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var child2GlobalCheckFlowIn, new Vector3(0f, 1f, 2f), true);
            
            flowOut2Ref.ConnectToFlowDestination(child2GlobalCheckFlowIn);
            
            TransformHelpers.GetWorldPosition(nodeCreator, out var child2Target, out var child2WorldPos);
            child2Target.SetValue(child2TransformIndex);
            TransformHelpers.SetWorldPosition(nodeCreator, out var child3Target, out var child3pos, out var child3flowInRef, out var child3flowOutRef);
            child3pos.ConnectToSource(child2WorldPos);
            child3Target.SetValue(child3TransformIndex);
            TransformHelpers.GetWorldPosition(nodeCreator, out var child3Target2, out var child3WorldPos);
            child3Target2.SetValue(child3TransformIndex);
            
            _child3GlobalMatrixCheckBox.SetupCheck(child3WorldPos, child3flowOutRef,  new Vector3(0f, 1f, 2f), true);
            
            context.AddSequence(flowOutRef2, localCheckFlowIn, globalCheckFlowIn, childGlobalCheckFlowIn, flowIn2Ref, child3flowInRef);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_gameObject);
        }
    }
}