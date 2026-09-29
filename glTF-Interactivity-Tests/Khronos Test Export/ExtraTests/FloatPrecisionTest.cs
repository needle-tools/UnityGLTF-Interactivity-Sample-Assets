using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export.ExtraTests
{
    /// <summary>
    /// KHR_interactivity defines the float type as IEEE-754 double precision and requires
    /// type/intToFloat to be lossless. Covers deserialization of double literals (node values and
    /// variable declarations), double runtime storage, the spec-defined float to int conversion
    /// (truncation and 32-bit wrap-around), and negative zero semantics.
    ///
    /// A float32 implementation rounds the expected literal the same way as the tested value, so the
    /// checks compare an in-graph residual (e.g. x - 16777216 == 1) instead of comparing x with a literal.
    /// Values are authored as C# doubles, which are serialized with round-trip precision.
    /// Negative zero is detected with 1 / x == -Inf, since math/eq treats -0 and +0 as equal.
    /// </summary>
    public class FloatPrecisionTest : ITestCase
    {
        // 2^24 + 1: the first integer that is not representable as float32
        private const double TwoPow24Plus1 = 16777217.0;
        private const double TwoPow24 = 16777216.0;

        private CheckBox _literal2Pow24;
        private CheckBox _literal2Pow53;
        private CheckBox _literalDecimalSum;
        private CheckBox _literalDoubleMax;
        private CheckBox _literalSubnormal;
        private CheckBox _literalNegZero;

        private CheckBox _variableFloatInit;
        private CheckBox _variableFloat3Init;
        private CheckBox _variableSetGet;

        private CheckBox _arithmeticSmallAdd;
        private CheckBox _arithmeticPi;
        private CheckBox _arithmeticRange;
        private CheckBox _arithmeticRound;

        private CheckBox _intToFloatMax;
        private CheckBox _intToFloatMin;
        private CheckBox _intToFloatZero;

        private CheckBox _floatToIntPosInf;
        private CheckBox _floatToIntNegInf;
        private CheckBox _floatToIntNegFraction;
        private CheckBox _floatToIntNegZero;

        private CheckBox _floatToIntMax;
        private CheckBox _floatToIntWrap2Pow31;
        private CheckBox _floatToIntWrap2Pow32;
        private CheckBox _floatToIntWrapNegative;
        private CheckBox _floatToIntLarge;

        private CheckBox _floatToBoolNaN;
        private CheckBox _floatToBoolNegZero;
        private CheckBox _floatToBoolSubnormal;

        private CheckBox _negZeroNeg;
        private CheckBox _negZeroEq;
        private CheckBox _negZeroMin;
        private CheckBox _negZeroMax;
        private CheckBox _negZeroRound;
        private CheckBox _negZeroAbs;

        public string GetTestName()
        {
            return "Extras/Float Precision";
        }

        public string GetTestDescription()
        {
            return "Float values MUST be IEEE-754 double precision: literal and variable deserialization, type conversions, and negative zero.";
        }

        public void PrepareObjects(TestContext context)
        {
            _literal2Pow24 = context.AddCheckBox("literal 16777217 - 16777216 == 1");
            _literal2Pow53 = context.AddCheckBox("literal (2^53-1) - (2^53-2) == 1");
            _literalDecimalSum = context.AddCheckBox("0.1 + 0.2 != 0.3");
            _literalDoubleMax = context.AddCheckBox("literal 1.79e308 is finite");
            _literalSubnormal = context.AddCheckBox("literal 5e-324 > 0");
            _literalNegZero = context.AddCheckBox("literal -0.0 is -0");
            context.NewRow();
            _variableFloatInit = context.AddCheckBox("float var init 16777217");
            _variableFloat3Init = context.AddCheckBox("float3 var init [2^24+1, 1e-300, 1+2^-52]");
            _variableSetGet = context.AddCheckBox("var set/get keeps 16777217");
            context.NewRow();
            _arithmeticSmallAdd = context.AddCheckBox("(1 + 1e-10) - 1 > 0");
            _arithmeticPi = context.AddCheckBox("Pi - 3 == 0.14159265358979312");
            _arithmeticRange = context.AddCheckBox("1e30 * 1e30 is finite");
            _arithmeticRound = context.AddCheckBox("round(0.49999999999999994) == 0");
            context.NewRow();
            _intToFloatMax = context.AddCheckBox("intToFloat(2147483647) - 2147483646 == 1");
            _intToFloatMin = context.AddCheckBox("intToFloat(-2147483647) + 2147483646 == -1");
            _intToFloatZero = context.AddCheckBox("intToFloat(0) is +0");
            context.NewRow();
            _floatToIntPosInf = context.AddCheckBox("floatToInt(+Inf) == 0");
            _floatToIntNegInf = context.AddCheckBox("floatToInt(-Inf) == 0");
            _floatToIntNegFraction = context.AddCheckBox("floatToInt(-0.9) == 0");
            _floatToIntNegZero = context.AddCheckBox("floatToInt(-0) == 0");
            context.NewRow();
            _floatToIntMax = context.AddCheckBox("floatToInt(2147483647.0) == 2147483647");
            _floatToIntWrap2Pow31 = context.AddCheckBox("floatToInt(2147483648.0) == -2147483648");
            _floatToIntWrap2Pow32 = context.AddCheckBox("floatToInt(4294967301.0) == 5");
            _floatToIntWrapNegative = context.AddCheckBox("floatToInt(-4294967301.0) == -5");
            _floatToIntLarge = context.AddCheckBox("floatToInt(1e20) == 1661992960");
            context.NewRow();
            _floatToBoolNaN = context.AddCheckBox("floatToBool(NaN) == false");
            _floatToBoolNegZero = context.AddCheckBox("floatToBool(-0) == false");
            _floatToBoolSubnormal = context.AddCheckBox("floatToBool(5e-324) == true");
            context.NewRow();
            _negZeroNeg = context.AddCheckBox("neg(0) is -0");
            _negZeroEq = context.AddCheckBox("-0 == 0");
            _negZeroMin = context.AddCheckBox("min(0, -0) is -0");
            _negZeroMax = context.AddCheckBox("max(-0, 0) is +0");
            _negZeroRound = context.AddCheckBox("round(-0.3) is -0");
            _negZeroAbs = context.AddCheckBox("abs(-0) is +0");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            // Input values are either literals (object) or connections (ValueOutRef)
            void SetInput(GltfInteractivityExportNode node, string socket, object value)
            {
                if (value is ValueOutRef valueOut)
                    node.ValueIn(socket).ConnectToSource(valueOut);
                else
                    node.SetValueInSocket(socket, value);
            }

            ValueOutRef Op1<TSchema>(object a) where TSchema : GltfInteractivityNodeSchema, new()
            {
                var node = nc.CreateNode<TSchema>();
                SetInput(node, "a", a);
                return node.FirstValueOut();
            }

            ValueOutRef Op2<TSchema>(object a, object b) where TSchema : GltfInteractivityNodeSchema, new()
            {
                var node = nc.CreateNode<TSchema>();
                SetInput(node, "a", a);
                SetInput(node, "b", b);
                return node.FirstValueOut();
            }

            void Check(CheckBox cb, ValueOutRef value, object expected)
            {
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(value, out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void CheckApprox(CheckBox cb, ValueOutRef value, double expected, float tolerance)
            {
                cb.proximityCheckDistance = tolerance;
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(value, out var flow, expected, true);
                context.AddToCurrentEntrySequence(flow);
            }

            // 1 / -0 == -Inf and 1 / +0 == +Inf
            void CheckSignedZero(CheckBox cb, ValueOutRef value, bool negative)
            {
                Check(cb, Op2<Math_DivNode>(1.0, value), negative ? float.NegativeInfinity : float.PositiveInfinity);
            }

            ValueOutRef NegZero() => Op1<Math_NegNode>(0.0);

            ValueOutRef FloatToInt(object a) => Op1<Type_FloatToIntNode>(a);

            // ── JSON literal deserialization ──
            Check(_literal2Pow24, Op2<Math_SubNode>(TwoPow24Plus1, TwoPow24), 1.0);
            Check(_literal2Pow53, Op2<Math_SubNode>(9007199254740991.0, 9007199254740990.0), 1.0);
            Check(_literalDecimalSum, Op2<Math_EqNode>(Op2<Math_AddNode>(0.1, 0.2), 0.3), false);
            Check(_literalDoubleMax, Op1<Math_IsInfNode>(double.MaxValue), false);
            Check(_literalSubnormal, Op2<Math_GtNode>(double.Epsilon, 0.0), true);
            Check(_literalNegZero, Op2<Math_DivNode>(1.0, -0.0), float.NegativeInfinity);

            // ── Variable declaration deserialization and runtime storage ──
            {
                var varId = nc.Context.AddVariableWithIdIfNeeded("PrecisionFloatInit", TwoPow24Plus1,
                    GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float));
                VariablesHelpers.GetVariable(nc, varId, out var getVar);
                Check(_variableFloatInit, Op2<Math_SubNode>(getVar, TwoPow24), 1.0);
            }
            {
                var varId = nc.Context.AddVariableWithIdIfNeeded("PrecisionFloat3Init",
                    new double[] { TwoPow24Plus1, 1e-300, 1.0000000000000002 },
                    GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float3));
                VariablesHelpers.GetVariable(nc, varId, out var getVar);

                var extract = nc.CreateNode<Math_Extract3Node>();
                extract.ValueIn(Math_Extract3Node.IdValueIn).ConnectToSource(getVar);

                var xOk = Op2<Math_EqNode>(Op2<Math_SubNode>(extract.ValueOut(Math_Extract3Node.IdValueOutX), TwoPow24), 1.0);
                var yOk = Op2<Math_GtNode>(extract.ValueOut(Math_Extract3Node.IdValueOutY), 0.0);
                var zOk = Op2<Math_GtNode>(Op2<Math_SubNode>(extract.ValueOut(Math_Extract3Node.IdValueOutZ), 1.0), 0.0);
                Check(_variableFloat3Init, Op2<Math_AndNode>(Op2<Math_AndNode>(xOk, yOk), zOk), true);
            }
            {
                var varId = nc.Context.AddVariableWithIdIfNeeded("PrecisionFloatSetGet", 0.0,
                    GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float));
                context.NewEntryPoint(_variableSetGet.GetText());
                VariablesHelpers.SetVariable(nc, varId, out var setValue, out var setFlow, out _);
                setValue.ConnectToSource(Op2<Math_AddNode>(TwoPow24, 1.0));
                context.AddToCurrentEntrySequence(setFlow);

                VariablesHelpers.GetVariable(nc, varId, out var getVar);
                _variableSetGet.SetupCheck(Op2<Math_SubNode>(getVar, TwoPow24), out var checkFlow, 1.0, false);
                context.AddToCurrentEntrySequence(checkFlow);
            }

            // ── Double arithmetic ──
            Check(_arithmeticSmallAdd, Op2<Math_GtNode>(Op2<Math_SubNode>(Op2<Math_AddNode>(1.0, 1e-10), 1.0), 0.0), true);
            CheckApprox(_arithmeticPi, Op2<Math_SubNode>(nc.CreateNode<Math_PiNode>().FirstValueOut(), 3.0), System.Math.PI - 3.0, 1e-12f);
            Check(_arithmeticRange, Op1<Math_IsInfNode>(Op2<Math_MulNode>(1e30, 1e30)), false);
            // A naive floor(x + 0.5) returns 1, because 0.49999999999999994 + 0.5 rounds up to 1.0
            Check(_arithmeticRound, Op1<Math_RoundNode>(0.49999999999999994), 0.0);

            // ── type/intToFloat MUST be lossless and MUST NOT produce -0 ──
            Check(_intToFloatMax, Op2<Math_SubNode>(Op1<Type_IntToFloatNode>(int.MaxValue), 2147483646.0), 1.0);
            Check(_intToFloatMin, Op2<Math_AddNode>(Op1<Type_IntToFloatNode>(-int.MaxValue), 2147483646.0), -1.0);
            CheckSignedZero(_intToFloatZero, Op1<Type_IntToFloatNode>(0), false);

            // ── type/floatToInt: zero, infinite and NaN inputs return 0, otherwise truncate and wrap modulo 2^32 ──
            Check(_floatToIntPosInf, FloatToInt(float.PositiveInfinity), 0);
            Check(_floatToIntNegInf, FloatToInt(float.NegativeInfinity), 0);
            Check(_floatToIntNegFraction, FloatToInt(-0.9), 0);
            Check(_floatToIntNegZero, FloatToInt(NegZero()), 0);

            Check(_floatToIntMax, FloatToInt(2147483647.0), int.MaxValue);
            Check(_floatToIntWrap2Pow31, FloatToInt(2147483648.0), int.MinValue);
            Check(_floatToIntWrap2Pow32, FloatToInt(4294967301.0), 5);
            Check(_floatToIntWrapNegative, FloatToInt(-4294967301.0), -5);
            // 1e20 mod 2^32 == 1661992960, same as 1e20 | 0 in ECMAScript
            Check(_floatToIntLarge, FloatToInt(1e20), 1661992960);

            // ── type/floatToBool: false if NaN or equal to zero ──
            Check(_floatToBoolNaN, Op1<Type_FloatToBoolNode>(float.NaN), false);
            Check(_floatToBoolNegZero, Op1<Type_FloatToBoolNode>(NegZero()), false);
            Check(_floatToBoolSubnormal, Op1<Type_FloatToBoolNode>(double.Epsilon), true);

            // ── Negative zero ──
            CheckSignedZero(_negZeroNeg, NegZero(), true);
            Check(_negZeroEq, Op2<Math_EqNode>(NegZero(), 0.0), true);
            // For math/min and math/max, negative zero is less than positive zero
            CheckSignedZero(_negZeroMin, Op2<Math_MinNode>(0.0, NegZero()), true);
            CheckSignedZero(_negZeroMax, Op2<Math_MaxNode>(NegZero(), 0.0), false);
            // Negative values greater than -0.5 MUST be rounded to negative zero
            CheckSignedZero(_negZeroRound, Op1<Math_RoundNode>(-0.3), true);
            CheckSignedZero(_negZeroAbs, Op1<Math_AbsNode>(NegZero()), false);
        }
    }
}
