using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    [TestCreator.IgnoreTestCase]
    public class Math_Combine4x4Test : ITestCase
    {
        private CheckBox _checkBox;
     
        public string GetTestName()
        {
            return "math/combine4x4";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _checkBox = context.AddCheckBox("combine4x4");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                matrix[i] = i;
          
            var combineNode = nodeCreator.CreateNode<Math_Combine4x4Node>();
            var index = 0;
            foreach (var v in combineNode.ValueInConnection)
            {
                combineNode.SetValueInSocket(v.Key, matrix[index]);
                index++;
            }
            
            context.NewEntryPoint(_checkBox.GetText());
            _checkBox.SetupCheck(combineNode.FirstValueOut(), out var flow, matrix, false);
            context.AddToCurrentEntrySequence(flow);
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_Extract4x4Test : AbstractMath_ExtractTest<Math_Extract4x4Node>
    {
        public override object Value
        {
            get
            {
                var m = new Matrix4x4(
                    new Vector4(0, 1, 2, 3),
                    new Vector4(4, 5, 6, 7),
                    new Vector4(8, 9, 10, 11),
                    new Vector4(12, 13, 14, 15));
                return m;
            }
        }

        public override float ValueComponent(int index)
        {
            var v = (Matrix4x4)Value;
            return MatrixHelpers.GltfGetElement(v, index);
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_Extract4Test : AbstractMath_ExtractTest<Math_Extract4Node>
    {
        public override object Value { get => new Vector4(2f, 4f, 6f, 8f); }
        public override float ValueComponent(int index)
        {
            var v = (Vector4)Value;
            switch (index)
            {
                case 0 : return v.x;
                case 1 : return v.y;
                case 2 : return v.z;
                case 3 : return v.w;
            }
            return 0;
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_Extract3Test : AbstractMath_ExtractTest<Math_Extract3Node>
    {
        public override object Value { get => new Vector3(2f, 4f, 6f); }
        public override float ValueComponent(int index)
        {
            var v = (Vector3)Value;
            switch (index)
            {
                case 0 : return v.x;
                case 1 : return v.y;
                case 2 : return v.z;
            }
            return 0;
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_Extract2Test : AbstractMath_ExtractTest<Math_Extract2Node>
    {
        public override object Value { get => new Vector2(2f, 4f); }
        public override float ValueComponent(int index)
        {
            var v = (Vector2)Value;
            switch (index)
            {
                case 0 : return v.x;
                case 1 : return v.y;
            }
            return 0;
        }
    }
        
    public abstract class AbstractMath_ExtractTest<TSchema> : ITestCase where TSchema : GltfInteractivityNodeSchema, new() 
    {
        private CheckBox[] _checkBoxes;
        private string[] _socketName;
        
        public abstract object Value { get; }

        public abstract float ValueComponent(int index);
        
        public string GetTestName()
        {
            return  GltfInteractivityNodeSchema.GetSchema<TSchema>().Op;
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            var outSockets = GltfInteractivityNodeSchema.GetSchema<TSchema>().OutputValueSockets;
            _checkBoxes = new CheckBox[outSockets.Count];
            _socketName = new string[outSockets.Count];
            int index = 0;
            foreach (var s in outSockets)
            {
                var checkBox = context.AddCheckBox(s.Key);
                _socketName[index] = s.Key;
                checkBox.proximityCheckDistance = 0.01f;
                _checkBoxes[index] = checkBox;
                index++;
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var extractNode = nodeCreator.CreateNode<TSchema>();
            extractNode.ValueIn(Math_Extract2Node.IdValueIn).SetValue(Value);
            context.NewEntryPoint(GetTestName());
            int index = 0;
            foreach (var c in _checkBoxes)
            {
                c.SetupCheck(extractNode.ValueOut(_socketName[index]), out var flow, ValueComponent(index), false);
                context.AddToCurrentEntrySequence(flow);
                index++;
            }
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_QuatFromAxisAngleTest : ITestCase
    {
        private CheckBox _angleCheckBox;
     
        public string GetTestName()
        {
            return "math/quatFromAxisAngle";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
           _angleCheckBox = context.AddCheckBox("quatFromAxisAngle");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var axis =  new Vector3(0, 1, 0);
            var angle = 90f;
            var angleNode = nodeCreator.CreateNode<Math_QuatFromAxisAngleNode>();
            angleNode.ValueIn(Math_QuatFromAxisAngleNode.IdAngle).SetValue(angle * Mathf.Deg2Rad);
            angleNode.ValueIn(Math_QuatFromAxisAngleNode.IdAxis).SetValue(axis);

            var quat = Quaternion.AngleAxis(angle, axis);
            context.NewEntryPoint("quatFromAxisAngle");
            _angleCheckBox.proximityCheckDistance = 0.01f;
            _angleCheckBox.SetupCheck(angleNode.FirstValueOut(), out var flow, quat, true);
            context.AddToCurrentEntrySequence(flow);
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_QuatToAxisAngleTest : ITestCase
    {
        private CheckBox _axisCheckBox;
        private CheckBox _angleCheckBox;
        
        public string GetTestName()
        {
            return "math/quatToAxisAngle";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _axisCheckBox = context.AddCheckBox("Axis");
            _angleCheckBox = context.AddCheckBox("Angle");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var quat = Quaternion.Euler(30f, 45f, 60f);
            var axisAngleNode = nodeCreator.CreateNode<Math_QuatToAxisAngleNode>();
            axisAngleNode.ValueIn(Math_QuatToAxisAngleNode.IdValueA).SetValue(quat);

            quat.ToAngleAxis(out var angle, out var axis);
            context.NewEntryPoint("quatToAxisAngle");

            _axisCheckBox.SetupCheck(axisAngleNode.ValueOut(Math_QuatToAxisAngleNode.IdOutAxis), out var flowAxis, axis, true);
            _axisCheckBox.proximityCheckDistance = 0.01f;
            context.AddToCurrentEntrySequence(flowAxis);
            _angleCheckBox.proximityCheckDistance = 0.01f;
            _angleCheckBox.SetupCheck(axisAngleNode.ValueOut(Math_QuatToAxisAngleNode.IdOutAngle), out var flowAngle, Mathf.Deg2Rad * angle, true);
            context.AddToCurrentEntrySequence(flowAngle);
        }
    }
    
    [TestCreator.IgnoreTestCase]
    public class Math_SelectTest : ITestCase
    {
        private CheckBox _whenTrueCheckBox;
        private CheckBox _whenFalseCheckBox;

        public string GetTestName()
        {
            return "math/select";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _whenTrueCheckBox = context.AddCheckBox("When True");
            _whenFalseCheckBox = context.AddCheckBox("When False");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var selectNodeTrue = nodeCreator.CreateNode<Math_SelectNode>();
            selectNodeTrue.ValueIn(Math_SelectNode.IdCondition).SetValue(true);
            selectNodeTrue.ValueIn(Math_SelectNode.IdValueA).SetValue(3f);
            selectNodeTrue.ValueIn(Math_SelectNode.IdValueB).SetValue(1f);

            context.NewEntryPoint(_whenTrueCheckBox.GetText());
            _whenTrueCheckBox.SetupCheck(selectNodeTrue.FirstValueOut(), out var flowTrue, 3f, false);
            context.AddToCurrentEntrySequence(flowTrue);

            var selectNodeFalse = nodeCreator.CreateNode<Math_SelectNode>();
            selectNodeFalse.ValueIn(Math_SelectNode.IdCondition).SetValue(false);
            selectNodeFalse.ValueIn(Math_SelectNode.IdValueA).SetValue(3f);
            selectNodeFalse.ValueIn(Math_SelectNode.IdValueB).SetValue(1f);

            context.NewEntryPoint(_whenFalseCheckBox.GetText());
            _whenFalseCheckBox.SetupCheck(selectNodeFalse.FirstValueOut(), out var flowFalse, 1f, false);
            context.AddToCurrentEntrySequence(flowFalse);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_SwitchTest : ITestCase
    {
        private CheckBox _selectionCheckBox;
        private CheckBox _defaultCheckBox;
        private CheckBox _specialCasesCheckBox;

        public string GetTestName()
        {
            return "math/switch";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _selectionCheckBox = context.AddCheckBox("Selection");
            _defaultCheckBox = context.AddCheckBox("Default");
            _specialCasesCheckBox = context.AddCheckBox("Negative Cases [-2,-1,0]");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var switchNode = nodeCreator.CreateNode<Math_SwitchNode>();
            switchNode.Configuration[Math_SwitchNode.IdConfigCases].Value = new int[] { 0, 1, 2 };
            switchNode.ValueIn(Math_SwitchNode.IdSelection).SetValue(1);
            switchNode.ValueIn(Math_SwitchNode.IdDefaultValue).SetValue(0);
            switchNode.ValueIn("0").SetValue(11);
            switchNode.ValueIn("1").SetValue(22);
            switchNode.ValueIn("2").SetValue(33);

            context.NewEntryPoint(_selectionCheckBox.GetText());
            _selectionCheckBox.SetupCheck(switchNode.FirstValueOut(), out var flowSelection, 22, false);
            context.AddToCurrentEntrySequence(flowSelection);

            var switchDefaultNode = nodeCreator.CreateNode<Math_SwitchNode>();
            switchDefaultNode.Configuration[Math_SwitchNode.IdConfigCases].Value = new int[] { 0, 1, 2 };
            switchDefaultNode.ValueIn(Math_SwitchNode.IdSelection).SetValue(30);
            switchDefaultNode.ValueIn(Math_SwitchNode.IdDefaultValue).SetValue(99);
            switchDefaultNode.ValueIn("0").SetValue(11);
            switchDefaultNode.ValueIn("1").SetValue(22);
            switchDefaultNode.ValueIn("2").SetValue(33);

            context.NewEntryPoint(_defaultCheckBox.GetText());
            _defaultCheckBox.SetupCheck(switchDefaultNode.FirstValueOut(), out var flowDefault, 99, false);
            context.AddToCurrentEntrySequence(flowDefault);

            var switchNegNode = nodeCreator.CreateNode<Math_SwitchNode>();
            switchNegNode.Configuration[Math_SwitchNode.IdConfigCases].Value = new int[] { -2, -1, 0 };
            switchNegNode.ValueIn(Math_SwitchNode.IdSelection).SetValue(-2);
            switchNegNode.ValueIn(Math_SwitchNode.IdDefaultValue).SetValue(0);
            switchNegNode.ValueIn("-1").SetValue(11);
            switchNegNode.ValueIn("-2").SetValue(22);
            switchNegNode.ValueIn("0").SetValue(33);

            context.NewEntryPoint(_specialCasesCheckBox.GetText());
            _specialCasesCheckBox.SetupCheck(switchNegNode.FirstValueOut(), out var flowNeg, 22, false);
            context.AddToCurrentEntrySequence(flowNeg);
        }
    }

    [TestCreator.IgnoreTestCase]
    public class Math_RandomTest : ITestCase
    {
        private CheckBox _randomCheckBox;
        private CheckBox _randomSameCheckBox;
        private CheckBox _monteCarlo1kCheckBox;
        private CheckBox _monteCarlo10kCheckBox;

        public string GetTestName()
        {
            return "math/random";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _randomCheckBox = context.AddCheckBox("Random (new number in new flow)");
            _randomSameCheckBox = context.AddCheckBox("Random (same number in current flow)");
            _monteCarlo1kCheckBox = context.AddCheckBox("Monte Carlo 1k(random number distribution)");
            _monteCarlo10kCheckBox = context.AddCheckBox("Monte Carlo 10k(random number distribution)");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Test for new random number
            var randomNode = nodeCreator.CreateNode<Math_RandomNode>();

            var lastRandomNumberVarId = nodeCreator.Context.AddVariableWithIdIfNeeded(
                "LastRandomNumber" + Guid.NewGuid().ToString(), "-1",
                typeof(float));

            VariablesHelpers.SetVariable(nodeCreator, lastRandomNumberVarId, out var setVarValue, out var setVarFlowIn,
                out var setVarFlowOut);
            setVarValue.ConnectToSource(randomNode.FirstValueOut());
            VariablesHelpers.GetVariable(nodeCreator, lastRandomNumberVarId, out var getVarValue);

            context.NewEntryPoint(_randomCheckBox.GetText());
            context.AddToCurrentEntrySequence(setVarFlowIn);

            _randomCheckBox.SetupCheckValueDiffers(out var vA, out var vB, out var checkFlow);
            vA.ConnectToSource(randomNode.FirstValueOut());
            vB.ConnectToSource(getVarValue);
            setVarFlowOut.ConnectToFlowDestination(checkFlow);

            // Test for same random number
            var randomSameNode = nodeCreator.CreateNode<Math_RandomNode>();

            var subtractNode = nodeCreator.CreateNode<Math_SubNode>();
            subtractNode.ValueIn(Math_SubNode.IdValueA).ConnectToSource(randomSameNode.FirstValueOut());
            subtractNode.ValueIn(Math_SubNode.IdValueB).ConnectToSource(randomSameNode.FirstValueOut());

            context.NewEntryPoint(_randomSameCheckBox.GetText());
            _randomSameCheckBox.SetupCheck(subtractNode.FirstValueOut(), out var flowSame, 0f, false);
            context.AddToCurrentEntrySequence(flowSame);

            // Monte Carlo
            void AddMonteCarloCheckBox(CheckBox checkBox, int iterations, float proxRange)
            {
                var randomNodeX = nodeCreator.CreateNode<Math_RandomNode>();
                var randomNodeY = nodeCreator.CreateNode<Math_RandomNode>();

                var combineXY = nodeCreator.CreateNode<Math_Combine2Node>();
                combineXY.ValueIn(Math_Combine2Node.IdValueA).ConnectToSource(randomNodeX.FirstValueOut());
                combineXY.ValueIn(Math_Combine2Node.IdValueB).ConnectToSource(randomNodeY.FirstValueOut());

                var multiplyNode = nodeCreator.CreateNode<Math_MulNode>();
                multiplyNode.ValueIn(Math_MulNode.IdValueA).ConnectToSource(combineXY.FirstValueOut());
                multiplyNode.ValueIn(Math_MulNode.IdValueB).SetValue(new Vector2(2f, 2f));

                var subNode = nodeCreator.CreateNode<Math_SubNode>();
                subNode.ValueIn(Math_SubNode.IdValueA).ConnectToSource(multiplyNode.FirstValueOut());
                subNode.ValueIn(Math_SubNode.IdValueB).SetValue(new Vector2(1f, 1f));
                var xy = subNode.FirstValueOut();

                var forLoopNode = nodeCreator.CreateNode<Flow_ForLoopNode>();
               
                forLoopNode.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(iterations);
                forLoopNode.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);

                context.NewEntryPoint(_monteCarlo1kCheckBox.GetText());
                context.AddToCurrentEntrySequence(forLoopNode.FlowIn());

                var lengthNode = nodeCreator.CreateNode<Math_LengthNode>();
                lengthNode.ValueIn(Math_LengthNode.IdValueA).ConnectToSource(xy);

                var ltNode = nodeCreator.CreateNode<Math_LtNode>();
                ltNode.ValueIn(Math_LtNode.IdValueA).ConnectToSource(lengthNode.FirstValueOut());
                ltNode.ValueIn(Math_LtNode.IdValueB).SetValue(1f);
                
                context.AddPlusOneCounter(out var insideCounter, out var incrementFlowIn);
                
                var branchNode = nodeCreator.CreateNode<Flow_BranchNode>();
                forLoopNode.FlowOut(Flow_ForLoopNode.IdLoopBody).ConnectToFlowDestination(branchNode.FlowIn());
                
                branchNode.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(ltNode.FirstValueOut());
                branchNode.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(incrementFlowIn);

                var divideNode = nodeCreator.CreateNode<Math_DivNode>();
                divideNode.ValueIn(Math_DivNode.IdValueA).ConnectToSource(insideCounter)
                    .SetType(TypeRestriction.LimitToFloat);
                divideNode.ValueIn(Math_DivNode.IdValueB).SetValue((float)iterations);

                var multiplyNode2 = nodeCreator.CreateNode<Math_MulNode>();
                multiplyNode2.ValueIn(Math_MulNode.IdValueA).ConnectToSource(divideNode.FirstValueOut());
                multiplyNode2.ValueIn(Math_MulNode.IdValueB).SetValue(4f);
                
                
                
                checkBox.proximityCheckDistance = proxRange;
                checkBox.SetupCheck(multiplyNode2.FirstValueOut(), out var flowMonteCarlo, Math.PI, true);
                context.AddLog(checkBox.GetText() + " Inside Circle: {0} / {1}", out var logFlow, out var logout, 2,
                    out var values);
                values[0].ConnectToSource(insideCounter);
                values[1].SetValue(iterations);
                
                
                logout.ConnectToFlowDestination(flowMonteCarlo);
                forLoopNode.FlowOut(Flow_ForLoopNode.IdCompleted).ConnectToFlowDestination(logFlow);
                
                
            }
            
            AddMonteCarloCheckBox(_monteCarlo1kCheckBox, 1000, 0.3f);
            AddMonteCarloCheckBox(_monteCarlo10kCheckBox, 10000, 0.08f);
            
            

        }
    }

    // [TestCreator.IgnoreTestCase]
    // public class RandomLog : ITestCase
    // {
    //     public string GetTestName()
    //     {
    //         return "RANDOMOUT";
    //     }
    //
    //     public string GetTestDescription()
    //     {
    //         return "RANDOMOUT";
    //     }
    //
    //     public void PrepareObjects(TestContext context)
    //     {
    //
    //     }
    //
    //     public void CreateNodes(TestContext context)
    //     {
    //         var nodeCreator = context.interactivityExportContext;
    //
    //         var forLoopNode = nodeCreator.CreateNode<Flow_ForLoopNode>();
    //         forLoopNode.ValueIn(Flow_ForLoopNode.IdEndIndex).SetValue(5000);
    //         forLoopNode.ValueIn(Flow_ForLoopNode.IdStartIndex).SetValue(0);
    //         context.NewEntryPoint("output");
    //         var randomNode2 = nodeCreator.CreateNode<Math_RandomNode>();
    //         var randomNode3 = nodeCreator.CreateNode<Math_RandomNode>();
    //
    //         context.AddLog("Random: {0} {1}", out var logFlow, out var logout, randomNode2.FirstValueOut(),
    //             randomNode3.FirstValueOut());
    //         context.AddToCurrentEntrySequence(forLoopNode.FlowIn());
    //         forLoopNode.FlowOut(Flow_ForLoopNode.IdLoopBody).ConnectToFlowDestination(logFlow);
    //     }
    // }

    [TestCreator.IgnoreTestCase]
    public class Math_MatDecomposeTest : ITestCase
    {
        private CheckBox _translateCheckBox;
        private CheckBox _rotateCheckBox;
        private CheckBox _scaleCheckBox;
        private CheckBox _isValidCheckBox;

        private CheckBox _invalidTranslateCheckBox;
        private CheckBox _invalidRotateCheckBox;
        private CheckBox _invalidScaleCheckBox;
        private CheckBox _invalidIsValidCheckBox;
        

        public string GetTestName()
        {
            return "math/matDecompose";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _translateCheckBox = context.AddCheckBox("Translate");
            _rotateCheckBox = context.AddCheckBox("Rotate");
            _scaleCheckBox = context.AddCheckBox("Scale");
            _isValidCheckBox = context.AddCheckBox("isValid");
            context.NewRow();
            
            _invalidTranslateCheckBox = context.AddCheckBox("invalid, Translate");
            _invalidRotateCheckBox = context.AddCheckBox("invalid, Rotate");
            _invalidScaleCheckBox = context.AddCheckBox("invalid, Scale");
            _invalidIsValidCheckBox = context.AddCheckBox("invalid. isValid");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            var translate = new Vector3(1f, 2f, 3f);
            var rotate = Quaternion.Euler(30f, 45f, 60f);
            var scale = new Vector3(2f, 2f, 2f);
            
            var matComposeNode = nodeCreator.CreateNode<Math_MatComposeNode>();
            matComposeNode.ValueIn(Math_MatComposeNode.IdInputTranslation).SetValue(translate);
            matComposeNode.ValueIn(Math_MatComposeNode.IdInputRotation).SetValue(rotate);
            matComposeNode.ValueIn(Math_MatComposeNode.IdInputScale).SetValue(scale);
            
            var matDecomposeNode = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            matDecomposeNode.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(matComposeNode.FirstValueOut());
            //matDecomposeNode.ValueIn(Math_MatDecomposeNode.IdInput).SetValue(mat);

            context.NewEntryPoint("matDecompose");

            _translateCheckBox.proximityCheckDistance = 0.001f;
            _rotateCheckBox.proximityCheckDistance = 0.001f;
            _scaleCheckBox.proximityCheckDistance = 0.001f;

            _translateCheckBox.SetupCheck(matDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var flowTranslate, translate, true);
            context.AddToCurrentEntrySequence(flowTranslate);
            _rotateCheckBox.SetupCheck(matDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputRotation), out var flowRotate, rotate, true);
            context.AddToCurrentEntrySequence(flowRotate);
            _scaleCheckBox.SetupCheck(matDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputScale), out var flowScale, scale, true);
            context.AddToCurrentEntrySequence(flowScale);
            
            _isValidCheckBox.SetupCheck(matDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputIsValid), out var flowValid, true, false);
            context.AddToCurrentEntrySequence(flowValid);
            
            //Invalid Test
            
            translate = new Vector3(1f, 2f, 3f);
            rotate = Quaternion.Euler(30f, 45f, 60f);
            scale = new Vector3(2f, 2f, float.NaN);

            var invalidMatComposeNode = nodeCreator.CreateNode<Math_MatComposeNode>();
            invalidMatComposeNode.ValueIn(Math_MatComposeNode.IdInputTranslation).SetValue(translate);
            invalidMatComposeNode.ValueIn(Math_MatComposeNode.IdInputRotation).SetValue(rotate);
            invalidMatComposeNode.ValueIn(Math_MatComposeNode.IdInputScale).SetValue(scale);
            
            var invalidMatDecomposeNode = nodeCreator.CreateNode<Math_MatDecomposeNode>();
            invalidMatDecomposeNode.ValueIn(Math_MatDecomposeNode.IdInput).ConnectToSource(invalidMatComposeNode.FirstValueOut());

            context.NewEntryPoint("matDecompose - invalid result");
            
            _invalidTranslateCheckBox.SetupCheck(invalidMatDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputTranslation), out var invalidFlowTranslate, Vector3.zero, true);
            context.AddToCurrentEntrySequence(invalidFlowTranslate);
            _invalidRotateCheckBox.SetupCheck(invalidMatDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputRotation), out var invalidFlowRotate, Quaternion.identity, true);
            context.AddToCurrentEntrySequence(invalidFlowRotate);
            _invalidScaleCheckBox.SetupCheck(invalidMatDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputScale), out var invalidFlowScale, Vector3.one, true);
            context.AddToCurrentEntrySequence(invalidFlowScale);
            _invalidIsValidCheckBox.SetupCheck(invalidMatDecomposeNode.ValueOut(Math_MatDecomposeNode.IdOutputIsValid), out var invalidFlowValid, false, false);
            context.AddToCurrentEntrySequence(invalidFlowValid);
        }
    }
}