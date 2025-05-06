using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class TestContext : IDisposable
    {
        public GltfInteractivityExportNodes interactivityExportContext;

        private TestLayout _layout = new ();
        private CheckBox _checkBoxPrefab;
        private TextMeshPro _caseLabelPrefab;
        public int maxRows = 15;
        
        private Transform _root;

        public IEnumerable<TextMeshPro> CaseLabels => cases.Select(c => c.caseLabel);
        public IEnumerable<CheckBox> CheckBoxes => cases.SelectMany(c => c.checkBoxes);

        public class Case
        {
            public string CaseName => caseLabel.text;
            public TextMeshPro caseLabel;
            public List<CheckBox> checkBoxes = new List<CheckBox>();
            
            public List<(GltfInteractivityNode node, string caseName)> entryNodes = new List<(GltfInteractivityNode, string)>();
        }
        
        public List<Case> cases = new List<Case>();
        private Case currentCase => cases[cases.Count - 1];

        public TestContext(CheckBox defaultCheckBox, TextMeshPro caseLabelPrefab, Transform root)
        {
            _checkBoxPrefab = defaultCheckBox;
            _caseLabelPrefab = caseLabelPrefab;
            _root = root;
            _layout.coloumnSpaceWidth = _checkBoxPrefab.CheckBoxSize.x / 10f;
        }

        public void NewEntryPoint(FlowInRef flowIn, string name)
        {
            NewEntryPoint(out var flow, name);
            flow.ConnectToFlowDestination(flowIn);
        }
        
        public void NewEntryPoint(out FlowOutRef flow, string name)
        {
            var nodeCreator = interactivityExportContext;
            var startNode = nodeCreator.CreateNode(new Event_OnStartNode());
            flow = startNode.FlowOut(Event_OnStartNode.IdFlowOut);
            currentCase.entryNodes.Add((startNode, name));
        }
        
        public void AddLog(string message, out FlowInRef flowIn, out FlowOutRef flowOut,params ValueOutRef[] values)
        {
            AddLog(message, out flowIn, out flowOut, values.Length, out var valueIn);
            for (int i = 0; i < values.Length; i++)
                valueIn[i].ConnectToSource(values[i]);
        }
        
        public void AddLog(string message, out FlowInRef flowIn, out FlowOutRef flowOut, int valueCount,  out ValueInRef[] values)
        {
            var nodeCreator = interactivityExportContext;
            var log = nodeCreator.AddLog(GltfInteractivityExportNodes.LogLevel.Info, message);
            flowIn = log.FlowIn(Debug_LogNode.IdFlowIn);
            flowOut = log.FlowOut(Debug_LogNode.IdFlowOut);
            values = new ValueInRef[valueCount];
            for (int i = 0; i < valueCount; i++)
            {
                var value = log.ValueIn(i.ToString());
                values[i] = value;
            }
        }
        
        public void AddSequence(FlowOutRef flowIn, FlowInRef[] sequences)
        {
            var nodeCreator = interactivityExportContext;
            
            var sequenceNode = nodeCreator.CreateNode(new Flow_SequenceNode());
            flowIn.ConnectToFlowDestination(sequenceNode.FlowIn(Flow_SequenceNode.IdFlowIn));
            
            for (int i = 0; i < sequences.Length; i++)
            {
                var sequenceFlowOut = sequenceNode.FlowOut( "s"+i.ToString("D3"));
                sequenceFlowOut.ConnectToFlowDestination(sequences[i]);
            }
        }

        public void AddPlusOneCounter(out ValueOutRef varValue, out FlowInRef flowInToIncrease)
        {
            var nodeCreator = interactivityExportContext;
            
            var newVarName = Guid.NewGuid().ToString();
            var newVarId = nodeCreator.Context.AddVariableWithIdIfNeeded(newVarName, 0, typeof(int));
            
            VariablesHelpers.GetVariable(nodeCreator, newVarId, out var loopRangeCounter);
            varValue = loopRangeCounter;
            var addLoopRangeCounter = nodeCreator.CreateNode(new Math_AddNode());
            addLoopRangeCounter.ValueIn(Math_AddNode.IdValueA).ConnectToSource(loopRangeCounter);
            addLoopRangeCounter.ValueIn(Math_AddNode.IdValueB).SetValue(1);
            
            var setVar = VariablesHelpers.SetVariable(nodeCreator, newVarId);
            setVar.ValueIn(Variable_SetNode.IdInputValue).ConnectToSource(addLoopRangeCounter.FirstValueOut());
            flowInToIncrease = setVar.FlowIn(Variable_SetNode.IdFlowIn);
        }
    
        public void NewRow()
        {
            _layout.NextRow();
        }

        public Case NewTestCase(string name)
        {
            NewRow();
            var newCase = new Case();
            
            var newLabel = GameObject.Instantiate(_caseLabelPrefab, _root);
            newLabel.transform.localPosition = _layout.CurrentLabelPosition();
            newLabel.text = name;
            newLabel.gameObject.SetActive(true);
            newLabel.gameObject.name = "CaseLabel_" + name;

            newCase.caseLabel = newLabel;
            cases.Add(newCase);
            return newCase;
        }
    
        public CheckBox AddCheckBox(string name)
        {
            var newCheckBox = GameObject.Instantiate(_checkBoxPrefab, _root);
            newCheckBox.transform.localPosition = _layout.ReserveSpace(newCheckBox.CheckBoxSize);
            newCheckBox.gameObject.SetActive(true);
            newCheckBox.gameObject.name = "CheckBox_" + name;
            newCheckBox.SetText(name);
            newCheckBox.SetCase(currentCase);
            currentCase.checkBoxes.Add(newCheckBox);
            return newCheckBox;
        }


        public void Dispose()
        {
            foreach (var checkBox in CheckBoxes)
                GameObject.DestroyImmediate(checkBox.gameObject);
        
            foreach (var caseLabel in CaseLabels)
                GameObject.DestroyImmediate(caseLabel.gameObject);
            
            cases.Clear();
        }
    }
}