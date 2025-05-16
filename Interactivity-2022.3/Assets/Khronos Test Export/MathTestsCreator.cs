using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityGLTF.Interactivity;

namespace Khronos_Test_Export
{
    public class MathTestCreator : TestCreator
    {
        abstract class TestCase
        {
            public string schema;

            public virtual void Run()
            {
            }

            public bool autoCreateTestsForAllSupportedInputs = true;
            
            public bool approximate = false;

            public abstract object A { get; }
            public abstract object B { get; }
            public abstract object C { get; }
            public abstract object Expected { get; }
        }

        abstract class TestCase<I, O> : TestCase
        {
            public I a;
            public I b;
            public I c;
            public O expected;
            public override object A => a;
            public override object B => b;
            public override object C => c;
            public override object Expected => expected;
        }

        class ZeroArg<O> : TestCase<float, O>
        {
            public Func<O> operation;
            public override void Run() => expected = operation();
        }

        class OneArg<I, O> : TestCase<I, O>
        {
            public Func<I, O> operation;
            public override void Run() => expected = operation(a);
        }

        class TwoArg<I, O> : TestCase<I, O>
        {
            public Func<I, I, O> operation;
            public override void Run() => expected = operation(a, b);
        }

        class ThreeArg<I, O> : TestCase<I, O>
        {
            public Func<I, I, I, O> operation;
            public override void Run() => expected = operation(a, b, c);
        }

