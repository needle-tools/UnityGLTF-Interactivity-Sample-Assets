using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    [TestCreator.IgnoreTestCase]
    public class MathTestCase : ITestCase
    {
        public string schema = "math/add";

        public class SubMathTest
        {
            public object a, b, c;
            public bool approximateEquality = false;
            public object expected;
            public bool newRow = false;
        }
        
        
        
        public List<SubMathTest> subTests = new List<SubMathTest>();

        public SubMathTest AddSubTest(bool newRow = false)
        {
            var subTest = new SubMathTest();
            subTests.Add(subTest);
            subTest.newRow = newRow;
            return subTest;
        }
        
        
        private static Dictionary<string, Type> schemasByTypeName = null;
        private static Dictionary<Type, GltfInteractivityNodeSchema> schemaInstances =
            new Dictionary<Type, GltfInteractivityNodeSchema>();
        
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
                .Where(t => !t.IsAbstract)
                .ToList();

            // Is not collecting all schema classes
            // > maybe https://issuetracker.unity3d.com/issues/not-all-assemblies-are-found-in-the-current-appdomain-when-scanning-with-typecache
            //var schemas = TypeCache.GetTypesDerivedFrom<GltfInteractivityNodeSchema>();

            schemasByTypeName = new Dictionary<string, Type>();
            foreach (var schema in schemas)
            {
                var instance = (GltfInteractivityNodeSchema)System.Activator.CreateInstance(schema);
                if (instance == null)
                {
                    Debug.LogWarning($"Failed to create instance of schema: {schema.FullName}");
                    continue;
                }

                if (!schemasByTypeName.ContainsKey(instance.Op))
                {
                    schemasByTypeName.Add(instance.Op, schema);
                    schemaInstances.Add(schema, instance);
                }
                else
                {
                    Debug.LogWarning($"Duplicate schema found: {instance.Op} Type: " + schema.FullName);
                }
            }
        }
        
        public static GltfInteractivityNodeSchema GetSchemaInstance(string name)
        {
            if (schemasByTypeName == null)
                Setup();

            if (schemasByTypeName == null)
                throw new Exception("No schemas found");
            
            
            if (schemaInstances.TryGetValue(GetSchema(name), out var schemaInstance))
            {
                return schemaInstance;
            }

            throw new Exception($"Schema not found: {name}");
        }
        
        public static Type GetSchema(string name)
        {
            if (schemasByTypeName == null)
                Setup();

            if (schemasByTypeName == null)
                throw new Exception("No schemas found");

            if (schemasByTypeName.TryGetValue(name, out var schemaType))
            {
                return schemaType;
            }

            throw new Exception($"Schema not found: {name}");
        }
        
        private CheckBox[] _checkBoxes;

        public string GetTestName()
        {
            return schema;
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

            string ValueToStr(object v)
            {
                if (v is float f)
                    return f.ToString("F5", invariantCulture);
                else if (v is bool b)
                    return b.ToString(invariantCulture);
                else if (v is double d)
                    return d.ToString("F5", invariantCulture);
                else if (v is Vector2 v2)
                    return v2.ToString("F5");
                else if (v is Vector3 v3)
                    return v3.ToString("F5");
                else if (v is Vector4 v4)
                    return v4.ToString("F5");
                else if (v is Quaternion q)
                    return q.ToString("F5");
                else if (v is Matrix4x4 m)
                    return m.ToString("F5");      
                else
                    return v.ToString();
            }

            
            _checkBoxes = new CheckBox[subTests.Count];
            int index = 0;
            foreach (var subTest in subTests)
            {
                if (subTest.newRow)
                    context.NewRow();
                var testName = "";

                var schemaInstance = GltfInteractivityNodeSchema.GetSchema(GetSchema(schema));
                if (schemaInstance.InputValueSockets.ContainsKey("a"))
                    testName += "[a] " + ValueToStr(subTest.a) + " ";
                if (schemaInstance.InputValueSockets.ContainsKey("b"))
                    testName += "[b] " + ValueToStr(subTest.b) + " ";
                if (schemaInstance.InputValueSockets.ContainsKey("c"))
                    testName += "[c] " + ValueToStr(subTest.c) + " ";
                
                testName += "= " + ValueToStr(subTest.expected);
                
                _checkBoxes[index] = context.AddCheckBox(testName);
                
                index++;
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            int index = 0;
            foreach (var subTest in subTests)
            {
                var testNode = nodeCreator.CreateNode(GetSchema(schema));
                context.NewEntryPoint(_checkBoxes[index].GetText());

                if (testNode.ValueInConnection.ContainsKey("a"))
                    testNode.SetValueInSocket("a", subTest.a, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.a.GetType())));
                if (testNode.ValueInConnection.ContainsKey("b"))
                    testNode.SetValueInSocket("b", subTest.b, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.b.GetType())));
                if (testNode.ValueInConnection.ContainsKey("c"))
                    testNode.SetValueInSocket("c", subTest.c, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.c.GetType())));

                var schemaExpectedType = testNode.Schema.OutputValueSockets["value"].expectedType;
                
                if ((schemaExpectedType != null && schemaExpectedType.typeIndex != GltfTypes.TypeIndex(typeof(bool))
                     || schemaExpectedType == null))
                    testNode.OutputValueSocket["value"].expectedType = ExpectedType.GtlfType(GltfTypes.TypeIndex(subTest.expected.GetType()));

                _checkBoxes[index].SetupCheck(testNode.FirstValueOut(), out var checkFlowIn, subTest.expected,
                    subTest.approximateEquality);
                context.AddToCurrentEntrySequence(checkFlowIn);
                index++;
            }
        }
    }
}