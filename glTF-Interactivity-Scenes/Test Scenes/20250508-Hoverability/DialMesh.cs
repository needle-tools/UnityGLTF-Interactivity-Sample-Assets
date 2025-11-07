using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DialMesh : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Color32 color;
        public Vector2 uv;
    }

    // Public properties for the mesh
    public float innerRadius = 0.3f;
    public float outerRadius = 0.5f;
    public int segments = 32;
    public float height = 0.2f;

    private Mesh _mesh;
    private float _lastInnerRadius;
    private float _lastOuterRadius;
    private int _lastSegments;
    private float _lastHeight;

    // Called when the component is enabled
    private void OnEnable()
    {
        _mesh = new Mesh();
        _mesh.name = "DialMesh";
        _mesh.hideFlags = HideFlags.DontSave;
        GetComponent<MeshFilter>().sharedMesh = _mesh;

        // Cache initial values
        _lastInnerRadius = innerRadius;
        _lastOuterRadius = outerRadius;
        _lastSegments = segments;
        _lastHeight = height;

        // Generate the mesh
        GenerateMesh();
    }

    // Cleanup when the component is disabled
    private void OnDisable()
    {
        if (_mesh != null)
        {
            DestroyImmediate(_mesh);
            _mesh = null;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Check if any properties have changed
        if (innerRadius != _lastInnerRadius ||
            outerRadius != _lastOuterRadius ||
            segments != _lastSegments ||
            height != _lastHeight)
        {
            // Update cached values
            _lastInnerRadius = innerRadius;
            _lastOuterRadius = outerRadius;
            _lastSegments = segments;
            _lastHeight = height;

            // Regenerate the mesh
            GenerateMesh();
        }
    }

    private void GenerateMesh()
    {
        if (_mesh == null)
            return;

        // Ensure segments is at least 4
        int actualSegments = Mathf.Max(4, segments);

        // Calculate the total number of vertices
        // Each segment has 4 vertices (inner/outer at start and end of segment)
        int totalVertices = (actualSegments + 1) * 4;

        // Clear the mesh
        _mesh.Clear();

        // Use the new Mesh API with NativeArrays
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];

        // Define the vertex attributes
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
        vertexAttributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);

        // Set vertex buffer parameters
        meshData.SetVertexBufferParams(totalVertices, vertexAttributes);
        vertexAttributes.Dispose();

        // Get the vertex buffer
        var vertexBuffer = meshData.GetVertexData<VertexData>(0);

        // Create vertices
        int vertexIndex = 0;
        for (int i = 0; i <= actualSegments; i++)
        {
            // Calculate angle for this segment
            float angle = i * 2 * Mathf.PI / actualSegments;
            float nextAngle = (i + 1) * 2 * Mathf.PI / actualSegments;
            
            // Calculate linear interpolation factors for Z offset (0 to 1)
            float t1 = (float)i / actualSegments;
            float t2 = (float)(i + 1) / actualSegments;
            
            // Calculate Z positions for inner edges
            float innerZ1 = height * t1;
            float innerZ2 = height * t2;

            // Inner edge vertex for current angle
            float innerX1 = innerRadius * Mathf.Cos(angle);
            float innerY1 = innerRadius * Mathf.Sin(angle);
            
            // Inner edge vertex for next angle
            float innerX2 = innerRadius * Mathf.Cos(nextAngle);
            float innerY2 = innerRadius * Mathf.Sin(nextAngle);

            // Outer edge vertices (these will now have the same Z as inner vertices)
            float outerX1 = outerRadius * Mathf.Cos(angle);
            float outerY1 = outerRadius * Mathf.Sin(angle);
            float outerX2 = outerRadius * Mathf.Cos(nextAngle);
            float outerY2 = outerRadius * Mathf.Sin(nextAngle);

            // Create the four vertices for this segment
            // 1. Inner current
            vertexBuffer[vertexIndex++] = new VertexData
            {
                position = new Vector3(innerX1, innerY1, innerZ1),
                normal = CalculateNormal(innerX1, innerY1, innerZ1, outerX1, outerY1, innerZ1, innerX2, innerY2, innerZ2),
                color = new Color32(255, 255, 255, 255),
                uv = new Vector2(0.5f + innerX1 / outerRadius / 2, 0.5f + innerY1 / outerRadius / 2)
            };

            // 2. Outer current
            vertexBuffer[vertexIndex++] = new VertexData
            {
                position = new Vector3(outerX1, outerY1, innerZ1), // Same Z as inner
                normal = new Vector3(0, 0, 1),
                color = new Color32(255, 255, 255, 255),
                uv = new Vector2(0.5f + outerX1 / outerRadius / 2, 0.5f + outerY1 / outerRadius / 2)
            };

            // 3. Inner next
            vertexBuffer[vertexIndex++] = new VertexData
            {
                position = new Vector3(innerX2, innerY2, innerZ2),
                normal = CalculateNormal(innerX2, innerY2, innerZ2, outerX2, outerY2, innerZ2, innerX1, innerY1, innerZ1),
                color = new Color32(255, 255, 255, 255),
                uv = new Vector2(0.5f + innerX2 / outerRadius / 2, 0.5f + innerY2 / outerRadius / 2)
            };

            // 4. Outer next
            vertexBuffer[vertexIndex++] = new VertexData
            {
                position = new Vector3(outerX2, outerY2, innerZ2), // Same Z as inner
                normal = new Vector3(0, 0, 1),
                color = new Color32(255, 255, 255, 255),
                uv = new Vector2(0.5f + outerX2 / outerRadius / 2, 0.5f + outerY2 / outerRadius / 2)
            };
        }

        // Calculate the total number of triangles
        // Each segment creates 2 triangles
        int totalTriangles = actualSegments * 6; // 2 triangles × 3 indices × segments

        // Set the index buffer parameters
        meshData.SetIndexBufferParams(totalTriangles, IndexFormat.UInt32);
        var indexData = meshData.GetIndexData<int>();

        // Create triangles
        int triangleIndex = 0;
        for (int i = 0; i < actualSegments; i++)
        {
            int baseIndex = i * 4;
            int nextBaseIndex = ((i + 1) % actualSegments) * 4;

            // For segments that aren't the last one
            // Triangle 1: Inner current, Outer current, Inner next
            indexData[triangleIndex++] = baseIndex;     // Inner current
            indexData[triangleIndex++] = baseIndex + 1; // Outer current
            indexData[triangleIndex++] = baseIndex + 2; // Inner next

            // Triangle 2: Inner next, Outer current, Outer next
            indexData[triangleIndex++] = baseIndex + 2; // Inner next
            indexData[triangleIndex++] = baseIndex + 1; // Outer current
            indexData[triangleIndex++] = baseIndex + 3; // Outer next
        }

        // Set the submesh
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, totalTriangles, MeshTopology.Triangles));

        // Apply the mesh data
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh);

        // Recalculate the normals to make lighting look better
        _mesh.RecalculateNormals();
        _mesh.RecalculateTangents();
        _mesh.RecalculateBounds();
        _mesh.Optimize();
    }
    
    // Helper method to calculate normals for inner edge vertices
    private Vector3 CalculateNormal(float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3)
    {
        // Calculate two edges of the triangle
        Vector3 edge1 = new Vector3(x2 - x1, y2 - y1, z2 - z1);
        Vector3 edge2 = new Vector3(x3 - x1, y3 - y1, z3 - z1);
        
        // Calculate the normal using cross product
        Vector3 normal = Vector3.Cross(edge1, edge2).normalized;
        
        // Ensure the normal points outward (this might need adjustment based on your winding order)
        if (normal.z < 0)
            normal = -normal;
            
        return normal;
    }
}
