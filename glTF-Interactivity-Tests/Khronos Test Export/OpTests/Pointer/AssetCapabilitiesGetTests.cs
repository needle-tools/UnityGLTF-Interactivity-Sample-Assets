using System;
using System.Collections.Generic;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;

namespace Khronos_Test_Export
{
    /// <summary>
    /// Tests the read-only "Asset Capabilities" (KHR_interactivity spec §4.2.1) and
    /// "Implementation-Specific Runtime Limits" (§4.2.2) virtual pointers.
    ///
    /// - asset/majorVersion and asset/minorVersion have deterministic values (2 / 0) that are asserted.
    /// - asset/extensions/&lt;EXT&gt;/enabled resolves to true for an extension that is used by the
    ///   exported asset (KHR_interactivity is always present in an interactivity graph).
    ///   The virtual object only exists for extensions that are listed in extensionsUsed AND supported
    ///   by the implementation. Two made-up extensions cover both halves of that rule: one is listed in
    ///   extensionsUsed (not extensionsRequired) but unsupported, the other is not listed at all. In both
    ///   cases the pointer cannot be resolved: pointer/get returns isValid = false and the bool default
    ///   value (false).
    /// - limits/* are implementation-specific (a runtime may report int.MaxValue), so their exact
    ///   value is not asserted. The spec requires every limit to be at least 1, so we assert
    ///   value &gt;= 1 (in addition to isValid).
    /// </summary>
    public class AssetCapabilitiesGetTests : ITestCase, IDisposable
    {
        private class PointerCase
        {
            public string label;
            public string pointer;
            public int gltfType;
            // When non-null, the value output is asserted to equal this exactly.
            public object expectedValue;
            // When true, the value output is only asserted to be >= 1 (spec requires limits >= 1).
            public bool atLeastOne;
            // Expected isValid output of the pointer/get node.
            public bool expectedIsValid = true;
        }

        private static readonly string EnabledExtension = "KHR_interactivity";
        // Listed in extensionsUsed, but no implementation supports it.
        private static readonly string UnsupportedExtension = "KHR_this_extension_does_not_exist";
        // Not listed in extensionsUsed at all.
        private static readonly string UnlistedExtension = "KHR_this_extension_is_not_used";

        private PointerCase[] pointerCases = new PointerCase[]
        {
            new PointerCase
            {
                label = "asset/majorVersion",
                pointer = AssetHelpers.AssetMajorVersionPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                expectedValue = 2,
            },
            new PointerCase
            {
                label = "asset/minorVersion",
                pointer = AssetHelpers.AssetMinorVersionPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                expectedValue = 0,
            },
            new PointerCase
            {
                label = "asset/extensions/" + EnabledExtension + "/enabled",
                pointer = string.Format(AssetHelpers.AssetExtensionEnabledPointerFormat, EnabledExtension),
                gltfType = GltfTypes.TypeIndex(typeof(bool)),
                expectedValue = true,
            },
            new PointerCase
            {
                label = "asset/extensions/" + UnsupportedExtension + "/enabled",
                pointer = string.Format(AssetHelpers.AssetExtensionEnabledPointerFormat, UnsupportedExtension),
                gltfType = GltfTypes.TypeIndex(typeof(bool)),
                expectedValue = false,
                expectedIsValid = false,
            },
            new PointerCase
            {
                label = "asset/extensions/" + UnlistedExtension + "/enabled",
                pointer = string.Format(AssetHelpers.AssetExtensionEnabledPointerFormat, UnlistedExtension),
                gltfType = GltfTypes.TypeIndex(typeof(bool)),
                expectedValue = false,
                expectedIsValid = false,
            },
            new PointerCase
            {
                label = "limits/maxActiveAnimations",
                pointer = AssetHelpers.LimitMaxActiveAnimationsPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                atLeastOne = true,
            },
            new PointerCase
            {
                label = "limits/maxActiveDelays",
                pointer = AssetHelpers.LimitMaxActiveDelaysPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                atLeastOne = true,
            },
            new PointerCase
            {
                label = "limits/maxActivePropertyInterpolations",
                pointer = AssetHelpers.LimitMaxActivePropertyInterpolationsPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                atLeastOne = true,
            },
            new PointerCase
            {
                label = "limits/maxActiveVariableInterpolations",
                pointer = AssetHelpers.LimitMaxActiveVariableInterpolationsPointer,
                gltfType = GltfTypes.TypeIndex(typeof(int)),
                atLeastOne = true,
            },
        };

