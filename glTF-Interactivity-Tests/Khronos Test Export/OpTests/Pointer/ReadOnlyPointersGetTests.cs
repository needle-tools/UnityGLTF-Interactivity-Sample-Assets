using System;
using System.Collections.Generic;
using System.Linq;
using GLTF.Schema;
using UnityEditor;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ReadOnlyPointersGetTests : ITestCase, IDisposable
    {
        private (string, Func<TestContext, (object, string) >)[] ReadOnlyPointers = new(string, Func<TestContext, (object, string)>)[]
        {
            ("/animations.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Animations.Count, null)),
            ("/cameras.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Cameras.Count, null)),
            ("/materials.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Materials.Count, null)),
            ("/materials/{}/doubleSided", (TestContext c) =>
                {
                    var root = c.interactivityExportContext.Context.exporter.GetRoot();
                    var dsMaterialId = c.interactivityExportContext.Context.exporter.GetMaterialId(root, doubleSidedMaterial);
                    return (true, $"/materials/{dsMaterialId.Id}/doubleSided");
                }),
            ("/meshes.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Meshes.Count, null)),
            ("/meshes/0/primitives.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Meshes[0].Primitives.Count, null)),
            ("/meshes/0/primitives/0/material",(TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Meshes[0].Primitives[0].Material.Id, null)),
           
            ("/nodes/{}/weights.length", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var nodeWithWeights = root.Nodes.FirstOrDefault(n => n.Weights != null && n.Weights.Count > 0);
                    if (nodeWithWeights == null)
                        throw new Exception("No node with weights found");
                    var nodeWithWeightsIndex = root.Nodes.IndexOf(nodeWithWeights);
                    return (nodeWithWeights.Weights.Count, $"/nodes/{nodeWithWeightsIndex}/weights");
                    
                }), 
            ("/meshes/{0}/weights.length", (TestContext c) => 
            {
                var meshId = c.interactivityExportContext.Context.exporter.GetMeshId(blendShapeMesh);
                return (meshId.Value.Weights.Count, $"/meshes/{meshId.Id}/weights.length"); 
            }),
            ("/nodes.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Nodes.Count, null)),
            ("/nodes/{}/camera", (TestContext c) =>
            {
                var cameraNodeId = c.interactivityExportContext.Context.exporter.GetTransformIndex(cameraObject.transform);
                var root = c.interactivityExportContext.Context.exporter.GetRoot();
                return (root.Nodes[cameraNodeId].Camera.Id, $"/nodes/{cameraNodeId}/camera");
            }),
            ("/nodes/0/children.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Nodes[0].Children.Count, null)),
            
            ("/nodes/{}/children/{}", (TestContext c) =>
                {
                    var root = c.interactivityExportContext.Context.exporter.GetRoot();
                    var parentNode = root.Nodes.FirstOrDefault(n => n.Children != null && n.Children.Count > 0);
                    if (parentNode == null)
                        throw new Exception("No node with children found");
                    var parentIndex = root.Nodes.IndexOf(parentNode);
                    return (parentNode.Children[0].Id, $"/nodes/{parentIndex}/children/0");
                    
                }),
            ("/nodes/{nodeWithMesh}/mesh", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Nodes.First( n => n.Mesh != null).Mesh.Id, null)),
            ("/nodes/1/parent", (TestContext c) =>
            {
                    var r = c.interactivityExportContext.Context.exporter.GetRoot();
                    var n = r.Nodes[1];
                    var parent = r.Nodes.Find(n2 => n2.Children?.FirstOrDefault(c => c.Id == 1) != null);
                    return (r.Nodes.IndexOf(parent), null);
            }),
            ("/scene", (TestContext c) => (0, null)),
            ("/scenes.length", (TestContext c) => (1, null)),
            ("/scenes/0/nodes.length", (TestContext c) => (c.interactivityExportContext.Context.exporter.GetRoot().Nodes.Count, null)),
            
            ("/scenes/0/nodes/0",  (TestContext c) => (0, null)),
        
            ("/nodes/{}/skin", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var skinnedNode = root.Nodes.FirstOrDefault(n => n.Mesh != null && n.Skin != null);
                    if (skinnedNode == null)
                        throw new Exception("No skinned node found");
                    var skinnedNodeIndex = root.Nodes.IndexOf(skinnedNode);
                    return (skinnedNode.Skin.Id, $"/nodes/{skinnedNodeIndex}/skin");
                    
                }),
            ("/skins.length", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    return (root.Skins != null ? root.Skins.Count : 0, null);
                }),
            ("/skins/0/joints.length", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var skin = root.Skins[0];
                    return (skin.Joints.Count, null);
                }),
            ("/skins/{}/joints/{}", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var skin = root.Skins.FirstOrDefault(s => s.Joints != null && s.Joints.Count > 0);
                    if (skin == null)
                        throw new Exception("No skin with joints found");
                    var skinIndex = root.Skins.IndexOf(skin);
                    return (skin.Joints[0].Id, $"/skins/{skinIndex}/joints/0");
                }),
            ("/skins/{}/skeleton", context =>
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var skin = root.Skins.FirstOrDefault();
                    if (skin.Skeleton == null)
                        skin.Skeleton = new NodeId() { Id = 0, Root = root };
                    return (skin.Skeleton.Id, $"/skins/{root.Skins.IndexOf(skin)}/skeleton");
                }),
        };
        
        
        private List<CheckBox> readOnlyPointerCheckBoxes = new List<CheckBox>();
        private List<CheckBox> isValidCheckBoxes = new List<CheckBox>();
        private List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

        private static string BlendShapePrefabGUID = "052432051dcf00a43b1afd492c2e6511";
        private static Mesh blendShapeMesh;

        private static string doubleSidedMaterialGUID = "5011c7566aa47e54187e060419a0cae6";
        private static Material doubleSidedMaterial;
        private static GameObject cameraObject;
        
        private static string skinnendPrefabGUID = "92a32205625a8ac428228f4a0a66e324";
        private static Mesh skinnedMesh;
        
        public string GetTestName()
        {
            return "pointer/CoreReadOnlyPointers_GetTests";
            
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            
            readOnlyPointerCheckBoxes.Clear();
            isValidCheckBoxes.Clear();
            
            if (!context.Root.GetComponentInChildren<Camera>())
            {
                cameraObject = new GameObject("TestCamera");
                cameraObject.transform.SetParent(context.Root);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
                cameraObject.AddComponent<Camera>();
                objectsToDestroy.Add(cameraObject);
            }
            
            // Add a default Cube with a double sided material
            var doubleSidedMaterialPath = AssetDatabase.GUIDToAssetPath(doubleSidedMaterialGUID);
            doubleSidedMaterial = AssetDatabase.LoadAssetAtPath<Material>(doubleSidedMaterialPath);
            
            var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeObject.name = "TestCube";
            cubeObject.transform.SetParent(context.Root);
            cubeObject.transform.localScale = Vector3.one * 0.0001f;
            cubeObject.transform.localPosition = Vector3.zero;
            cubeObject.transform.localRotation = Quaternion.identity;
            var cubeRenderer = cubeObject.GetComponent<MeshRenderer>();
            cubeRenderer.sharedMaterial = doubleSidedMaterial;
            objectsToDestroy.Add(cubeObject);
            
            // Add a skinned mesh
            var skinnedPrefabPath = AssetDatabase.GUIDToAssetPath(skinnendPrefabGUID);
            var skinnedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(skinnedPrefabPath);
            
            var skinnendObject = GameObject.Instantiate(skinnedPrefab, context.Root);
            objectsToDestroy.Add(skinnendObject);
            var skinnendObjectSMR = skinnendObject.GetComponentInChildren<SkinnedMeshRenderer>();
            skinnedMesh = skinnendObjectSMR.sharedMesh;
            skinnendObject.transform.localScale = Vector3.one * 0.0000001f;
            
            var blendShapeMeshPath = AssetDatabase.GUIDToAssetPath(BlendShapePrefabGUID);
            var blendShapeMeshPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(blendShapeMeshPath);
            
            var blendShapeObject = UnityEngine.Object.Instantiate(blendShapeMeshPrefab, context.Root);
            objectsToDestroy.Add(blendShapeObject);
            var blendShapeMeshSMR = blendShapeMeshPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            blendShapeMeshSMR.SetBlendShapeWeight(0, 0.5f);
            blendShapeMeshSMR.SetBlendShapeWeight(1, 0.4f);
            blendShapeObject.transform.localScale = Vector3.one * 0.0000001f;
            blendShapeMesh = blendShapeMeshSMR.sharedMesh;

            // Second copy to force node/{0}/weights creation on export
            var blendShapeObject2 = UnityEngine.Object.Instantiate(blendShapeMeshPrefab, context.Root);
            objectsToDestroy.Add(blendShapeObject2);
            blendShapeObject2.transform.localScale = Vector3.one * 0.0000001f;
            var blendShapeMeshSMR2 = blendShapeMeshPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            blendShapeMeshSMR2.SetBlendShapeWeight(0, 0.1f);
            blendShapeMeshSMR2.SetBlendShapeWeight(1, 0.2f);
            
            if (!context.Root.GetComponentInChildren<Animation>())
            {
                var aniObject = new GameObject("TestAnimation");
                aniObject.transform.SetParent(context.Root);
                aniObject.transform.localPosition = Vector3.zero;
                aniObject.transform.localRotation = Quaternion.identity;
                var animation = aniObject.AddComponent<Animation>();
                // Create a dummy animation clip
                var clip = new AnimationClip();
                clip.name = "TestAnimationClip";
                clip.legacy = true;
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
                
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                objectsToDestroy.Add(aniObject);
                objectsToDestroy.Add(clip);
            }
            
            foreach (var pair in ReadOnlyPointers)
            {
                var checkBox = context.AddCheckBox(pair.Item1);
                var checkBoxIsValid = context.AddCheckBox(pair.Item1 + " isValid");
                readOnlyPointerCheckBoxes.Add(checkBox);
                isValidCheckBoxes.Add(checkBoxIsValid);
                context.NewRow();
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            for (int i = 0; i < ReadOnlyPointers.Length; i++)
            {
                context.NewEntryPoint(ReadOnlyPointers[i].Item1);
                var result = ReadOnlyPointers[i].Item2(context);
                var value = result.Item1; 
                var pointerGet = nodeCreator.CreateNode<Pointer_GetNode>();
                var pointer = string.IsNullOrEmpty(result.Item2) ? ReadOnlyPointers[i].Item1 : result.Item2;
                
                if (pointer.Contains("{nodeWithMesh}"))
                {
                    var root = context.interactivityExportContext.Context.exporter.GetRoot();
                    var nodeWithMesh = root.Nodes.FirstOrDefault(n => n.Mesh != null);
                    if (nodeWithMesh != null)
                    {
                        var nodeIndex = root.Nodes.IndexOf(nodeWithMesh);
                        pointerGet.ValueIn("nodeWithMesh").SetValue(nodeIndex);
                        //pointer = pointer.Replace("{nodeWithMesh}", nodeWithMesh.Mesh.Id.ToString());
                    }
                    else
                    {
                        Debug.LogWarning("No node with mesh found, skipping pointer: " + pointer);
                        continue;
                    }
                }
                PointersHelper.AddPointerConfig(pointerGet, pointer, GltfTypes.TypeIndex(value.GetType()));
                
                var checkBox = readOnlyPointerCheckBoxes[i];
                checkBox.SetupCheck(pointerGet.ValueOut(Pointer_GetNode.IdValue), out var checkFlowIn, value);
                
                var isValidCheckBox = isValidCheckBoxes[i];
                isValidCheckBox.SetupCheck(pointerGet.ValueOut(Pointer_GetNode.IdIsValid), out var isValidFlowIn, true);
                context.AddToCurrentEntrySequence(checkFlowIn, isValidFlowIn); 
                
            }
            
        }

        public void Dispose()
        {
            foreach (var obj in objectsToDestroy)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            objectsToDestroy.Clear();
            readOnlyPointerCheckBoxes.Clear();
            isValidCheckBoxes.Clear();
            
        }
    }
}