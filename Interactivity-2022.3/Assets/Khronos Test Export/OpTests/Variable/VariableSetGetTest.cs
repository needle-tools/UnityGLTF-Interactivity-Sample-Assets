using System;
using UnityEngine;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;

namespace Khronos_Test_Export
{
    public class VariableSetGetTest : ITestCase
    {
        // private CheckBox checkFloatSet;
        // private CheckBox checkVector2Set;
        // private CheckBox checkVector3Set;
        // private CheckBox checkVector4Set;
        // private CheckBox checkBoolSet;
        // private CheckBox checkIntSet;

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
            // checkBoolSet = context.AddCheckBox("bool");
            // checkIntSet = context.AddCheckBox("int");
            // checkFloatSet = context.AddCheckBox("float");
            // checkVector2Set = context.AddCheckBox("float2");
            // checkVector3Set = context.AddCheckBox("float3");
            // checkVector4Set = context.AddCheckBox("float4");
            //
            // context.NewRow();
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
            checkDefaultVector4Get = context.AddCheckBox("default float");
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;

            // void AddSubTestInput(Type type, CheckBox checkBox, object valueToSet)
            // {
            //     var gltfType = GltfTypes.TypeIndex(type);
            //     var nullValue = GltfTypes.GetNullByType(gltfType);
            //     var varId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTest_"+GltfTypes.allTypes[gltfType], nullValue, gltfType);
            //
            //     ValueOutRef valueOutRef;
            //     switch (type)
            //     {
            //         case Type boolType when boolType == typeof(bool):
            //             
            //             
            //             
            //             valueOutRef = new ValueOutRef<bool>(boolType);
            //             break;
            //         case Type intType when intType == typeof(int):
            //             valueOutRef = new ValueOutRef<int>(intType);
            //             break;
            //         case Type floatType when floatType == typeof(float):
            //             valueOutRef = new ValueOutRef<float>(floatType);
            //             break;
            //         case Type vector2Type when vector2Type == typeof(Vector2):
            //             valueOutRef = new ValueOutRef<Vector2>(vector2Type);
            //             break;
            //         case Type vector3Type when vector3Type == typeof(Vector3):
            //             valueOutRef = new ValueOutRef<Vector3>(vector3Type);
            //             break;
            //         case Type vector4Type when vector4Type == typeof(Vector4):
            //             valueOutRef = new ValueOutRef<Vector4>(vector4Type);
            //             break;
            //         default:
            //             throw new ArgumentException("Unsupported type: " + type);
            //     }
            //     
            //     var setVarNode = VariablesHelpers.SetVariable(nodeCreator, varId);
            //     
            //     
            //     context.SetEntryPoint(setVarNode.FlowIn(Variable_SetNode.IdFlowIn), "Set Variable " + GltfTypes.allTypes[gltfType]);
            //   
            //     VariablesHelpers.GetVariable(nodeCreator, varId, out var getVar);
            //     
            //     checkBox.SetupCheck(context, getVar, out var checkFlow, valueToSet, false);
            //     setVarNode.FlowOut(Variable_SetNode.IdFlowOut).ConnectToFlowDestination(checkFlow);
            //     
            // }
            //
            void AddSubTestStaticInput(Type type, CheckBox checkBox, object valueToSet)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var nullValue = GltfTypes.GetNullByType(gltfType);
                var varId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTest_"+GltfTypes.allTypes[gltfType], nullValue, gltfType);
                
                VariablesHelpers.SetVariableStaticValue(nodeCreator, varId, valueToSet, out var setFlow, out var setOutFlow);
                context.SetEntryPoint(setFlow, "Set Variable " + GltfTypes.allTypes[gltfType]);
              
                VariablesHelpers.GetVariable(nodeCreator, varId, out var getVar);
                
                checkBox.SetupCheck(context, getVar, out var checkFlow, valueToSet, false);
                setOutFlow.ConnectToFlowDestination(checkFlow);
            }
            
            void AddSubTestGetDefault(Type type, CheckBox checkBox, object valueToSet)
            {
                var gltfType = GltfTypes.TypeIndex(type);
                var varId = nodeCreator.Context.AddVariableWithIdIfNeeded("VarSetTest_"+GltfTypes.allTypes[gltfType], valueToSet, gltfType);
                
                VariablesHelpers.GetVariable(nodeCreator, varId, out var getVar);
                
                checkBox.SetupCheck(context, getVar, out var checkFlow, valueToSet, false);
                context.SetEntryPoint(checkFlow, "Get default value from Variable " + GltfTypes.allTypes[gltfType]);
            }     
            
            // AddSubTestInput(typeof(bool), checkBoolSet, true);
            // AddSubTestInput(typeof(int), checkIntSet, 1);
            // AddSubTestInput(typeof(float), checkFloatSet, 1f);
            // AddSubTestInput(typeof(Vector2), checkVector2Set, Vector2.one);
            // AddSubTestInput(typeof(Vector3), checkVector3Set, Vector3.one);
            // AddSubTestInput(typeof(Vector4), checkVector4Set, Vector4.one);
            
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