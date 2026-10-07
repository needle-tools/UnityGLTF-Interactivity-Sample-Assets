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
            public float approximateDelta = 0.0001f;
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
            _checkBoxes = new CheckBox[subTests.Count];
            var schemaInstance = GltfInteractivityNodeSchema.GetSchema(schemaType);

            // Full precision for the exported names (entry points, variables, readme),
            // compact matrices for the space-limited 3D label only
            string BuildName(SubMathTest subTest, bool compactMatrices)
            {
                string ValueToStr(object v) => TestValueFormat.ToStr(v, compactMatrices);

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
                return testName;
            }

            int index = 0;
            foreach (var subTest in subTests)
            {
                if (subTest.newRow)
                    context.NewRow();

                _checkBoxes[index] = context.AddCheckBox(BuildName(subTest, false), labelText: BuildName(subTest, true));
                
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
                    _checkBoxes[index].proximityCheckDistance = subTest.approximateDelta;
                    _checkBoxes[index].SetupCheck(testNode.FirstValueOut(), out var checkFlowIn, subTest.expected,
                        subTest.approximateEquality);
                    context.AddToCurrentEntrySequence(checkFlowIn);
                }
                index++;
            }
        }
    }
}