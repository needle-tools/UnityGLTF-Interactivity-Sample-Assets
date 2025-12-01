using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class AdjustTopology : MonoBehaviour
{
    public enum TopologyMode { Original, Lines, Points }

    public TopologyMode mode = TopologyMode.Original;

    [SerializeField] private Mesh originalMesh;
    private Mesh modifiedMesh;
    private MeshFilter filter;
    private TopologyMode lastMode;

    void OnEnable()
    {
        filter = GetComponent<MeshFilter>();
        if (filter == null) return;

        if (originalMesh == null)
        {
            originalMesh = filter.sharedMesh;
        }

        if (modifiedMesh == null)
        {
            modifiedMesh = Object.Instantiate(originalMesh);
            modifiedMesh.hideFlags = HideFlags.DontSave;
        }

        lastMode = mode;
        ApplyTopology();
    }

    void OnDisable()
    {
        if (filter != null && originalMesh != null)
        {
            filter.mesh = originalMesh;
        }
    }

    void Update()
    {
        // In case mode changes in editor
        if (Application.isEditor && !Application.isPlaying && mode != lastMode)
        {
            lastMode = mode;
            ApplyTopology();
        }
    }

    void ApplyTopology()
    {
        if (filter == null || modifiedMesh == null || originalMesh == null) return;

        switch (mode)
        {
            case TopologyMode.Original:
                filter.mesh = originalMesh;
                break;
            case TopologyMode.Lines:
                SetLinesTopology();
                filter.mesh = modifiedMesh;
                break;
            case TopologyMode.Points:
                SetPointsTopology();
                filter.mesh = modifiedMesh;
                break;
        }
    }

    void SetLinesTopology()
    {
        int[] tris = originalMesh.triangles;
        Dictionary<(int, int), int> edgeCount = new Dictionary<(int, int), int>();

        // Count edge usages
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            AddEdge(edgeCount, a, b);
            AddEdge(edgeCount, b, c);
            AddEdge(edgeCount, c, a);
        }

        // Collect edges used exactly once (boundary edges, removing diagonals)
        List<int> lineIndices = new List<int>();
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value == 1)
            {
                lineIndices.Add(kvp.Key.Item1);
                lineIndices.Add(kvp.Key.Item2);
            }
        }

        modifiedMesh.SetIndices(lineIndices.ToArray(), MeshTopology.Lines, 0);
    }

    void SetPointsTopology()
    {
        int vertexCount = originalMesh.vertexCount;
        int[] pointIndices = new int[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            pointIndices[i] = i;
        }
        modifiedMesh.SetIndices(pointIndices, MeshTopology.Points, 0);
    }

    void AddEdge(Dictionary<(int, int), int> edgeCount, int a, int b)
    {
        var key = a < b ? (a, b) : (b, a);
        if (edgeCount.ContainsKey(key))
            edgeCount[key]++;
        else
            edgeCount[key] = 1;
    }
}
