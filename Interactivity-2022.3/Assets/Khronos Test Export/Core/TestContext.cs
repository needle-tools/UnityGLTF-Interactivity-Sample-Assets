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

        public class Entry
        {
            public string name = "";
            public GltfInteractivityExportNode node = null;
            public float? delayedExecutionTime = null;
            public bool requiresUserInteraction = false;
        }
        
        public class Case
        {
            public string CaseName => caseLabel.text;
            public TextMeshPro caseLabel;
            public List<CheckBox> checkBoxes = new List<CheckBox>();
            
            public List<Entry> entryNodes = new List<Entry>();
        }
        
        public List<Case> cases = new List<Case>();
        public int CurrentCaseIndex = -1;
        private Case currentCase => cases[CurrentCaseIndex];
        
        private Entry _lastEntryPoint;
        private GltfInteractivityExportNode _lastEntryPointFallbackSequence = null;
        private GltfInteractivityExportNode _lastEntryPointNodeSequence = null;
        private GltfInteractivityExportNode _lastDelayedFallback = null;
        private List<FlowInRef> _currentEntryFlows = new List<FlowInRef>();
        private List<FlowInRef> _currentFallbackFlows = new List<FlowInRef>();
        
        
        public TestContext(CheckBox defaultCheckBox, TextMeshPro caseLabelPrefab, Transform root)
        {
            _checkBoxPrefab = defaultCheckBox;
            _caseLabelPrefab = caseLabelPrefab;
            _root = root;
            _layout.coloumnSpaceWidth = _checkBoxPrefab.CheckBoxSize.x / 10f;
        }

        private void UpdateEntrySequences()
        {
            if (_currentFallbackFlows.Count > 0)
            {
                if (_lastEntryPoint.delayedExecutionTime != null)
                {
                    if (_lastDelayedFallback == null)
                    {
                        _lastDelayedFallback = interactivityExportContext.CreateNode(new Flow_SetDelayNode());
                        _lastDelayedFallback.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(_lastEntryPoint.delayedExecutionTime.Value);
                    }
                    
                    _lastEntryPoint.node.FlowOut(Event_OnStartNode.IdFlowOut).ConnectToFlowDestination(_lastDelayedFallback.FlowIn(Flow_SequenceNode.IdFlowIn));
                }
                
                if (_lastEntryPointFallbackSequence == null && (_currentFallbackFlows.Count > 1 || _lastDelayedFallback == null))
                {
                    var nodeCreator = interactivityExportContext;
                    _lastEntryPointFallbackSequence = nodeCreator.CreateNode(new Flow_SequenceNode());
                }
                
                
                if (_lastEntryPointFallbackSequence != null)
                {
                    _lastEntryPointFallbackSequence.FlowConnections.Clear();
                    foreach (var flow in _currentFallbackFlows)
                        _lastEntryPointFallbackSequence.FlowOut((_lastEntryPointFallbackSequence.FlowConnections.Count+1).ToString("D3")).ConnectToFlowDestination(flow);
                }
            }

            if (_currentEntryFlows.Count > 1)
            {
                if (_lastEntryPointNodeSequence == null)
                {
                    var nodeCreator = interactivityExportContext;
                    _lastEntryPointNodeSequence = nodeCreator.CreateNode(new Flow_SequenceNode());
                }
                _lastEntryPointNodeSequence.FlowConnections.Clear();
                foreach (var flow in _currentEntryFlows)
                    _lastEntryPointNodeSequence.FlowOut(_lastEntryPointNodeSequence.FlowConnections.Count.ToString("D3")).ConnectToFlowDestination(flow);
            }
            
            
            var startFlow = _lastEntryPoint.node.FlowOut(Event_OnStartNode.IdFlowOut);

            if (_lastEntryPointFallbackSequence != null)
            {
                if (_lastDelayedFallback == null)
                {
                    startFlow.ConnectToFlowDestination(_lastEntryPointFallbackSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
                    startFlow = _lastEntryPointFallbackSequence.FlowOut("000");
                }
                else
                {
                    startFlow.ConnectToFlowDestination(_lastDelayedFallback.FlowIn(Flow_SetDelayNode.IdFlowIn));
                    _lastDelayedFallback.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(_lastEntryPointFallbackSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
                    startFlow = _lastDelayedFallback.FlowOut(Flow_SetDelayNode.IdFlowOut);
                }
            }
            else if (_currentFallbackFlows.Count == 1)
            {
                if (_lastDelayedFallback != null)
                {
                    startFlow.ConnectToFlowDestination(_lastDelayedFallback.FlowIn(Flow_SetDelayNode.IdFlowIn));
                    _lastDelayedFallback.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(_currentFallbackFlows[0]);
                    startFlow = _lastDelayedFallback.FlowOut(Flow_SetDelayNode.IdFlowOut);
                }
            }
            
            if (_lastEntryPointNodeSequence != null)
            {
                startFlow.ConnectToFlowDestination(_lastEntryPointNodeSequence.FlowIn(Flow_SequenceNode.IdFlowIn));
                startFlow = _lastEntryPointNodeSequence.FlowOut("000");
            }
            else
                if (_currentEntryFlows.Count == 1)
                    startFlow.ConnectToFlowDestination(_currentEntryFlows[0]);
        }
        
        public void NewEntryPoint(FlowInRef flowIn, string name, float? delayedExecutionTime = null, bool requiresUserInteraction = false)
        {
            NewEntryPoint(name, delayedExecutionTime, requiresUserInteraction);
            AddToCurrentEntrySequence(flowIn);
            UpdateEntrySequences();
        }
        
        public void NewEntryPoint(string name, float? delayedExecutionTime = null, bool requiresUserInteraction = false)
        {
            var nodeCreator = interactivityExportContext;
            var startNode = nodeCreator.CreateNode(new Event_OnStartNode());
            
            var newEntry = new Entry();
            newEntry.node = startNode;
            newEntry.name = name;
            newEntry.delayedExecutionTime = delayedExecutionTime;
            newEntry.requiresUserInteraction = requiresUserInteraction;

            _currentEntryFlows.Clear();
            _currentFallbackFlows.Clear();
            currentCase.entryNodes.Add(newEntry);
            _lastEntryPoint = newEntry;
            _lastEntryPointFallbackSequence = null;
            _lastEntryPointNodeSequence = null;
            _lastDelayedFallback = null;
        }

        public void AddFallbackToLastEntryPoint(FlowInRef flow)
        {
            if (_lastEntryPoint == null)
            {
                Debug.LogError("AddFallbackToLastEntryPoint requires a call of NewEntryPoint before.");
                return;
            }
            _currentFallbackFlows.Add(flow);
            UpdateEntrySequences();
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
        
        public void AddToCurrentEntrySequence(FlowInRef flow)
        {
            if (_lastEntryPoint == null)
            {
                Debug.LogError("AddToLastEntrySequence requires a call of NewEntryPoint before.");
                return;
            }
            _currentEntryFlows.Add(flow);
            UpdateEntrySequences();
        }
        
        public void AddToCurrentEntrySequence(params FlowInRef[] flows)
        {
            if (_lastEntryPoint == null)
            {
                Debug.LogError("AddToLastEntrySequence requires a call of NewEntryPoint before.");
                return;
            }
            foreach (var flow in flows)
                _currentEntryFlows.Add(flow);
            UpdateEntrySequences();
        }
        
        public void AddSequence(FlowOutRef flowIn, FlowInRef[] sequences)
        {
            var nodeCreator = interactivityExportContext;
            
            var sequenceNode = nodeCreator.CreateNode(new Flow_SequenceNode());
            if (flowIn.socket.Value.Node != null)
            {
                sequenceNode.FlowOut("s000").socket.Value.Socket = flowIn.socket.Value.Socket;
                sequenceNode.FlowOut("s000").socket.Value.Node = flowIn.socket.Value.Node;
            }
            
            flowIn.ConnectToFlowDestination(sequenceNode.FlowIn(Flow_SequenceNode.IdFlowIn));
            
            for (int i = 0; i < sequences.Length; i++)
            {
                var sequenceFlowOut = sequenceNode.FlowOut("s"+ (sequenceNode.FlowConnections.Count).ToString("D3"));
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
            CurrentCaseIndex = cases.Count - 1;
            return newCase;
        }

        public CheckBox AddCheckBox(string name, bool asWaiting = false)
        {
            var newCheckBox = GameObject.Instantiate(_checkBoxPrefab, _root);
            newCheckBox.transform.localPosition = _layout.ReserveSpace(newCheckBox.CheckBoxSize);
            newCheckBox.gameObject.SetActive(true);
            newCheckBox.gameObject.name = "CheckBox_" + name;
            newCheckBox.SetText(name);
            newCheckBox.SetCase(currentCase);
            newCheckBox.context = this;
            currentCase.checkBoxes.Add(newCheckBox);
            if (asWaiting)
                newCheckBox.Waiting();
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