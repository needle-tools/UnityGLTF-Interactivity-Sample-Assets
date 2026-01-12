using System;
using UnityEngine;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEditor;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    public class PointerMorphTargetsTest : ITestCase, IDisposable
    {
        private List<Object> _createdObjects = new List<Object>();

        private string MeshWithMorphGUID = "052432051dcf00a43b1afd492c2e6511";

        private GameObject meshWithoutMorph;
        private GameObject meshWithMorph;
        private GameObject nodeWithoutMesh;
        private GameObject meshWithMorphNonStatic;
        private GameObject meshWithMorph_MeshAndNodeWeights;

        private CheckBox weightLengthWithoutMesh;
        private CheckBox weight0WithoutMesh;
        private CheckBox nonStaticWeight0WithoutMesh;
        
        private CheckBox weightLengthWithoutMorphIsValid;
        private CheckBox weightLengthWithoutMorphLength;
        private CheckBox weight0WithoutMorph;
        private CheckBox nonStaticWeight0WithoutMorph;
        
        private CheckBox weightLengthWithStaticMorph;
        private CheckBox weight0WithMorphIsValid;
        private CheckBox weight0WithMorphValue;

        private CheckBox weightLengthWithNonStaticMorph;
        private CheckBox nonStaticWeight0WithMorphIsValid;
        private CheckBox nonStaticWeight0WithMorphValue;
        
        private CheckBox meshAndNodeWeight0Value;


        private CheckBox setWeightAndReadBack;

        
        private Mesh withoutStaticWeightsMesh;
        
        public string GetTestName()
        {
            return "pointer/get_set_morphtargets";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            meshWithoutMorph = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshWithoutMorph.name = "Node without Morph Targets";
            _createdObjects.Add(meshWithoutMorph);
            meshWithoutMorph.transform.parent = context.Root;
            meshWithoutMorph.transform.localScale = Vector3.one * 0.00001f;

            meshWithMorph = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(MeshWithMorphGUID)));
            meshWithMorph.name = "Node with Morph Targets (static weights)";
            _createdObjects.Add(meshWithMorph);
            meshWithMorph.transform.parent = context.Root;
            meshWithMorph.transform.localScale = Vector3.one * 0.00001f;

            
            meshWithMorphNonStatic = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(MeshWithMorphGUID)));
            meshWithMorphNonStatic.name = "Node with Morph Targets (non static weights)";
            _createdObjects.Add(meshWithMorphNonStatic);
            var smr = meshWithMorphNonStatic.GetComponentInChildren<SkinnedMeshRenderer>();
            withoutStaticWeightsMesh = Mesh.Instantiate(smr.sharedMesh);
            smr.sharedMesh = withoutStaticWeightsMesh;
            smr.SetBlendShapeWeight(0, 0.5f);
            meshWithMorphNonStatic.transform.parent = context.Root;
            meshWithMorphNonStatic.transform.localScale = Vector3.one * 0.00001f;

            
            meshWithMorph_MeshAndNodeWeights = GameObject.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(MeshWithMorphGUID)));
            meshWithMorph_MeshAndNodeWeights.name = "Node with Morph Targets (mesh and node weights)";
            _createdObjects.Add(meshWithMorph_MeshAndNodeWeights);
            meshWithMorph_MeshAndNodeWeights.transform.parent = context.Root;
            meshWithMorph_MeshAndNodeWeights.transform.localScale = Vector3.one * 0.00001f;
            meshWithMorph_MeshAndNodeWeights.GetComponentInChildren<SkinnedMeshRenderer>().SetBlendShapeWeight(0, 0.6f);

            
            
            nodeWithoutMesh = new GameObject("Node without a Mesh");
            nodeWithoutMesh.transform.parent = context.Root;
            nodeWithoutMesh.transform.localScale = Vector3.one * 0.00001f;
            _createdObjects.Add(nodeWithoutMesh);
            
            // weights.length
            weightLengthWithoutMesh = context.AddCheckBox("weights.length from Node without Mesh (isValid == false)");
            context.NewRow();
            weightLengthWithoutMorphIsValid = context.AddCheckBox("weights.length from Node without morph (isValid == true)");
            weightLengthWithoutMorphLength = context.AddCheckBox("weights.length from Node without morph (length == 0)");
            context.NewRow();
            weightLengthWithStaticMorph = context.AddCheckBox("weights.Length from Node with Mesh with static-Morph Targets (length == 2)");
            weightLengthWithNonStaticMorph = context.AddCheckBox("weights.Length from Node with Mesh with nonStatic-Morph Targets (length == 2)");

            
            
            // weights[0] with static weights from mesh.weights
            context.NewRow();
            weight0WithoutMesh = context.AddCheckBox("weights[0] from Node without Mesh (isValid == false)");
            weight0WithoutMorph = context.AddCheckBox("weights[0] from Node without morph (isValid == false)"); 

            weight0WithMorphIsValid = context.AddCheckBox("static weights[0] from Node with Mesh with Morph Targets (isValid == true)");
            weight0WithMorphValue = context.AddCheckBox("static weights[0] from Node with Mesh with Morph Targets (value == 0.1)");
            
            // non static weights[0] 
            context.NewRow();
            nonStaticWeight0WithMorphIsValid = context.AddCheckBox("nonStatic weights[0] from Node with Mesh with Morph Targets (isValid == true)");
            nonStaticWeight0WithMorphValue = context.AddCheckBox("nonStatic weights[0] from Node with Mesh with Morph Targets (value == 0.5)");
            
            meshAndNodeWeight0Value = context.AddCheckBox("mesh and node weights[0] (value == 0.6)");
            
            context.NewRow();
            setWeightAndReadBack = context.AddCheckBox("Set weight and read back");

        }

        public void CreateNodes(TestContext context)
        {
            var withOutStaticMeshID = context.interactivityExportContext.Context.exporter.GetMeshId(withoutStaticWeightsMesh);
            withOutStaticMeshID.Value.Weights = null;
            
            var nodeCreator = context.interactivityExportContext;

            context.NewEntryPoint("Get weights.length - Without Mesh");
            var pWeightLength_withoutMesh = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeightLength_withoutMesh, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights.length", GltfTypes.Int);
            pWeightLength_withoutMesh.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(nodeWithoutMesh.transform));
            weightLengthWithoutMesh.SetupCheck(pWeightLength_withoutMesh.ValueOut(Pointer_GetNode.IdIsValid), out var test1Flow, false);
            context.AddToCurrentEntrySequence(test1Flow);

            context.NewEntryPoint("Get weights.length - Without Morph Targets");
            var pWeightLength_withoutMorph = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeightLength_withoutMorph, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights.length", GltfTypes.Int);
            pWeightLength_withoutMorph.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithoutMorph.transform));
            weightLengthWithoutMorphIsValid.SetupCheck(pWeightLength_withoutMorph.ValueOut(Pointer_GetNode.IdIsValid), out var test2Flow, true);
            weightLengthWithoutMorphLength.SetupCheck(pWeightLength_withoutMorph.ValueOut(Pointer_GetNode.IdValue), out var test3Flow, 0);
            context.AddToCurrentEntrySequence(test2Flow, test3Flow);
            
            context.NewEntryPoint("Get weights.length - With static-Morph Targets");
            var pWeightLength_withMorph = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeightLength_withMorph, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights.length", GltfTypes.Int);
            pWeightLength_withMorph.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorph.transform));
            weightLengthWithStaticMorph.SetupCheck(pWeightLength_withMorph.ValueOut(Pointer_GetNode.IdValue), out var test4Flow, meshWithMorph.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.blendShapeCount);
            context.AddToCurrentEntrySequence(test4Flow);
            
            context.NewEntryPoint("Get weights.length - With nonStatic-Morph Targets");
            var pWeightLength_withNonStaticMorph = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeightLength_withNonStaticMorph, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights.length", GltfTypes.Int);
            pWeightLength_withNonStaticMorph.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorphNonStatic.transform));
            weightLengthWithNonStaticMorph.SetupCheck(pWeightLength_withNonStaticMorph.ValueOut(Pointer_GetNode.IdValue), out var test4bFlow, meshWithMorphNonStatic.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.blendShapeCount);
            context.AddToCurrentEntrySequence(test4bFlow);
            
            
            
            context.NewEntryPoint("Get node/{}/weights/0 - Without Mesh");
            var pWeight0_withoutMesh = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeight0_withoutMesh, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pWeight0_withoutMesh.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(nodeWithoutMesh.transform));
            weight0WithoutMesh.SetupCheck(pWeight0_withoutMesh.ValueOut(Pointer_GetNode.IdIsValid), out var test5Flow, false);
            context.AddToCurrentEntrySequence(test5Flow);

            context.NewEntryPoint("Get node/{}/weights/0 - Without Morph Targets");
            var pWeight0_withoutMorph = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeight0_withoutMorph, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pWeight0_withoutMorph.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithoutMorph.transform));
            weight0WithoutMorph.SetupCheck(pWeight0_withoutMorph.ValueOut(Pointer_GetNode.IdIsValid), out var test6Flow, true);
            context.AddToCurrentEntrySequence(test6Flow);
            
            
            context.NewEntryPoint("Get node/{}/weights/0 - With static Morph Targets");
            var pWeight0_withMorph = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeight0_withMorph, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pWeight0_withMorph.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorph.transform));
            weight0WithMorphIsValid.SetupCheck(pWeight0_withMorph.ValueOut(Pointer_GetNode.IdIsValid), out var test7Flow, true);
            weight0WithMorphValue.SetupCheck(pWeight0_withMorph.ValueOut(Pointer_GetNode.IdValue), out var test8Flow, meshWithMorph.GetComponentInChildren<SkinnedMeshRenderer>().GetBlendShapeWeight(0));
            context.AddToCurrentEntrySequence(test7Flow, test8Flow);

            
            context.NewEntryPoint("Get node/{}/weights/0 - With nonStatic Morph Targets");
            var pWeight0_withMorphNonStatic = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeight0_withMorphNonStatic, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pWeight0_withMorphNonStatic.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorphNonStatic.transform));
            nonStaticWeight0WithMorphIsValid.SetupCheck(pWeight0_withMorphNonStatic.ValueOut(Pointer_GetNode.IdIsValid), out var test9Flow, true);
            nonStaticWeight0WithMorphValue.SetupCheck(pWeight0_withMorphNonStatic.ValueOut(Pointer_GetNode.IdValue), out var test10Flow, meshWithMorphNonStatic.GetComponentInChildren<SkinnedMeshRenderer>().GetBlendShapeWeight(0));
            context.AddToCurrentEntrySequence(test9Flow, test10Flow);

            context.NewEntryPoint("Get node/{}/weights/0 - With mesh and node weights");
            var pWeight0 = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pWeight0, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pWeight0.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorph_MeshAndNodeWeights.transform));
            meshAndNodeWeight0Value.SetupCheck(pWeight0.ValueOut(Pointer_GetNode.IdValue), out var test11Flow, meshWithMorph_MeshAndNodeWeights.GetComponentInChildren<SkinnedMeshRenderer>().GetBlendShapeWeight(0));
            context.AddToCurrentEntrySequence(test11Flow);
            
            
            context.NewEntryPoint("Set weight and read back");
            var pSetWeight = nodeCreator.CreateNode<Pointer_SetNode>();
            var pGetWeight = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(pSetWeight, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            PointersHelper.SetupPointerTemplateAndTargetInput(pGetWeight, PointersHelper.IdPointerNodeIndex, "/nodes/{"+PointersHelper.IdPointerNodeIndex+"}/weights/0", GltfTypes.Float);
            pSetWeight.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorph_MeshAndNodeWeights.transform));
            pSetWeight.ValueIn(Pointer_SetNode.IdValue).SetValue(0.9f);
            
            pGetWeight.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(context.interactivityExportContext.Context.exporter.GetTransformIndex(meshWithMorph_MeshAndNodeWeights.transform));

           
            setWeightAndReadBack.SetupCheck(pGetWeight.ValueOut(Pointer_GetNode.IdValue), out var test12Flow, 0.9f);
            context.AddToCurrentEntrySequence(pSetWeight.FlowIn(), test12Flow);
        }

        public void Dispose()
        {
            _createdObjects.ForEach(Object.DestroyImmediate);
        }
    }
}