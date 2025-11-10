using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class MeshOutline : MonoBehaviour
{
    public Color outlineColor = Color.black;
    public float size = 0.1f;

    private GameObject outlineObject;
    private Material outlineMaterial;

    void OnEnable()
    {
        EnsureOutlineObject();
    }

    void OnValidate()
    {
        if (outlineObject)
        {
            outlineObject.transform.localScale = OutlineScale(transform);
        }
        if (outlineMaterial)
        {
            outlineMaterial.color = outlineColor;
        }
    }

    Vector3 OutlineScale(Transform originalTransform)
    {
        var lossyScale = originalTransform.lossyScale;
        var adjustedScale = new Vector3(
            lossyScale.x + size,
            lossyScale.y + size,
            lossyScale.z + size
        );
        adjustedScale = new Vector3(
            adjustedScale.x / lossyScale.x,
            adjustedScale.y / lossyScale.y,
            adjustedScale.z / lossyScale.z
        );
        return adjustedScale;
    }

    void EnsureOutlineObject()
    {
        ClearOutlineObject();
        MeshFilter parentMeshFilter = GetComponent<MeshFilter>();
        if (!parentMeshFilter || !parentMeshFilter.sharedMesh)
            return;

        // Create a child object to hold the outline
        var outlineObject = new GameObject("Outline");
        outlineObject.hideFlags = HideFlags.DontSave;
        outlineObject.transform.SetParent(transform);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = OutlineScale(transform);
        // Add MeshFilter and MeshRenderer to the outline object
        var meshFilter = outlineObject.AddComponent<MeshFilter>();
        var meshRenderer = outlineObject.AddComponent<MeshRenderer>();
        // Copy the mesh from the parent object, invert mesh
        if (parentMeshFilter)
        {
            Mesh outlineMesh = Instantiate(parentMeshFilter.sharedMesh);
            // remove normals, tangents, uv, colors
            outlineMesh.uv = null;
            outlineMesh.uv2 = null;
            outlineMesh.colors = null;
            outlineMesh.tangents = null;
            outlineMesh.normals = null;
            // flip winding order
            int[] triangles = outlineMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 1];
                triangles[i + 1] = temp;
            }
            outlineMesh.triangles = triangles;
            meshFilter.mesh = outlineMesh;
            var newMaterial = new Material(Shader.Find("UnityGLTF/UnlitGraph"));
            newMaterial.color = outlineColor;
            newMaterial.hideFlags = HideFlags.DontSave;
            outlineMaterial = newMaterial;
            meshRenderer.sharedMaterial = outlineMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        }

        this.outlineObject = outlineObject;
    }

    void ClearOutlineObject()
    {
        if (!outlineObject) return;

        if (Application.isPlaying)
            Destroy(outlineMaterial);
        else
            DestroyImmediate(outlineMaterial);

        if (Application.isPlaying)
            Destroy(outlineObject);
        else
            DestroyImmediate(outlineObject);
    }

    void OnDisable()
    {
        ClearOutlineObject();
    }

    void OnDestroy()
    {
        ClearOutlineObject();
    }
}
