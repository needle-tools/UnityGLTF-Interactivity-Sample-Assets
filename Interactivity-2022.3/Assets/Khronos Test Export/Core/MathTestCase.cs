using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    [TestCreator.IgnoreTestCase]
    public class MathTestCase : ITestCase
    {
        public string schema = "math/add";
        public object a, b, c;
        public object expected;

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
                    schemasByTypeName.Add(instance.Op, schema);
                else
                {
                    Debug.LogWarning($"Duplicate schema found: {instance.Op} Type: " + schema.FullName);
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
                var schema = (GltfInteractivityNodeSchema)System.Activator.CreateInstance(schemaType);
                return schema;
            }

            throw new Exception($"Schema not found: {name}");
        }


        private CheckBox basicTestCheckBox;

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
            basicTestCheckBox = context.AddCheckBox("basic");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var testNode = nodeCreator.CreateNode(GetSchema(schema));
            context.NewEntryPoint("Basic float");

            if (testNode.ValueInConnection.ContainsKey("a"))
                testNode.SetValueInSocket("a", a, TypeRestriction.LimitToFloat);
            if (testNode.ValueInConnection.ContainsKey("b"))
                testNode.SetValueInSocket("b", b, TypeRestriction.LimitToFloat);
            if (testNode.ValueInConnection.ContainsKey("c"))
                testNode.SetValueInSocket("c", c, TypeRestriction.LimitToFloat);

            var schemaExpectedType = testNode.Schema.OutputValueSockets["value"].expectedType;
            var typeRestriction = expected is float ? TypeRestriction.LimitToFloat : TypeRestriction.LimitToBool;
            var expectedRestriction = expected is float ? ExpectedType.Float : ExpectedType.Bool;

            if ((schemaExpectedType != null && schemaExpectedType.typeIndex != GltfTypes.TypeIndex(typeof(bool))
                 || schemaExpectedType == null))
                testNode.OutputValueSocket["value"].expectedType = expectedRestriction;


            var isSpecialValue = expected.Equals(float.NaN) || expected.Equals(float.PositiveInfinity) ||
                                 expected.Equals(float.NegativeInfinity);

            var testApproximateEquality =
                schema == "math/e" || schema == "math/pi" || schema == "math/inf" || expected is float && !isSpecialValue;

            basicTestCheckBox.SetupCheck(testNode.FirstValueOut(), out var checkFlowIn, expected,
                testApproximateEquality);
            context.AddToCurrentEntrySequence(checkFlowIn);

        }
    }
}