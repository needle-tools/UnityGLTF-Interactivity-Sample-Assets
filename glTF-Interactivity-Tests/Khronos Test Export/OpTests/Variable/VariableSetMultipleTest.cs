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
        public CheckBox _duplicateIndexCheckBox;

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
            _duplicateIndexCheckBox = context.AddCheckBox("Duplicate index [var4, var4]");
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

            // Duplicate indices in the variables configuration are valid; the variable is set once.
            var duplicateNode = nodeCreator.CreateNode<Variable_SetNode>();
            var var4 = nodeCreator.Context.AddVariableWithIdIfNeeded("var4_" + Guid.NewGuid().ToString(), typeof(int));
            duplicateNode.Configuration[Variable_SetNode.IdConfigVarIndices].Value = new int[] {var4, var4};
            duplicateNode.ValueIn(var4.ToString()).SetValue(44);

            context.NewEntryPoint("Set variable with duplicate index");
            context.AddToCurrentEntrySequence(duplicateNode.FlowIn());

            VariablesHelpers.GetVariable(nodeCreator, var4, out var var4Value);
            _duplicateIndexCheckBox.SetupCheck(out var value4, out var flow4In, 44, false);
            value4.ConnectToSource(var4Value);
            context.AddToCurrentEntrySequence(flow4In);
        }
    }
}