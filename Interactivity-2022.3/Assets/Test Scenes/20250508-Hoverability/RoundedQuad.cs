using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoundedQuad : MonoBehaviour
{
    // Create a properly aligned interleaved buffer structure
    [StructLayout(LayoutKind.Sequential)]
    private struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Color32 color;
        public Vector2 uv;
    }

    [SerializeField, Min(0.000f)]
    private float _cornerRadius = 0.1f;
    
    [SerializeField, Range(4, 32)]
    private int _cornerSegments = 8;
    
    [SerializeField]
    private Color _color = Color.white;
    
    [SerializeField, Min(0.000f)]
    private float _outlineWidth = 0.01f;
    
    [SerializeField]
    private Color _outlineColor = Color.black;
    
    [SerializeField]
    private Color _outlineInnerColor = Color.black;
    
    [SerializeField, Range(1, 5)]
    private int _outlineLoopCount = 1;
    
    private Mesh _mesh;
    private Vector3 _lastScale;
    private float _lastCornerRadius;
    private int _lastCornerSegments;
    private Color _lastColor;
    private float _lastOutlineWidth;
    private Color _lastOutlineColor;
    private Color _lastOutlineInnerColor;
    private int _lastOutlineLoopCount;
    
    private void OnEnable()
    {
        _mesh = new Mesh();
        _mesh.name = "RoundedQuad";
        _mesh.hideFlags = HideFlags.DontSave; // Mark the mesh as DontSave to prevent serialization
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        
        _lastScale = transform.lossyScale;
        _lastCornerRadius = _cornerRadius;
        _lastCornerSegments = _cornerSegments;
        _lastColor = _color;
        _lastOutlineWidth = _outlineWidth;
        _lastOutlineColor = _outlineColor;
        _lastOutlineInnerColor = _outlineInnerColor;
        _lastOutlineLoopCount = _outlineLoopCount;
        
        GenerateMesh(1f, _color.linear, _color.linear);
    }
    
    private void OnDisable()
    {
        if (_mesh != null)
        {
            DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
    
    private void Update()
    {
        bool needsUpdate = false;
        
        if (transform.lossyScale != _lastScale)
        {
            needsUpdate = true;
            _lastScale = transform.lossyScale;
        }
        
        if (_cornerRadius != _lastCornerRadius || _cornerSegments != _lastCornerSegments)
        {
            needsUpdate = true;
            _lastCornerRadius = _cornerRadius;
            _lastCornerSegments = _cornerSegments;
        }
        
        if (_color != _lastColor)
        {
            needsUpdate = true;
            _lastColor = _color;
        }
        
        if (_outlineWidth != _lastOutlineWidth || _outlineColor != _lastOutlineColor || _outlineInnerColor != _lastOutlineInnerColor || _outlineLoopCount != _lastOutlineLoopCount)
        {
            needsUpdate = true;
            _lastOutlineWidth = _outlineWidth;
            _lastOutlineColor = _outlineColor;
            _lastOutlineInnerColor = _outlineInnerColor;
            _lastOutlineLoopCount = _outlineLoopCount;
        }
        
        if (needsUpdate)
        {
            GenerateMesh(1f, _color.linear, _color.linear);
        }
    }
    
    private void GenerateMesh(float scale, Color innerColor, Color outerColor)
    {
        if (_mesh == null)
            return;
        
        // Apply world scale to the corner radius to counter its effect - handle X and Y separately
        Vector3 worldScale = transform.lossyScale;
        float scaleX = Mathf.Abs(worldScale.x);
        float scaleY = Mathf.Abs(worldScale.y);
        
        // Fixed size of 1x1 unit quad
        float halfWidth = scale * 0.5f;
        float halfHeight = scale * 0.5f;
        
        float minRadius = Mathf.Min(scaleX * 0.5f, scaleY * 0.5f);
        float clampedCornerRadius = Mathf.Min(_cornerRadius, minRadius);
        
        // SPECIAL CASE: When cornerRadius is 0, create a simple quad
        if (clampedCornerRadius <= 0.0001f)
        {
            CreateSimpleQuad(scale, innerColor);
            return;
        }
        
        // SPECIAL CASE: When cornerRadius equals the minimum radius (fully rounded on shorter side)
        bool isMaximumRounding = Mathf.Approximately(clampedCornerRadius, minRadius);
        bool isWidthConstrained = false;
        bool isHeightConstrained = false;
        
        float innerCornerX = halfWidth - clampedCornerRadius / scaleX;
        float innerCornerY = halfHeight - clampedCornerRadius / scaleY;
        
        // For maximum rounding case, adjust inner corners
        if (isMaximumRounding)
        {
            isWidthConstrained = scaleX <= scaleY;
            isHeightConstrained = scaleY <= scaleX;
            
            bool isCircle = isWidthConstrained && isHeightConstrained;
            
            if (isWidthConstrained)
            {
                innerCornerX = 0;
            }
            if (isHeightConstrained)
            {
                innerCornerY = 0;
            }
        }
        
        // Center + 4 corner centers + corner segments
        int totalVertices = 5 + (_cornerSegments * 4);
        
        // Determine if we need to add outline
        bool generateOutline = _outlineWidth > 0.001f;
        int outlineVertexCount = 0;
        int actualLoopCount = 0;
        
        if (generateOutline)
        {
            // Cap the loop count to valid range
            actualLoopCount = Mathf.Max(1, _outlineLoopCount);
            // We need twice the edge vertices for each loop - once for inner edge and once for outer edge
            outlineVertexCount = _cornerSegments * 4 * 2 * actualLoopCount; 
        }
        
        // Use the new Mesh API with NativeArrays
        var vertices = new NativeArray<Vector3>(totalVertices + outlineVertexCount, Allocator.Temp);
        var normals = new NativeArray<Vector3>(totalVertices + outlineVertexCount, Allocator.Temp);
        var uvs = new NativeArray<Vector2>(totalVertices + outlineVertexCount, Allocator.Temp);
        var colors = new NativeArray<Color32>(totalVertices + outlineVertexCount, Allocator.Temp);
        
        // Create standard mesh vertices first
        
        // Center vertex positions
        vertices[0] = new Vector3(0, 0, 0);
        normals[0] = Vector3.forward;
        uvs[0] = new Vector2(0.5f, 0.5f);
        colors[0] = innerColor;
        
        // Inner corner positions - these define where the rounded corners start
        // The outer edges of the quad are always at ±halfWidth and ±halfHeight
        vertices[1] = new Vector3(innerCornerX, innerCornerY, 0);
        vertices[2] = new Vector3(-innerCornerX, innerCornerY, 0);
        vertices[3] = new Vector3(-innerCornerX, -innerCornerY, 0);
        vertices[4] = new Vector3(innerCornerX, -innerCornerY, 0);
        
        for (int i = 1; i <= 4; i++)
        {
            normals[i] = Vector3.forward;
            colors[i] = innerColor;
            
            // Calculate UVs for corner centers
            uvs[i] = new Vector2(
                Mathf.InverseLerp(-halfWidth, halfWidth, vertices[i].x),
                Mathf.InverseLerp(-halfHeight, halfHeight, vertices[i].y)
            );
        }
        
        // Generate vertices for the rounded corners
        int vertexIndex = 5;
        
        // Store the outer edge vertices indices for the outline
        var outerEdgeVertices = new List<int>(_cornerSegments * 4);
        
        // Generate the corners
        for (int corner = 0; corner < 4; corner++)
        {
            // Correct angle calculations for each corner
            // We need to start at the right angle for each corner quadrant
            float startAngle = 0;
            if (corner == 0) startAngle = 0; // Top-right: start at 0 degrees
            if (corner == 1) startAngle = 90 * Mathf.Deg2Rad; // Top-left: start at 90 degrees
            if (corner == 2) startAngle = 180 * Mathf.Deg2Rad; // Bottom-left: start at 180 degrees
            if (corner == 3) startAngle = 270 * Mathf.Deg2Rad; // Bottom-right: start at 270 degrees
            
            Vector3 cornerCenter = vertices[corner + 1];
            
            for (int i = 0; i < _cornerSegments; i++)
            {
                // Create a proper arc that spans exactly 90 degrees
                float angle = startAngle + (i * 90f * Mathf.Deg2Rad / (_cornerSegments - 1));
                
                // Calculate the corner vertex position properly
                float x = cornerCenter.x + Mathf.Cos(angle) * clampedCornerRadius / scaleX;
                float y = cornerCenter.y + Mathf.Sin(angle) * clampedCornerRadius / scaleY;
                
                vertices[vertexIndex] = new Vector3(x, y, 0);
                normals[vertexIndex] = Vector3.forward;
                colors[vertexIndex] = outerColor;
                uvs[vertexIndex] = new Vector2(
                    Mathf.InverseLerp(-halfWidth, halfWidth, x),
                    Mathf.InverseLerp(-halfHeight, halfHeight, y)
                );
                
                // Store this as an outer edge vertex for the outline
                outerEdgeVertices.Add(vertexIndex);
                
                vertexIndex++;
            }
        }
        
        // Calculate appropriate triangle count based on geometry
        // For maximum rounding case, we'll skip some triangles
        int centerTriangles = isMaximumRounding ? 0 : 4;
        int cornerTrianglesPerCorner = (_cornerSegments - 1);
        
        // Calculate connecting triangles based on the constraint
        int connectingTriangles;
        if (isMaximumRounding)
        {
            if (isWidthConstrained && isHeightConstrained)
            {
                // No connecting triangles needed
                connectingTriangles = 0;
            }
            else
            {
                // Only need vertical connections (top and bottom)
                connectingTriangles = 4; // 2 triangles (1 quad) * 2 connections
            }
        }
        else
        {
            // Need all connections (4 corners)
            connectingTriangles = 8; // 2 triangles (1 quad) * 4 connections
        }
        
        int totalTriangles = (centerTriangles + 4 * cornerTrianglesPerCorner + connectingTriangles) * 3;
        
        // Calculate outline triangle count
        int outlineTriangles = 0;
        if (generateOutline)
        {
            // Each loop needs _cornerSegments*4*6 triangles (2 triangles per segment, 3 indices per triangle)
            outlineTriangles = _cornerSegments * 4 * 6 * actualLoopCount;
        }
        
        var triangles = new NativeArray<int>(totalTriangles, Allocator.Temp);
        var outlineTrianglesArray = generateOutline ? 
            new NativeArray<int>(outlineTriangles, Allocator.Temp) : 
            new NativeArray<int>(0, Allocator.Temp);
        
        // Generate triangles
        int triangleIndex = 0;
        
        // Center triangles to corner centers - skip in max rounding case
        if (!isMaximumRounding)
        {
            for (int i = 0; i < 4; i++)
            {
                triangles[triangleIndex++] = 0;  // Center vertex
                triangles[triangleIndex++] = i + 1;  // This corner center
                triangles[triangleIndex++] = ((i + 1) % 4) + 1;  // Next corner center
            }
        }
        
        // Corner triangles
        int cornerStartVertex = 5;
        for (int corner = 0; corner < 4; corner++)
        {
            int cornerCenter = corner + 1;
            int nextCorner = (corner + 1) % 4;
            int nextCornerCenter = nextCorner + 1;
            int nextCornerFirstVertex = 5 + (nextCorner * _cornerSegments);
            
            // Fan triangles for each corner segment
            for (int i = 0; i < _cornerSegments - 1; i++)
            {
                // Triangle between corner center, current segment and next segment
                triangles[triangleIndex++] = cornerCenter;
                triangles[triangleIndex++] = cornerStartVertex + i;
                triangles[triangleIndex++] = cornerStartVertex + i + 1;
            }
            
            // For max rounding case, we only need some of the connecting quads
            // Skip connections that would cross through the center
            bool skipConnection = false;
            if (isMaximumRounding)
            {
                // In circle case (both constraints true), we skip all connecting triangles
                bool isCircle = isWidthConstrained && isHeightConstrained;
                if (isCircle)
                {
                    skipConnection = true;
                }
                else if (!isWidthConstrained)
                {
                    // Skip left-to-right connections (connections for corners 1->2 and 3->0)
                    skipConnection = (corner == 1 || corner == 3);
                }
                else
                {
                    // Skip top-to-bottom connections (connections for corners 0->1 and 2->3)
                    skipConnection = (corner == 0 || corner == 2);
                }
            }
            
            if (!skipConnection)
            {
                // Connect the last point of this corner to the first point of the next corner
                // using TWO triangles to form a quad between corners
                int lastPointOfCorner = cornerStartVertex + _cornerSegments - 1;
                int firstPointOfNextCorner = nextCornerFirstVertex;
                
                // First triangle: last corner point, next corner center, corner center (flipped winding order)
                triangles[triangleIndex++] = lastPointOfCorner;
                triangles[triangleIndex++] = nextCornerCenter;
                triangles[triangleIndex++] = cornerCenter;
                
                // Second triangle: last corner point, first point of next corner, next corner center (flipped winding order)
                triangles[triangleIndex++] = lastPointOfCorner;
                triangles[triangleIndex++] = firstPointOfNextCorner;
                triangles[triangleIndex++] = nextCornerCenter;
            }
            
            cornerStartVertex += _cornerSegments;
        }
        
        // Generate outline mesh if needed
        if (generateOutline)
        {
            // Create the outline vertices using the same angle calculation as the inner vertices
            int outlinesIndex = 0;
            
            // Create multiple outline loops
            for (int loopIndex = 0; loopIndex < actualLoopCount; loopIndex++)
            {
                int currentCorner = 0;
                Vector3 currentCornerCenter = Vector3.zero;
                
                // Calculate the base inner edge - for the first loop it's the base object edge
                // For subsequent loops, it's the outer edge of the previous loop
                var innerEdgeVertices = new List<int>(_cornerSegments * 4);
                int innerEdgeStartIndex;
                
                if (loopIndex == 0)
                {
                    // For the first loop, inner edge is the original mesh edge (duplicate vertices with different color)
                    innerEdgeStartIndex = totalVertices + (loopIndex * outerEdgeVertices.Count * 2);
                    
                    // Generate inner outline edge vertices (duplicates of the outer edge vertices)
                    for (int i = 0; i < outerEdgeVertices.Count; i++)
                    {
                        int edgeVertexIndex = outerEdgeVertices[i];
                        
                        // Create a new vertex with the same position but different color
                        int innerOutlineIndex = innerEdgeStartIndex + i;
                        vertices[innerOutlineIndex] = vertices[edgeVertexIndex];
                        normals[innerOutlineIndex] = normals[edgeVertexIndex];
                        uvs[innerOutlineIndex] = uvs[edgeVertexIndex];
                        colors[innerOutlineIndex] = _outlineInnerColor; // Apply inner outline color
                        
                        // Store inner outline vertex index for triangulation
                        innerEdgeVertices.Add(innerOutlineIndex);
                    }
                }
                else
                {
                    // For subsequent loops, inner edge is the outer edge of the previous loop
                    // Use the outer vertices from the previous loop
                    int previousLoopOuterStartIndex = totalVertices + ((loopIndex - 1) * outerEdgeVertices.Count * 2) + outerEdgeVertices.Count;
                    innerEdgeStartIndex = totalVertices + (loopIndex * outerEdgeVertices.Count * 2);
                    
                    // Copy the outer vertices from the previous loop as our inner vertices
                    for (int i = 0; i < outerEdgeVertices.Count; i++)
                    {
                        int previousOuterIndex = previousLoopOuterStartIndex + i;
                        int currentInnerIndex = innerEdgeStartIndex + i;
                        
                        // Copy from previous outer to current inner
                        vertices[currentInnerIndex] = vertices[previousOuterIndex];
                        normals[currentInnerIndex] = normals[previousOuterIndex];
                        uvs[currentInnerIndex] = uvs[previousOuterIndex];
                        
                        // Use a color between inner and outer color based on loop index
                        float t = (float)(loopIndex) / actualLoopCount;
                        t = Mathf.Sqrt(t);
                        colors[currentInnerIndex] = Color32.Lerp(_outlineInnerColor, _outlineColor, t);
                        
                        // Store inner outline vertex index for triangulation
                        innerEdgeVertices.Add(currentInnerIndex);
                    }
                }
                
                // Now create outer edge vertices for this outline loop
                int outerEdgeStartIndex = innerEdgeStartIndex + outerEdgeVertices.Count;
                var outerOutlineVertices = new List<int>(_cornerSegments * 4);
                
                // Calculate the width of this outline loop (increasing with each loop)
                float loopOutlineWidth = _outlineWidth * (loopIndex + 1) / actualLoopCount;
                
                // Generate outline vertices following the same angle logic as the inner mesh
                for (int i = 0; i < outerEdgeVertices.Count; i++)
                {
                    Vector3 basePos = vertices[outerEdgeVertices[i]];
                    Vector3 normal = normals[outerEdgeVertices[i]];
                    
                    // Determine which corner we're in and get its center
                    int segmentInCorner = i % _cornerSegments;
                    if (segmentInCorner == 0) {
                        currentCorner = i / _cornerSegments;
                        currentCornerCenter = vertices[currentCorner + 1]; // Corner center is at index corner+1
                    }
                    
                    // Calculate the angle for this vertex the same way we did for the inner vertices
                    float startAngle = 0;
                    if (currentCorner == 0) startAngle = 0; // Top-right: start at 0 degrees
                    if (currentCorner == 1) startAngle = 90 * Mathf.Deg2Rad; // Top-left: start at 90 degrees
                    if (currentCorner == 2) startAngle = 180 * Mathf.Deg2Rad; // Bottom-left: start at 180 degrees
                    if (currentCorner == 3) startAngle = 270 * Mathf.Deg2Rad; // Bottom-right: start at 270 degrees
                    
                    float angle = startAngle + (segmentInCorner * 90f * Mathf.Deg2Rad / (_cornerSegments - 1));
                    
                    // Calculate outline vertex position using the same corner center and angle as inner vertex
                    // but with increased radius - each loop gets wider
                    float outlineRadius = clampedCornerRadius + (loopOutlineWidth / Mathf.Min(scaleX, scaleY));
                    float x = currentCornerCenter.x + Mathf.Cos(angle) * outlineRadius / scaleX;
                    float y = currentCornerCenter.y + Mathf.Sin(angle) * outlineRadius / scaleY;
                    
                    // Set the vertex
                    int outlineVertIndex = outerEdgeStartIndex + i;
                    vertices[outlineVertIndex] = new Vector3(x, y, 0);
                    normals[outlineVertIndex] = normal;
                    
                    // Use gradient color between inner and outer based on loop index 
                    float t = (float)(loopIndex + 1) / actualLoopCount;
                    t = Mathf.Sqrt(t);
                    colors[outlineVertIndex] = Color32.Lerp(_outlineInnerColor, _outlineColor, t);
                    
                    uvs[outlineVertIndex] = new Vector2(
                        Mathf.InverseLerp(-halfWidth - loopOutlineWidth, halfWidth + loopOutlineWidth, x),
                        Mathf.InverseLerp(-halfHeight - loopOutlineWidth, halfHeight + loopOutlineWidth, y)
                    );
                    
                    // Store outer outline vertex index for triangulation
                    outerOutlineVertices.Add(outlineVertIndex);
                }
                
                // Create outline triangles by connecting inner and outer vertices
                for (int i = 0; i < innerEdgeVertices.Count; i++)
                {
                    int nextIndex = (i + 1) % innerEdgeVertices.Count;
                    
                    // First triangle of the quad
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[i];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[i];
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[nextIndex];
                    
                    // Second triangle of the quad
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[nextIndex];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[i];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[nextIndex];
                }
            }
        }
        
        // Clear the mesh and assign the new data
        _mesh.Clear();
        
        // Set the mesh data using the new API
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];
        
        // Set vertex buffer - defining a single interleaved buffer with all vertex attributes
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
        vertexAttributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
        
        // Set the total vertex count (base mesh + outline if enabled)
        meshData.SetVertexBufferParams(totalVertices + outlineVertexCount, vertexAttributes);
        vertexAttributes.Dispose();
        
        // Get the vertex buffer
        var vertexBuffer = meshData.GetVertexData<VertexData>(0);
        for (int i = 0; i < totalVertices + outlineVertexCount; i++)
        {
            vertexBuffer[i] = new VertexData
            {
                position = vertices[i],
                normal = normals[i],
                color = colors[i],
                uv = uvs[i]
            };
        }
        
        // Set index buffer size to include both the main mesh and outline
        int totalIndices = triangleIndex + (generateOutline ? outlineTriangles : 0);
        meshData.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);
        var indexData = meshData.GetIndexData<int>();
        
        // Add main mesh triangles
        for (int i = 0; i < triangleIndex; i++)
        {
            indexData[i] = triangles[i];
        }
        
        // Add outline triangles
        if (generateOutline)
        {
            for (int i = 0; i < outlineTriangles; i++)
            {
                indexData[triangleIndex + i] = outlineTrianglesArray[i];
            }
        }
        
        // Set submeshes (main quad and outline)
        meshData.subMeshCount = generateOutline ? 2 : 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleIndex, MeshTopology.Triangles));
        
        if (generateOutline)
        {
            meshData.SetSubMesh(1, new SubMeshDescriptor(triangleIndex, outlineTriangles, MeshTopology.Triangles));
        }
        
        // Apply the mesh data
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh);
        
        // Set bounds, optimize the mesh
        _mesh.RecalculateBounds();
        _mesh.Optimize();
        
        // Clean up native arrays
        vertices.Dispose();
        normals.Dispose();
        uvs.Dispose();
        colors.Dispose();
        triangles.Dispose();
        outlineTrianglesArray.Dispose();
    }
    
    // Creates a simple quad mesh when cornerRadius is 0
    private void CreateSimpleQuad(float scale, Color color)
    {
        // Fixed size of 1x1 unit quad
        float halfWidth = scale * 0.5f;
        float halfHeight = scale * 0.5f;
        
        // Determine if we need to add outline
        bool generateOutline = _outlineWidth > 0.001f;
        int actualLoopCount = generateOutline ? Mathf.Max(1, _outlineLoopCount) : 0;
        int totalVertices = 4 + (generateOutline ? 4 * actualLoopCount : 0); // 4 base vertices + 4 outline vertices per loop
        
        // Clear the mesh
        _mesh.Clear();
        
        // Create a simple quad with 4 vertices and 2 triangles
        var meshDataArray = Mesh.AllocateWritableMeshData(1);
        var meshData = meshDataArray[0];
        
        // Set vertex buffer
        var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
        vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
        vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
        vertexAttributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
        vertexAttributes[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
        
        // Set buffer size based on whether we're generating an outline
        meshData.SetVertexBufferParams(totalVertices, vertexAttributes);
        vertexAttributes.Dispose();
        
        // Define the four corners of the quad
        var vertexBuffer = meshData.GetVertexData<VertexData>(0);
        
        // Base quad vertices
        // Bottom-left
        vertexBuffer[0] = new VertexData
        {
            position = new Vector3(-halfWidth, -halfHeight, 0),
            normal = Vector3.forward,
            color = color,
            uv = new Vector2(0, 0)
        };
        
        // Top-left
        vertexBuffer[1] = new VertexData
        {
            position = new Vector3(-halfWidth, halfHeight, 0),
            normal = Vector3.forward,
            color = color,
            uv = new Vector2(0, 1)
        };
        
        // Top-right
        vertexBuffer[2] = new VertexData
        {
            position = new Vector3(halfWidth, halfHeight, 0),
            normal = Vector3.forward,
            color = color,
            uv = new Vector2(1, 1)
        };
        
        // Bottom-right
        vertexBuffer[3] = new VertexData
        {
            position = new Vector3(halfWidth, -halfHeight, 0),
            normal = Vector3.forward,
            color = color,
            uv = new Vector2(1, 0)
        };
        
        // Calculate indices count (base quad + outline if enabled)
        int indicesCount = 6; // 6 indices for base quad 
        if (generateOutline) {
            // Each outline loop adds 8 triangles (24 indices)
            indicesCount += 24 * actualLoopCount;
        }
        
        meshData.SetIndexBufferParams(indicesCount, IndexFormat.UInt32);
        var indexData = meshData.GetIndexData<int>();
        
        // Triangle 1
        indexData[0] = 0; // Bottom-left
        indexData[1] = 2; // Top-right
        indexData[2] = 1; // Top-left
        
        // Triangle 2
        indexData[3] = 0; // Bottom-left
        indexData[4] = 3; // Bottom-right
        indexData[5] = 2; // Top-right
        
        // Create outline vertices and triangles if needed
        if (generateOutline)
        {
            // Calculate world scale for outline sizing
            Vector3 worldScale = transform.lossyScale;
            float scaleMinimum = Mathf.Min(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y));
            
            int outlineIndex = 6;
            
            // Create each outline loop
            for (int loopIndex = 0; loopIndex < actualLoopCount; loopIndex++)
            {
                // Calculate the outline width for this loop
                float loopOutlineWidth = _outlineWidth * (loopIndex + 1) / actualLoopCount;
                float outlineOffset = loopOutlineWidth / scaleMinimum;
                
                // Calculate color gradient for this loop
                float t = (float)(loopIndex + 1) / actualLoopCount;
                Color loopColor = Color.Lerp(_outlineInnerColor, _outlineColor, t);
                
                // Current loop's vertex indices (base index for this loop's vertices)
                int loopBaseIndex = 4 + (loopIndex * 4);
                
                // Add outline vertices for this loop
                // Bottom-left outline
                vertexBuffer[loopBaseIndex] = new VertexData
                {
                    position = new Vector3(-halfWidth - outlineOffset, -halfHeight - outlineOffset, 0),
                    normal = Vector3.forward,
                    color = loopColor,
                    uv = new Vector2(0, 0)
                };
                
                // Top-left outline
                vertexBuffer[loopBaseIndex + 1] = new VertexData
                {
                    position = new Vector3(-halfWidth - outlineOffset, halfHeight + outlineOffset, 0),
                    normal = Vector3.forward,
                    color = loopColor,
                    uv = new Vector2(0, 1)
                };
                
                // Top-right outline
                vertexBuffer[loopBaseIndex + 2] = new VertexData
                {
                    position = new Vector3(halfWidth + outlineOffset, halfHeight + outlineOffset, 0),
                    normal = Vector3.forward,
                    color = loopColor,
                    uv = new Vector2(1, 1)
                };
                
                // Bottom-right outline
                vertexBuffer[loopBaseIndex + 3] = new VertexData
                {
                    position = new Vector3(halfWidth + outlineOffset, -halfHeight - outlineOffset, 0),
                    normal = Vector3.forward,
                    color = loopColor,
                    uv = new Vector2(1, 0)
                };
                
                // Source vertices - either the base quad or the previous outline loop
                int sourceBaseIndex = loopIndex == 0 ? 0 : 4 + ((loopIndex - 1) * 4);
                
                // Add outline triangles (8 triangles forming 4 quads around the base quad or previous loop)
                
                // Bottom edge
                indexData[outlineIndex++] = sourceBaseIndex; // Source bottom-left
                indexData[outlineIndex++] = loopBaseIndex; // Current outline bottom-left
                indexData[outlineIndex++] = sourceBaseIndex + 3; // Source bottom-right
                
                indexData[outlineIndex++] = sourceBaseIndex + 3; // Source bottom-right
                indexData[outlineIndex++] = loopBaseIndex; // Current outline bottom-left
                indexData[outlineIndex++] = loopBaseIndex + 3; // Current outline bottom-right
                
                // Left edge
                indexData[outlineIndex++] = sourceBaseIndex; // Source bottom-left
                indexData[outlineIndex++] = sourceBaseIndex + 1; // Source top-left
                indexData[outlineIndex++] = loopBaseIndex; // Current outline bottom-left
                
                indexData[outlineIndex++] = sourceBaseIndex + 1; // Source top-left
                indexData[outlineIndex++] = loopBaseIndex + 1; // Current outline top-left
                indexData[outlineIndex++] = loopBaseIndex; // Current outline bottom-left
                
                // Top edge
                indexData[outlineIndex++] = sourceBaseIndex + 1; // Source top-left
                indexData[outlineIndex++] = sourceBaseIndex + 2; // Source top-right
                indexData[outlineIndex++] = loopBaseIndex + 1; // Current outline top-left
                
                indexData[outlineIndex++] = sourceBaseIndex + 2; // Source top-right
                indexData[outlineIndex++] = loopBaseIndex + 2; // Current outline top-right
                indexData[outlineIndex++] = loopBaseIndex + 1; // Current outline top-left
                
                // Right edge
                indexData[outlineIndex++] = sourceBaseIndex + 2; // Source top-right
                indexData[outlineIndex++] = sourceBaseIndex + 3; // Source bottom-right
                indexData[outlineIndex++] = loopBaseIndex + 2; // Current outline top-right
                
                indexData[outlineIndex++] = sourceBaseIndex + 3; // Source bottom-right
                indexData[outlineIndex++] = loopBaseIndex + 3; // Current outline bottom-right
                indexData[outlineIndex++] = loopBaseIndex + 2; // Current outline top-right
            }
        }
        
        // Set submeshes
        meshData.subMeshCount = generateOutline ? 2 : 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, 6, MeshTopology.Triangles));
        
        if (generateOutline)
        {
            meshData.SetSubMesh(1, new SubMeshDescriptor(6, indicesCount - 6, MeshTopology.Triangles));
        }
        
        // Apply the mesh data
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, _mesh);
        
        // Set bounds, optimize the mesh
        _mesh.RecalculateBounds();
        _mesh.Optimize();
    }
}
