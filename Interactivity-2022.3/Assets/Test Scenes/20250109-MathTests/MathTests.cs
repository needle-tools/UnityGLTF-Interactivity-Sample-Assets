#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
// TODO make runtime capable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityGLTF.Interactivity.Schema;
using UnityGLTF.Interactivity.VisualScripting;

public class MathTests : MonoBehaviour, IInteractivityExport
{
    public string schema = "math/add";
    public object a, b, c;
    public object expected;
    public Transform pass;
    public TMPro.TMP_Text label;
    
    private static Dictionary<string, Type> schemasByTypeName = null;
    
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }
    
    static void Setup()
    {
        var schemas = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => GetLoadableTypes(assembly))
            .Where(t => t.IsSubclassOf(typeof(GltfInteractivityNodeSchema)))
            .Where( t => !t.IsAbstract)
            .ToList();
        
        // Is not collecting all schema classes
        // > maybe https://issuetracker.unity3d.com/issues/not-all-assemblies-are-found-in-the-current-appdomain-when-scanning-with-typecache
        //var schemas = TypeCache.GetTypesDerivedFrom<GltfInteractivityNodeSchema>();
        
        schemasByTypeName = new Dictionary<string, Type>();
        foreach (var schema in schemas)
        {
            var instance = (GltfInteractivityNodeSchema) System.Activator.CreateInstance(schema);
            if (instance == null)
            {
                Debug.LogWarning($"Failed to create instance of schema: {schema.FullName}");
                continue;
            }
            if (!schemasByTypeName.ContainsKey(instance.Op))
                schemasByTypeName.Add(instance.Op, schema);
            else
            {
                Debug.LogWarning($"Duplicate schema found: {instance.Op} Type: "+schema.FullName);
            }
        }
    }
    
    static GltfInteractivityNodeSchema GetSchema(string name)
    {
        if (schemasByTypeName == null)
            Setup();
        
        if (schemasByTypeName == null)
            throw new Exception("No schemas found");
        
        if (schemasByTypeName.TryGetValue(name, out var schemaType))
        {
            var schema = (GltfInteractivityNodeSchema) System.Activator.CreateInstance(schemaType);
            return schema;
        }
        
        throw new Exception($"Schema not found: {name}");
    }

    public void OnInteractivityExport(VisualScriptingExportContext context, GltfInteractivityExportNodes nodes)
    {
        if (!isActiveAndEnabled) return;
        
        var testNode = nodes.CreateNode(GetSchema(schema));
        if (testNode.ValueSocketConnectionData.ContainsKey("a")) testNode.SetValueInSocket("a", a, TypeRestriction.LimitToFloat);
        if (testNode.ValueSocketConnectionData.ContainsKey("b")) testNode.SetValueInSocket("b", b, TypeRestriction.LimitToFloat);
        if (testNode.ValueSocketConnectionData.ContainsKey("c")) testNode.SetValueInSocket("c", c, TypeRestriction.LimitToFloat);
        testNode.OutValueSocket["value"].expectedType = ExpectedType.Float;

        var equalsNode = default(GltfInteractivityNode);

        if (expected == null)
        {
            Debug.LogWarning("Expected value is null. Schema: " + schema);
        }
        
        var expectedRestriction = expected is float ? TypeRestriction.LimitToFloat : TypeRestriction.LimitToBool;
        var isSpecialValue = expected.Equals(float.NaN) || expected.Equals(float.PositiveInfinity) || expected.Equals(float.NegativeInfinity);
        var testApproximateEquality = schema == "math/e" || schema == "math/pi" || expected is float && !isSpecialValue;
        if (testApproximateEquality)
        {
            // Approximate equality: abs(A - B) < epsilon
            var subtractNode = nodes.CreateNode(GetSchema("math/sub"));
            subtractNode.SetValueInSocketSource("a", testNode, "value");
            subtractNode.SetValueInSocket("b", expected, expectedRestriction);
            var absNode = nodes.CreateNode(GetSchema("math/abs"));
            absNode.SetValueInSocketSource("a", subtractNode, "value");
            var lessThanNode = nodes.CreateNode(GetSchema("math/lt"));
            lessThanNode.SetValueInSocketSource("a", absNode, "value");
            lessThanNode.SetValueInSocket("b", 0.0001f);
            equalsNode = lessThanNode;   
        }
        else
        {
            equalsNode = nodes.CreateNode(GetSchema("math/eq"));
            equalsNode.SetValueInSocket("a", expected, expectedRestriction);
            equalsNode.SetValueInSocketSource("b", testNode, "value");

            // Special case: comparison between NaN and NaN is always false – to get a green checkbox, we need to invert the result.
            // Testing with math/isnan is a separate test case!
            if (expected.Equals(float.NaN))
            {
                var notNode = nodes.CreateNode(GetSchema("math/not"));
                notNode.SetValueInSocketSource("a", equalsNode, "value", TypeRestriction.LimitToBool);
                equalsNode = notNode;
            }
        }
        
        // for logging: switch based on the equals node
        var switchNode = nodes.CreateNode(GetSchema("flow/branch"));
        switchNode.SetValueInSocketSource("condition", equalsNode, "value", TypeRestriction.LimitToBool);
        var loggingNode1 = nodes.CreateNode(new ADBE_OutputConsoleNode());
        loggingNode1.SetValueInSocket("message", "Failed: " + schema + ". Expected: " + expected + ". Actual: ");
        var loggingNode2 = nodes.CreateNode(new ADBE_OutputConsoleNode());
        loggingNode2.SetValueInSocketSource("message", testNode, "value");
        switchNode.SetFlowOut("false", loggingNode1, "in");
        loggingNode1.SetFlowOut("out", loggingNode2, "in");

        var combine3Node = nodes.CreateNode(GetSchema("math/combine3"));
        combine3Node.SetValueInSocket("a", 0f);
        combine3Node.SetValueInSocket("b", 0f);
        combine3Node.SetValueInSocketSource("c", equalsNode, "value", TypeRestriction.LimitToFloat);
        
        var setPositionNode = nodes.CreateNode(GetSchema("pointer/set"));
        UnitsHelper.AddPointerConfig(setPositionNode, "/nodes/{nodeIndex}/translation", GltfTypes.Float3);
        int thisTransformIndex = context.exporter.GetTransformIndex(pass);
        UnitsHelper.AddPointerTemplateValueInput(setPositionNode, "nodeIndex", thisTransformIndex);
        
        setPositionNode.SetValueInSocketSource("value", combine3Node, "value", TypeRestriction.LimitToFloat3);
        
        var startNode = nodes.CreateNode(GetSchema("event/onStart"));
        var sequenceNode = nodes.CreateNode(GetSchema("flow/sequence"));
        sequenceNode.FlowSocketConnectionData.Add("0", new GltfInteractivityNode.FlowSocketData { });
        sequenceNode.FlowSocketConnectionData.Add("1", new GltfInteractivityNode.FlowSocketData { });
        startNode.SetFlowOut("out", sequenceNode, "in");
        sequenceNode.SetFlowOut("0", setPositionNode, "in");
        sequenceNode.SetFlowOut("1", switchNode, "in");
        
        label.text = schema;
    }
}

#endif