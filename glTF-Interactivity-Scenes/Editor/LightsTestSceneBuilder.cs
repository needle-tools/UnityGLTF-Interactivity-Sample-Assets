#if UNITY_EDITOR && HAVE_VISUAL_SCRIPTING
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using GraphBuilder = Tests.Editor.PerformanceTestSceneBuilder.GraphBuilder;

namespace Tests.Editor
{
    /// <summary>
    /// Exporter test scene for Light members (KHR_lights_punctual pointers): color, intensity, range and spotAngle of a
    /// point, a spot and a directional light.
    ///  - EXPORTED: the getters read the values written by the light export, so export and getter conversions must match.
    ///  - SET, THEN GET: setter and getter conversions must be inverse.
    ///  - FROM A VARIABLE: the light comes from a GameObject variable, which the exporter can't resolve, so the graph
    ///    reads the light ref from /nodes/{}/extensions/KHR_lights_punctual/light (not supported by every viewer).
    /// The last step restores the exported values, so RUN checks them again. The test lights sit behind the board to
    /// not tint it. Intensities depend on GraphicsSettings.lightsUseLinearIntensity (squared when off).
    /// Re-run the menu item after changing this file.
    /// </summary>
    public static class LightsTestSceneBuilder
    {
        public const string SceneName = "20261020-Lights";

        private const float LabelWidth = 2.4f;
        private const float CellWidth = 1.5f;
        private const float ColumnWidth = LabelWidth + 4 * CellWidth;
        private const float ColorTolerance = 1e-3f;

        private class TestLight
        {
            public string label;
            public Light light;
            public string[] members;
            // exported values and the values set by SET, THEN GET
            public Dictionary<string, object> initial, changed;
        }

        [MenuItem("glTF Interactivity/Exporter Test Scenes/Lights")]
        public static void Create() => Create(true);