        // Parallel to pointerCases: value checkbox is null when only isValid is checked.
        private List<CheckBox> valueCheckBoxes = new List<CheckBox>();
        private List<CheckBox> isValidCheckBoxes = new List<CheckBox>();

        public string GetTestName()
        {
            return "pointer/AssetCapabilities_GetTests";
        }

        public string GetTestDescription()
        {
            return "Reads the asset capability and runtime limit virtual pointers (KHR_interactivity §4.2.1 / §4.2.2).";
        }

        public void PrepareObjects(TestContext context)
        {
            valueCheckBoxes.Clear();
            isValidCheckBoxes.Clear();

            foreach (var pointerCase in pointerCases)
            {
                CheckBox valueCheckBox = null;
                if (pointerCase.expectedValue != null)
                    valueCheckBox = context.AddCheckBox(pointerCase.label);
                else if (pointerCase.atLeastOne)
                    valueCheckBox = context.AddCheckBox(pointerCase.label + " >= 1");

                var isValidCheckBox = context.AddCheckBox(pointerCase.label + " isValid");

                valueCheckBoxes.Add(valueCheckBox);
                isValidCheckBoxes.Add(isValidCheckBox);
                context.NewRow();
            }
        }

        public void CreateNodes(TestContext context)
        {
            var nodeCreator = context.interactivityExportContext;
            nodeCreator.Context.exporter.DeclareExtensionUsage(UnsupportedExtension);

            for (int i = 0; i < pointerCases.Length; i++)
            {
                var pointerCase = pointerCases[i];
                context.NewEntryPoint(pointerCase.label);

                var pointerGet = nodeCreator.CreateNode<Pointer_GetNode>();
                PointersHelper.AddPointerConfig(pointerGet, pointerCase.pointer, pointerCase.gltfType);

                var isValidCheckBox = isValidCheckBoxes[i];
                isValidCheckBox.SetupCheck(pointerGet.ValueOut(Pointer_GetNode.IdIsValid), out var isValidFlowIn, pointerCase.expectedIsValid);

                var valueCheckBox = valueCheckBoxes[i];
                if (valueCheckBox != null)
                {
                    ValueOutRef checkedValue;
                    if (pointerCase.atLeastOne)
                    {
                        // Assert value >= 1 by comparing to 1 and checking the resulting bool is true.
                        var geNode = nodeCreator.CreateNode<Math_GeNode>();
                        geNode.ValueIn(Math_GeNode.IdValueA).ConnectToSource(pointerGet.ValueOut(Pointer_GetNode.IdValue));
                        geNode.ValueIn(Math_GeNode.IdValueB).SetValue(1);
                        checkedValue = geNode.ValueOut(Math_GeNode.IdOut);
                    }
                    else
                    {
                        checkedValue = pointerGet.ValueOut(Pointer_GetNode.IdValue);
                    }

                    var expected = pointerCase.atLeastOne ? (object)true : pointerCase.expectedValue;
                    valueCheckBox.SetupCheck(checkedValue, out var checkFlowIn, expected);
                    context.AddToCurrentEntrySequence(checkFlowIn, isValidFlowIn);
                }
                else
                {
                    context.AddToCurrentEntrySequence(isValidFlowIn);
                }
            }
        }

        public void Dispose()
        {
            valueCheckBoxes.Clear();
            isValidCheckBoxes.Clear();
        }
    }
}
