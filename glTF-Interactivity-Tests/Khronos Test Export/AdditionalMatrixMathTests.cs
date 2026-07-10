using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Coverage for the P1 "Math — vector / matrix / quaternion" gap:
    ///   * math/transpose, math/determinant, math/inverse, math/matMul at 2x2 and 3x3
    ///     (the table only exercised 4x4), including singular determinant/inverse.
    ///   * math/matMul non-commutativity (A·B != B·A).
    ///   * math/quatFromDirections antiparallel + parallel directions.
    ///   * math/quatFromUpForward degenerate (colinear up/forward).
    ///   * math/quatToAxisAngle identity quaternion (angle 0, arbitrary axis).
    ///
    /// The 2x2/3x3 matrix results are read back component-by-component through the
    /// extract2x2 / extract3x3 nodes and compared as floats, because the checkbox
    /// proximity path only supports matrix comparison for float4x4. Reference values
    /// are computed by embedding the NxN matrix in the upper-left block of an identity
    /// float4x4 and using Unity's (already-trusted) Matrix4x4 math, then reading the
    /// block back out in the same column-major order the extract nodes emit.
    /// </summary>
    internal static class MatrixMathRef
    {
        // Column-major: m0,m1 = column 0 (rows 0,1); m2,m3 = column 1 (rows 0,1).
        public static Matrix4x4 To4x4(GltfFloat2x2 m)
        {
            var r = Matrix4x4.identity;
            r.SetColumn(0, new Vector4(m.m0, m.m1, 0f, 0f));
            r.SetColumn(1, new Vector4(m.m2, m.m3, 0f, 0f));
            return r; // columns 2,3 stay identity -> block diagonal
        }

        public static Matrix4x4 To4x4(GltfFloat3x3 m)
        {
            var r = Matrix4x4.identity;
            r.SetColumn(0, new Vector4(m.m0, m.m1, m.m2, 0f));
            r.SetColumn(1, new Vector4(m.m3, m.m4, m.m5, 0f));
            r.SetColumn(2, new Vector4(m.m6, m.m7, m.m8, 0f));
            return r; // column 3 stays identity -> block diagonal
        }

        public static float[] From2x2(Matrix4x4 r)
        {
            var c0 = r.GetColumn(0);
            var c1 = r.GetColumn(1);
            return new[] { c0.x, c0.y, c1.x, c1.y };
        }

        public static float[] From3x3(Matrix4x4 r)
        {
            var c0 = r.GetColumn(0);
            var c1 = r.GetColumn(1);
            var c2 = r.GetColumn(2);
            return new[] { c0.x, c0.y, c0.z, c1.x, c1.y, c1.z, c2.x, c2.y, c2.z };
        }

        // Sets a matrix value on a union-typed (float2x2/3x3/4x4) input socket and pins the
        // concrete type, mirroring how MathTestCase feeds matrix inputs so the output
        // type-dependency resolves for the downstream extract node.
        public static void SetMatrix(ValueInRef input, object matrix)
        {
            input.SetValue(matrix).SetType(TypeRestriction.LimitToType(GltfTypes.TypeIndex(matrix.GetType())));
        }

        // Wires component-wise proximity checks for each extracted float output.
        public static void CheckComponents(TestContext context, GltfInteractivityExportNode extractNode,
            string idValueIn, ValueOutRef matrixResult, float[] expected, CheckBox[] boxes)
        {
            extractNode.ValueIn(idValueIn).ConnectToSource(matrixResult);
            for (int i = 0; i < expected.Length; i++)
            {
                boxes[i].proximityCheckDistance = 0.0005f;
                boxes[i].SetupCheck(extractNode.ValueOut(i.ToString()), out var flow, expected[i], true);
                context.AddToCurrentEntrySequence(flow);
            }
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Transpose2x2Test : ITestCase
    {
        private static readonly GltfFloat2x2 Input = new GltfFloat2x2(4f, 2f, 1f, 3f);
        private CheckBox[] _boxes;

        public string GetTestName() => "math/transpose2x2";
        public string GetTestDescription() => "Transpose of a float2x2, compared component-wise.";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[4];
            for (int i = 0; i < 4; i++)
                _boxes[i] = context.AddCheckBox($"transpose2x2[{i}]");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From2x2(Matrix4x4.Transpose(MatrixMathRef.To4x4(Input)));

            var node = nc.CreateNode<Math_TransposeNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_TransposeNode.IdValueA), Input);

            var extract = nc.CreateNode<Math_Extract2x2Node>();
            context.NewEntryPoint(GetTestName());
            MatrixMathRef.CheckComponents(context, extract, Math_Extract2x2Node.IdValueIn, node.FirstValueOut(), expected, _boxes);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Transpose3x3Test : ITestCase
    {
        private static readonly GltfFloat3x3 Input = new GltfFloat3x3(1f, 2f, 0f, 0f, 1f, 2f, 2f, 0f, 1f);
        private CheckBox[] _boxes;

        public string GetTestName() => "math/transpose3x3";
        public string GetTestDescription() => "Transpose of a float3x3, compared component-wise.";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[9];
            for (int i = 0; i < 9; i++)
            {
                _boxes[i] = context.AddCheckBox($"transpose3x3[{i}]");
                if (i == 4)
                    context.NewRow();
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From3x3(Matrix4x4.Transpose(MatrixMathRef.To4x4(Input)));

            var node = nc.CreateNode<Math_TransposeNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_TransposeNode.IdValueA), Input);

            var extract = nc.CreateNode<Math_Extract3x3Node>();
            context.NewEntryPoint(GetTestName());
            MatrixMathRef.CheckComponents(context, extract, Math_Extract3x3Node.IdValueIn, node.FirstValueOut(), expected, _boxes);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Determinant2x2Test : ITestCase
    {
        private static readonly GltfFloat2x2 Input = new GltfFloat2x2(4f, 2f, 1f, 3f); // det = 4*3 - 1*2 = 10
        private static readonly GltfFloat2x2 Singular = new GltfFloat2x2(2f, 4f, 1f, 2f); // rows proportional -> det = 0
        private CheckBox _valueBox;
        private CheckBox _singularBox;

        public string GetTestName() => "math/determinant2x2";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _valueBox = context.AddCheckBox("determinant2x2");
            _singularBox = context.AddCheckBox("determinant2x2 singular = 0");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            var node = nc.CreateNode<Math_DeterminantNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_DeterminantNode.IdValueA), Input);
            context.NewEntryPoint(GetTestName());
            _valueBox.proximityCheckDistance = 0.0005f;
            _valueBox.SetupCheck(node.FirstValueOut(), out var flow, MatrixMathRef.To4x4(Input).determinant, true);
            context.AddToCurrentEntrySequence(flow);

            var singularNode = nc.CreateNode<Math_DeterminantNode>();
            MatrixMathRef.SetMatrix(singularNode.ValueIn(Math_DeterminantNode.IdValueA), Singular);
            _singularBox.proximityCheckDistance = 0.0005f;
            _singularBox.SetupCheck(singularNode.FirstValueOut(), out var singularFlow, 0f, true);
            context.AddToCurrentEntrySequence(singularFlow);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Determinant3x3Test : ITestCase
    {
        private static readonly GltfFloat3x3 Input = new GltfFloat3x3(1f, 2f, 0f, 0f, 1f, 2f, 2f, 0f, 1f); // det = 9
        private static readonly GltfFloat3x3 Singular = new GltfFloat3x3(1f, 2f, 3f, 1f, 2f, 3f, 4f, 5f, 6f); // equal columns -> det = 0
        private CheckBox _valueBox;
        private CheckBox _singularBox;

        public string GetTestName() => "math/determinant3x3";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _valueBox = context.AddCheckBox("determinant3x3");
            _singularBox = context.AddCheckBox("determinant3x3 singular = 0");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            var node = nc.CreateNode<Math_DeterminantNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_DeterminantNode.IdValueA), Input);
            context.NewEntryPoint(GetTestName());
            _valueBox.proximityCheckDistance = 0.0005f;
            _valueBox.SetupCheck(node.FirstValueOut(), out var flow, MatrixMathRef.To4x4(Input).determinant, true);
            context.AddToCurrentEntrySequence(flow);

            var singularNode = nc.CreateNode<Math_DeterminantNode>();
            MatrixMathRef.SetMatrix(singularNode.ValueIn(Math_DeterminantNode.IdValueA), Singular);
            _singularBox.proximityCheckDistance = 0.0005f;
            _singularBox.SetupCheck(singularNode.FirstValueOut(), out var singularFlow, 0f, true);
            context.AddToCurrentEntrySequence(singularFlow);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Inverse2x2Test : ITestCase
    {
        private static readonly GltfFloat2x2 Input = new GltfFloat2x2(4f, 2f, 1f, 3f); // det = 10, invertible
        private static readonly GltfFloat2x2 Singular = new GltfFloat2x2(2f, 4f, 1f, 2f); // det = 0
        private CheckBox[] _boxes;
        private CheckBox _isValidBox;
        private CheckBox _singularIsValidBox;

        public string GetTestName() => "math/inverse2x2";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[4];
            for (int i = 0; i < 4; i++)
                _boxes[i] = context.AddCheckBox($"inverse2x2[{i}]");
            _isValidBox = context.AddCheckBox("inverse2x2 isValid");
            context.NewRow();
            _singularIsValidBox = context.AddCheckBox("singular inverse2x2 isValid=false");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From2x2(MatrixMathRef.To4x4(Input).inverse);

            var node = nc.CreateNode<Math_InverseNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_InverseNode.IdValueA), Input);
            context.NewEntryPoint(GetTestName());

            var extract = nc.CreateNode<Math_Extract2x2Node>();
            MatrixMathRef.CheckComponents(context, extract, Math_Extract2x2Node.IdValueIn,
                node.ValueOut(Math_InverseNode.IdOut), expected, _boxes);

            _isValidBox.SetupCheck(node.ValueOut(Math_InverseNode.IdIsValid), out var validFlow, true, false);
            context.AddToCurrentEntrySequence(validFlow);

            // Singular input: determinant is zero -> isValid must be false (spec).
            var singularNode = nc.CreateNode<Math_InverseNode>();
            MatrixMathRef.SetMatrix(singularNode.ValueIn(Math_InverseNode.IdValueA), Singular);
            _singularIsValidBox.SetupCheck(singularNode.ValueOut(Math_InverseNode.IdIsValid), out var singularFlow, false, false);
            context.AddToCurrentEntrySequence(singularFlow);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_Inverse3x3Test : ITestCase
    {
        private static readonly GltfFloat3x3 Input = new GltfFloat3x3(1f, 2f, 0f, 0f, 1f, 2f, 2f, 0f, 1f); // det = 9
        private static readonly GltfFloat3x3 Singular = new GltfFloat3x3(1f, 2f, 3f, 1f, 2f, 3f, 4f, 5f, 6f); // det = 0
        private CheckBox[] _boxes;
        private CheckBox _isValidBox;
        private CheckBox _singularIsValidBox;

        public string GetTestName() => "math/inverse3x3";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[9];
            for (int i = 0; i < 9; i++)
            {
                _boxes[i] = context.AddCheckBox($"inverse3x3[{i}]");
                if (i == 4)
                    context.NewRow();
            }
            _isValidBox = context.AddCheckBox("inverse3x3 isValid");
            context.NewRow();
            _singularIsValidBox = context.AddCheckBox("singular inverse3x3 isValid=false");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From3x3(MatrixMathRef.To4x4(Input).inverse);

            var node = nc.CreateNode<Math_InverseNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_InverseNode.IdValueA), Input);
            context.NewEntryPoint(GetTestName());

            var extract = nc.CreateNode<Math_Extract3x3Node>();
            MatrixMathRef.CheckComponents(context, extract, Math_Extract3x3Node.IdValueIn,
                node.ValueOut(Math_InverseNode.IdOut), expected, _boxes);

            _isValidBox.SetupCheck(node.ValueOut(Math_InverseNode.IdIsValid), out var validFlow, true, false);
            context.AddToCurrentEntrySequence(validFlow);

            var singularNode = nc.CreateNode<Math_InverseNode>();
            MatrixMathRef.SetMatrix(singularNode.ValueIn(Math_InverseNode.IdValueA), Singular);
            _singularIsValidBox.SetupCheck(singularNode.ValueOut(Math_InverseNode.IdIsValid), out var singularFlow, false, false);
            context.AddToCurrentEntrySequence(singularFlow);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_MatMul2x2Test : ITestCase
    {
        private static readonly GltfFloat2x2 A = new GltfFloat2x2(4f, 2f, 1f, 3f);
        private static readonly GltfFloat2x2 B = new GltfFloat2x2(1f, 0f, 2f, 1f);
        private CheckBox[] _boxes;

        public string GetTestName() => "math/matMul2x2";
        public string GetTestDescription() => "True matrix product of two float2x2, compared component-wise.";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[4];
            for (int i = 0; i < 4; i++)
                _boxes[i] = context.AddCheckBox($"matMul2x2[{i}]");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From2x2(MatrixMathRef.To4x4(A) * MatrixMathRef.To4x4(B));

            var node = nc.CreateNode<Math_MatMulNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_MatMulNode.IdValueA), A);
            MatrixMathRef.SetMatrix(node.ValueIn(Math_MatMulNode.IdValueB), B);

            var extract = nc.CreateNode<Math_Extract2x2Node>();
            context.NewEntryPoint(GetTestName());
            MatrixMathRef.CheckComponents(context, extract, Math_Extract2x2Node.IdValueIn, node.FirstValueOut(), expected, _boxes);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_MatMul3x3Test : ITestCase
    {
        private static readonly GltfFloat3x3 A = new GltfFloat3x3(1f, 2f, 0f, 0f, 1f, 2f, 2f, 0f, 1f);
        private static readonly GltfFloat3x3 B = new GltfFloat3x3(1f, 0f, 1f, 2f, 1f, 0f, 0f, 3f, 1f);
        private CheckBox[] _boxes;

        public string GetTestName() => "math/matMul3x3";
        public string GetTestDescription() => "True matrix product of two float3x3, compared component-wise.";

        public void PrepareObjects(TestContext context)
        {
            _boxes = new CheckBox[9];
            for (int i = 0; i < 9; i++)
            {
                _boxes[i] = context.AddCheckBox($"matMul3x3[{i}]");
                if (i == 4)
                    context.NewRow();
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;
            var expected = MatrixMathRef.From3x3(MatrixMathRef.To4x4(A) * MatrixMathRef.To4x4(B));

            var node = nc.CreateNode<Math_MatMulNode>();
            MatrixMathRef.SetMatrix(node.ValueIn(Math_MatMulNode.IdValueA), A);
            MatrixMathRef.SetMatrix(node.ValueIn(Math_MatMulNode.IdValueB), B);

            var extract = nc.CreateNode<Math_Extract3x3Node>();
            context.NewEntryPoint(GetTestName());
            MatrixMathRef.CheckComponents(context, extract, Math_Extract3x3Node.IdValueIn, node.FirstValueOut(), expected, _boxes);
        }
    }

    /// <summary>
    /// math/matMul is a true (non-commutative) matrix product: for the chosen A and B
    /// (rotations about different axes), A·B must differ from B·A. Passes when the two
    /// products are not equal.
    /// </summary>
    [TestCreator.IgnoreTestCase]
    public class Math_MatMulNonCommutativeTest : ITestCase
    {
        private CheckBox _differsBox;

        public string GetTestName() => "math/matMul-nonCommutative";
        public string GetTestDescription() => "A·B != B·A for non-commuting matrices.";

        public void PrepareObjects(TestContext context)
        {
            _differsBox = context.AddCheckBox("A·B != B·A");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            var a = Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
            var b = Matrix4x4.TRS(new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);

            var ab = nc.CreateNode<Math_MatMulNode>();
            MatrixMathRef.SetMatrix(ab.ValueIn(Math_MatMulNode.IdValueA), a);
            MatrixMathRef.SetMatrix(ab.ValueIn(Math_MatMulNode.IdValueB), b);

            var ba = nc.CreateNode<Math_MatMulNode>();
            MatrixMathRef.SetMatrix(ba.ValueIn(Math_MatMulNode.IdValueA), b);
            MatrixMathRef.SetMatrix(ba.ValueIn(Math_MatMulNode.IdValueB), a);

            context.NewEntryPoint(GetTestName());
            _differsBox.SetupCheckValueDiffers(out var valueAB, out var valueBA, out var flow);
            valueAB.ConnectToSource(ab.FirstValueOut());
            valueBA.ConnectToSource(ba.FirstValueOut());
            context.AddToCurrentEntrySequence(flow);
        }
    }

    /// <summary>
    /// math/quatFromDirections edge cases:
    ///   * parallel (identical) directions -> identity quaternion (checked directly).
    ///   * antiparallel directions -> the axis is implementation-defined, but the
    ///     resulting rotation must still map direction a onto direction b. Verified by
    ///     rotating a with the produced quaternion (via math/rotate3D) and comparing to b.
    /// </summary>
    [TestCreator.IgnoreTestCase]
    public class Math_QuatFromDirectionsEdgeTest : ITestCase
    {
        private CheckBox _parallelBox;
        private CheckBox _antiparallelBox;

        public string GetTestName() => "math/quatFromDirections-edge";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _parallelBox = context.AddCheckBox("parallel dirs -> identity");
            _antiparallelBox = context.AddCheckBox("antiparallel: rotate(a) == b");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            // Parallel / identical directions -> identity quaternion.
            var parallelNode = nc.CreateNode<Math_QuatFromDirectionsNode>();
            parallelNode.ValueIn(Math_QuatFromDirectionsNode.IdValueA).SetValue(new Vector3(0f, 1f, 0f));
            parallelNode.ValueIn(Math_QuatFromDirectionsNode.IdValueB).SetValue(new Vector3(0f, 1f, 0f));

            context.NewEntryPoint("parallel");
            _parallelBox.proximityCheckDistance = 0.001f;
            _parallelBox.SetupCheck(parallelNode.FirstValueOut(), out var parallelFlow, Quaternion.identity, true);
            context.AddToCurrentEntrySequence(parallelFlow);

            // Antiparallel: axis is arbitrary, but the rotation must map a onto b.
            var a = new Vector3(1f, 0f, 0f);
            var b = new Vector3(-1f, 0f, 0f);
            var antiNode = nc.CreateNode<Math_QuatFromDirectionsNode>();
            antiNode.ValueIn(Math_QuatFromDirectionsNode.IdValueA).SetValue(a);
            antiNode.ValueIn(Math_QuatFromDirectionsNode.IdValueB).SetValue(b);

            var rotateNode = nc.CreateNode<Math_Rotate3dNode>();
            rotateNode.ValueIn(Math_Rotate3dNode.IdInputVector).SetValue(a);
            rotateNode.ValueIn(Math_Rotate3dNode.IdInputQuaternion).ConnectToSource(antiNode.FirstValueOut());

            context.NewEntryPoint("antiparallel");
            _antiparallelBox.proximityCheckDistance = 0.001f;
            _antiparallelBox.SetupCheck(rotateNode.FirstValueOut(), out var antiFlow, b, true);
            context.AddToCurrentEntrySequence(antiFlow);
        }
    }

    /// <summary>
    /// math/quatFromUpForward with colinear up/forward: the up axis is ambiguous and the
    /// implementation picks an arbitrary perpendicular, but the forward axis must still be
    /// honoured. Verified by rotating the canonical forward (0,0,1) with the produced
    /// quaternion (via math/rotate3D) and comparing to the requested forward direction.
    /// </summary>
    [TestCreator.IgnoreTestCase]
    public class Math_QuatFromUpForwardDegenerateTest : ITestCase
    {
        private CheckBox _forwardBox;

        public string GetTestName() => "math/quatFromUpForward-degenerate";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _forwardBox = context.AddCheckBox("colinear up/forward: rotate(fwd) == forward");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            var forward = new Vector3(0f, 1f, 0f);
            var up = new Vector3(0f, 1f, 0f); // colinear with forward -> degenerate

            var node = nc.CreateNode<Math_QuatFromUpForwardNode>();
            node.ValueIn(Math_QuatFromUpForwardNode.IdForward).SetValue(forward);
            node.ValueIn(Math_QuatFromUpForwardNode.IdUp).SetValue(up);

            var rotateNode = nc.CreateNode<Math_Rotate3dNode>();
            rotateNode.ValueIn(Math_Rotate3dNode.IdInputVector).SetValue(new Vector3(0f, 0f, 1f));
            rotateNode.ValueIn(Math_Rotate3dNode.IdInputQuaternion).ConnectToSource(node.FirstValueOut());

            context.NewEntryPoint(GetTestName());
            _forwardBox.proximityCheckDistance = 0.001f;
            _forwardBox.SetupCheck(rotateNode.FirstValueOut(), out var flow, forward, true);
            context.AddToCurrentEntrySequence(flow);
        }
    }

    /// <summary>
    /// math/quatToAxisAngle for the identity quaternion: the angle must be 0 (the axis is
    /// an arbitrary unit vector per spec, so only the angle is asserted).
    /// </summary>
    [TestCreator.IgnoreTestCase]
    public class Math_QuatToAxisAngleIdentityTest : ITestCase
    {
        private CheckBox _angleBox;

        public string GetTestName() => "math/quatToAxisAngle-identity";
        public string GetTestDescription() => "";

        public void PrepareObjects(TestContext context)
        {
            _angleBox = context.AddCheckBox("identity -> angle 0");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            var node = nc.CreateNode<Math_QuatToAxisAngleNode>();
            node.ValueIn(Math_QuatToAxisAngleNode.IdValueA).SetValue(Quaternion.identity);

            context.NewEntryPoint(GetTestName());
            _angleBox.proximityCheckDistance = 0.001f;
            _angleBox.SetupCheck(node.ValueOut(Math_QuatToAxisAngleNode.IdOutAngle), out var flow, 0f, true);
            context.AddToCurrentEntrySequence(flow);
        }
    }
}
