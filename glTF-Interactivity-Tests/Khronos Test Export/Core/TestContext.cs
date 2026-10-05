using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityGLTF.Interactivity;
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
        private TextMeshPro _labelPrefab;
        public int maxRows = 15;

        /// <summary>
        /// When set, the test cases don't all start on onStart, but one after another: each case starts
        /// once the previous one has finished (its longest delayedExecutionTime plus a buffer). Used for
        /// the all-in-one export, where starting everything at once makes the first frame so slow that
        /// time based checks (interpolate, setDelay, ...) would measure against a jumping clock.
        /// </summary>
        public bool runCasesSequentially = false;
        /// <summary> Extra wait between two cases, so each case starts in a new frame (seconds). </summary>
        public float sequentialCaseGap = 0.1f;

        private Transform _root;
        private GameObject _runningCaseArrow;

        public Transform Root => _root;

        public IEnumerable<TextMeshPro> CaseLabels => cases.Select(c => c.caseLabel);
        public IEnumerable<CheckBox> CheckBoxes => cases.SelectMany(c => c.checkBoxes);
        public IEnumerable<TextMeshPro> Labels => cases.SelectMany(c => c.labels);
        public IEnumerable<CheckBox> Markers => cases.SelectMany(c => c.markers);
        public IEnumerable<GameObject> Arrows => cases.SelectMany(c => c.arrows);

        public class Entry
        {
            public string name = "";
            public GltfInteractivityExportNode node = null;
            /// <summary> The flow that starts this entry: onStart [out], or the starter's first output when the cases run sequentially. </summary>
            public FlowOutRef startFlow = null;
            public float? delayedExecutionTime = null;
            public bool requiresUserInteraction = false;
        }

        public class Case
        {
            public string CaseName => caseLabel.text;
            public TextMeshPro caseLabel;
            public List<CheckBox> checkBoxes = new List<CheckBox>();
            public List<TextMeshPro> labels = new List<TextMeshPro>();

            /// <summary>
            /// Purely visual on-object checkmark markers (see <see cref="AddObjectMarker"/>). Not part
            /// of <see cref="checkBoxes"/>, so they don't show up as separate sub-tests in the exported
            /// test json/readme - they just mirror the pass/fail state of a real check right at the object.
            /// </summary>
            public List<CheckBox> markers = new List<CheckBox>();
            public List<GameObject> arrows = new List<GameObject>();

            public List<Entry> entryNodes = new List<Entry>();

            /// <summary> Sequential run only: fires all entry points of this case. </summary>
            public GltfInteractivityExportNode startFanOut = null;
            public List<GltfInteractivityExportNode> earlyCompletionTriggers = new List<GltfInteractivityExportNode>();

            /// <summary> How long this case needs until all its checks are done (seconds). </summary>
            public float ExecutionTime => entryNodes.Select(e => e.delayedExecutionTime ?? 0f).DefaultIfEmpty(0f).Max();
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
        private List<GltfInteractivityExportNode> _earlyCompletionTriggers = new List<GltfInteractivityExportNode>();

        public void AddResultEventForAllTests()
        {
            var maxExecutionTime = cases.Select( c => c.entryNodes.Max( e => e.delayedExecutionTime ?? 0)).Max();
            var allCheckBoxes = cases.SelectMany(c => c.checkBoxes).Where( c => c.ResultPassValueVarId != -1);
            if (maxExecutionTime > 0)
                maxExecutionTime += 0.5f; // Add some buffer time to ensure all checks are done
            if (runCasesSequentially)
                maxExecutionTime = cases.Sum(SequentialCaseRunTime);

            var start = interactivityExportContext.CreateNode<Event_OnStartNode>();
            var startEvent = interactivityExportContext.CreateNode<Event_SendNode>();
            var onStartEventArgs = new Dictionary<string, GltfInteractivityNode.EventValues>();
            onStartEventArgs.Add("expectedDuration", new GltfInteractivityNode.EventValues
                {
                    Type = GltfTypes.TypeIndex(typeof(float)),
                    Value = maxExecutionTime
                });
            var startEventId = interactivityExportContext.Context.AddEventWithIdIfNeeded("test/onStart", onStartEventArgs);
            startEvent.Configuration[Event_SendNode.IdEvent].Value = startEventId;
            start.FlowOut().ConnectToFlowDestination(startEvent.FlowIn());
            startEvent.ValueIn("expectedDuration").SetValue(maxExecutionTime);

            FlowOutRef startFlowToResult = null;
            GltfInteractivityExportNode delayNode = null;
            if (runCasesSequentially)
            {
                // Early completion triggers are handled per case in the sequential run
                startFlowToResult = AddSequentialCaseRun(startEvent.FlowOut());
            }
            else if (maxExecutionTime == 0)
            {
                startFlowToResult = startEvent.FlowOut();
            }
            else
            {
                delayNode = interactivityExportContext.CreateNode<Flow_SetDelayNode>();
                delayNode.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(maxExecutionTime);
                startEvent.FlowOut().ConnectToFlowDestination(delayNode.FlowIn());
                startFlowToResult = delayNode.FlowOut(Flow_SetDelayNode.IdFlowDone);
            }

            ValueOutRef prevAndResult = null;
            foreach (var checkBox in allCheckBoxes)
            {
                VariablesHelpers.GetVariable(interactivityExportContext, checkBox.ResultPassValueVarId, out var resultVar);

                if (prevAndResult != null)
                {
                    var andNode = interactivityExportContext.CreateNode<Math_AndNode>();
                    andNode.ValueIn("a").ConnectToSource(prevAndResult);
                    andNode.ValueIn("b").ConnectToSource(resultVar);
                    prevAndResult = andNode.FirstValueOut();
                }
                else
                {
                    prevAndResult = resultVar;
                }
            }

            if (prevAndResult == null)
            {
                Debug.LogError("No checkboxes with ResultPassValueVarId found. Cannot create result event.");
                return;
            }
            
            var branch = interactivityExportContext.CreateNode<Flow_BranchNode>();
            branch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(prevAndResult);
            startFlowToResult.ConnectToFlowDestination(branch.FlowIn());

            // Let test cases that already know their result (e.g. a UserInteraction test whose
            // required gesture just completed) skip the rest of the wait instead of sitting through
            // the full delayedExecutionTime window before test/onSuccess or test/onFailed fires.
            foreach (var trigger in runCasesSequentially ? Enumerable.Empty<GltfInteractivityExportNode>() : _earlyCompletionTriggers)
            {
                var triggerFlowOut = trigger.FlowOut("000");
                if (delayNode != null)
                {
                    var cancelDelay = interactivityExportContext.CreateNode<Flow_CancelDelayNode>();
                    cancelDelay.ValueIn(Flow_CancelDelayNode.IdDelay).ConnectToSource(delayNode.ValueOut(Flow_SetDelayNode.IdOutLastDelay));
                    triggerFlowOut.ConnectToFlowDestination(cancelDelay.FlowIn());
                    cancelDelay.FlowOut().ConnectToFlowDestination(branch.FlowIn());
                }
                else
                {
                    triggerFlowOut.ConnectToFlowDestination(branch.FlowIn());
                }
            }

            var passEvent = interactivityExportContext.CreateNode<Event_SendNode>();
            passEvent.Configuration[Event_SendNode.IdEvent].Value = interactivityExportContext.Context.AddEventWithIdIfNeeded("test/onSuccess");

            var failEvent = interactivityExportContext.CreateNode<Event_SendNode>();
            failEvent.Configuration[Event_SendNode.IdEvent].Value = interactivityExportContext.Context.AddEventWithIdIfNeeded("test/onFailed");

            branch.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(passEvent.FlowIn());
            branch.FlowOut(Flow_BranchNode.IdFlowOutFalse).ConnectToFlowDestination(failEvent.FlowIn());

        }

        /// <summary> Time a case gets in the sequential run before the next case starts (seconds). </summary>
        private float SequentialCaseRunTime(Case testCase)
        {
            var executionTime = testCase.ExecutionTime;
            if (executionTime > 0)
                executionTime += 0.5f; // Same buffer as for the overall result, so all fallback checks are done
            return executionTime + sequentialCaseGap;
        }

        private GltfInteractivityExportNode GetCaseStartFanOut(Case testCase)
        {
            if (testCase.startFanOut == null)
                testCase.startFanOut = interactivityExportContext.CreateNode<Flow_SequenceNode>();
            return testCase.startFanOut;
        }

        /// <summary>
        /// Chains all cases: moves the running case arrow to the case, starts all its entry points and
        /// continues with the next case after <see cref="SequentialCaseRunTime"/>, or as soon as one of
        /// the case's early completion triggers fired. Returns the flow that fires after the last case.
        /// </summary>
        private FlowOutRef AddSequentialCaseRun(FlowOutRef startFlow)
        {
            var nodeCreator = interactivityExportContext;
            var flow = startFlow;
            var arrowIndex = _runningCaseArrow ? nodeCreator.Context.exporter.GetTransformIndex(_runningCaseArrow.transform) : -1;

            foreach (var testCase in cases)
            {
                if (arrowIndex != -1)
                {
                    var moveArrow = nodeCreator.CreateNode<Pointer_SetNode>();
                    PointersHelper.SetupPointerTemplateAndTargetInput(moveArrow, PointersHelper.IdPointerNodeIndex, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/translation", GltfTypes.Float3);
                    moveArrow.ValueIn(Pointer_SetNode.IdValue).SetValue(AnimationTestHelper.ToGltf(RunningCaseArrowPosition(testCase)));
                    moveArrow.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(arrowIndex);
                    flow.ConnectToFlowDestination(moveArrow.FlowIn(Pointer_SetNode.IdFlowIn));
                    flow = moveArrow.FlowOut(Pointer_SetNode.IdFlowOut);
                }

                var fanOut = GetCaseStartFanOut(testCase);
                flow.ConnectToFlowDestination(fanOut.FlowIn(Flow_SequenceNode.IdFlowIn));

                // Runs after all entry points of the case were started
                var caseDone = nodeCreator.CreateNode<Flow_SetDelayNode>();
                caseDone.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(SequentialCaseRunTime(testCase));
                fanOut.FlowOut(fanOut.FlowConnections.Count.ToString("D3")).ConnectToFlowDestination(caseDone.FlowIn(Flow_SetDelayNode.IdFlowIn));

                // Continue only once, either after the case's time or when it completed early
                var continueOnce = nodeCreator.CreateNode<Flow_DoNNode>();
                continueOnce.ValueIn(Flow_DoNNode.IdN).SetValue(1);
                caseDone.FlowOut(Flow_SetDelayNode.IdFlowDone).ConnectToFlowDestination(continueOnce.FlowIn(Flow_DoNNode.IdFlowIn));

                foreach (var trigger in testCase.earlyCompletionTriggers)
                {
                    var cancelDelay = nodeCreator.CreateNode<Flow_CancelDelayNode>();
                    cancelDelay.ValueIn(Flow_CancelDelayNode.IdDelay).ConnectToSource(caseDone.ValueOut(Flow_SetDelayNode.IdOutLastDelay));
                    trigger.FlowOut("000").ConnectToFlowDestination(cancelDelay.FlowIn());
                    cancelDelay.FlowOut().ConnectToFlowDestination(continueOnce.FlowIn(Flow_DoNNode.IdFlowIn));
                }

                flow = continueOnce.FlowOut(Flow_DoNNode.IdOut);
            }

            if (arrowIndex != -1)
            {
                HideOnFlow(_runningCaseArrow.transform, out var hideArrow, out var afterHideArrow);
                flow.ConnectToFlowDestination(hideArrow);
                flow = afterHideArrow;
            }

            return flow;
        }

        /// <summary>
        /// Adds the arrow that shows which case is currently running in the sequential run
        /// (see <see cref="runCasesSequentially"/>). It points at the case label from the left and
        /// is moved from case to case by the graph. Call after all cases were prepared.
        /// </summary>
        public void AddRunningCaseArrow(Color color)
        {
            if (_runningCaseArrow || cases.Count == 0)
                return;

            _runningCaseArrow = CreateArrow("RunningCaseArrow", RunningCaseArrowPosition(cases[0]), color);
            // The arrow's tip points down (-Y), turn it towards the label
            _runningCaseArrow.transform.rotation = Quaternion.FromToRotation(Vector3.down, cases[0].caseLabel.rectTransform.right);
            _runningCaseArrow.transform.localScale = Vector3.one * 0.4f;
        }

        /// <summary> Local position (in Root space) of the running case arrow's tip for the given case: left of its label. </summary>
        private Vector3 RunningCaseArrowPosition(Case testCase)
        {
            const float gap = 0.15f;
            var labelTransform = testCase.caseLabel.rectTransform;
            var rect = labelTransform.rect;
            var labelLeft = labelTransform.TransformPoint(new Vector3(rect.xMin, rect.center.y, 0f));
            return _root.InverseTransformPoint(labelLeft - labelTransform.right * gap);
        }
        
        public TestContext(CheckBox defaultCheckBox, TextMeshPro caseLabelPrefab, TextMeshPro labelPrefab, Transform root)
        {
            _checkBoxPrefab = defaultCheckBox;
            _caseLabelPrefab = caseLabelPrefab;
            _labelPrefab = labelPrefab;
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
                        _lastDelayedFallback = interactivityExportContext.CreateNode<Flow_SetDelayNode>();
                        _lastDelayedFallback.ValueIn(Flow_SetDelayNode.IdDuration).SetValue(_lastEntryPoint.delayedExecutionTime.Value);
                    }
                    
                    _lastEntryPoint.startFlow.ConnectToFlowDestination(_lastDelayedFallback.FlowIn(Flow_SequenceNode.IdFlowIn));
                }
                
                if (_lastEntryPointFallbackSequence == null && (_currentFallbackFlows.Count > 1 || _lastDelayedFallback == null))
                {
                    var nodeCreator = interactivityExportContext;
                    _lastEntryPointFallbackSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
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
                    _lastEntryPointNodeSequence = nodeCreator.CreateNode<Flow_SequenceNode>();
                }
                _lastEntryPointNodeSequence.FlowConnections.Clear();
                foreach (var flow in _currentEntryFlows)
                    _lastEntryPointNodeSequence.FlowOut(_lastEntryPointNodeSequence.FlowConnections.Count.ToString("D3")).ConnectToFlowDestination(flow);
            }
            
            
            var startFlow = _lastEntryPoint.startFlow;

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

            var newEntry = new Entry();
            if (runCasesSequentially)
            {
                // Started by the case's fan-out, see AddSequentialCaseRun
                var caseFanOut = GetCaseStartFanOut(currentCase);
                var starter = nodeCreator.CreateNode<Flow_SequenceNode>();
                caseFanOut.FlowOut(caseFanOut.FlowConnections.Count.ToString("D3")).ConnectToFlowDestination(starter.FlowIn(Flow_SequenceNode.IdFlowIn));
                newEntry.node = starter;
                newEntry.startFlow = starter.FlowOut("000");
            }
            else
            {
                var startNode = nodeCreator.CreateNode<Event_OnStartNode>();
                newEntry.node = startNode;
                newEntry.startFlow = startNode.FlowOut(Event_OnStartNode.IdFlowOut);
            }
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

        /// <summary>
        /// Returns a flow-in socket a test case can wire into once it already knows its final
        /// result (e.g. right after the last event of a UserInteraction test's required gesture
        /// has fired), so the shared test/onSuccess / test/onFailed evaluation runs immediately
        /// instead of waiting out the rest of the entry point's delayedExecutionTime window.
        /// </summary>
        public void AddEarlyCompletionTrigger(out FlowInRef flowIn)
        {
            var proxy = interactivityExportContext.CreateNode<Flow_SequenceNode>();
            flowIn = proxy.FlowIn(Flow_SequenceNode.IdFlowIn);
            _earlyCompletionTriggers.Add(proxy);
            currentCase.earlyCompletionTriggers.Add(proxy);
        }

        /// <summary>
        /// Changes the serialized graph after export, for valid JSON the exporter can't produce
        /// (see <see cref="TestFileExporterPlugin.TestFileExportContext.AddExtensionJsonPatch"/>).
        /// The patch gets the KHR_interactivity extension object; the test graph is graphs[0].
        /// </summary>
        public void PatchSerializedExtension(Action<JObject> patch)
        {
            if (interactivityExportContext.Context is TestFileExporterPlugin.TestFileExportContext testExportContext)
                testExportContext.AddExtensionJsonPatch(patch);
            else
                Debug.LogError("PatchSerializedExtension requires the test file exporter plugin.");
        }

        public void PatchSerializedGraph(Action<JObject> patch)
        {
            PatchSerializedExtension(extension => patch((JObject)extension["graphs"][0]));
        }

        /// <summary> The serialized node of <paramref name="node"/> inside a graph passed to a patch. </summary>
        public static JObject SerializedNode(JObject graph, GltfInteractivityExportNode node)
        {
            if (node.Index < 0)
                throw new InvalidOperationException($"Node {node.Schema.Op} was removed or merged during export and can't be patched.");
            return (JObject)graph["nodes"][node.Index];
        }

        /// <summary> Index of the type with the given signature in a graph passed to a patch; the type is added if missing. </summary>
        public static int SerializedTypeIndex(JObject graph, string signature)
        {
            if (!(graph["types"] is JArray types))
                graph["types"] = types = new JArray();
            for (int i = 0; i < types.Count; i++)
                if ((string)types[i]["signature"] == signature)
                    return i;
            types.Add(new JObject { ["signature"] = signature });
            return types.Count - 1;
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
            var log = nodeCreator.AddLog(GltfInteractivityExportNodes.LogLevel.Info, message.Replace("\n",""));
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
        
        public void AddSequence(FlowOutRef flowIn, params FlowInRef[] sequences)
        {
            var nodeCreator = interactivityExportContext;
            
            var sequenceNode = nodeCreator.CreateNode<Flow_SequenceNode>();
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
            var addLoopRangeCounter = nodeCreator.CreateNode<Math_AddNode>();
            addLoopRangeCounter.ValueIn(Math_AddNode.IdValueA).ConnectToSource(loopRangeCounter);
            addLoopRangeCounter.ValueIn(Math_AddNode.IdValueB).SetValue(1);
            
            var setVar = VariablesHelpers.SetVariable(nodeCreator, newVarId, out var setVarSocket, out _, out _);
            setVarSocket.ConnectToSource(addLoopRangeCounter.FirstValueOut());
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

        /// <summary>
        /// Instantiates an instruction label (using labelPrefab) at a fixed local position, meant to
        /// tell the tester what to do for manual/user-interaction test cases. Use <see cref="HideOnFlow"/>
        /// to hide it once the requested interaction was performed successfully.
        /// </summary>
        public TextMeshPro AddLabel(string text, Vector3 localPosition)
        {
            var newLabel = GameObject.Instantiate(_labelPrefab, _root);
            newLabel.transform.localPosition = localPosition;
            newLabel.text = text;
            newLabel.gameObject.SetActive(true);
            newLabel.gameObject.name = "Label_" + currentCase.CaseName;
            currentCase.labels.Add(newLabel);
            return newLabel;
        }

        /// <summary>
        /// Creates a flow-in socket that, when triggered, hides the given object (by scaling it to zero)
        /// so the tester can see at a glance which interaction was already performed successfully.
        /// </summary>
        public void HideOnFlow(Transform target, out FlowInRef flowIn)
        {
            HideOnFlow(target, out flowIn, out _);
        }

        public void HideOnFlow(Transform target, out FlowInRef flowIn, out FlowOutRef flowOut)
        {
            var targetIndex = interactivityExportContext.Context.exporter.GetTransformIndex(target);

            var setScale = interactivityExportContext.CreateNode<Pointer_SetNode>();
            PointersHelper.SetupPointerTemplateAndTargetInput(setScale, PointersHelper.IdPointerNodeIndex, "/nodes/[" + PointersHelper.IdPointerNodeIndex + "]/scale", GltfTypes.Float3);
            setScale.ValueIn(Pointer_SetNode.IdValue).SetValue(Vector3.zero);
            setScale.ValueIn(PointersHelper.IdPointerNodeIndex).SetValue(targetIndex);

            flowIn = setScale.FlowIn(Pointer_SetNode.IdFlowIn);
            flowOut = setScale.FlowOut(Pointer_SetNode.IdFlowOut);
        }

        /// <summary>
        /// Instantiates a bare checkbox (no label text, not reserved in the checkbox grid) at a fixed
        /// local position - meant to be placed directly on/above an interactive object so the tester
        /// gets immediate pass/fail feedback right where they're looking, in addition to the regular
        /// checkbox list. It is not added to <see cref="Case.checkBoxes"/>, so it won't appear as its
        /// own sub-test in the exported json/readme; wire the same flow into it as the real check.
        /// </summary>
        public CheckBox AddObjectMarker(Vector3 localPosition, bool asWaiting = true)
        {
            var marker = GameObject.Instantiate(_checkBoxPrefab, _root);
            marker.transform.localPosition = localPosition;
            marker.gameObject.SetActive(true);
            marker.gameObject.name = "Marker_" + currentCase.CaseName;
            marker.SetText("");
            marker.SetCase(currentCase);
            marker.context = this;
            if (asWaiting)
                marker.Waiting();
            currentCase.markers.Add(marker);
            return marker;
        }

        /// <summary>
        /// Creates a simple 3D "waypoint" arrow (a downward-pointing cone on a short shaft) hovering
        /// above a target position, to visually point the tester at the object they need to interact
        /// with. Combine with <see cref="HideOnFlow"/> to make it disappear once the interaction was
        /// performed.
        /// </summary>
        public GameObject AddPointerArrow(Vector3 localPosition, Color color)
        {
            var arrow = CreateArrow("PointerArrow_" + currentCase.CaseName, localPosition, color);
            currentCase.arrows.Add(arrow);
            return arrow;
        }

        /// <summary> Builds the arrow object (tip at its origin, pointing down) under <see cref="Root"/>. </summary>
        private GameObject CreateArrow(string name, Vector3 localPosition, Color color)
        {
            const float coneHeight = 0.7f;
            const float coneRadius = 0.5f;
            const int segments = 12;

            var root = new GameObject(name);
            root.transform.SetParent(_root, false);
            root.transform.localPosition = localPosition;

            var mat = new Material(Shader.Find("Standard")) { color = color };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.6f);

            var head = new GameObject("ArrowHead");
            head.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero; // apex, points at the target below
            for (int i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * coneRadius, coneHeight, Mathf.Sin(angle) * coneRadius);
            }
            vertices[segments + 1] = new Vector3(0, coneHeight, 0); // base center, closes the cap

            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % segments + 1;
                triangles.Add(0); triangles.Add(b); triangles.Add(a); // side face
                triangles.Add(segments + 1); triangles.Add(a); triangles.Add(b); // base cap
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            head.AddComponent<MeshFilter>().mesh = mesh;
            head.AddComponent<MeshRenderer>().material = mat;

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "ArrowShaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localPosition = new Vector3(0, coneHeight + 0.6f, 0);
            shaft.transform.localScale = new Vector3(0.18f, 0.6f, 0.18f);
            shaft.GetComponent<MeshRenderer>().material = mat;

            return root;
        }

        public CheckBox AddCheckBox(string name, bool asWaiting = false, bool flowOnce = false)
        {
            var newCheckBox = GameObject.Instantiate(_checkBoxPrefab, _root);
            newCheckBox.transform.localPosition = _layout.ReserveSpace(newCheckBox.CheckBoxSize);
            newCheckBox.gameObject.SetActive(true);
            newCheckBox.gameObject.name = "CheckBox_" + name;
            newCheckBox.SetText(name);
            newCheckBox.SetCase(currentCase);
            newCheckBox.context = this;
            newCheckBox.flowOnce = flowOnce;
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

            foreach (var label in Labels)
                GameObject.DestroyImmediate(label.gameObject);

            foreach (var marker in Markers)
                GameObject.DestroyImmediate(marker.gameObject);

            foreach (var arrow in Arrows)
                GameObject.DestroyImmediate(arrow);

            if (_runningCaseArrow)
                GameObject.DestroyImmediate(_runningCaseArrow);
            _runningCaseArrow = null;

            cases.Clear();
        }
    }
}