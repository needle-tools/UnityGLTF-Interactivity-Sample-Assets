using UnityEngine;

[GLTFInteractivityCompile]
public partial class TestComponent : MonoBehaviour
{
    public float speed = 10;
    
    // Start is called before the first frame update
    void Start()
    {
        transform.position = new Vector3(0, 0, 0);
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(new Vector3(0, Time.deltaTime * speed, 0));

        for (int i = 0; i < 10; i++)
        {
            transform.position += transform.TransformDirection(Vector3.forward * Time.deltaTime * speed);
        }
    }
}

static class MenuItem
{
    [UnityEditor.MenuItem("Tests/Log generated AST")]
    static void LogAST()
    {
        var ast = TestComponent.GetAST();
        Debug.Log("AST: " + ast);
    }
}