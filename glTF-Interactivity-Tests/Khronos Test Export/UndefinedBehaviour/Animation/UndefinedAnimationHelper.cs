using System.Collections.Generic;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityGLTF;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Shared graph building blocks for the undefined behaviour animation tests (see <see cref="UndefinedBehaviourTestAttribute"/>).
    /// The animated objects come from <see cref="AnimationTestHelper"/>: a linear clip from the origin to TargetPosition over T
    /// seconds, so with TargetPosition.y == T the node's Y translation equals the applied effective timestamp.
    /// </summary>
    internal static class UndefinedAnimationHelper
    {
        /// <summary> Clip length of the test animations; with <see cref="TargetPosition"/> Y == effective timestamp. </summary>
        public const float T = 2f;
        public static readonly UnityEngine.Vector3 TargetPosition = new UnityEngine.Vector3(1f, 2f, 3f);

        /// <summary> The number of event/onTick activations so far. One tick per frame, so equal counts mean the same frame. </summary>
        public static ValueOutRef CreateTickCounter(TestContext context)
        {
            var onTick = context.interactivityExportContext.CreateNode<Event_OnTickNode>();
            context.AddPlusOneCounter(out var count, out var increase);
            onTick.FlowOut(Event_OnTickNode.IdFlowOut).ConnectToFlowDestination(increase);
            return count;
        }

        public static int AddVariable(TestContext context, string name, object initValue, string gltfType)
        {
            return context.interactivityExportContext.Context.AddVariableWithIdIfNeeded(name, initValue, gltfType);
        }

        public static ValueOutRef Get(TestContext context, int variableId)
        {
            VariablesHelpers.GetVariable(context.interactivityExportContext, variableId, out var value);
            return value;
        }

        /// <summary> Sets the variable to <paramref name="value"/> unless it was already set (still 0), so it keeps the first flow's value. </summary>
        public static FlowInRef RecordFirst(TestContext context, int intVariableId, int value)
        {
            var nodeCreator = context.interactivityExportContext;
            var isUnset = nodeCreator.CreateNode<Math_EqNode>();
            isUnset.ValueIn(Math_EqNode.IdValueA).ConnectToSource(Get(context, intVariableId));
            isUnset.ValueIn("b").SetValue(0);
            var branch = nodeCreator.CreateNode<Flow_BranchNode>();
            branch.ValueIn(Flow_BranchNode.IdCondition).ConnectToSource(isUnset.FirstValueOut());
            VariablesHelpers.SetVariableStaticValue(nodeCreator, intVariableId, value, out var setFlow, out _);
            branch.FlowOut(Flow_BranchNode.IdFlowOutTrue).ConnectToFlowDestination(setFlow);
            return branch.FlowIn(Flow_BranchNode.IdFlowIn);
        }

        public static ValueOutRef Sub(TestContext context, ValueOutRef a, ValueOutRef b)
        {
            var sub = context.interactivityExportContext.CreateNode<Math_SubNode>();
            sub.ValueIn(Math_SubNode.IdValueA).ConnectToSource(a);
            sub.ValueIn("b").ConnectToSource(b);
            return sub.FirstValueOut();
        }

        public static ValueOutRef IsNaN(TestContext context, ValueOutRef value)
        {
            var isNaN = context.interactivityExportContext.CreateNode<Math_IsNaNNode>();
            isNaN.ValueIn(Math_IsNaNNode.IdValueA).ConnectToSource(value);
            return isNaN.FirstValueOut();
        }

        /// <summary> min &lt;= value &lt;= max; false for NaN. </summary>
        public static ValueOutRef InRange(TestContext context, ValueOutRef value, float min, ValueOutRef max)
        {
            var nodeCreator = context.interactivityExportContext;
            var ge = nodeCreator.CreateNode<Math_GeNode>();
            ge.ValueIn(Math_GeNode.IdValueA).ConnectToSource(value);
            ge.ValueIn("b").SetValue(min);
            var le = nodeCreator.CreateNode<Math_LeNode>();
            le.ValueIn(Math_LeNode.IdValueA).ConnectToSource(value);
            le.ValueIn("b").ConnectToSource(max);
            var and = nodeCreator.CreateNode<Math_AndNode>();
            and.ValueIn(Math_AndNode.IdValueA).ConnectToSource(ge.FirstValueOut());
            and.ValueIn("b").ConnectToSource(le.FirstValueOut());
            return and.FirstValueOut();
        }

        public static ValueOutRef GreaterThan(TestContext context, ValueOutRef value, float limit)
        {
            var gt = context.interactivityExportContext.CreateNode<Math_GtNode>();
            gt.ValueIn(Math_GtNode.IdValueA).ConnectToSource(value);
            gt.ValueIn("b").SetValue(limit);
            return gt.FirstValueOut();
        }

        public static ValueOutRef State(TestContext context, int animationIndex, string property, string gltfType)
        {
            return AnimationTestHelper.CreateAnimationStateGet(context.interactivityExportContext, animationIndex, property, gltfType);
        }

        /// <summary> A float accessor (SCALAR) for extra animation samplers. </summary>
        public static AccessorId ExportFloatAccessor(GLTFSceneExporter exporter, float[] values, bool withMinMax)
        {
            var bytes = new byte[values.Length * sizeof(float)];
            System.Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            List<double> min = null, max = null;
            if (withMinMax)
            {
                min = new List<double> { double.MaxValue };
                max = new List<double> { double.MinValue };
                foreach (var v in values)
                {
                    min[0] = System.Math.Min(min[0], v);
                    max[0] = System.Math.Max(max[0], v);
                }
            }
            return exporter.ExportAccessor(bytes, (uint)values.Length, GLTFAccessorAttributeType.SCALAR, GLTFComponentType.Float, min, max);
        }

        /// <summary> An extension object written as plain JSON, for extensions UnityGLTF has no class for. </summary>
        public class JsonExtension : IExtension
        {
            private readonly string _name;
            private readonly JObject _value;

            public JsonExtension(string name, JObject value)
            {
                _name = name;
                _value = value;
            }

            public JProperty Serialize() => new JProperty(_name, _value.DeepClone());

            public IExtension Clone(GLTFRoot root) => new JsonExtension(_name, (JObject)_value.DeepClone());
        }
    }
}
