using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ColorMode
{
    SingleColor,
    LinearGradient,
    RadialGradient
}

[ExecuteAlways]
public class SetVertexColor : MonoBehaviour
{
    [Header("Color Settings")]
    public ColorMode colorMode = ColorMode.SingleColor;
    
    [Header("Single Color")]
    public Color singleColor = Color.white;
    
    [Header("Gradient Settings")]
    public Gradient gradient = new Gradient();
    
    [Header("Gradient Direction (for Linear)")]
    public Vector3 gradientDirection = Vector3.up;
    
    [Header("Radial Gradient Center")]
    public Vector3 radialCenter = Vector3.zero;
    public float radialRadius = 1f;
    
    // Serialized variables to track changes
    [SerializeField, HideInInspector]
    private ColorMode lastColorMode;
    [SerializeField, HideInInspector]
    private Color lastSingleColor;
    [SerializeField, HideInInspector]
    private Gradient lastGradient;
    [SerializeField, HideInInspector]
    private Vector3 lastGradientDirection;
    [SerializeField, HideInInspector]
    private Vector3 lastRadialCenter;
    [SerializeField, HideInInspector]
    private float lastRadialRadius;
    [SerializeField, HideInInspector]
    private Mesh lastMesh; // Track mesh changes
    
    // Components and vertex data
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh additionalVertexStreamMesh;
    
    void Start()
    {
        Initialize();
        ApplyVertexColors();
    }
    
    void Update()
    {
        if (HasInputChanged() || HasMeshChanged())
        {
            ApplyVertexColors();
            UpdateLastValues();
        }
    }
    
    void OnEnable()
    {
        Initialize();
        ApplyVertexColors();
        UpdateLastValues();
    }
    
    void OnDisable()
    {
        ClearVertexColors();
    }
    
    void OnDestroy()
    {
        ClearVertexColors();
        
        // Clean up the additional vertex stream mesh
        if (additionalVertexStreamMesh)
        {
            if (Application.isPlaying)
                Destroy(additionalVertexStreamMesh);
            else
                DestroyImmediate(additionalVertexStreamMesh);
        }
    }
    
    void Initialize()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        if (!meshFilter || !meshRenderer)
        {
            Debug.LogError("MeshFilter and MeshRenderer required on " + gameObject.name);
            return;
        }
        
        if (!meshFilter.sharedMesh)
        {
            Debug.LogError("No mesh found on MeshFilter of " + gameObject.name);
            return;
        }
        
