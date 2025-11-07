using System;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class VariableSetMultipleTest : ITestCase
    {
        public CheckBox _var1CheckBox;
        public CheckBox _var2CheckBox;
        public CheckBox _var3CheckBox;
        
        public string GetTestName()
        {
            return "variable/setMultiple";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _var1CheckBox = context.AddCheckBox("[var1]");
            _var2CheckBox = context.AddCheckBox("[var2]");
            _var3CheckBox = context.AddCheckBox("[var3]");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            
            var node = nodeCreator.CreateNode<Variable_SetNode>();

            var var1 = nodeCreator.Context.AddVariableWithIdIfNeeded("var1_" + Guid.NewGuid().ToString(), typeof(int));
            var var2 = nodeCreator.Context.AddVariableWithIdIfNeeded("var2_" + Guid.NewGuid().ToString(), typeof(int));
            var var3 = nodeCreator.Context.AddVariableWithIdIfNeeded("var3_" + Guid.NewGuid().ToString(), typeof(int));

            node.Configuration[Variable_SetNode.IdConfigVarIndices].Value = new int[] {var2, var1, var3};

            node.ValueIn(var1.ToString()).SetValue(11);
            node.ValueIn(var2.ToString()).SetValue(22);
            node.ValueIn(var3.ToString()).SetValue(33);

            context.NewEntryPoint("Set multiple variables");
            context.AddToCurrentEntrySequence(node.FlowIn());
 
            VariablesHelpers.GetVariable(nodeCreator, var1, out var var1Value);
            VariablesHelpers.GetVariable(nodeCreator, var2, out var var2Value);
            VariablesHelpers.GetVariable(nodeCreator, var3, out var var3Value);
            
            _var1CheckBox.SetupCheck(out var value1, out var flow1In, 11, false);
            _var2CheckBox.SetupCheck(out var value2, out var flow2In, 22, false);
            _var3CheckBox.SetupCheck(out var value3, out var flow3In, 33, false);

            value1.ConnectToSource(var1Value);
            value2.ConnectToSource(var2Value);
            value3.ConnectToSource(var3Value);
            
            context.AddToCurrentEntrySequence(flow1In);
            context.AddToCurrentEntrySequence(flow2In);
            context.AddToCurrentEntrySequence(flow3In);
        }
    }
}