using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class MathTestCreator : TestCreator
    {
        abstract class TestCase
        {
            public virtual void Run()
            {
            }

            public bool autoCreateTestsForAllSupportedInputs = true;
            
            public bool approximate = false;

            public abstract object A { get; }
            public abstract object B { get; }
            public abstract object C { get; }
            public abstract object Expected { get; }
            
            public abstract Type SchemaType { get; }
        }

        abstract class TestCase<TSchema, I, O> : TestCase where TSchema : GltfInteractivityNodeSchema
        {
            public I a;
            public I b;
            public I c;
            public O expected;
            public override object A => a;
            public override object B => b;
            public override object C => c;
            public override object Expected => expected;
            public override Type SchemaType => typeof(TSchema);
        }

        abstract class TestCase<TSchema, I1, I2, O> : TestCase where TSchema : GltfInteractivityNodeSchema
        {
            public I1 a;
            public I2 b;
            public I1 c;
            public O expected;
            public override object A => a;
            public override object B => b;
            public override object C => c;
            public override object Expected => expected;
            public override Type SchemaType => typeof(TSchema);
        }
        
        abstract class TestCase<TSchema, I1, I2, I3, O> : TestCase where TSchema : GltfInteractivityNodeSchema
        {
            public I1 a;
            public I2 b;
            public I3 c;
            public O expected;
            public override object A => a;
            public override object B => b;
            public override object C => c;
            public override object Expected => expected;
            public override Type SchemaType => typeof(TSchema);
        }
        
        class ZeroArg<TSchema, O> : TestCase<TSchema, float, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<O> operation;
            public override void Run() => expected = operation();
        }

        class OneArg<TSchema, I, O> : TestCase<TSchema, I, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<I, O> operation;
            public override void Run() => expected = operation(a);
        }

        class TwoArg<TSchema, I, O> : TestCase<TSchema, I, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<I, I, O> operation;
            public override void Run() => expected = operation(a, b);
        }

        class TwoArg<TSchema, I1, I2, O> : TestCase<TSchema, I1, I2, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<I1, I2, O> operation;
            public override void Run() => expected = operation(a, b);
        }
        
        class ThreeArg<TSchema, I, O> : TestCase<TSchema, I, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<I, I, I, O> operation;
            public override void Run() => expected = operation(a, b, c);
        }
        
        class ThreeArg<TSchema, I1, I2, I3, O> : TestCase<TSchema, I1, I2, I3, O> where TSchema : GltfInteractivityNodeSchema
        {
            public Func<I1, I2, I3, O> operation;
            public override void Run() => expected = operation(a, b, c);
        }

        private static List<TestCase> mathCases = new List<TestCase>()
        {
            // Constant Nodes
            new ZeroArg<Math_ENode, float>()
            {
                approximate = true,
                operation = () => 2.718281828459045f,
            },
            new ZeroArg<Math_PiNode, float>()
            {
                approximate = true,
                operation = () => Mathf.PI,
            },
            new ZeroArg<Math_InfNode, float>()
            {
                operation = () => Mathf.Infinity,
            },
            new ZeroArg<Math_NaNNode, float>()
            {
                operation = () => float.NaN,
            },

            // Arithmetic Nodes
            new OneArg<Math_AbsNode, float, float>()
            {
                a = -7,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<Math_AbsNode, float, float>()
            {
                a = 7,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<Math_AbsNode, float, float>()
            {
                a = 0,
                operation = (a) => Mathf.Abs(a),
            },
            new OneArg<Math_SignNode, float, float>()
            {
                a = -9,
                operation = (a) => Mathf.Sign(a),
            },
            new OneArg<Math_SignNode, float, float>()
            {
                a = 9,
                operation = (a) => Mathf.Sign(a),
            },
            new OneArg<Math_TruncNode, float, float>()
            {
                a = 9.234324f,
                operation = (a) => Mathf.Sign(a) * Mathf.Floor(Mathf.Abs(a)),
            },
            new OneArg<Math_FloorNode, float, float>()
            {
                a = -2323.4346f,
                operation = (a) => Mathf.Floor(a),
            },
            new OneArg<Math_CeilNode, float, float>()
            {
                a = 757.003244f,
                operation = (a) => Mathf.Ceil(a),
            },
            new OneArg<Math_FractNode, float, float>()
            {
                a = -32434.96784f,
                approximate = true,
                operation = (a) => a - Mathf.Floor(a),
            },
            new OneArg<Math_NegNode, float, float>()
            {
                a = -8923448.234f,
                operation = (a) => -a,
            },
            // Arithmetic with two inputs
            new TwoArg<Math_AddNode, float, float>()
            {
                a = -1f,
                b = 3f,
                operation = (a, b) => a + b,
            },
            new TwoArg<Math_SubNode, float, float>()
            {
                a = 7f,
                b = 9f,
                operation = (a, b) => a - b,
            },
            new TwoArg<Math_MulNode, float, float>()
            {
                a = 345.234432f,
                b = 1 / 345.234432f,
                approximate = true,
                operation = (a, b) => a * b,
            },
            new TwoArg<Math_DivNode, float, float>()
            {
                a = 8989.324f,
                b = 2134.234f,
                approximate = true,
                operation = (a, b) => a / b,
            },
            new TwoArg<Math_RemNode, float, float>()
            {
                a = 19.423534f,
                b = 2.234f,
                approximate = true,
                operation = (a, b) => a % b,
            },
            new TwoArg<Math_MinNode, float, float>()
            {
                a = 17.21323f,
                b = -324.234f,
                operation = (a, b) => Mathf.Min(a, b),
            },
            new TwoArg<Math_MaxNode, float, float>()
            {
                a = 4653.234f,
                b = 91293923.234f,
                operation = (a, b) => Mathf.Max(a, b),
            },
            new ThreeArg<Math_ClampNode, float, float>()
            {
                a = 9,
                b = 2,
                c = 3,
                operation = (a, b, c) => Mathf.Clamp(a, b, c),
            },
            new OneArg<Math_SaturateNode, float, float>()
            {
                a = -1.5f,
                operation = (a) => Mathf.Clamp01(a),
            },
            new ThreeArg<Math_MixNode, float, float>()
            {
                a = 1f,
                b = 2f,
                c = 2f,
                operation = (a, b, c) => Mathf.LerpUnclamped(a, b, c),
            },
            // Comparison Nodes
            new TwoArg<Math_EqNode, float, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = 1.324f,
                b = 2.32423f,
                operation = (a, b) => a == b,
            },
            new TwoArg<Math_EqNode, bool, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = true,
                b = false,
                operation = (a, b) => a == b,
            },
            new TwoArg<Math_EqNode, Vector2, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = new Vector2(4,5),
                b = new Vector2(4,5),
                operation = (a, b) => a == b,
            },
            new TwoArg<Math_EqNode, float, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = float.NaN,
                b = float.NaN,
                operation = (a, b) => false,
            },
            new TwoArg<Math_EqNode, float, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = float.NaN,
                b = 1f,
                operation = (a, b) => false,
            },
            new TwoArg<Math_EqNode, float, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = float.PositiveInfinity,
                b = float.PositiveInfinity,
                operation = (a, b) => true,
            },
            new TwoArg<Math_EqNode, float, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = float.PositiveInfinity,
                b = float.NegativeInfinity,
                operation = (a, b) => false,
            },
            new TwoArg<Math_EqNode, Matrix4x4, bool>()
            {
                autoCreateTestsForAllSupportedInputs = false,
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                b = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                operation = (a, b) => true,
            },
            new TwoArg<Math_LtNode, float, bool>()
            {
                a = 1,
                b = 2,
                operation = (a, b) => a < b,
            },
            new TwoArg<Math_LeNode, float, bool>()
            {
                a = 1.3465f,
                b = 1.3465f,
                operation = (a, b) => a <= b,
            },
            new TwoArg<Math_GtNode, float, bool>()
            {
                a = 1,
                b = 2,
                operation = (a, b) => a > b,
            },
            new TwoArg<Math_GeNode, float, bool>()
            {
                a = 1.3465f,
                b = 1.3465f,
                operation = (a, b) => a >= b,
            },
            // Special nodes
            new OneArg<Math_IsNaNNode, float, bool>()
            {
                a = float.NaN,
                operation = (a) => float.IsNaN(a),
            },
            new OneArg<Math_IsNaNNode, float, bool>()
            {
                a = 1f,
                operation = (a) => float.IsNaN(a),
            },
            new OneArg<Math_IsInfNode, float, bool>()
            {
                a = float.PositiveInfinity,
                operation = (a) => float.IsInfinity(a),
            },
            new OneArg<Math_IsInfNode, float, bool>()
            {
                a = float.NegativeInfinity,
                operation = (a) => float.IsInfinity(a),
            },
            // math/select
            new OneArg<Math_RadNode, float, float>()
            {
                a = 75,
                approximate = true,
                operation = (a) => a * Mathf.PI / 180,
            },
            new OneArg<Math_DegNode, float, float>()
            {
                a = Mathf.PI * 0.35f,
                approximate = true,
                operation = (a) => a * 180 / Mathf.PI,
            },
            new OneArg<Math_SinNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Sin(a),
            },
            new OneArg<Math_CosNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Cos(a),
            },
            new OneArg<Math_TanNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => Mathf.Tan(a),
            },
            new OneArg<Math_AsinNode, float, float>()
            {
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Asin(a),
            },
            new OneArg<Math_AcosNode, float, float>()
            {
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Acos(a),
            },
            new OneArg<Math_AtanNode, float, float>()
            {
                a = 0.5f,
                approximate = true,
                operation = (a) => Mathf.Atan(a),
            },
            new TwoArg<Math_Atan2Node, float, float>()
            {
                a = 0.5f,
                b = 0.5f,
                approximate = true,
                operation = (a, b) => Mathf.Atan2(a, b),
            },
            // Hyperbolic nodes
            new OneArg<Math_SinHNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Sinh(a),
            },
            new OneArg<Math_CosHNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Cosh(a),
            },
            new OneArg<Math_TanHNode, float, float>()
            {
                a = 4.324f,
                approximate = true,
                operation = (a) => (float)Math.Tanh(a),
            },
            new OneArg<Math_AsinHNode, float, float>()
            {
                a = 0.5f,
                approximate = true,
                operation = (a) => (float)Math.Asinh(a),
            },
            new OneArg<Math_AcosHNode, float, float>()
            {
                a = 1.5f,
                approximate = true,
                operation = (a) => (float)Math.Acosh(a),
            },
            new OneArg<Math_AtanHNode, float, float>()
            {
                a = 0.5f,
                approximate = true,
                operation = (a) => (float)Math.Atanh(a),
            },
            // Exponential nodes
            new OneArg<Math_ExpNode, float, float>()
            {
                a = 1.2132f,
                approximate = true,
                operation = (a) => Mathf.Exp(a),
            },
            new OneArg<Math_LogNode, float, float>()
            {
                a = 26436.23423f,
                approximate = true,
                operation = (a) => Mathf.Log(a),
            },
            new OneArg<Math_Log2Node, float, float>()
            {
                a = 6443.243f,
                approximate = true,
                operation = (a) => Mathf.Log(a, 2),
            },
            new OneArg<Math_Log10Node, float, float>()
            {
                a = 8768.24f,
                approximate = true,
                operation = (a) => Mathf.Log10(a),
            },
            new OneArg<Math_SqrtNode, float, float>()
            {
                a = 4556.234f,
                approximate = true,
                operation = (a) => Mathf.Sqrt(a),
            },
            new OneArg<Math_CbrtNode, float, float>()
            {
                a = 9769.234f,
                approximate = true,
                operation = (a) => Mathf.Pow(a, 1f / 3f),
            },
            new TwoArg<Math_PowNode, float, float>()
            {
                a = 7.764f,
                b = 2.345f,
                approximate = true,
                operation = (a, b) => Mathf.Pow(a, b),
            },
            new OneArg<Math_TransposeNode, Matrix4x4, Matrix4x4>()
            {
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                approximate = true,
                operation = (a) => Matrix4x4.Transpose(a),
            },
            new OneArg<Math_InverseNode, Matrix4x4, Matrix4x4>()
            {
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                approximate = true,
                operation = (a) => Matrix4x4.Inverse(a),
            },
            new OneArg<Math_DeterminantNode, Matrix4x4, float>()
            {
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                approximate = true,
                operation = (a) => Matrix4x4.Determinant(a),
            },
            new TwoArg<Math_Transform_Float4Node, Vector4, Matrix4x4, Vector4>()
            {
                a = new Vector4(1f,2f,3f,4f),
                b = Matrix4x4.TRS(Vector3.right, Quaternion.identity, Vector3.up),
                approximate = true,
                operation = (a,b) =>  b * a,
            },
            new TwoArg<Math_MatMulNode, Matrix4x4, Matrix4x4>()
            {
                a = Matrix4x4.TRS(Vector3.one, Quaternion.identity, Vector3.up),
                b = Matrix4x4.TRS(Vector3.right, Quaternion.identity, Vector3.up),
                approximate = true,
                operation = (a,b) =>  a * b,
            },
            new ThreeArg<Math_MatComposeNode, Vector3, Quaternion, Vector3, Matrix4x4>()
            {
                a = Vector3.one,
                b = Quaternion.identity,
                c = Vector3.up,
                approximate = true,
                operation = (a,b,c) =>  Matrix4x4.TRS(a, b, c),
            },
            new OneArg<Math_LengthNode, Vector2, float>()
            {
                a = new Vector2(13f, 2f),
                approximate = true,
                autoCreateTestsForAllSupportedInputs = false,
                operation = (a) => a.magnitude,
            },
            new OneArg<Math_LengthNode, Vector3, float>()
            {
                a = new Vector3(13f, 2f, 15f),
                approximate = true,
                autoCreateTestsForAllSupportedInputs = false,
                operation = (a) => a.magnitude,
            },            
            new OneArg<Math_LengthNode, Vector4, float>()
            {
                a = new Vector4(13f, 2f,23f,123f),
                approximate = true,
                autoCreateTestsForAllSupportedInputs = false,
                operation = (a) => a.magnitude,
            },      
            new TwoArg<Math_AndNode, bool, bool>()
            {
                a = true,
                b = false,
                operation = (a, b) => a && b,
            },
            new TwoArg<Math_AndNode, bool, bool>()
            {
                a = true,
                b = true,
                operation = (a, b) => a && b,
            },
            new TwoArg<Math_AndNode, bool, bool>()
            {
                a = false,
                b = false,
                operation = (a, b) => a && b,
            },
            new TwoArg<Math_OrNode, bool, bool>()
            {
                a = true,
                b = false,
                operation = (a, b) => a || b,
            },
            new TwoArg<Math_OrNode, bool, bool>()
            {
                a = false,
                b = false,
                operation = (a, b) => a || b,
            },
            new TwoArg<Math_OrNode, bool, bool>()
            {
                a = true,
                b = true,
                operation = (a, b) => a || b,
            },
            new TwoArg<Math_XorNode, bool, bool>()
            {
                a = true,
                b = false,
                operation = (a, b) => a ^ b,
            },
            new TwoArg<Math_XorNode, bool, bool>()
            {
                a = false,
                b = false,
                operation = (a, b) => a ^ b,
            },
            new TwoArg<Math_XorNode, bool, bool>()
            {
                a = true,
                b = true,
                operation = (a, b) => a ^ b,
            },
            new OneArg<Math_NotNode, bool, bool>()
            {
                a = true,
                operation = (a) => !a,
            },
            new OneArg<Math_NotNode, bool, bool>()
            {
                a = false,
                operation = (a) => !a,
            },
            new TwoArg<Math_DotNode, Vector2, float>()
            {
                a = new Vector2(1f, 2f),
                b = new Vector2(3f, 4f),
                approximate = true,
                operation = (a, b) => Vector2.Dot(a, b),
                autoCreateTestsForAllSupportedInputs = false,
            },
            new TwoArg<Math_DotNode, Vector3, float>()
            {
                a = new Vector3(1f, 2f, 3f),
                b = new Vector3(4f, 5f, 6f),
                approximate = true,
                operation = (a, b) => Vector3.Dot(a, b),
                autoCreateTestsForAllSupportedInputs = false,
            },
            new TwoArg<Math_DotNode, Vector4, float>()
            {
                a = new Vector4(1f, 2f, 3f, 4f),
                b = new Vector4(5f, 6f, 7f, 8f),
                approximate = true,
                operation = (a, b) => Vector4.Dot(a, b),
                autoCreateTestsForAllSupportedInputs = false,
            },
            new ThreeArg<Math_Rotate3dNode, Vector3, Vector3, float, Vector3>()
            {
                a = new Vector3(1f, 2f, 3f),
                b = new Vector3(0f, 1f, 0f),
                c = 0.5f,
                approximate = true,
                operation = (a, b, c) => Quaternion.AngleAxis(Mathf.Rad2Deg * c, b) * a,
            },
            new ThreeArg<Math_Rotate3dNode, Vector3, Vector3, float, Vector3>()
            {
                a = new Vector3(1f, 2f, 3f),
                b = new Vector3(1f, 0f, 0f),
                c = -0.5f,
                approximate = true,
                operation = (a, b, c) => Quaternion.AngleAxis(Mathf.Rad2Deg * c, b) * a,
            },
            new ThreeArg<Math_Rotate3dNode, Vector3, Vector3, float, Vector3>()
            {
                a = new Vector3(1f, 2f, 3f),
                b = new Vector3(0f, 0f, 1f),
                c = -0.5f,
                approximate = true,
                operation = (a, b, c) => Quaternion.AngleAxis(Mathf.Rad2Deg * c, b) * a,
            },
            new TwoArg<Math_Rotate2dNode, Vector2, float, Vector2>()
            {
                a = new Vector2(1f, 2f),
                b = 0.5f,
                approximate = true,
                operation = (a, b) =>
                { 
                    var r = Quaternion.AngleAxis(Mathf.Rad2Deg * b, Vector3.forward) * a;
                    return new Vector2(r.x, r.y);
                },
            },
            new TwoArg<Math_Rotate2dNode, Vector2, float, Vector2>()
            {
                a = new Vector2(0f, 1f),
                b = 0.5f,
                approximate = true,
                operation = (a, b) =>
                { 
                    var r = Quaternion.AngleAxis(Mathf.Rad2Deg * b, Vector3.forward) * a;
                    return new Vector2(r.x, r.y);
                },
            },
            new TwoArg<Math_Rotate2dNode, Vector2, float, Vector2>()
            {
                a = new Vector2(1f, 2f),
                b = -0.3f,
                approximate = true,
                operation = (a, b) =>
                { 
                    var r = Quaternion.AngleAxis(Mathf.Rad2Deg * b, Vector3.forward) * a;
                    return new Vector2(r.x, r.y);
                },
            },
            
        };
        
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
            foreach (var group in mathCases.GroupBy(t => t.SchemaType))
            {
                var newTest = new MathTestCase();
                newTestCases.Add(newTest);
                newTest.schemaType = group.First().SchemaType;
                var schemaInstance = GltfInteractivityNodeSchema.GetSchema(newTest.schemaType);
                
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
                    //bool first = true;
                    foreach (var suppType in aSocket.SupportedTypes.Where(s =>
                                 GltfTypes.GetTypeMapping(typeA).GltfSignature != s))
                    {
                        //first = true;
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
                            //first = false;
                        }
                    }
                }
            }

            List<ITestCase> additionalCases = new List<ITestCase>();
            additionalCases.Add(new Math_SelectTest());
            additionalCases.Add(new Math_SwitchTest());
            additionalCases.Add(new Math_RandomTest());
            additionalCases.Add(new Math_MatDecomposeTest());
            
            return newTestCases.Concat(additionalCases).OrderBy(c => c.GetTestName()).ToArray();
        }
        
#if UNITY_EDITOR

        [CustomEditor(typeof(MathTestCreator))]
        public class MathInspector : UnityEditor.Editor
        {
            public bool exportAllInOne = true;
            public bool exportIndividual = true;

            public override void OnInspectorGUI()
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MathTestCreator.testExporter)));

                exportIndividual = GUILayout.Toggle(exportIndividual, "Export Individual Tests");
                exportAllInOne = GUILayout.Toggle(exportAllInOne, "Export All In One");

                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MathTestCreator.indexFilename)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(MathTestCreator.testName)));
                
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
                
                if (GUILayout.Button("Export Tests"))
                {
                    ((MathTestCreator)target).ExportTests(exportAllInOne, exportIndividual);
                }
            }
        }
#endif
    }
}