        // Initialize additional vertex stream mesh
        if (additionalVertexStreamMesh == null)
        {
            additionalVertexStreamMesh = new Mesh();
            additionalVertexStreamMesh.hideFlags = HideFlags.DontSave;
        }
    }
    
    void ApplyVertexColors()
    {
        if (!meshFilter || !meshFilter.sharedMesh || !meshRenderer)
            return;
            
        Mesh originalMesh = meshFilter.sharedMesh;
        Vector3[] vertices = originalMesh.vertices;
        Color[] originalColors = originalMesh.colors;
        
        // Store original mesh if this is the first time
        if (!lastMesh)
        {
            lastMesh = originalMesh;
        }
        
        // If no original colors, create white colors
        if (originalColors.Length == 0)
        {
            originalColors = new Color[vertices.Length];
            for (int i = 0; i < originalColors.Length; i++)
            {
                originalColors[i] = Color.white;
            }
        }
        
        // Calculate new colors
        Color[] newColors = new Color[vertices.Length];
        Bounds bounds = originalMesh.bounds;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Color calculatedColor = CalculateVertexColor(vertices[i], bounds);
            
            // Multiply with original vertex color (or white if none)
            Color baseColor = i < originalColors.Length ? originalColors[i] : Color.white;
            newColors[i] = new Color(
                calculatedColor.r * baseColor.r,
                calculatedColor.g * baseColor.g,
                calculatedColor.b * baseColor.b,
                calculatedColor.a * baseColor.a
            );
        }
        
        // Create additional vertex stream mesh with only vertex colors
        additionalVertexStreamMesh.Clear();
        additionalVertexStreamMesh.vertices = meshFilter.sharedMesh.vertices;
        additionalVertexStreamMesh.colors = newColors;
        // additionalVertexStreamMesh.SetIndices(originalMesh.GetIndices(0), MeshTopology.Triangles, 0);
        
        // Apply as additional vertex stream
        meshRenderer.additionalVertexStreams = additionalVertexStreamMesh;
    }
    
    void ClearVertexColors()
    {
        if (!meshRenderer)
            return;
            
        // Remove additional vertex streams
        meshRenderer.additionalVertexStreams = null;
    }
    
    bool HasMeshChanged()
    {
        if (!meshFilter || !meshFilter.sharedMesh)
            return false;
            
        if (lastMesh != meshFilter.sharedMesh)
        {
            Debug.Log("Mesh changed to: " + meshFilter.sharedMesh.name);
            return true;
        }
        
        return false;
    }
    
    Color CalculateVertexColor(Vector3 vertex, Bounds bounds)
    {
        switch (colorMode)
        {
            case ColorMode.SingleColor:
                return singleColor;
                
            case ColorMode.LinearGradient:
                // Project vertex onto gradient direction
                Vector3 normalizedDirection = gradientDirection.normalized;
                float minProjection = Vector3.Dot(bounds.min, normalizedDirection);
                float maxProjection = Vector3.Dot(bounds.max, normalizedDirection);
                float vertexProjection = Vector3.Dot(vertex, normalizedDirection);
                
                float t = Mathf.InverseLerp(minProjection, maxProjection, vertexProjection);
                return gradient.Evaluate(t);
                
            case ColorMode.RadialGradient:
                // Calculate distance from radial center
                float distance = Vector3.Distance(vertex, radialCenter);
                float radialT = Mathf.Clamp01(distance / radialRadius);
                return gradient.Evaluate(1f - radialT); // Invert so center is at t=0
                
            default:
                return Color.white;
        }
    }
    
    bool HasInputChanged()
    {
        return colorMode != lastColorMode ||
               singleColor != lastSingleColor ||
               !GradientsEqual(gradient, lastGradient) ||
               gradientDirection != lastGradientDirection ||
               radialCenter != lastRadialCenter ||
               radialRadius != lastRadialRadius;
    }
    
    bool GradientsEqual(Gradient a, Gradient b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        
        // Simple comparison - could be more thorough
        if (a.colorKeys.Length != b.colorKeys.Length) return false;
        if (a.alphaKeys.Length != b.alphaKeys.Length) return false;
        
        for (int i = 0; i < a.colorKeys.Length; i++)
        {
            if (a.colorKeys[i].color != b.colorKeys[i].color ||
                Mathf.Abs(a.colorKeys[i].time - b.colorKeys[i].time) > 0.001f)
                return false;
        }
        
        for (int i = 0; i < a.alphaKeys.Length; i++)
        {
            if (Mathf.Abs(a.alphaKeys[i].alpha - b.alphaKeys[i].alpha) > 0.001f ||
                Mathf.Abs(a.alphaKeys[i].time - b.alphaKeys[i].time) > 0.001f)
                return false;
        }
        
        return true;
    }
    
    void UpdateLastValues()
    {
        lastColorMode = colorMode;
        lastSingleColor = singleColor;
        lastGradient = new Gradient();
        lastGradient.SetKeys(gradient.colorKeys, gradient.alphaKeys);
        lastGradientDirection = gradientDirection;
        lastRadialCenter = radialCenter;
        lastRadialRadius = radialRadius;
        lastMesh = meshFilter ? meshFilter.sharedMesh : null;
    }
    
    void OnValidate()
    {
        // Ensure radial radius is not negative
        if (radialRadius < 0f)
            radialRadius = 0f;
            
        // In editor, update colors when values change
        if (!Application.isPlaying && enabled)
        {
#if UNITY_EDITOR
            // Delay the execution to avoid issues during serialization
            EditorApplication.delayCall += () =>
            {
                if (this && gameObject && enabled)
                {
                    ApplyVertexColors();
                    UpdateLastValues();
                }
            };
#endif
        }
    }
    
    [ContextMenu("Apply Vertex Colors")]
    void ForceApplyVertexColors()
    {
        Initialize();
        ApplyVertexColors();
        UpdateLastValues();
    }
    
    [ContextMenu("Clear Vertex Colors")]
    void ForceClearVertexColors()
    {
        ClearVertexColors();
        Debug.Log("Cleared vertex colors. Original mesh unchanged.");
    }
    
    [ContextMenu("Debug Mesh State")]
    void DebugMeshState()
    {
        Debug.Log("=== Mesh State Debug ===");
        Debug.Log("MeshFilter.sharedMesh: " + (meshFilter?.sharedMesh?.name ?? "null"));
        Debug.Log("lastMesh: " + (lastMesh?.name ?? "null"));
        Debug.Log("additionalVertexStreamMesh: " + (additionalVertexStreamMesh?.name ?? "null"));
        Debug.Log("Has additional vertex streams: " + (meshRenderer?.additionalVertexStreams != null));
        
        if (meshFilter?.sharedMesh)
        {
            Debug.Log("Current sharedMesh ID: " + meshFilter.sharedMesh.GetInstanceID());
            Debug.Log("Vertex count: " + meshFilter.sharedMesh.vertexCount);
            Debug.Log("Has vertex colors: " + (meshFilter.sharedMesh.colors.Length > 0));
        }
    }
}
