using System.IO;
using UnityEngine;
using UnityGLTF.Interactivity.Export;

[GLTFInteractivityCompile]
public partial class TestComponent : MonoBehaviour
{
    public float speed = 10;
    
    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector3(0, 1, 0);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = transform.position + new Vector3(0f, 0.1f, 0f); // Works
        
        // adding this currently breaks the AST
        // transform.position = new Vector3(0f, Mathf.Sin(Time.time), 0f); // Stops at Mathf.Sin
        // transform.position = new Vector3(1f, 0.1f, 0f);  // Works
        
        /* AST generation doesn't work yet
        var v = transform.localPosition;
        v.x += Time.deltaTime * speed;
        transform.localPosition = v;
        */
        
        // transform.Rotate(new Vector3(0, Time.deltaTime * speed, 0));

        /*
        for (int i = 0; i < 10; i++)
        {
            transform.position += transform.TransformDirection(Vector3.forward * Time.deltaTime * speed);
        }
        */
    }
}

static class MenuItem
{
    [UnityEditor.MenuItem("Tests/Log generated AST")]
    static void LogAST()
    {
        var ast = TestComponent.GetAST();
        Debug.Log("AST:\n" + ast);

        File.WriteAllText("Assets/flowchart.mermaid", ast.ToMermaidFlowchart());
    }
}