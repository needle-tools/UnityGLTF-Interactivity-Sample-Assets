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
        public Type schemaType;

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
   
        private CheckBox[] _checkBoxes;

        public string GetTestName()
        {
            return GltfInteractivityNodeSchema.GetSchema(schemaType).Op;
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
                GltfInteractivityNodeSchema.GetSchema(schemaType);
                var schemaInstance = GltfInteractivityNodeSchema.GetSchema(schemaType);
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
                var testNode = nodeCreator.CreateNode(schemaType);
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