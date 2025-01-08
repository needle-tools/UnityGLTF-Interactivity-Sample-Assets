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

    public void OnDisable()
    {
        if (Application.isPlaying) Destroy(clone);
        else DestroyImmediate(clone);
        GetComponent<Renderer>().sharedMaterial = source;
    }
}
