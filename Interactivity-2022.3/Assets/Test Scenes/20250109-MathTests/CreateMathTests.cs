#if UNITY_EDITOR
// TODO make runtime capable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CreateMathTests : MonoBehaviour
{
    public MathTests prefab;

    abstract class TestCase
    {
        public string schema;
        public virtual void Run() {}
        public abstract object A { get; }
        public abstract object B { get; }
        public abstract object C { get; }
        public abstract object Expected { get; }
    }
    
    abstract class TestCase<I,O>: TestCase {
        public I a;
        public I b;
        public I c;
        public O expected;
        public override object A => a;
        public override object B => b;
        public override object C => c;
        public override object Expected => expected;
    }

    class ZeroArg<O>: TestCase<float,O>
    {
        public Func<O> operation;
        public override void Run() => expected = operation();
    }
    
    class OneArg<I,O>: TestCase<I,O>
    {
        public Func<I,O> operation;
        public override void Run() => expected = operation(a);
    }
    
    class TwoArg<I,O>: TestCase<I,O>
    {
        public Func<I,I,O> operation;
        public override void Run() => expected = operation(a, b);
    }
    
    class ThreeArg<I,O>: TestCase<I,O>
    {
        public Func<I,I,I,O> operation;
        public override void Run() => expected = operation(a, b, c);
    }
    
    private static List<TestCase> testCases = new List<TestCase>()
    {
        // Constant Nodes
        new ZeroArg<float>()
        {
            schema = "math/e",
            operation = () => 2.718281828459045f,
        },
        new ZeroArg<float>()
        {
            schema = "math/pi",
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
        new OneArg<float,float>()
        {
            schema = "math/abs",
            a = -7,
            operation = (a) => Mathf.Abs(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/sign",
            a = -9,
            operation = (a) => Mathf.Sign(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/trunc",
            a = 9.234324f,
            operation = (a) => Mathf.Sign(a) * Mathf.Floor(Mathf.Abs(a)),
        },
        new OneArg<float,float>()
        {
            schema = "math/floor",
            a = -2323.4346f,
            operation = (a) => Mathf.Floor(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/ceil",
            a = 757.003244f,
            operation = (a) => Mathf.Ceil(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/fract",
            a = -32434.96784f,
            operation = (a) => a - Mathf.Floor(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/neg",
            a = -8923448.234f,
            operation = (a) => -a,
        },
        // Arithmetic with two inputs
        new TwoArg<float,float>()
        {
            schema = "math/add",
            a = -1f,
            b = 3f,
            operation = (a, b) => a + b,
        },
        new TwoArg<float,float>()
        {
            schema = "math/sub",
            a = 7f,
            b = 9f,
            operation = (a, b) => a - b,
        },
        new TwoArg<float,float>()
        {
            schema = "math/mul",
            a = 345.234432f,
            b = 1 / 345.234432f,
            operation = (a, b) => a * b,
        },
        new TwoArg<float,float>()
        {
            schema = "math/div",
            a = 8989.324f,
            b = 2134.234f,
            operation = (a, b) => a / b,
        },
        new TwoArg<float,float>()
        {
            schema = "math/rem",
            a = 19.423534f,
            b = 2.234f,
            operation = (a, b) => a % b,
        },
        new TwoArg<float,float>()
        {
            schema = "math/min",
            a = 17.21323f,
            b = -324.234f,
            operation = (a, b) => Mathf.Min(a, b),
        },
        new TwoArg<float,float>()
        {
            schema = "math/max",
            a = 4653.234f,
            b = 91293923.234f,
            operation = (a, b) => Mathf.Max(a, b),
        },
        new ThreeArg<float,float>()
        {
            schema = "math/clamp",
            a = 9,
            b = 2,
            c = 3,
            operation = (a, b, c) => Mathf.Clamp(a, b, c),
        },
        new OneArg<float,float>()
        {
            schema = "math/saturate",
            a = -1.5f,
            operation = (a) => Mathf.Clamp01(a),
        },
        new ThreeArg<float,float>()
        {
            schema = "math/mix",
            a = 1f,
            b = 2f,
            c = 2f,
            operation = (a, b, c) => Mathf.LerpUnclamped(a, b, c),
        },
        // Comparison Nodes
        new TwoArg<float,bool>()
        {
            schema = "math/eq",
            a = 1.324f,
            b = 2.32423f,
            operation = (a, b) => a == b,
        },
        new TwoArg<float,bool>()
        {
            schema = "math/lt",
            a = 1,
            b = 2,
            operation = (a, b) => a < b,
        },
        new TwoArg<float,bool>()
        {
            schema = "math/le",
            a = 1.3465f,
            b = 1.3465f,
            operation = (a, b) => a <= b,
        },
        new TwoArg<float,bool>()
        {
            schema = "math/gt",
            a = 1,
            b = 2,
            operation = (a, b) => a > b,
        },
        new TwoArg<float,bool>()
        {
            schema = "math/ge",
            a = 1.3465f,
            b = 1.3465f,
            operation = (a, b) => a >= b,
        },
        // Special nodes
        new OneArg<float,bool>()
        {
            schema = "math/isnan",
            a = float.NaN,
            operation = (a) => float.IsNaN(a),
        },
        new OneArg<float,bool>()
        {
            schema = "math/isinf",
            a = float.PositiveInfinity,
            operation = (a) => float.IsInfinity(a),
        },
        // math/select
        new OneArg<float,float>()
        {
            schema = "math/rad",
            a = 75,
            operation = (a) => a * Mathf.PI / 180,
        },
        new OneArg<float,float>()
        {
            schema = "math/deg",
            a = Mathf.PI * 0.35f,
            operation = (a) => a * 180 / Mathf.PI,
        },
        new OneArg<float,float>()
        {
            schema = "math/sin",
            a = 4.324f,
            operation = (a) => Mathf.Sin(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/cos",
            a = 4.324f, 
            operation = (a) => Mathf.Cos(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/tan",
            a = 4.324f,
            operation = (a) => Mathf.Tan(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/asin",
            a = 0.5f,
            operation = (a) => Mathf.Asin(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/acos",
            a = 0.5f,
            operation = (a) => Mathf.Acos(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/atan",
            a = 0.5f,
            operation = (a) => Mathf.Atan(a),
        },
        new TwoArg<float,float>()
        {
            schema = "math/atan2",
            a = 0.5f,
            b = 0.5f,
            operation = (a, b) => Mathf.Atan2(a, b),
        },
        // Hyperbolic nodes
        new OneArg<float,float>()
        {
            schema = "math/sinh",
            a = 4.324f,
            operation = (a) => (float) Math.Sinh(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/cosh",
            a = 4.324f,
            operation = (a) => (float) Math.Cosh(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/tanh",
            a = 4.324f,
            operation = (a) => (float) Math.Tanh(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/asinh",
            a = 0.5f,
            operation = (a) => (float) Math.Asinh(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/acosh",
            a = 1.5f,
            operation = (a) => (float) Math.Acosh(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/atanh",
            a = 0.5f,
            operation = (a) => (float) Math.Atanh(a),
        },
        // Exponential nodes
        new OneArg<float,float>()
        {
            schema = "math/exp",
            a = 1.2132f,
            operation = (a) => Mathf.Exp(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/log",
            a = 26436.23423f,
            operation = (a) => Mathf.Log(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/log2",
            a = 6443.243f,
            operation = (a) => Mathf.Log(a, 2),
        },
        new OneArg<float,float>()
        {
            schema = "math/log10",
            a = 8768.24f,
            operation = (a) => Mathf.Log10(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/sqrt",
            a = 4556.234f,
            operation = (a) => Mathf.Sqrt(a),
        },
        new OneArg<float,float>()
        {
            schema = "math/cbrt",
            a = 9769.234f,
            operation = (a) => Mathf.Pow(a, 1f / 3f),
        },
        new TwoArg<float,float>()
        {
            schema = "math/pow",
            a = 7.764f,
            b = 2.345f,
            operation = (a, b) => Mathf.Pow(a, b),
        },
    };

    [ContextMenu("Create Tests")]
    void CreateTests()
    {
        var childStateByName = new Dictionary<string, bool>();
        // delete all child game objects
        for (var k = transform.childCount - 1; k >= 0; k--) {
            var child = transform.GetChild(k);
            childStateByName.Add(child.name, child.gameObject.activeSelf);
            DestroyImmediate(child.gameObject);
        }

        var x = 0;
        var y = 0;
        foreach (var testCase in testCases)
        {
            testCase.Run();
            
            var test = PrefabUtility.InstantiatePrefab(prefab) as MathTests;
            test.name = testCase.schema;
            
            if (childStateByName.TryGetValue(test.name, out var active))
                test.gameObject.SetActive(active);
            
            test.transform.SetParent(this.transform, false);
            test.schema = testCase.schema;
            test.a = testCase.A;
            test.b = testCase.B;
            test.c = testCase.C;
            test.expected = testCase.Expected;
            test.label.text = testCase.schema;
            
            // align
            test.transform.localPosition = new Vector3(-x * 1.5f, -y * 1.5f, 0);
            x++;
            if (x > 10)
            {
                x = 0;
                y++;
            }
        }
    }
}

#endif