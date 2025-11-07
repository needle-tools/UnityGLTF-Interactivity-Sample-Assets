using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoundedQuad : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Color32 color;
        public Vector2 uv;
    }

    [Min(0.000f)]
    public float _cornerRadius = 0.1f;
    public Color _color = Color.white;
    [Range(3, 32)]
    public int _cornerSegments = 8;

    public Material contentMaterial;
    public Material outlineMaterial;

    [Min(0.000f)]
    public float _outlineWidth = 0.01f;
    public Color _outlineColor = new Color(0,0,0,0);
    public Color _outlineInnerColor = new Color(0,0,0,0.5f);
    [Range(1, 5)]
    public int _outlineLoopCount = 1;
    public Vector2 _outlineOffset = Vector2.zero;
    
    // Outline UV mapping
    public enum OutlineUVMode {
        Default,        // Standard UVs (0-1 mapping from object bounds)
        FlowAlongPath   // UV flows along the outline path (for gradient textures)
    }
    
    public OutlineUVMode _outlineUVMode = OutlineUVMode.FlowAlongPath;
    
    // Debug visualization options
    [HideInInspector]
    public bool _debugDistances = false;
    
    private Mesh _mesh;
    private Vector3 _lastScale;
    private float _lastCornerRadius;
    private int _lastCornerSegments;
    private Color _lastColor;
    private float _lastOutlineWidth;
    private Color _lastOutlineColor;
    private Color _lastOutlineInnerColor;
    private int _lastOutlineLoopCount;
    private Vector2 _lastOutlineOffset;
    private bool _lastDebugDistances;
    private OutlineUVMode _lastOutlineUVMode;
    
    // Helper function to calculate colors for outline vertices using percentage-based approach
    private Color32 CalculateColor(float percentageToEdge)
    {
        // In debug mode, show a gradient from red to green
        if (_debugDistances)
        {
            return new Color32(
                (byte)(255 * (1f - percentageToEdge)), 
                (byte)(255 * percentageToEdge), 
                0,
                255); // Full alpha
        }
        else
        {
            // Simple linear interpolation based on the loop percentage
            percentageToEdge = Mathf.Pow(percentageToEdge, 0.75f);
            return Color32.Lerp(_outlineInnerColor, _outlineColor, percentageToEdge);
        }
    }
    
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
        _lastOutlineOffset = _outlineOffset;
        _lastDebugDistances = _debugDistances;
        _lastOutlineUVMode = _outlineUVMode;
        
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
        
        if (_outlineWidth != _lastOutlineWidth || _outlineColor != _lastOutlineColor || _outlineInnerColor != _lastOutlineInnerColor || _outlineLoopCount != _lastOutlineLoopCount || _outlineOffset != _lastOutlineOffset || _outlineUVMode != _lastOutlineUVMode)
        {
            needsUpdate = true;
            _lastOutlineWidth = _outlineWidth;
            _lastOutlineColor = _outlineColor;
            _lastOutlineInnerColor = _outlineInnerColor;
            _lastOutlineLoopCount = _outlineLoopCount;
            _lastOutlineOffset = _outlineOffset;
            _lastOutlineUVMode = _outlineUVMode;
        }
        
        if (_debugDistances != _lastDebugDistances)
        {
            needsUpdate = true;
            _lastDebugDistances = _debugDistances;
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
        
        // Ensure we have at least 3 segments even with zero corner radius
        int segments = _cornerRadius > 0 ? Mathf.Max(3, _cornerSegments) : 3;
        
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
        int totalVertices = 5 + (segments * 4);
        
        // Determine if we need to add outline
        bool generateOutline = _outlineWidth > 0.001f;
        int outlineVertexCount = 0;
        int actualLoopCount = 0;
        
        if (generateOutline)
        {
            // Cap the loop count to valid range
            actualLoopCount = Mathf.Max(1, _outlineLoopCount);
            // We need twice the edge vertices for each loop - once for inner edge and once for outer edge
            outlineVertexCount = segments * 4 * 2 * actualLoopCount; 
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
        var outerEdgeVertices = new List<int>(segments * 4);
        
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
            
            for (int i = 0; i < segments; i++)
            {
                // Create a proper arc that spans exactly 90 degrees
                float angle = startAngle + (i * 90f * Mathf.Deg2Rad / (segments - 1));
                
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
        int cornerTrianglesPerCorner = (segments - 1);
        
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
            // Each loop needs segments*4*6 triangles (2 triangles per segment, 3 indices per triangle)
            outlineTriangles = segments * 4 * 6 * actualLoopCount;
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
                triangles[triangleIndex++] = ((i + 1) % 4) + 1;  // Next corner center
                triangles[triangleIndex++] = i + 1;  // This corner center
            }
        }
        
        // Corner triangles
        int cornerStartVertex = 5;
        for (int corner = 0; corner < 4; corner++)
        {
            int cornerCenter = corner + 1;
            int nextCorner = (corner + 1) % 4;
            int nextCornerCenter = nextCorner + 1;
            int nextCornerFirstVertex = 5 + (nextCorner * segments);
            
            // Fan triangles for each corner segment
            for (int i = 0; i < segments - 1; i++)
            {
                // Triangle between corner center, current segment and next segment
                // Keep original winding order (already correct)
                triangles[triangleIndex++] = cornerCenter;
                triangles[triangleIndex++] = cornerStartVertex + i + 1;
                triangles[triangleIndex++] = cornerStartVertex + i;
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
                int lastPointOfCorner = cornerStartVertex + segments - 1;
                int firstPointOfNextCorner = nextCornerFirstVertex;
                
                // First triangle: corner center, next corner center, last corner point
                triangles[triangleIndex++] = cornerCenter;
                triangles[triangleIndex++] = nextCornerCenter;
                triangles[triangleIndex++] = lastPointOfCorner;
                
                // Second triangle: next corner center, first point of next corner, last corner point
                triangles[triangleIndex++] = nextCornerCenter;
                triangles[triangleIndex++] = firstPointOfNextCorner;
                triangles[triangleIndex++] = lastPointOfCorner;
            }
            
            cornerStartVertex += segments;
        }
        
        // Generate outline mesh if needed
        if (generateOutline)
        {
            // Create the outline vertices using the same angle calculation as the inner vertices
            int outlinesIndex = 0;

            // Create multiple outline loops
            for (int loopIndex = 0; loopIndex < actualLoopCount; loopIndex++)
            {
                // Calculate inner and outer edge indices for this loop
                int innerEdgeStartIndex = totalVertices + (loopIndex * outerEdgeVertices.Count * 2);
                int outerEdgeStartIndex = innerEdgeStartIndex + outerEdgeVertices.Count;
                
                var innerEdgeVertices = new List<int>(segments * 4);
                var outerOutlineVertices = new List<int>(segments * 4);
                
                // Calculate the width of this outline loop (increasing with each loop)
                float loopOutlineWidth = _outlineWidth * (loopIndex + 1) / actualLoopCount;
                
                // Calculate the percentage for this loop (0-1 from inner to outer edge)
                float loopPercentage = (float)(loopIndex + 1) / actualLoopCount;
                
                // Generate inner and outer edges for this loop
                for (int i = 0; i < outerEdgeVertices.Count; i++)
                {
                    int segmentInCorner = i % segments;
                    int currentCorner = i / segments;
                    Vector3 currentCornerCenter = vertices[currentCorner + 1]; // Corner center is at index corner+1
                    
                    // Calculate angle for this segment
                    float startAngle = 0;
                    if (currentCorner == 0) startAngle = 0; // Top-right: start at 0 degrees
                    if (currentCorner == 1) startAngle = 90 * Mathf.Deg2Rad; // Top-left: start at 90 degrees
                    if (currentCorner == 2) startAngle = 180 * Mathf.Deg2Rad; // Bottom-left: start at 180 degrees
                    if (currentCorner == 3) startAngle = 270 * Mathf.Deg2Rad; // Bottom-right: start at 270 degrees
                    
                    float angle = startAngle + (segmentInCorner * 90f * Mathf.Deg2Rad / (segments - 1));
                    
                    // Calculate outline offset percentage
                    float offsetPercentage = (float)(loopIndex + 1) / actualLoopCount;
                    if (_debugDistances) offsetPercentage = 1;
                    float offsetX = _outlineOffset.x * offsetPercentage * _outlineWidth;
                    float offsetY = _outlineOffset.y * offsetPercentage * _outlineWidth;
                    
                    // Calculate position along outline (normalized 0-1)
                    float outlinePosition = (float)i / outerEdgeVertices.Count;
                    
                    // 1. Create inner vertex for this loop
                    int innerVertexIndex = innerEdgeStartIndex + i;
                    Vector3 sourceVertexPos;
                    Vector3 sourceVertexNormal;
                    Vector2 sourceVertexUV;
                    
                    if (loopIndex == 0) {
                        // For first loop, use the original mesh edge vertex
                        int sourceIndex = outerEdgeVertices[i];
                        sourceVertexPos = vertices[sourceIndex];
                        sourceVertexNormal = normals[sourceIndex];
                        sourceVertexUV = uvs[sourceIndex];
                    } else {
                        // For subsequent loops, use previous loop's outer edge
                        int previousOuterIndex = (innerEdgeStartIndex - outerEdgeVertices.Count) + i;
                        sourceVertexPos = vertices[previousOuterIndex];
                        sourceVertexNormal = normals[previousOuterIndex];
                        sourceVertexUV = uvs[previousOuterIndex];
                    }
                    
                    // Set inner vertex properties
                    vertices[innerVertexIndex] = sourceVertexPos;
                    normals[innerVertexIndex] = sourceVertexNormal;

                    var percentage  = offsetPercentage - 1f / actualLoopCount;
                    
                    // UV mapping depends on the selected mode
                    if (_outlineUVMode == OutlineUVMode.FlowAlongPath)
                    {
                        // U is position along outline path (consistent across all loops)
                        // V is fixed at 0 for inner vertices
                        uvs[innerVertexIndex] = new Vector2(outlinePosition, percentage);
                    }
                    else
                    {
                        // Default UV mapping mode - use source vertex UVs
                        uvs[innerVertexIndex] = sourceVertexUV;
                    }
                    
                    // Calculate color for inner vertex based on percentage
                    colors[innerVertexIndex] = CalculateColor(percentage);
                    
                    innerEdgeVertices.Add(innerVertexIndex);
                    
                    // 2. Create outer vertex for this loop
                    int outlineVertIndex = outerEdgeStartIndex + i;
                    
                    // Calculate radius for this outline loop
                    float outlineRadius = clampedCornerRadius + loopOutlineWidth;
                    
                    // Calculate position
                    float x = currentCornerCenter.x + Mathf.Cos(angle) * outlineRadius / scaleX + offsetX;
                    float y = currentCornerCenter.y + Mathf.Sin(angle) * outlineRadius / scaleY + offsetY;
                    
                    // Set outer vertex properties
                    vertices[outlineVertIndex] = new Vector3(x, y, 0);
                    normals[outlineVertIndex] = sourceVertexNormal;
                    
                    // UV mapping for outer edge
                    if (_outlineUVMode == OutlineUVMode.FlowAlongPath)
                    {
                        // For outer edge, maintain same U coordinate (position along outline)
                        // but set V coordinate to 1 (edge of texture)
                        uvs[outlineVertIndex] = new Vector2(outlinePosition, offsetPercentage);
                    }
                    else
                    {
                        // Default UV mapping mode - standard mapping based on position
                        uvs[outlineVertIndex] = new Vector2(
                            Mathf.InverseLerp(-halfWidth, halfWidth, x),
                            Mathf.InverseLerp(-halfHeight, halfHeight, y)
                        );
                    }
                    
                    // Calculate color for outer vertex
                    colors[outlineVertIndex] = CalculateColor(offsetPercentage);
                    
                    outerOutlineVertices.Add(outlineVertIndex);
                }
                
                // Create outline triangles by connecting inner and outer vertices
                for (int i = 0; i < innerEdgeVertices.Count; i++)
                {
                    int nextIndex = (i + 1) % innerEdgeVertices.Count;
                    
                    // First triangle of the quad - flipped winding order
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[i];
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[nextIndex];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[i];
                    
                    // Second triangle of the quad - flipped winding order
                    outlineTrianglesArray[outlinesIndex++] = innerEdgeVertices[nextIndex];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[nextIndex];
                    outlineTrianglesArray[outlinesIndex++] = outerOutlineVertices[i];
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
        
        // Assign the materials
        var materials = new List<Material>();
        materials.Add(contentMaterial);
        if (generateOutline)
        {
            materials.Add(outlineMaterial);
        }
        GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(RoundedQuad))]
public class RoundedQuadEditor : UnityEditor.Editor
{
    private SerializedProperty cornerRadiusProp;
    private SerializedProperty outlineWidthProp;
    private SerializedProperty outlineOffsetProp;
    
    private void OnEnable()
    {
        cornerRadiusProp = serializedObject.FindProperty("_cornerRadius");
        outlineWidthProp = serializedObject.FindProperty("_outlineWidth");
        outlineOffsetProp = serializedObject.FindProperty("_outlineOffset");
    }
    
    public override void OnInspectorGUI()
    {
        // Draw the default inspector
        base.OnInspectorGUI();
    }
    
    private void OnSceneGUI()
    {
        RoundedQuad quad = (RoundedQuad)target;
        Transform transform = quad.transform;
        
        // Get world space size
        Vector3 worldScale = transform.lossyScale;
        Vector3 worldPos = transform.position;
        
        // Cache some values for handle drawing
        float halfWidth = 0.5f * worldScale.x;
        float halfHeight = 0.5f * worldScale.y;
        float maxPossibleRadius = Mathf.Min(halfWidth, halfHeight);
        float cornerRadius = Mathf.Min(quad._cornerRadius, maxPossibleRadius);
        
        // Ensure handle size is consistent in screen space
        float handleSize = HandleUtility.GetHandleSize(worldPos) * 0.05f;
        
        // Draw corner radius handle
        Vector3 topRightCorner = worldPos + transform.right * halfWidth + transform.up * halfHeight;
        Vector3 topRightInnerCorner = worldPos + transform.right * (halfWidth - cornerRadius) + transform.up * (halfHeight - cornerRadius);
        
        // Draw simple visual indicator for the corner radius
        Handles.DrawWireArc(topRightInnerCorner, transform.forward, transform.right, 90f, cornerRadius);
        
        // Use ScaleValueHandle for direct radius adjustment
        EditorGUI.BeginChangeCheck();
        
        // Position the handle in the middle of the arc (45 degrees)
        Vector3 handlePos = topRightInnerCorner + transform.right * cornerRadius * 0.7071f + transform.up * cornerRadius * 0.7071f;
        
        Handles.color = Color.green;
        float newRadius = Handles.ScaleValueHandle(
            cornerRadius,
            handlePos,
            Quaternion.LookRotation(transform.forward, transform.right + transform.up),
            HandleUtility.GetHandleSize(handlePos) * 0.5f,
            Handles.DotHandleCap,
            0
        );
        
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(quad, "Change Corner Radius");
            // Clamp to maximum possible radius
            cornerRadiusProp.floatValue = Mathf.Max(0, Mathf.Min(newRadius, maxPossibleRadius));
            serializedObject.ApplyModifiedProperties();
        }
        
        // Outline width handle
        EditorGUI.BeginChangeCheck();
        
        // Calculate position for outline width handle
        Vector3 outlineWidthPos = worldPos + transform.right * (halfWidth + quad._outlineWidth * worldScale.x * 0.5f);
        
        // Draw outline width handle
        outlineWidthPos = Handles.Slider(
            outlineWidthPos, 
            transform.right, 
            handleSize, 
            Handles.DotHandleCap, 
            0f
        );
        
        // Apply outline width changes
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(quad, "Change Outline Width");
            
            float newOutlineWidth = ((outlineWidthPos - worldPos).magnitude - halfWidth) * 2 / worldScale.x;
            outlineWidthProp.floatValue = Mathf.Max(0.001f, newOutlineWidth);
            
            serializedObject.ApplyModifiedProperties();
        }
        
        // Outline offset handle
        Handles.color = Color.magenta;
        EditorGUI.BeginChangeCheck();
        
        // Calculate position for outline offset handle
        Vector2 outlineOffset = quad._outlineOffset;
        Vector3 outlineOffsetPos = worldPos + 
            transform.right * outlineOffset.x * quad._outlineWidth * worldScale.x + 
            transform.up * outlineOffset.y * quad._outlineWidth * worldScale.y;
        
        // Draw outline offset handle
        outlineOffsetPos = Handles.Slider2D(
            outlineOffsetPos, 
            transform.forward, 
            transform.right, 
            transform.up, 
            handleSize, 
            Handles.CircleHandleCap, 
            0f
        );
        
        // Apply outline offset changes
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(quad, "Change Outline Offset");
            
            // Calculate new offset values
            Vector3 newOffsetVector = outlineOffsetPos - worldPos;
            
            // Convert to normalized offset values
            if (quad._outlineWidth > 0)
            {
                float newOffsetX = newOffsetVector.x / (quad._outlineWidth * worldScale.x);
                float newOffsetY = newOffsetVector.y / (quad._outlineWidth * worldScale.y);
                
                // Set the new values
                outlineOffsetProp.vector2Value = new Vector2(newOffsetX, newOffsetY);
                
                serializedObject.ApplyModifiedProperties();
            }
        }
        
        // Draw guide lines to help visualize the outline offset
        Handles.color = new Color(1f, 0.5f, 0.8f, 0.3f);
        Handles.DrawLine(worldPos, outlineOffsetPos);
    }
}
#endif