        public static void Create(bool askToOverwrite)
        {
            var kit = ExporterTestSceneKit.Begin(SceneName, "LIGHTS",
                $"LIGHT COLOR, INTENSITY, RANGE AND SPOT ANGLE AS KHR_LIGHTS_PUNCTUAL POINTERS. LINEAR INTENSITY {(GraphicsSettings.lightsUseLinearIntensity ? "ON" : "OFF")}",
                1, ColumnWidth, cleanUp: false, askToOverwrite);
            if (kit == null)
                return;
            var g = kit.g;

            var lightsRoot = new GameObject("TestLights").transform;
            // behind the board (the board faces -Z of its root, which is turned by 180 degrees)
            lightsRoot.position = new Vector3(ColumnWidth * -0.5f, -3f, -3f);

            var lights = new[]
            {
                new TestLight
                {
                    label = "POINT", light = CreateLight(lightsRoot, "PointLight", LightType.Point, new Vector3(-2f, 0f, 0f)),
                    members = new[] { nameof(Light.color), nameof(Light.intensity), nameof(Light.range) },
                    initial = Values(new Color(1f, 0.85f, 0.6f), 1.3f, 6f),
                    changed = Values(new Color(1f, 0.5f, 0.25f), 2.5f, 7.5f),
                },
                new TestLight
                {
                    label = "SPOT", light = CreateLight(lightsRoot, "SpotLight", LightType.Spot, new Vector3(0f, 0f, 0f)),
                    members = new[] { nameof(Light.color), nameof(Light.intensity), nameof(Light.range), nameof(Light.spotAngle) },
                    initial = Values(new Color(0.6f, 0.8f, 1f), 2f, 8f, 40f),
                    changed = Values(new Color(0.2f, 0.6f, 1f), 3f, 9f, 55f),
                },
                new TestLight
                {
                    label = "DIRECTIONAL", light = CreateLight(lightsRoot, "DirectionalLight", LightType.Directional, new Vector3(2f, 0f, 0f)),
                    members = new[] { nameof(Light.color), nameof(Light.intensity) },
                    initial = Values(new Color(0.9f, 0.95f, 1f), 0.4f),
                    changed = Values(new Color(0.9f, 0.8f, 0.3f), 0.7f),
                },
            };
            foreach (var test in lights)
                Apply(test.light, test.initial);

            var exportedChecks = new List<ControlInput>();
            var setChecks = new List<ControlInput>();

            // --- EXPORTED: get on Start (and on RUN after the restore)
            kit.Section("EXPORTED VALUES (GET)");
            for (int i = 0; i < lights.Length; i++)
            {
                var test = lights[i];
                var row = Row(kit, test.label);
                g.Row(i * 1200f);
                for (int m = 0; m < test.members.Length; m++)
                {
                    var member = test.members[m];
                    var indicator = Cell(kit, row, m, member);
                    var actual = Get(g, test.light, member);
                    exportedChecks.Add(kit.Check(indicator, Matches(kit, actual, test.initial[member]),
                        $"{test.label} {member} exported: graph {{0}}, expected {ExporterTestSceneKit.Format(test.initial[member])}", new[] { actual }));
                }
            }

            // --- SET, THEN GET
            kit.Section("SET, THEN GET");
            for (int i = 0; i < lights.Length; i++)
            {
                var test = lights[i];
                var row = Row(kit, test.label);
                g.Row(5000f + i * 1200f);
                for (int m = 0; m < test.members.Length; m++)
                {
                    var member = test.members[m];
                    var indicator = Cell(kit, row, m, member);
                    var set = Set(g, test.light, member, test.changed[member]);
                    var actual = Get(g, test.light, member);
                    setChecks.Add(kit.Check(indicator, Matches(kit, actual, test.changed[member]),
                        $"{test.label} {member} after set: graph {{0}}, expected {ExporterTestSceneKit.Format(test.changed[member])}", new[] { actual },
                        new GraphBuilder.Chain { enter = set.assign, exit = set.assigned }));
                }
            }

            // --- FROM A VARIABLE (point light): GameObject variable -> Light target, resolved at runtime
            var point = lights[0];
            var spot = lights[1];
            g.Declare("pointLightObject", point.light.gameObject);
            g.Declare("spotLightObject", spot.light.gameObject);
            kit.Section("LIGHT FROM A VARIABLE (NODE LIGHT POINTER)");
            {
                g.Row(10000f);
                var row = Row(kit, "GET POINT");
                var colorIndicator = Cell(kit, row, 0, nameof(Light.color));
                var color = GetFromVariable(g, "pointLightObject", nameof(Light.color));
                exportedChecks.Add(kit.Check(colorIndicator, Matches(kit, color, point.initial[nameof(Light.color)]),
                    $"variable POINT color: graph {{0}}, expected {ExporterTestSceneKit.Format(point.initial[nameof(Light.color)])}", new[] { color }));
                var intensityIndicator = Cell(kit, row, 1, nameof(Light.intensity));
                var intensity = GetFromVariable(g, "pointLightObject", nameof(Light.intensity));
                exportedChecks.Add(kit.Check(intensityIndicator, Matches(kit, intensity, point.initial[nameof(Light.intensity)]),
                    $"variable POINT intensity: graph {{0}}, expected {ExporterTestSceneKit.Format(point.initial[nameof(Light.intensity)])}", new[] { intensity }));
                var rangeIndicator = Cell(kit, row, 2, nameof(Light.range));
                var range = GetFromVariable(g, "pointLightObject", nameof(Light.range));
                exportedChecks.Add(kit.Check(rangeIndicator, Matches(kit, range, point.initial[nameof(Light.range)]),
                    $"variable POINT range: graph {{0}}, expected {ExporterTestSceneKit.Format(point.initial[nameof(Light.range)])}", new[] { range }));
            }
            {
                // set through the variable, read back from the light itself: both resolve to the same light
                g.Row(11500f);
                var row = Row(kit, "SET SPOT");
                var values = Values(new Color(1f, 0.3f, 0.7f), 1.7f, 4f, 30f);
                for (int m = 0; m < spot.members.Length; m++)
                {
                    var member = spot.members[m];
                    var indicator = Cell(kit, row, m, member);
                    var set = SetFromVariable(g, "spotLightObject", member, values[member]);
                    var actual = Get(g, spot.light, member);
                    setChecks.Add(kit.Check(indicator, Matches(kit, actual, values[member]),
                        $"variable SPOT {member} after set: graph {{0}}, expected {ExporterTestSceneKit.Format(values[member])}", new[] { actual },
                        new GraphBuilder.Chain { enter = set.assign, exit = set.assigned }));
                }
            }

            foreach (var check in exportedChecks)
                kit.AddStep(check);
            foreach (var check in setChecks)
                kit.AddStep(check);

            // --- restore the exported values for the next RUN (last step, after all checks)
            g.Row(13000f);
            GraphBuilder.Chain restore = null;
            foreach (var test in lights)
            foreach (var member in test.members)
            {
                var set = Set(g, test.light, member, test.initial[member]);
                if (restore == null)
                    restore = new GraphBuilder.Chain { enter = set.assign };
                else
                    GraphBuilder.Flow(restore.exit, set.assign);
                restore.exit = set.assigned;
            }
            kit.AddStep(restore);

            kit.Finish();
        }

