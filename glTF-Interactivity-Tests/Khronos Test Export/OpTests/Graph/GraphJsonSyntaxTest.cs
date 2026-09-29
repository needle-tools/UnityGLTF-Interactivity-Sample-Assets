using System;
using Newtonsoft.Json.Linq;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// JSON that looks wrong but is valid. The exporter can't write it, so the serialized graph is changed after export:
    ///  - integers written as float literals (variable index 0.0, int values 5.0 and 2.0) are exact integers
    ///  - several types with the signature "custom"
    ///  - internal events without id are told apart by their index
    ///  - variables with the same name are separate variables
    ///  - doubled brackets in a pointer template are literal brackets, not template parameters
    /// </summary>
    public class GraphJsonSyntaxTest : ITestCase
    {
        private CheckBox _floatLiteralIntegersCheckBox;
        private CheckBox _customTypesCheckBox;
        private CheckBox _eventWithoutIdReceivedCheckBox;
        private CheckBox _otherEventWithoutIdCheckBox;
        private CheckBox _sameNameFirstVariableCheckBox;
        private CheckBox _sameNameSecondVariableCheckBox;
        private CheckBox _escapedBracketsCheckBox;

        public string GetTestName()
        {
            return "graph/json syntax";
        }

        public string GetTestDescription()
        {
            return "Integers written as float literals, duplicate custom types, internal events without id, variables with the same name and doubled brackets in pointer templates.";
        }

        public void PrepareObjects(TestContext context)
        {
            _floatLiteralIntegersCheckBox = context.AddCheckBox("integers written\nas 5.0 and 2.0");
            _customTypesCheckBox = context.AddCheckBox("two custom\ntypes", true);
            _eventWithoutIdReceivedCheckBox = context.AddCheckBox("event without id:\nreceived", true);
            _otherEventWithoutIdCheckBox = context.AddCheckBox("event without id:\nother event not received", true);
            _otherEventWithoutIdCheckBox.Negate();
            context.NewRow();
            _sameNameFirstVariableCheckBox = context.AddCheckBox("same variable name:\nfirst unchanged");
            _sameNameSecondVariableCheckBox = context.AddCheckBox("same variable name:\nsecond set");
            _escapedBracketsCheckBox = context.AddCheckBox("pointer with doubled\nbrackets: isValid");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // Integers as float literals: int variable 5.0, variable/get index N.0, math/add input 2.0
            var intVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("FloatLiteralInt_" + Guid.NewGuid(), 5, GltfTypes.Int);
            var intVarGet = VariablesHelpers.GetVariable(nodeCreator, intVarId, out var intVarValue);
            var add = nodeCreator.CreateNode<Math_AddNode>();
            add.ValueIn(Math_AddNode.IdValueA).ConnectToSource(intVarValue);
            add.ValueIn(Math_AddNode.IdValueB).SetValue(2);
            context.NewEntryPoint(_floatLiteralIntegersCheckBox.GetText());
            _floatLiteralIntegersCheckBox.SetupCheck(add.FirstValueOut(), out var addCheckFlow, 7);
            context.AddToCurrentEntrySequence(addCheckFlow);
            context.PatchSerializedGraph(graph =>
            {
                graph["variables"][intVarId]["value"] = new JArray(5.0);
                TestContext.SerializedNode(graph, intVarGet)["configuration"][Variable_GetNode.IdConfigVarIndex]["value"] = new JArray((double)intVarId);
                TestContext.SerializedNode(graph, add)["values"][Math_AddNode.IdValueB]["value"] = new JArray(2.0);
            });

            // Two types with the signature "custom"
            context.NewEntryPoint(_customTypesCheckBox.GetText());
            _customTypesCheckBox.SetupCheck(out var customTypesFlow);
            context.AddToCurrentEntrySequence(customTypesFlow);
            context.PatchSerializedGraph(graph =>
            {
                var types = (JArray)graph["types"];
                types.Add(new JObject { ["signature"] = "custom" });
                types.Add(new JObject { ["signature"] = "custom" });
            });

            // Two internal events: the id is removed after export
            var eventA = nodeCreator.Context.AddEventWithIdIfNeeded("InternalEventA_" + Guid.NewGuid());
            var eventB = nodeCreator.Context.AddEventWithIdIfNeeded("InternalEventB_" + Guid.NewGuid());
            var send = nodeCreator.CreateNode<Event_SendNode>();
            send.Configuration[Event_SendNode.IdEvent].Value = eventA;
            var receiveA = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveA.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventA;
            var receiveB = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveB.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventB;
            context.NewEntryPoint(send.FlowIn(), _eventWithoutIdReceivedCheckBox.GetText(), 1f);
            _eventWithoutIdReceivedCheckBox.SetupCheck(receiveA.FlowOut());
            _otherEventWithoutIdCheckBox.SetupNegateCheck(receiveB.FlowOut());
            context.PatchSerializedGraph(graph =>
            {
                ((JObject)graph["events"][eventA]).Remove("id");
                ((JObject)graph["events"][eventB]).Remove("id");
            });

            // Two int variables with the same name: setting the second one keeps the first one
            var firstVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("SameNameFirst_" + Guid.NewGuid(), 10, GltfTypes.Int);
            var secondVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("SameNameSecond_" + Guid.NewGuid(), 20, GltfTypes.Int);
            VariablesHelpers.SetVariableStaticValue(nodeCreator, secondVarId, 30, out var setSecondFlow, out _);
            VariablesHelpers.GetVariable(nodeCreator, firstVarId, out var firstVarValue);
            VariablesHelpers.GetVariable(nodeCreator, secondVarId, out var secondVarValue);
            context.NewEntryPoint(_sameNameFirstVariableCheckBox.GetText());
            _sameNameFirstVariableCheckBox.SetupCheck(firstVarValue, out var firstVarCheckFlow, 10);
            _sameNameSecondVariableCheckBox.SetupCheck(secondVarValue, out var secondVarCheckFlow, 30);
            context.AddToCurrentEntrySequence(setSecondFlow, firstVarCheckFlow, secondVarCheckFlow);
            context.PatchSerializedGraph(graph =>
            {
                graph["variables"][firstVarId]["name"] = "sameName";
                graph["variables"][secondVarId]["name"] = "sameName";
            });

            // "/nodes/0/extras/[[a]]/{{b}}" has no template parameters and isn't an Object Model pointer
            var pointerGet = nodeCreator.CreateNode<Pointer_GetNode>();
            PointersHelper.AddPointerConfig(pointerGet, "/nodes/0/translation", GltfTypes.Float3);
            context.NewEntryPoint(_escapedBracketsCheckBox.GetText());
            _escapedBracketsCheckBox.SetupCheck(pointerGet.ValueOut(Pointer_GetNode.IdIsValid), out var pointerCheckFlow, false);
            context.AddToCurrentEntrySequence(pointerCheckFlow);
            context.PatchSerializedGraph(graph =>
                TestContext.SerializedNode(graph, pointerGet)["configuration"][Pointer_GetNode.IdPointer]["value"] = new JArray("/nodes/0/extras/[[a]]/{{b}}"));
        }
    }
}
