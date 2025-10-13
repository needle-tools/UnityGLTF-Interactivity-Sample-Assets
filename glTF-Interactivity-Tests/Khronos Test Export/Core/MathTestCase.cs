using System;
using System.Collections.Generic;
using System.Globalization;
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
            public object a, b, c, d;
            public string[] socketNames = new []{"a", "b", "c", "d"};
            public bool approximateEquality = false;
            public object expected;
            public bool newRow = false;
        }

        public class IsValidSubTest : SubMathTest
        {
            public bool shouldBeValid = false;
        }
        
        public List<SubMathTest> subTests = new List<SubMathTest>();

        
        public SubMathTest AddSubTest(bool newRow = false)
        {
            var subTest = new SubMathTest();
            subTests.Add(subTest);
            subTest.newRow = newRow;
            return subTest;
        }

        public IsValidSubTest AddIsValidTest(bool newRow = false)
        {
            var subTest = new IsValidSubTest();
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
                    return f.ToString("F2", invariantCulture);
                else if (v is bool b)
                    return b.ToString(invariantCulture);
                else if (v is double d)
                    return d.ToString("F2", invariantCulture);
                else if (v is Vector2 v2)
                    return v2.ToString("F2");
                else if (v is Vector3 v3)
                    return v3.ToString("F2");
                else if (v is Vector4 v4)
                    return v4.ToString("F2");
                else if (v is Quaternion q)
                    return q.ToString("F2");
                else if (v is Matrix4x4 m)
                {
                    var format = "F1";
                    var formatProvider = (IFormatProvider) CultureInfo.InvariantCulture.NumberFormat;
                    return string.Format("[{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}]",
                        (object) m.m00.ToString(format, formatProvider), (object) m.m01.ToString(format, formatProvider), (object) m.m02.ToString(format, formatProvider), (object) m.m03.ToString(format, formatProvider), 
                        (object) m.m10.ToString(format, formatProvider), (object) m.m11.ToString(format, formatProvider), (object) m.m12.ToString(format, formatProvider), (object) m.m13.ToString(format, formatProvider), 
                        (object) m.m20.ToString(format, formatProvider), (object) m.m21.ToString(format, formatProvider), (object) m.m22.ToString(format, formatProvider), (object) m.m23.ToString(format, formatProvider),
                        (object) m.m30.ToString(format, formatProvider), (object) m.m31.ToString(format, formatProvider), (object) m.m32.ToString(format, formatProvider), (object) m.m33.ToString(format, formatProvider));
                }
                else
                    return v.ToString();
            }
            
            _checkBoxes = new CheckBox[subTests.Count];
            var schemaInstance = GltfInteractivityNodeSchema.GetSchema(schemaType);
            int index = 0;
            foreach (var subTest in subTests)
            {
                if (subTest.newRow)
                    context.NewRow();
                var testName = "";
                if (subTest is IsValidSubTest)
                    testName += "Invalid:";
                
                if (schemaInstance.InputValueSockets.ContainsKey(subTest.socketNames[0]))
                    testName += $"[{subTest.socketNames[0]}] " + ValueToStr(subTest.a) + " ";
                if (schemaInstance.InputValueSockets.ContainsKey(subTest.socketNames[1]))
                    testName += $"[{subTest.socketNames[1]}] " + ValueToStr(subTest.b) + " ";
                if (schemaInstance.InputValueSockets.ContainsKey(subTest.socketNames[2]))
                    testName += $"[{subTest.socketNames[2]}] " + ValueToStr(subTest.c) + " ";
                if (schemaInstance.InputValueSockets.ContainsKey(subTest.socketNames[3]))
                    testName += $"[{subTest.socketNames[3]}] " + ValueToStr(subTest.d) + " ";

                if (subTest.expected != null)
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

                if (testNode.ValueInConnection.ContainsKey(subTest.socketNames[0]))
                    testNode.SetValueInSocket(subTest.socketNames[0], subTest.a, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.a.GetType())));
                if (testNode.ValueInConnection.ContainsKey(subTest.socketNames[1]))
                    testNode.SetValueInSocket(subTest.socketNames[1], subTest.b, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.b.GetType())));
                if (testNode.ValueInConnection.ContainsKey(subTest.socketNames[2]))
                    testNode.SetValueInSocket(subTest.socketNames[2], subTest.c, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.c.GetType())));
                if (testNode.ValueInConnection.ContainsKey(subTest.socketNames[3]))
                    testNode.SetValueInSocket(subTest.socketNames[3], subTest.d, TypeRestriction.LimitToType(GltfTypes.TypeIndex(subTest.d.GetType())));

                var schemaExpectedType = testNode.Schema.OutputValueSockets["value"].expectedType;
                
                if (subTest.expected != null && (schemaExpectedType != null && schemaExpectedType.typeIndex != GltfTypes.TypeIndex(typeof(bool))
                     || schemaExpectedType == null))
                    testNode.OutputValueSocket["value"].expectedType = ExpectedType.GtlfType(GltfTypes.TypeIndex(subTest.expected.GetType()));

                if (subTest is IsValidSubTest isValidSubTest)
                {
                    _checkBoxes[index].SetupCheck(testNode.ValueOut("isValid"), out var checkFlowIn, isValidSubTest.shouldBeValid);
                    context.AddToCurrentEntrySequence(checkFlowIn);               
                }
                else
                {
                    _checkBoxes[index].SetupCheck(testNode.FirstValueOut(), out var checkFlowIn, subTest.expected,
                        subTest.approximateEquality);
                    context.AddToCurrentEntrySequence(checkFlowIn);
                }
                index++;
            }
        }
    }
}