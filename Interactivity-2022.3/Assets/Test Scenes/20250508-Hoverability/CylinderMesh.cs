using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))] 
public class CylinderMesh : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    
    public float height = 1f;
    public float radius = 0.5f;
    public float topFactor = 1f; // 1 = cylinder, 0 = cone, other values for truncated cone
    public int segments = 32;

    private Mesh mesh;
    private bool isDirty = true;

    private void OnEnable()
    {
        GenerateMesh();
    }

    private void OnValidate()
    {
        isDirty = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (isDirty)
        {
            GenerateMesh();
            isDirty = false;
        }
    }
    
    private void GenerateMesh()
    {
        if (!mesh)
        {
            mesh = new Mesh();
            mesh.name = "Procedural Cylinder";
            GetComponent<MeshFilter>().sharedMesh = mesh;
            mesh.hideFlags = HideFlags.DontSave;
        }
            
        mesh.Clear();
        
        // Calculate vertex count based on the new approach:
        // - 1 center vertex for bottom cap
        // - 1 center vertex for top cap
        // - segments vertices for bottom cap edge
        // - segments vertices for top cap edge
        // - segments vertices for bottom side edge
        // - segments vertices for top side edge
        int vertexCount = 2 + segments * 4;
        
        // Triangle count:
        // - segments triangles for bottom cap
        // - segments triangles for top cap
        // - segments * 2 triangles for sides (2 per quad)
        int triangleCount = segments * 4;
        
        // Create vertex arrays
        var positions = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
        var normals = new NativeArray<Vector3>(vertexCount, Allocator.Temp);
        var uvs = new NativeArray<Vector2>(vertexCount, Allocator.Temp);
        var indices = new NativeArray<int>(triangleCount * 3, Allocator.Temp);
        
        // Bottom center vertex (index 0)
        positions[0] = new Vector3(0, 0, 0);
        normals[0] = Vector3.down;
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        // Top center vertex (index 1)
        positions[1] = new Vector3(0, height, 0);
        normals[1] = Vector3.up;
        uvs[1] = new Vector2(0.5f, 0.5f);
        
        // Generate the bottom cap vertices with downward normals (indices 2 to 2+segments-1)
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            
            int vertexIndex = i + 2;
            
            positions[vertexIndex] = new Vector3(x, 0, z);
            normals[vertexIndex] = Vector3.down; // Bottom cap normals point down
            uvs[vertexIndex] = new Vector2((x / radius + 1) * 0.5f, (z / radius + 1) * 0.5f);
        }
        
        // Generate the top cap vertices with upward normals (indices 2+segments to 2+segments*2-1)
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * radius * topFactor;
            float z = Mathf.Sin(angle) * radius * topFactor;
            
            int vertexIndex = i + 2 + segments;
            
            positions[vertexIndex] = new Vector3(x, height, z);
            normals[vertexIndex] = Vector3.up; // Top cap normals point up
            uvs[vertexIndex] = new Vector2((x / (radius * topFactor) + 1) * 0.5f, (z / (radius * topFactor) + 1) * 0.5f);
        }
        
        // Generate the bottom side vertices with outward normals (indices 2+segments*2 to 2+segments*3-1)
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            
            int vertexIndex = i + 2 + segments * 2;
            
            positions[vertexIndex] = new Vector3(x, 0, z);
            normals[vertexIndex] = new Vector3(x, 0, z).normalized; // Side normals point outward
            uvs[vertexIndex] = new Vector2((float)i / segments, 0);
        }
        
        // Generate the top side vertices with outward normals (indices 2+segments*3 to 2+segments*4-1)
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle) * radius * topFactor;
            float z = Mathf.Sin(angle) * radius * topFactor;
            
            int vertexIndex = i + 2 + segments * 3;
            
            positions[vertexIndex] = new Vector3(x, height, z);
            
            // For the cone shape, normals need special calculation
            if (topFactor == 0) // Pure cone
            {
                // Cone vertex normal calculation - blend between side direction and up
                Vector3 sideDir = new Vector3(x, 0, z).normalized;
                Vector3 upDir = Vector3.up;
                normals[vertexIndex] = Vector3.Slerp(sideDir, upDir, 0.5f).normalized;
            }
            else if (topFactor != 1f) // Truncated cone
            {
                // Get the corresponding bottom vertex position
                float bottomX = Mathf.Cos(angle) * radius;
                float bottomZ = Mathf.Sin(angle) * radius;
                
                // Calculate direction from bottom to top vertex
                Vector3 bottomPos = new Vector3(bottomX, 0, bottomZ);
                Vector3 topPos = new Vector3(x, height, z);
                Vector3 sideDir = (topPos - bottomPos).normalized;
                
                // Calculate tangent around the cone at this point
                // For correct outward-facing normals, tangent should go counter-clockwise
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                
                // Normal is perpendicular to both the side direction and the tangent
                // Using sideDir first and tangent second gives outward-facing normals
                normals[vertexIndex] = Vector3.Cross(sideDir, tangent).normalized;
            }
            else // Perfect cylinder
            {
                // For perfect cylinder, normal is simply the radial direction
                normals[vertexIndex] = new Vector3(x, 0, z).normalized;
            }
            
            uvs[vertexIndex] = new Vector2((float)i / segments, 1);
        }
        
        // Same normal calculation logic needs to be applied to the bottom side vertices
        // Update the bottom side vertices normal calculation
        for (int i = 0; i < segments; i++)
        {
            float angle = 2 * Mathf.PI * i / segments;
            float bottomX = Mathf.Cos(angle) * radius;
            float bottomZ = Mathf.Sin(angle) * radius;
            
            int vertexIndex = i + 2 + segments * 2;
            
            if (topFactor != 1f && topFactor != 0f) // Truncated cone
            {
                // Get the corresponding top vertex position
                float topX = Mathf.Cos(angle) * radius * topFactor;
                float topZ = Mathf.Sin(angle) * radius * topFactor;
                
                // Calculate direction from bottom to top vertex
                Vector3 bottomPos = new Vector3(bottomX, 0, bottomZ);
                Vector3 topPos = new Vector3(topX, height, topZ);
                Vector3 sideDir = (topPos - bottomPos).normalized;
                
                // Calculate tangent around the cone at this point
                // For correct outward-facing normals, tangent should go counter-clockwise
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                
                // Normal is perpendicular to both the side direction and the tangent
                // Using sideDir first and tangent second gives outward-facing normals
                normals[vertexIndex] = Vector3.Cross(sideDir, tangent).normalized;
            }
            else if (topFactor == 0f) // Pure cone
            {
                // For pure cone bottom vertices, calculate direction to top
                Vector3 bottomPos = new Vector3(bottomX, 0, bottomZ);
                Vector3 topPos = new Vector3(0, height, 0); // Top center point for cone
                Vector3 sideDir = (topPos - bottomPos).normalized;
                
                // Calculate tangent around the cone at this point
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));
                
                // Normal is perpendicular to both the side direction and the tangent
                normals[vertexIndex] = Vector3.Cross(sideDir, tangent).normalized;
            }
            else // Perfect cylinder
            {
                // For perfect cylinder, normal is simply the radial direction
                normals[vertexIndex] = new Vector3(bottomX, 0, bottomZ).normalized;
            }
        }
        
        int triIndex = 0;
        
        // Bottom cap triangles
        int bottomCapStart = 2;
        for (int i = 0; i < segments; i++)
        {
            indices[triIndex++] = 0; // Bottom center
            indices[triIndex++] = bottomCapStart + i; // Current vertex
            indices[triIndex++] = bottomCapStart + (i + 1) % segments; // Next vertex
        }
        
        // Top cap triangles
        int topCapStart = 2 + segments;
        for (int i = 0; i < segments; i++)
        {
            indices[triIndex++] = 1; // Top center
            indices[triIndex++] = topCapStart + (i + 1) % segments; // Next vertex
            indices[triIndex++] = topCapStart + i; // Current vertex
        }
        
        // Side triangles
        int bottomSideStart = 2 + segments * 2;
        int topSideStart = 2 + segments * 3;
        for (int i = 0; i < segments; i++)
        {
            int bottomIndex = bottomSideStart + i;
            int topIndex = topSideStart + i;
            int nextBottomIndex = bottomSideStart + (i + 1) % segments;
            int nextTopIndex = topSideStart + (i + 1) % segments;
            
            // First triangle
            indices[triIndex++] = bottomIndex;
            indices[triIndex++] = topIndex;
            indices[triIndex++] = nextBottomIndex;
            
            // Second triangle
            indices[triIndex++] = nextBottomIndex;
            indices[triIndex++] = topIndex;
            indices[triIndex++] = nextTopIndex;
        }
        
        // Apply mesh data using the new Mesh API
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];
        
        // Define the vertex buffer layout with a SINGLE interleaved stream
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
        
        meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
        vertexAttributes.Dispose();
        
        // Get a single vertex buffer stream
        var vertexData = meshData.GetVertexData<float>(0);
        
        // Copy vertex data to the interleaved stream
        int stride = 3 + 3 + 2; // 3 floats for position, 3 for normal, 2 for UV
        for (int i = 0; i < vertexCount; i++)
        {
            int offset = i * stride;
            
            // Position (3 floats)
            vertexData[offset + 0] = positions[i].x;
            vertexData[offset + 1] = positions[i].y;
            vertexData[offset + 2] = positions[i].z;
            
            // Normal (3 floats)
            vertexData[offset + 3] = normals[i].x;
            vertexData[offset + 4] = normals[i].y;
            vertexData[offset + 5] = normals[i].z;
            
            // UV (2 floats)
            vertexData[offset + 6] = uvs[i].x;
            vertexData[offset + 7] = uvs[i].y;
        }
        
        // Set index buffer parameters and data
        meshData.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
        var meshIndices = meshData.GetIndexData<int>();
        for (int i = 0; i < indices.Length; i++)
        {
            meshIndices[i] = indices[i];
        }
        
        // Set submesh
        meshData.subMeshCount = 1;
        var bounds = new Bounds(Vector3.up * height * 0.5f, new Vector3(radius * 2 * Mathf.Max(1, topFactor), height, radius * 2 * Mathf.Max(1, topFactor)));
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles)
        {
            bounds = bounds,
            vertexCount = vertexCount
        });
        
        // Apply and dispose
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        mesh.RecalculateBounds();
        
        // Clean up native arrays
        positions.Dispose();
        normals.Dispose();
        uvs.Dispose();
        indices.Dispose();
    }
}