        private static List<TestCase> mathCases = new List<TestCase>()
        {
            // Constant Nodes
            new ZeroArg<float>()
            {
                schema = "math/e",
                approximate = true,
                operation = () => 2.718281828459045f,
            },
            new ZeroArg<float>()
            {
                schema = "math/pi",
                approximate = true,
                operation = () => Mathf.PI,
            },
            new ZeroArg<float>()
            {
                schema = "math/inf",
                operation = () => Mathf.Infinity,
            },
            new ZeroArg<float>()
            {
                schema = "math/nan",
                operation = () => float.NaN,
            },

            // Arithmetic Nodes
            new OneArg<float, float>()
            {
                schema = "math/abs",
                a = -7,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/abs",
                a = 7,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/abs",
                a = 0,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/sign",
                a = -9,
                operation = (a) => Mathf.Sign(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/sign",
                a = 9,
                operation = (a) => Mathf.Sign(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/trunc",
                a = 9.234324f,
                operation = (a) => Mathf.Sign(a) * Mathf.Floor(Mathf.Abs(a)),
            },
            new OneArg<float, float>()
            {
                schema = "math/floor",
                a = -2323.4346f,
                operation = (a) => Mathf.Floor(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/ceil",
                a = 757.003244f,
                operation = (a) => Mathf.Ceil(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/fract",
                a = -32434.96784f,
                approximate = true,
                operation = (a) => a - Mathf.Floor(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/neg",
                a = -8923448.234f,
                operation = (a) => -a,
            },
            // Arithmetic with two inputs
            new TwoArg<float, float>()
            {
                schema = "math/add",
                a = -1f,
                b = 3f,
                operation = (a, b) => a + b,
            },
            new TwoArg<float, float>()
            {
                schema = "math/sub",
                a = 7f,
                b = 9f,
                operation = (a, b) => a - b,
            },
            new TwoArg<float, float>()
            {
                schema = "math/mul",
                a = 345.234432f,
                b = 1 / 345.234432f,
                approximate = true,
                operation = (a, b) => a * b,
            },
            new TwoArg<float, float>()
            {
                schema = "math/div",
                a = 8989.324f,
                b = 2134.234f,
                approximate = true,
                operation = (a, b) => a / b,
            },
            new TwoArg<float, float>()
            {
                schema = "math/rem",
                a = 19.423534f,
                b = 2.234f,
                approximate = true,
                operation = (a, b) => a % b,
            },
            new TwoArg<float, float>()
            {
                schema = "math/min",
                a = 17.21323f,
                b = -324.234f,
                operation = (a, b) => Mathf.Min(a, b),
            },
            new TwoArg<float, float>()
            {
                schema = "math/max",
                a = 4653.234f,
                b = 91293923.234f,
                operation = (a, b) => Mathf.Max(a, b),
            },
            new ThreeArg<float, float>()
            {
                schema = "math/clamp",
                a = 9,
                b = 2,
                c = 3,
                operation = (a, b, c) => Mathf.Clamp(a, b, c),
            },
            new OneArg<float, float>()
            {
                schema = "math/saturate",
                a = -1.5f,
                operation = (a) => Mathf.Clamp01(a),
            },
            new ThreeArg<float, float>()
            {
                schema = "math/mix",
                a = 1f,
                b = 2f,
                c = 2f,
                operation = (a, b, c) => Mathf.LerpUnclamped(a, b, c),
            },
            // Comparison Nodes
            new TwoArg<float, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = 1.324f,
                b = 2.32423f,
                operation = (a, b) => a == b,
            },
            new TwoArg<bool, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = true,
                b = false,
                operation = (a, b) => a == b,
            },
            new TwoArg<Vector2, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = new Vector2(4,5),
                b = new Vector2(4,5),
                operation = (a, b) => a == b,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = float.NaN,
                b = float.NaN,
                operation = (a, b) => false,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = float.NaN,
                b = 1f,
                operation = (a, b) => false,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = float.PositiveInfinity,
                b = float.PositiveInfinity,
                operation = (a, b) => true,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = float.PositiveInfinity,
                b = float.NegativeInfinity,
                operation = (a, b) => false,
            },
            new TwoArg<Matrix4x4, bool>()
            {
                schema = "math/eq",
                autoCreateTestsForAllSupportedInputs = false,
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                b = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                operation = (a, b) => true,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/lt",
                a = 1,
                b = 2,
                operation = (a, b) => a < b,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/le",
                a = 1.3465f,
                b = 1.3465f,
                operation = (a, b) => a <= b,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/gt",
                a = 1,
                b = 2,
                operation = (a, b) => a > b,
            },
            new TwoArg<float, bool>()
            {
                schema = "math/ge",
                a = 1.3465f,
                b = 1.3465f,
                operation = (a, b) => a >= b,
            },
            // Special nodes
            new OneArg<float, bool>()
            {
                schema = "math/isnan",
                a = float.NaN,
                operation = (a) => float.IsNaN(a),
            },
            new OneArg<float, bool>()
            {
                schema = "math/isnan",
                a = 1f,
                operation = (a) => float.IsNaN(a),
            },
            new OneArg<float, bool>()
            {
                schema = "math/isinf",
                a = float.PositiveInfinity,
                operation = (a) => float.IsInfinity(a),
            },
            new OneArg<float, bool>()
            {
                schema = "math/isinf",
                a = float.NegativeInfinity,
                operation = (a) => float.IsInfinity(a),
            },
            // math/select
            new OneArg<float, float>()
            {
                schema = "math/rad",
                a = 75,
                approximate = true,
                operation = (a) => a * Mathf.PI / 180,
            },
            new OneArg<float, float>()
            {
                schema = "math/deg",
                a = Mathf.PI * 0.35f,
                approximate = true,
                operation = (a) => a * 180 / Mathf.PI,
            },
            new OneArg<float, float>()
            {
                schema = "math/sin",
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Sin(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/cos",
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Cos(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/tan",
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Tan(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/asin",
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Asin(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/acos",
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Acos(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/atan",
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Atan(a),
            },
            new TwoArg<float, float>()
            {
                schema = "math/atan2",
                a = 0.5f,
                b = 0.5f,
                approximate = true,
                operation = (a, b) => Mathf.Atan2(a, b),
            },
            // Hyperbolic nodes
            new OneArg<float, float>()
            {
                schema = "math/sinh",
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Sinh(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/cosh",
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Cosh(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/tanh",
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Tanh(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/asinh",
                a = 0.5f,
                approximate = true,
                operation = (a) => (float)Math.Asinh(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/acosh",
                a = 1.5f,
                approximate = true,
                operation = (a) => (float)Math.Acosh(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/atanh",
                a = 0.5f,
                approximate = true,
                operation = (a) => (float)Math.Atanh(a),
            },
            // Exponential nodes
            new OneArg<float, float>()
            {
                schema = "math/exp",
                a = 1.2132f,
                approximate = true,
                operation = (a) => Mathf.Exp(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/log",
                a = 26436.23423f,
                approximate = true,
                operation = (a) => Mathf.Log(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/log2",
                a = 6443.243f,
                approximate = true,
                operation = (a) => Mathf.Log(a, 2),
            },
            new OneArg<float, float>()
            {
                schema = "math/log10",
                a = 8768.24f,
                approximate = true,
                operation = (a) => Mathf.Log10(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/sqrt",
                a = 4556.234f,
                approximate = true,
                operation = (a) => Mathf.Sqrt(a),
            },
            new OneArg<float, float>()
            {
                schema = "math/cbrt",
                a = 9769.234f,
                approximate = true,
                operation = (a) => Mathf.Pow(a, 1f / 3f),
            },
            new TwoArg<float, float>()
            {
                schema = "math/pow",
                a = 7.764f,
                b = 2.345f,
                approximate = true,
                operation = (a, b) => Mathf.Pow(a, b),
            },
        };

        private void OnValidate()
        {
            testName = "Math Tests";
        }

        protected override void GenerateTestList()
        {
        }

        protected override ITestCase[] GetTests()
        {
            object CreateValueByGltfType(object value, string gltfType)
            {
                if (value is not float)
                {
                    Debug.Log("Value is not float: " + value);
                    return value;
                }
                if (gltfType == "float2")
                {
                    return new Vector2((float)value, (float)value);
                }

                if (gltfType == "float3")
                {
                    return new Vector3((float)value, (float)value, (float)value);
                }

                if (gltfType == "float4")
                {
                    return new Vector4((float)value, (float)value, (float)value, (float)value);
                }

                if (gltfType == "float4x4")
                {
                    var v4 = new Vector4((float)value, (float)value, (float)value, (float)value);
                    return new Matrix4x4(v4, v4, v4, v4);
                }

                if (gltfType == "int")
                {
                    return Mathf.RoundToInt((float)value);
                }

                return null;
            }

            var newTestCases = new List<ITestCase>();
            foreach (var group in mathCases.GroupBy(t => t.schema))
            {
                var newTest = new MathTestCase();
                newTestCases.Add(newTest);
                newTest.schema = group.First().schema;
                var schemaInstance = MathTestCase.GetSchemaInstance(newTest.schema);
                
                Type typeA = null;
                foreach (var testCase in group)
                {
                    testCase.Run();
                    var newTestCase = newTest.AddSubTest();
                    newTestCase.a = testCase.A;
                    newTestCase.b = testCase.B;
                    newTestCase.c = testCase.C;
                    newTestCase.expected = testCase.Expected;
                    newTestCase.approximateEquality = testCase.approximate;

                    typeA = testCase.A.GetType();
                }
                
                if (typeA == typeof(float) && schemaInstance.InputValueSockets.TryGetValue("a", out var aSocket))
                {
                    bool first = true;
                    foreach (var suppType in aSocket.SupportedTypes.Where(s =>
                                 GltfTypes.GetTypeMapping(typeA).GltfSignature != s))
                    {
                        first = true;
                        foreach (var testCase in group)
                        {
                            if (!testCase.autoCreateTestsForAllSupportedInputs)
                                continue;
                            var newA = CreateValueByGltfType(testCase.A, suppType);
                            object newB = testCase.B;
                            object newC = testCase.C;

                            object newExpected = testCase.Expected;
                            if (schemaInstance.OutputValueSockets["value"].SupportedTypes.Length > 1)
                                newExpected = CreateValueByGltfType(testCase.Expected, suppType);
                            
                            if (schemaInstance.InputValueSockets.TryGetValue("b", out var bSocket))
                                newB = CreateValueByGltfType(testCase.B, suppType);
                            if (schemaInstance.InputValueSockets.TryGetValue("c", out var cSocket))
                                newC = CreateValueByGltfType(testCase.C, suppType);

                            if (newA == null || newB == null || newC == null)
                                continue;

                            var extraCase = newTest.AddSubTest();
                            extraCase.a = newA;
                            extraCase.b = newB;
                            extraCase.c = newC;
                            extraCase.expected = newExpected;
                            extraCase.approximateEquality = testCase.approximate;
                            first = false;
                        }
                    }
                }
            }

            List<ITestCase> additionalCases = new List<ITestCase>();
            additionalCases.Add(new Math_SelectTest());
            additionalCases.Add(new Math_SwitchTest());
            additionalCases.Add(new Math_RandomTest());
            
            return newTestCases.Concat(additionalCases).OrderBy(c => c.GetTestName()).ToArray();
        }

        public override void ExportTests(bool exportAllInOne = true, bool exportIndividual = true)
        {
            testName = "Math Tests";
            indexFilename = "MathTests-Index";
            base.ExportTests(exportAllInOne, exportIndividual);
        }
#if UNITY_EDITOR

        [CustomEditor(typeof(MathTestCreator))]
        public class MathInspector : UnityEditor.Editor
        {
            public bool exportAllInOne = true;
            public bool exportIndividual = true;

            public override void OnInspectorGUI()
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MathTestCreator.testExporter)));

                exportIndividual = GUILayout.Toggle(exportIndividual, "Export Individual Tests");
                exportAllInOne = GUILayout.Toggle(exportAllInOne, "Export All In One");

                if (GUILayout.Button("Export Tests"))
                {
                    ((MathTestCreator)target).ExportTests(exportAllInOne, exportIndividual);
                }
            }
        }
#endif
    }
}