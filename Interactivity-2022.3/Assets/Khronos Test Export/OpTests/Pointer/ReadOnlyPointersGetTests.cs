using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class ReadOnlyPointersGetTests : ITestCase, IDisposable
    {
        private static (string, Func<TestContext, object>)[] ReadOnlyPointers = new(string, Func<TestContext, object>)[]
        {
            ("/animations.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Animations.Count),
            ("/cameras.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Cameras.Count),
            ("/materials.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Materials.Count),
//            ("/materials/{}/doubleSided",
            ("/meshes.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Meshes.Count),
            ("/meshes/0/primitives.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Meshes[0].Primitives.Count),
            ("/meshes/0/primitives/0/material",(TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Meshes[0].Primitives[0].Material.Id),
//            ("/meshes/{0}/weights.length",
//            ("/nodes.length", 
//            ("/nodes/{}/camera",
            ("/nodes/0/children.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Nodes[0].Children.Count),
//            ("/nodes/{}/children/{}",
            ("/nodes/{nodeWithMesh}/mesh", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Nodes.First( n => n.Mesh != null).Mesh.Id),
            ("/nodes/1/parent", (TestContext c) =>
            {
                    var r = c.interactivityExportContext.Context.exporter.GetRoot();
                    var n = r.Nodes[1];
                    var parent = r.Nodes.Find(n2 => n2.Children?.FirstOrDefault(c => c.Id == 1) != null);
                    return r.Nodes.IndexOf(parent);
            }),
            //("/nodes/{}/skin",
            //("/nodes/{}/weights.length",
            ("/scene", (TestContext c) => 0),
            ("/scenes.length", (TestContext c) => 1),
            ("/scenes/0/nodes.length", (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Nodes.Count),
            //("/scenes/0/nodes/{}",  (TestContext c) => c.interactivityExportContext.Context.exporter.GetRoot().Nodes.Count),
            // ("/skins.length",
            // ("/skins/{}/joints.length",
            // ("/skins/{}/joints/{}",
            // ("/skins/{}/skeleton"
        };
        
        
        private List<CheckBox> readOnlyPointerCheckBoxes = new List<CheckBox>();
        private List<CheckBox> isValidCheckBoxes = new List<CheckBox>();
        private List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();
        
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
                var cameraObject = new GameObject("TestCamera");
                cameraObject.transform.SetParent(context.Root);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;
                cameraObject.AddComponent<Camera>();
                objectsToDestroy.Add(cameraObject);
            }

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
                var value = ReadOnlyPointers[i].Item2(context);
                var pointerGet = nodeCreator.CreateNode<Pointer_GetNode>();
                var pointer = ReadOnlyPointers[i].Item1;
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