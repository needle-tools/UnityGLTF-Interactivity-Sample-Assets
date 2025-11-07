using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

public class RandomDistributionVisualisation : MonoBehaviour, IInteractivityExport
{
    public int numberOfSamples = 1000;

    public GameObject prefab;

    [SerializeField] private List<GameObject> _samples = new List<GameObject>();

    [CustomEditor(typeof(RandomDistributionVisualisation))]
    public class Inspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var monteCarlo = target as RandomDistributionVisualisation;
            if (GUILayout.Button("Generate"))
            {
                monteCarlo.Create();
            }
        }
        
    }
    public void Create()
    {
        if (prefab == null)
        {
            return;
        }

        for (int i = 0; i < _samples.Count; i++)
        {
            DestroyImmediate(_samples[i]);
        }
        
        _samples.Clear();

        for (int i = 0; i < numberOfSamples; i++)
        {
            var sample = Instantiate(prefab, transform);
            sample.transform.localPosition = Vector3.zero;
            _samples.Add(sample);
        }
        
    }

    public void OnInteractivityExport(GltfInteractivityExportNodes export)
    {
        var startNode = export.CreateNode<Event_OnStartNode>();
        
        var forLoopNode = export.CreateNode<Flow_ForLoopNode>();
        forLoopNode.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(numberOfSamples);
        forLoopNode.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);

        startNode.FlowOut().ConnectToFlowDestination(forLoopNode.FlowIn());
        
        var randomXNode = export.CreateNode<Math_RandomNode>();
        var randomYNode = export.CreateNode<Math_RandomNode>();
        
        var combineXYZNode = export.CreateNode<Math_Combine3Node>();
        combineXYZNode.ValueIn(Math_Combine3Node.IdValueA).ConnectToSource(randomXNode.FirstValueOut());
        combineXYZNode.ValueIn(Math_Combine3Node.IdValueB).SetValue(0f);
        combineXYZNode.ValueIn(Math_Combine3Node.IdValueC).ConnectToSource(randomYNode.FirstValueOut());
       
        var multiplyNode = export.CreateNode<Math_MulNode>();
        multiplyNode.ValueIn(Math_MulNode.IdValueA).ConnectToSource(combineXYZNode.FirstValueOut());
        multiplyNode.ValueIn(Math_MulNode.IdValueB).SetValue(new Vector3(2f,0f,2f));
        
        var subtractNode = export.CreateNode<Math_SubNode>();
        subtractNode.ValueIn(Math_SubNode.IdValueA).ConnectToSource(multiplyNode.FirstValueOut());
        subtractNode.ValueIn(Math_SubNode.IdValueB).SetValue(new Vector3(1f,0f,1f));
        
        var list = new VariableBasedList(export.Context, "samples", numberOfSamples, GltfTypes.TypeIndex(typeof(int)));
        ListHelpers.CreateListNodes(export, list);
        for (int i = 0; i < numberOfSamples; i++)
            list.AddItem(export.Context.exporter.GetTransformIndex(_samples[i].transform));

        TransformHelpers.SetLocalPosition(export, out var target, out var pos, out var flowIn, out var flowOut);
        
        ListHelpers.GetItem(export, list, out var indexInput, out var valueOutput);
        indexInput.ConnectToSource(forLoopNode.ValueOut(Flow_ForLoopNode.IdIndex));
        target.ConnectToSource(valueOutput);
        forLoopNode.FlowOut(Flow_ForLoopNode.IdLoopBody).ConnectToFlowDestination(flowIn);
        pos.ConnectToSource(subtractNode.FirstValueOut());

        //
        // var length = export.CreateNode<Math_LengthNode>();
        // length.ValueIn(Math_LengthNode.IdValueA).ConnectToSource(subtractNode.FirstValueOut());
        //
        // var ltNode = export.CreateNode<Math_LtNode>();
        // ltNode.ValueIn(Math_LtNode.IdValueA).ConnectToSource(length.FirstValueOut());
        // ltNode.ValueIn(Math_LtNode.IdValueB).SetValue(1f);
        //
        // var branchNode = export.CreateNode<Flow_BranchNode>();
        // flowOut.ConnectToFlowDestination(branchNode.FlowIn());
        // branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(ltNode.FirstValueOut());
        //
        // var setRedNode = export.CreateNode<Pointer_SetNode>();
        // PointersHelper.AddPointerConfig(setRedNode, "/materials/{0}/pbrMetallicRoughness/baseColorFactor", GltfTypes.Float4);
        //
    }
}
