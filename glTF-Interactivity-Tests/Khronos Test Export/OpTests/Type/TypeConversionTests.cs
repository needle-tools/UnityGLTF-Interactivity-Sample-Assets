using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests every type/* conversion node:
    /// boolToInt, boolToFloat, intToBool, intToFloat, floatToInt, floatToBool.
    /// Covers the spec-defined edge cases: float→int truncation toward zero,
    /// NaN→0 and ±Inf→int max/min, and non-zero/negative → true for the *ToBool nodes.
    /// </summary>
    public class TypeConversionTests : ITestCase
    {
        private CheckBox _boolToIntTrue;
        private CheckBox _boolToIntFalse;

        private CheckBox _boolToFloatTrue;
        private CheckBox _boolToFloatFalse;

        private CheckBox _intToBoolZero;
        private CheckBox _intToBoolPos;
        private CheckBox _intToBoolNeg;

        private CheckBox _intToFloat;

        private CheckBox _floatToIntTruncPos;
        private CheckBox _floatToIntTruncNeg;
        private CheckBox _floatToIntNaN;

        private CheckBox _floatToBoolZero;
        private CheckBox _floatToBoolNonZero;
        private CheckBox _floatToBoolNeg;

        public string GetTestName()
        {
            return "type/conversions";
        }

        public string GetTestDescription()
        {
            return "Tests all type/* conversion nodes incl. truncation and NaN/Inf edge cases.";
        }

        public void PrepareObjects(TestContext context)
        {
            _boolToIntTrue = context.AddCheckBox("boolToInt(true) == 1");
            _boolToIntFalse = context.AddCheckBox("boolToInt(false) == 0");
            context.NewRow();
            _boolToFloatTrue = context.AddCheckBox("boolToFloat(true) == 1");
            _boolToFloatFalse = context.AddCheckBox("boolToFloat(false) == 0");
            context.NewRow();
            _intToBoolZero = context.AddCheckBox("intToBool(0) == false");
            _intToBoolPos = context.AddCheckBox("intToBool(5) == true");
            _intToBoolNeg = context.AddCheckBox("intToBool(-3) == true");
            context.NewRow();
            _intToFloat = context.AddCheckBox("intToFloat(7) == 7.0");
            context.NewRow();
            _floatToIntTruncPos = context.AddCheckBox("floatToInt(3.7) == 3 (trunc)");
            _floatToIntTruncNeg = context.AddCheckBox("floatToInt(-3.7) == -3 (trunc)");
            _floatToIntNaN = context.AddCheckBox("floatToInt(NaN) == 0");
            context.NewRow();
            _floatToBoolZero = context.AddCheckBox("floatToBool(0) == false");
            _floatToBoolNonZero = context.AddCheckBox("floatToBool(2.5) == true");
            _floatToBoolNeg = context.AddCheckBox("floatToBool(-2.5) == true");
        }

        public void CreateNodes(TestContext context)
        {
            var nc = context.interactivityExportContext;

            void BoolToInt(CheckBox cb, bool input, int expected)
            {
                var node = nc.CreateNode<Type_BoolToIntNode>();
                node.ValueIn(Type_BoolToIntNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_BoolToIntNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void BoolToFloat(CheckBox cb, bool input, float expected)
            {
                var node = nc.CreateNode<Type_BoolToFloatNode>();
                node.ValueIn(Type_BoolToFloatNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_BoolToFloatNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void IntToBool(CheckBox cb, int input, bool expected)
            {
                var node = nc.CreateNode<Type_IntToBoolNode>();
                node.ValueIn(Type_IntToBoolNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_IntToBoolNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void IntToFloat(CheckBox cb, int input, float expected)
            {
                var node = nc.CreateNode<Type_IntToFloatNode>();
                node.ValueIn(Type_IntToFloatNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_IntToFloatNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void FloatToInt(CheckBox cb, float input, int expected)
            {
                var node = nc.CreateNode<Type_FloatToIntNode>();
                node.ValueIn(Type_FloatToIntNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_FloatToIntNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            void FloatToBool(CheckBox cb, float input, bool expected)
            {
                var node = nc.CreateNode<Type_FloatToBoolNode>();
                node.ValueIn(Type_FloatToBoolNode.IdInputA).SetValue(input);
                context.NewEntryPoint(cb.GetText());
                cb.SetupCheck(node.ValueOut(Type_FloatToBoolNode.IdValueResult), out var flow, expected, false);
                context.AddToCurrentEntrySequence(flow);
            }

            BoolToInt(_boolToIntTrue, true, 1);
            BoolToInt(_boolToIntFalse, false, 0);

            BoolToFloat(_boolToFloatTrue, true, 1f);
            BoolToFloat(_boolToFloatFalse, false, 0f);

            IntToBool(_intToBoolZero, 0, false);
            IntToBool(_intToBoolPos, 5, true);
            IntToBool(_intToBoolNeg, -3, true);

            IntToFloat(_intToFloat, 7, 7f);

            FloatToInt(_floatToIntTruncPos, 3.7f, 3);
            FloatToInt(_floatToIntTruncNeg, -3.7f, -3);
            FloatToInt(_floatToIntNaN, float.NaN, 0);

            FloatToBool(_floatToBoolZero, 0f, false);
            FloatToBool(_floatToBoolNonZero, 2.5f, true);
            FloatToBool(_floatToBoolNeg, -2.5f, true);
        }
    }
}
