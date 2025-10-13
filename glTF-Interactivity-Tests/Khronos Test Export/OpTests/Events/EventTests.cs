using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    public class EventTests : ITestCase
    {
        private CheckBox _withoutParametersCheckbox;
        private CheckBox _withParametersCheckbox;
        private CheckBox _parameterIntCheckbox;
        private CheckBox _parameterBoolCheckbox;
        private CheckBox _parameterFloatCheckbox;
        private CheckBox _defaultParameterIntCheckbox;
        private CheckBox _defaultParameterBoolCheckbox;
        private CheckBox _defaultParameterFloatCheckbox;  
        public string GetTestName()
        {
            return "event/send and receive";
        }

        public string GetTestDescription()
        {
            return "";
        }

        public void PrepareObjects(TestContext context)
        {
            _withoutParametersCheckbox = context.AddCheckBox("Without Parameters", true);
            _withParametersCheckbox = context.AddCheckBox("With Parameters (flow received)", true);
            _defaultParameterIntCheckbox = context.AddCheckBox("Default Event Value (Int)", false);
            _defaultParameterBoolCheckbox = context.AddCheckBox("Default Event Value (Bool)", false);
            _defaultParameterFloatCheckbox = context.AddCheckBox("Default Event Value (Float)", false);
            _parameterIntCheckbox = context.AddCheckBox("Rcv Parameter Int", true);
            _parameterBoolCheckbox = context.AddCheckBox("Rcv Parameter Bool", true);
            _parameterFloatCheckbox = context.AddCheckBox("Rcv Parameter Float", true);
        }

        public void CreateNodes(TestContext context)
        {
            var eventWithOutParameters =
                context.interactivityExportContext.Context.AddEventWithIdIfNeeded("_eventWithoutParameters" +
                    Guid.NewGuid());

            var paremeters = new Dictionary<string, GltfInteractivityNode.EventValues>();
            paremeters.Add("boolParameter", new GltfInteractivityNode.EventValues
            {
                Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Bool),
                Value = false
            });
            paremeters.Add("floatParameter", new GltfInteractivityNode.EventValues
            {
                Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Float),
                Value = 1f
            });
            paremeters.Add("intParameter", new GltfInteractivityNode.EventValues
            {
                Type = GltfTypes.TypeIndexByGltfSignature(GltfTypes.Int),
                Value = 1f
            });
            
            var eventWithParameters =
                context.interactivityExportContext.Context.AddEventWithIdIfNeeded(
                    "_eventWithParameters" + Guid.NewGuid(), paremeters);

            var nodeCreator = context.interactivityExportContext;

            // Without Parameters
            var sendNode = nodeCreator.CreateNode<Event_SendNode>();
            sendNode.Configuration[Event_SendNode.IdEvent].Value = eventWithOutParameters;
            
            context.NewEntryPoint(_withoutParametersCheckbox.GetText(), 1f);
            context.AddToCurrentEntrySequence(sendNode.FlowIn());
            
            var receiveNode = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveNode.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventWithParameters;
            
            _withoutParametersCheckbox.SetupCheck(receiveNode.FlowOut());
            
            // With Parameters
            
            var receiveNodeWithParameters = nodeCreator.CreateNode<Event_ReceiveNode>();
            receiveNodeWithParameters.Configuration[Event_ReceiveNode.IdEventConfig].Value = eventWithParameters;
            
            _defaultParameterIntCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("intParameter"), out var defFlowInt, 1);
            _defaultParameterBoolCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("boolParameter"), out var defFlowBool, false);
            _defaultParameterFloatCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("floatParameter"), out var defFlowFloat, 1f);
            context.AddToCurrentEntrySequence(defFlowInt);
            context.AddToCurrentEntrySequence(defFlowBool);
            context.AddToCurrentEntrySequence(defFlowFloat);

            var sendNodeWithParameters = nodeCreator.CreateNode<Event_SendNode>();
            sendNodeWithParameters.Configuration[Event_SendNode.IdEvent].Value = eventWithParameters;
            
            sendNodeWithParameters.ValueIn("boolParameter").SetValue(true);
            sendNodeWithParameters.ValueIn("floatParameter").SetValue(2f);
            sendNodeWithParameters.ValueIn("intParameter").SetValue(2);
            
            context.NewEntryPoint(_withParametersCheckbox.GetText(), 1f);
            context.AddToCurrentEntrySequence(sendNodeWithParameters.FlowIn());
            
            _withParametersCheckbox.SetupCheck(out var flowRecv);
            _parameterIntCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("intParameter"), out var flowInt, 2);
            _parameterBoolCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("boolParameter"), out var flowBool, true);
            _parameterFloatCheckbox.SetupCheck(receiveNodeWithParameters.ValueOut("floatParameter"), out var flowFloat, 2f);
            context.AddSequence(receiveNodeWithParameters.FlowOut(), flowRecv, flowInt, flowBool, flowFloat);
        }
    }
}