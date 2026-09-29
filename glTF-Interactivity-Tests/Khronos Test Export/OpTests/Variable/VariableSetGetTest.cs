using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;

namespace Khronos_Test_Export
{
    public class VariableSetGetTest : ITestCase
    {
        private CheckBox checkFloatSet;
        private CheckBox checkVector2Set;
        private CheckBox checkVector3Set;
        private CheckBox checkVector4Set;
        private CheckBox checkBoolSet;
        private CheckBox checkIntSet;

        private CheckBox checkStaticFloatSet;
        private CheckBox checkStaticVector2Set;
        private CheckBox checkStaticVector3Set;
        private CheckBox checkStaticVector4Set;
        private CheckBox checkStaticBoolSet;
        private CheckBox checkStaticIntSet;

        private CheckBox checkDefaultFloatGet;
        private CheckBox checkDefaultVector2Get;
        private CheckBox checkDefaultVector3Get;
        private CheckBox checkDefaultVector4Get;
        private CheckBox checkDefaultBoolGet;
        private CheckBox checkDefaultIntGet;
        
        
        public string GetTestName()
        {
            return "variable/set and get";
        }

        public string GetTestDescription()
        {
            return "Set and Get variable test";
        }
        
        public void PrepareObjects(TestContext context)
        {
            checkBoolSet = context.AddCheckBox("connected bool");
            checkIntSet = context.AddCheckBox("connected int");
            checkFloatSet = context.AddCheckBox("connected float");
            checkVector2Set = context.AddCheckBox("connected float2");
            checkVector3Set = context.AddCheckBox("connected float3");
            checkVector4Set = context.AddCheckBox("connected float4");
            context.NewRow();
            checkStaticBoolSet = context.AddCheckBox("static bool");
            checkStaticIntSet = context.AddCheckBox("static int");
            checkStaticFloatSet = context.AddCheckBox("static float");
            checkStaticVector2Set = context.AddCheckBox("static float2");
            checkStaticVector3Set = context.AddCheckBox("static float3");
            checkStaticVector4Set = context.AddCheckBox("static float4");
            context.NewRow();
            checkDefaultBoolGet = context.AddCheckBox("default bool");
            checkDefaultIntGet = context.AddCheckBox("default int");
            checkDefaultFloatGet = context.AddCheckBox("default float");
            checkDefaultVector2Get = context.AddCheckBox("default float2");
            checkDefaultVector3Get = context.AddCheckBox("default float3");
            checkDefaultVector4Get = context.AddCheckBox("default float4");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // The value input of variable/set is connected to another node's output instead of a literal.
            // The source is a second variable holding the value, read with variable/get.
            void AddSubTestInput(Type type, CheckBox checkBox, object valueToSet)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var nullValue = GltfTypes.GetNullByType(gltfType);
                var sourceVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTestSource_"+GltfTypes.allTypes[gltfType]+Guid.NewGuid().ToString(), valueToSet, gltfType);
                var targetVarId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTestTarget_"+GltfTypes.allTypes[gltfType]+Guid.NewGuid().ToString(), nullValue, gltfType);

                VariablesHelpers.GetVariable(nodeCreator, sourceVarId, out var sourceValue);
                VariablesHelpers.SetVariable(nodeCreator, targetVarId, out var setValue, out var setFlow, out var setOutFlow);
                setValue.ConnectToSource(sourceValue);
                context.NewEntryPoint(setFlow, "Set Variable (connected) " + GltfTypes.allTypes[gltfType]);

                VariablesHelpers.GetVariable(nodeCreator, targetVarId, out var getVar);

                checkBox.SetupCheck(getVar, out var checkFlow, valueToSet, false);
                setOutFlow.ConnectToFlowDestination(checkFlow);
            }

            void AddSubTestStaticInput(Type type, CheckBox checkBox, object valueToSet)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var nullValue = GltfTypes.GetNullByType(gltfType);
                var varId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTestStatic_"+GltfTypes.allTypes[gltfType]+GltfTypes.allTypes[gltfType]+Guid.NewGuid().ToString(), nullValue, gltfType);
                
                VariablesHelpers.SetVariableStaticValue(nodeCreator, varId, valueToSet, out var setFlow, out var setOutFlow);
                context.NewEntryPoint(setFlow, "Set Variable " + GltfTypes.allTypes[gltfType]);
              
                VariablesHelpers.GetVariable(nodeCreator, varId, out var getVar);
                
                checkBox.SetupCheck(getVar, out var checkFlow, valueToSet, false);
                setOutFlow.ConnectToFlowDestination(checkFlow);
            }
            
            void AddSubTestGetDefault(Type type, CheckBox checkBox, object valueToSet)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var varId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTest_"+GltfTypes.allTypes[gltfType]+Guid.NewGuid().ToString(), valueToSet, gltfType);
                
                VariablesHelpers.GetVariable(nodeCreator, varId, out var getVar);
                
                context.NewEntryPoint("Get default value from Variable " + GltfTypes.allTypes[gltfType]);
                checkBox.SetupCheck(getVar, out var checkFlow, valueToSet, false);
                context.AddToCurrentEntrySequence(checkFlow);
            }     
            
            AddSubTestInput(typeof(bool), checkBoolSet, true);
            AddSubTestInput(typeof(int), checkIntSet, 1);
            AddSubTestInput(typeof(float), checkFloatSet, 1f);
            AddSubTestInput(typeof(Vector2), checkVector2Set, Vector2.one);
            AddSubTestInput(typeof(Vector3), checkVector3Set, Vector3.one);
            AddSubTestInput(typeof(Vector4), checkVector4Set, Vector4.one);
            
            AddSubTestStaticInput(typeof(bool), checkStaticBoolSet, true);
            AddSubTestStaticInput(typeof(int), checkStaticIntSet, 1);
            AddSubTestStaticInput(typeof(float), checkStaticFloatSet, 1f);
            AddSubTestStaticInput(typeof(Vector2), checkStaticVector2Set, Vector2.one);
            AddSubTestStaticInput(typeof(Vector3), checkStaticVector3Set, Vector3.one);
            AddSubTestStaticInput(typeof(Vector4), checkStaticVector4Set, Vector4.one);
            
            AddSubTestGetDefault(typeof(bool), checkDefaultBoolGet, true);
            AddSubTestGetDefault(typeof(int), checkDefaultIntGet, 1);
            AddSubTestGetDefault(typeof(float), checkDefaultFloatGet, 1f);
            AddSubTestGetDefault(typeof(Vector2), checkDefaultVector2Get, Vector2.one);
            AddSubTestGetDefault(typeof(Vector3), checkDefaultVector3Get, Vector3.one);
            AddSubTestGetDefault(typeof(Vector4), checkDefaultVector4Get, Vector4.one);
        }

    }
}