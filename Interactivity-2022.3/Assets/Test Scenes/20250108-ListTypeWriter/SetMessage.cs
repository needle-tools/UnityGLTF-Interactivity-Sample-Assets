using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class SetMessage : MonoBehaviour
{
    public string message0 = "hello world!";
    public string message1 = "my name is world.\nI can dance!";
    public string message2 = "glTF is a file format for 3D scenes and models\npublished by the Khronos Group.\nThank you.";

#if UNITY_EDITOR
    private void OnValidate()
    {
        var vars = GetComponent<Variables>();
        vars.declarations.Set("message0", ConvertMessageToIntArray(message0));
        vars.declarations.Set("message1", ConvertMessageToIntArray(message1));
        vars.declarations.Set("message2", ConvertMessageToIntArray(message2));
        vars.declarations.Set("characters", GameObject.FindObjectsByType<MaterialCopy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OrderByDescending(x => x.transform.position.y).ThenByDescending(x => x.transform.position.x).Select(x => x.gameObject).ToArray());
        EditorUtility.SetDirty(vars);
    }
#endif
    
    List<int> ConvertMessageToIntArray(string message)
    {
        var result = new List<int>();
        foreach (var c in message)
        {
            result.Add(c);
        }
        return result;
    }
}
