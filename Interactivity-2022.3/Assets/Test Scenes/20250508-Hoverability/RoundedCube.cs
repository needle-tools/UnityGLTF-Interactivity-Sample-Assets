using UnityEngine;
using System.Linq;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoundedCube : MonoBehaviour
{
    public float edgeRadius = 0.1f;
    public int _subdivisions = 2;
    [Range(0, 1)]
    public float spherifyAmount = 0f;

    private Mesh mesh;
    private float _lastEdgeRadius;
    private int _lastSubdivisions;
    private float _lastSpherifyAmount;
    private Vector3 _lastScale;

    private void Awake()
    {
        _lastScale = transform.lossyScale;
        Generate();
    }

    private void OnEnable()
    {
        // Ensure mesh is generated when the component is enabled
        if (mesh == null)
        {
            _lastScale = transform.lossyScale;
            Generate();
        }
    }

    private void Update()
    {
        // Check if scale has changed
        if (transform.lossyScale != _lastScale)
        {
            _lastScale = transform.lossyScale;
            Generate();
        }
    }

    private void OnValidate()
    {
        // Regenerate mesh when values change in the inspector
        if (_lastEdgeRadius != edgeRadius || _lastSubdivisions != _subdivisions || _lastSpherifyAmount != spherifyAmount)
        {
            Generate();
            // Store current values to compare against on next change
            _lastEdgeRadius = edgeRadius;
            _lastSubdivisions = _subdivisions;
            _lastSpherifyAmount = spherifyAmount;
        }
    }

    private void Generate()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Rounded Cube";
		var subdivisions = _subdivisions;
        
        // Handle subdivision = 0 as a special case with minimum subdivisions
        bool simpleMesh = subdivisions == 0;
        
        // For simpleMesh (subdivisions=0), ensure we use at least 4 subdivisions
        // This gives us enough detail for the rounded corners while keeping the mesh simple
        subdivisions = simpleMesh ? 4 : (Mathf.Max(1, subdivisions) + 2);
        if (subdivisions % 2 == 1)
            subdivisions += 1;

        int vertCount = 6 * subdivisions * subdivisions;
        int indCount = 6 * (subdivisions - 1) * (subdivisions - 1) * 6;
        
        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Color32[] colors = new Color32[vertCount];
        int[] indices = new int[indCount];

        // Calculate world-space consistent radius by dividing by lossyScale
        Vector3 lossyScale = transform.lossyScale;
        Vector3 invertLossyScale = new Vector3(
            1f / lossyScale.x,
            1f / lossyScale.y,
            1f / lossyScale.z
        );
        
        // Calculate the actual radius to use for each axis
        // We need a fixed radius in local space regardless of lossyScale
        Vector3 radiusPerAxis = new Vector3(
            edgeRadius,
            edgeRadius,
            edgeRadius
        );
        
        // Define the base cube dimensions in local space (normalized size)
        Vector3 baseDimensions = Vector3.one;
        baseDimensions.Scale(lossyScale);
        
        // Calculate the effective size of the interior cube excluding the rounded edges
        // The total size needs to be Vector3.one, so the interior size should be smaller by twice the radius
        Vector3 interiorDimensions = new Vector3(
            baseDimensions.x - (radiusPerAxis.x * 2),
            baseDimensions.y - (radiusPerAxis.y * 2),
            baseDimensions.z - (radiusPerAxis.z * 2)
        );
        
        // Offset is half the interior dimensions (distance from center to interior edge)
        Vector3 offset = interiorDimensions / 2;
        
        // Size used for vertex generation is the full cube size
        Vector3 size = Vector3.one * 2;
        
        int indIndex = 0;
        int vertOffset = 0;

        // Generate the 6 faces of the cube
        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            // Calculate the 4 corners of this face
            Vector3 p1, p2, p3, p4;
            Vector3 n1, n2, n3, n4;
            Vector2 u1, u2, u3, u4;

            GetCubeVertex(faceIndex * 4, size, out p1, out n1, out u1);
            GetCubeVertex(faceIndex * 4 + 1, size, out p2, out n2, out u2);
            GetCubeVertex(faceIndex * 4 + 2, size, out p3, out n3, out u3);
            GetCubeVertex(faceIndex * 4 + 3, size, out p4, out n4, out u4);

            float sizeU = Vector3.Scale(p4 - p1, Vector3.one).magnitude;
            float sizeV = Vector3.Scale(p2 - p1, Vector3.one).magnitude;

            vertOffset = faceIndex * (subdivisions) * (subdivisions);

            // Generate vertices for this face
            for (int sy = 0; sy < subdivisions; sy++)
            {
                bool firstHalfY = sy < subdivisions / 2;
                int y = firstHalfY ? sy : sy - 1;
                Vector3 stretchA = firstHalfY ? p1 : p4;
                Vector3 stretchB = firstHalfY ? p2 : p3;
                
                // Choose the appropriate radius component based on face orientation
                float faceRadiusU, faceRadiusV, faceRadiusN;
                
                // Determine which radius to use for each dimension based on the current face
                switch (faceIndex / 2) // face/2: 0=X faces, 1=Y faces, 2=Z faces
                {
                    case 0: // X faces (0, 1)
                        faceRadiusU = radiusPerAxis.x;
                        faceRadiusV = radiusPerAxis.y;
                        faceRadiusN = radiusPerAxis.z;
                        break;
                    case 1: // Y faces (2, 3)
                        faceRadiusU = radiusPerAxis.x;
                        faceRadiusV = radiusPerAxis.z;
                        faceRadiusN = radiusPerAxis.y;
                        break;
                    case 2: // Z faces (4, 5)
                        faceRadiusU = radiusPerAxis.z;
                        faceRadiusV = radiusPerAxis.y;
                        faceRadiusN = radiusPerAxis.x;
                        break;
                    default:
                        faceRadiusU = faceRadiusV = faceRadiusN = edgeRadius; // Fallback
                        break;
                }
                
                float stretchV = (faceRadiusV * 2) / sizeV;
                float offV = firstHalfY ? 0 : 1 - stretchV;

                float py = y / (float)(subdivisions - 2);
                float pv = py * stretchV + offV;
                int yOff = vertOffset + sy * subdivisions;
                int yOffNext = vertOffset + (sy + 1) * subdivisions;

                Vector3 pointLeft = Vector3.Lerp(p1, p4, py);
                Vector3 pointRight = Vector3.Lerp(p2, p3, py);
                Vector2 uvLeft = Vector2.Lerp(u1, u4, pv);
                Vector2 uvRight = Vector2.Lerp(u2, u3, pv);

                for (int sx = 0; sx < subdivisions; sx++)
                {
                    bool firstHalfX = sx < subdivisions / 2;
                    int x = firstHalfX ? sx : sx - 1;
                    Vector3 stretch = firstHalfX ? stretchA : stretchB;
                    float stretchU = (faceRadiusU * 2) / sizeU;
                    float offU = firstHalfX ? 0 : 1 - stretchU;

                    float px = x / (float)(subdivisions - 2);
                    float pu = px * stretchU + offU;
                    int vertIndex = sx + yOff;

                    // Generate vertex normals - in model space
                    normals[vertIndex] = Vector3.Normalize(Vector3.Lerp(pointLeft, pointRight, px));

                    // Calculate vertex position:
                    // Start from the interior cube position (scaled by offset)
                    // Then add the rounded edge by moving in the normal direction by the radius amount
                    Vector3 interiorPoint = Vector3.Scale(stretch, offset);
                    var v = interiorPoint + normals[vertIndex] * faceRadiusU;
                    
                    // Store the original normal before scaling the vertex
                    Vector3 originalNormal = normals[vertIndex];
                    
                    // Apply spherification if enabled
                    if (spherifyAmount > 0)
                    {
                        // Create a spherical normal by normalizing the vertex position
                        Vector3 sphericalNormal = Vector3.Normalize(v);
                        // Blend between the original normal and the spherical normal
                        originalNormal = Vector3.Normalize(Vector3.Lerp(originalNormal, sphericalNormal, spherifyAmount));
                    }
                    
                    // Apply inverse scale to vertex positions
                    v.Scale(invertLossyScale);
                    vertices[vertIndex] = v;
                    
                    // For non-uniform scaling, normals need to be transformed by the inverse transpose of the scale matrix
                    // For a diagonal scale matrix, the inverse transpose is just 1/scale for each component
                    // This preserves perpendicularity after non-uniform scaling
                    
                    // Apply the inverse transpose to the normal
                    Vector3 transformedNormal = new Vector3(
                        originalNormal.x / invertLossyScale.x,
                        originalNormal.y / invertLossyScale.y,
                        originalNormal.z / invertLossyScale.z
                    );
                    normals[vertIndex] = Vector3.Normalize(transformedNormal);
                    
                    uvs[vertIndex] = Vector2.Lerp(uvLeft, uvRight, pu);
                    colors[vertIndex] = new Color32(255, 255, 255, 255);

                    // Generate triangles
                    if (sy != subdivisions - 1 && sx != subdivisions - 1)
                    {
                        // Keep original winding for top and bottom faces (indices 2 and 3),
                        // flip winding for other faces (indices 0, 1, 4, and 5)
                        if (faceIndex == 2 || faceIndex == 3)
                        {
                            // Original winding (clockwise)
                            indices[indIndex++] = (sx) + yOff;
                            indices[indIndex++] = (sx + 1) + yOff;
                            indices[indIndex++] = (sx + 1) + yOffNext;

                            indices[indIndex++] = (sx) + yOff;
                            indices[indIndex++] = (sx + 1) + yOffNext;
                            indices[indIndex++] = (sx) + yOffNext;
                        }
                        else
                        {
                            // Flipped winding (counter-clockwise)
                            indices[indIndex++] = (sx) + yOff;
                            indices[indIndex++] = (sx + 1) + yOffNext;
                            indices[indIndex++] = (sx + 1) + yOff;

                            indices[indIndex++] = (sx) + yOff;
                            indices[indIndex++] = (sx) + yOffNext;
                            indices[indIndex++] = (sx + 1) + yOffNext;
                        }
                    }
                }
            }
        }

        // Assign mesh data
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.colors32 = colors;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
    }

    private void GetCubeVertex(int index, Vector3 size, out Vector3 pos, out Vector3 normal, out Vector2 uv)
    {
        // Convert cube index to cube position
        int face = index / 4;
        int vert = index % 4;

        // Default values
        pos = Vector3.zero;
        normal = Vector3.zero;
        uv = Vector2.zero;

        // Calculate the face normal direction
        normal = Vector3.zero;
        normal[face / 2] = (face % 2 == 0) ? -1 : 1;

        // Position based on the face - use half size for proper positioning
        float halfX = size.x / 2;
        float halfY = size.y / 2;
        float halfZ = size.z / 2;
        
        switch (face)
        {
            case 0: // -X face
                pos.x = -halfX;
                pos.y = (vert == 0 || vert == 3) ? -halfY : halfY;
                pos.z = (vert == 0 || vert == 1) ? -halfZ : halfZ;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
            case 1: // +X face
                pos.x = halfX;
                pos.y = (vert == 0 || vert == 3) ? -halfY : halfY;
                pos.z = (vert == 0 || vert == 1) ? halfZ : -halfZ;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
            case 2: // -Y face
                pos.y = -halfY;
                pos.x = (vert == 0 || vert == 3) ? -halfX : halfX;
                pos.z = (vert == 0 || vert == 1) ? -halfZ : halfZ;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
            case 3: // +Y face
                pos.y = halfY;
                pos.x = (vert == 0 || vert == 3) ? -halfX : halfX;
                pos.z = (vert == 0 || vert == 1) ? halfZ : -halfZ;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
            case 4: // -Z face
                pos.z = -halfZ;
                pos.x = (vert == 0 || vert == 3) ? -halfX : halfX;
                pos.y = (vert == 0 || vert == 1) ? -halfY : halfY;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
            case 5: // +Z face
                pos.z = halfZ;
                pos.x = (vert == 0 || vert == 3) ? halfX : -halfX;
                pos.y = (vert == 0 || vert == 1) ? -halfY : halfY;
                uv.x = (vert == 0 || vert == 3) ? 0 : 1;
                uv.y = (vert == 0 || vert == 1) ? 0 : 1;
                break;
        }
    }
}