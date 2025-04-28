// #define INTERACTIVITY_EXPORT

using System.IO;
using System.Threading.Tasks;
using UnityEngine;

#if INTERACTIVITY_EXPORT
[GLTFInteractivityCompile]
#endif
public partial class AsyncTest : MonoBehaviour
{
    public GameObject other;
    
    // Start is called before the first frame update
    async void Start()
    {
        var result = await Method();
        Debug.Log(result);
    }

    async Task<float> Method()
    {
        var k = 1f;
        await Task.Delay(1000);
        k += 2;
        other.SetActive(false);
        await Task.Delay(500);
        k += 2;
        other.SetActive(true);
        return k;
    }
}

#if INTERACTIVITY_EXPORT
static class MenuItem2
{
    [UnityEditor.MenuItem("Tests/Log generated AST 2")]
    static void LogAST()
    {
        var ast = AsyncTest.GetAST();
        Debug.Log("AST:\n" + ast);

        File.WriteAllText("Assets/flowchart.mermaid", ast.ToMermaidFlowchart());
    }
}
#endif