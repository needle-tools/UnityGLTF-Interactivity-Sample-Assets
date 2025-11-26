#if HAVE_VISUAL_SCRIPTING

using Unity.VisualScripting;
using UnityEngine;

[ExecuteAlways]
public class MaterialCopy : MonoBehaviour
{
    public Material source;
    private Material clone;
    
    public void OnEnable()
    {
        clone = new Material(source);
        clone.name = source.name;
        clone.hideFlags = HideFlags.DontSave;
        GetComponent<Renderer>().sharedMaterial = clone;
    }

    private void OnValidate()
    {
        
        var sharedMat = GetComponent<Renderer>().sharedMaterial;
        if (sharedMat == source)
        {
            clone = new Material(source);
            clone.name = source.name;
            clone.hideFlags = HideFlags.DontSave;
            GetComponent<Renderer>().sharedMaterial = clone;
            sharedMat.name += " " + System.Guid.NewGuid();
        }
        if (sharedMat && sharedMat.name == source.name && sharedMat != source)
        {
            sharedMat.name += " " + System.Guid.NewGuid();
        }
    }

    public void OnDisable()
    {
        if (Application.isPlaying) Destroy(clone);
        else DestroyImmediate(clone);
        GetComponent<Renderer>().sharedMaterial = source;
    }
}

#endif