        private static Dictionary<string, object> Values(Color color, float intensity, float range = 0f, float spotAngle = 0f) =>
            new Dictionary<string, object>
            {
                [nameof(Light.color)] = color,
                [nameof(Light.intensity)] = intensity,
                [nameof(Light.range)] = range,
                [nameof(Light.spotAngle)] = spotAngle,
            };

        private static Light CreateLight(Transform parent, string name, LightType type, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            // pointing away from the board
            go.transform.localRotation = Quaternion.Euler(20f, 180f, 0f);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.shadows = LightShadows.None;
            return light;
        }

        private static void Apply(Light light, Dictionary<string, object> values)
        {
            light.color = (Color)values[nameof(Light.color)];
            light.intensity = (float)values[nameof(Light.intensity)];
            if (light.type != LightType.Directional)
                light.range = (float)values[nameof(Light.range)];
            if (light.type == LightType.Spot)
                light.spotAngle = (float)values[nameof(Light.spotAngle)];
        }

        private static Transform Row(ExporterTestSceneKit kit, string label)
        {
            var row = kit.Row();
            kit.Label(row, label, new Vector2(0f, 0f), LabelWidth - 0.1f, 0.14f);
            return row;
        }

        private static Material Cell(ExporterTestSceneKit kit, Transform row, int index, string member)
        {
            var x = LabelWidth + index * CellWidth;
            kit.Label(row, member.ToUpperInvariant(), new Vector2(x, 0f), CellWidth - 0.4f, 0.12f, ExporterTestSceneKit.DimTextColor);
            return kit.Indicator(row, member, x + CellWidth - 0.35f);
        }

        private static ValueOutput Matches(ExporterTestSceneKit kit, ValueOutput actual, object expected) =>
            kit.Matches(actual, expected, expected is Color ? ColorTolerance : 1e-3f * Mathf.Max(1f, Mathf.Abs(expected is float f ? f : 0f)));

        private static ValueOutput Get(GraphBuilder g, Light light, string member)
        {
            var unit = g.Add(new GetMember(new Member(typeof(Light), member)), 0f, 160f);
            unit.target.SetDefaultValue(light);
            return unit.value;
        }

        private static SetMember Set(GraphBuilder g, Light light, string member, object value)
        {
            var unit = g.Add(new SetMember(new Member(typeof(Light), member)));
            unit.target.SetDefaultValue(light);
            unit.input.SetDefaultValue(value);
            return unit;
        }

        private static ValueOutput GetFromVariable(GraphBuilder g, string variable, string member)
        {
            var target = g.Get(variable);
            var unit = g.Add(new GetMember(new Member(typeof(Light), member)), 0f, 160f);
            GraphBuilder.Value(target, unit.target);
            return unit.value;
        }

        private static SetMember SetFromVariable(GraphBuilder g, string variable, string member, object value)
        {
            var target = g.Get(variable);
            var unit = g.Add(new SetMember(new Member(typeof(Light), member)));
            GraphBuilder.Value(target, unit.target);
            unit.input.SetDefaultValue(value);
            return unit;
        }
    }
}
#endif
