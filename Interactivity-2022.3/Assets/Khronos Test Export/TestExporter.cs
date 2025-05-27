using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using UnityGLTF.Interactivity.VisualScripting;

namespace Khronos_Test_Export
{
    public class TestExporter : MonoBehaviour, IInteractivityExport
    {
        public CheckBox checkBoxPrefab;
        public TextMeshPro caseLabelPrefab;

        private TestContext currentTestContext;
        private ITestCase[] currentTestCases;

        private Dictionary<ITestCase, HashSet<string>> _schemaUsedInCase = new Dictionary<ITestCase, HashSet<string>>();
        private Dictionary<ITestCase, TestContext.Case> _testCase = new Dictionary<ITestCase, TestContext.Case>();

        public class JsonOutput
        {
            public string glbFileName;
            public string name;
            public JsonCaseOutput[] tests;
            public string[] usedSchemas;
        }

        [Serializable]
        public class JsonCaseOutput
        {
            public string name;
            public string description;
            public string[] usedSchemas;

            [Serializable]
            [JsonConverter(typeof(EntryPointJsonConverter))]
            public class EntryPoint
            {
                public string name;
                public int nodeId;
                public float? delayedExecutionTime;
                public bool requiresUserInteraction;
            }

            public EntryPoint[] entryPoints;

            [Serializable]
            [JsonConverter(typeof(SubTestsJsonConverter))]
            public class SubTests
            {
                public string name;
                public string resultVarName;
                public int resultVarId;
                public object expectedResultValue;
                public int successResultVarId;
                public string successResultVarName;
            }

            public SubTests[] subTests;
            
            public class EntryPointJsonConverter : JsonConverter<EntryPoint>
            {
                public override void WriteJson(JsonWriter writer, EntryPoint value, JsonSerializer serializer)
                {
                    var obj = new JObject();
                    obj[nameof(EntryPoint.name)] = value.name;
                    obj[nameof(EntryPoint.nodeId)] = value.nodeId;
                    if (value.delayedExecutionTime != null)
                        obj[nameof(EntryPoint.delayedExecutionTime)] = value.delayedExecutionTime;
                    if (value.requiresUserInteraction)
                        obj[nameof(EntryPoint.requiresUserInteraction)] = value.requiresUserInteraction;
                    obj.WriteTo(writer);
                }

                public override EntryPoint ReadJson(JsonReader reader, Type objectType, EntryPoint existingValue, bool hasExistingValue,
                    JsonSerializer serializer)
                {
                    throw new NotImplementedException();
                }
            }
            
            public class SubTestsJsonConverter : JsonConverter<SubTests>
            {
                public override void WriteJson(JsonWriter writer, SubTests value, JsonSerializer serializer)
                {
                    var obj = new JObject();
                    obj[nameof(SubTests.name)] = value.name;
                    obj[nameof(SubTests.resultVarName)] = value.resultVarName;
                    obj[nameof(SubTests.resultVarId)] = value.resultVarId;
                    obj["resultVarType"] = GltfTypes.GetTypeMapping(value.expectedResultValue.GetType()).GltfSignature;
                    GltfInteractivityNode.ValueSerializer.Serialize(value.expectedResultValue, obj);
                    obj[nameof(SubTests.expectedResultValue)] = obj["value"];
                    obj["successResultVarId"] = value.successResultVarId;
                    obj["successResultVarName"] = value.successResultVarName;
                    obj.Remove("value");
                    obj.WriteTo(writer);
                }

                public override SubTests ReadJson(JsonReader reader, Type objectType, SubTests existingValue, bool hasExistingValue,
                    JsonSerializer serializer)
                {
                    throw new NotImplementedException();
                }
            }
        }
    
        [Serializable]
        public class IndexEntry
        {
            public string label;
            public string name;
           // public string screenshot = "";
            public string[] tags;
            public Dictionary<string, string> variants = new Dictionary<string, string>();
        }

        private void CreateIndexJsonFile(string name, List<(ITestCase, string, string)> tests)
        {
            var indexData = new List<IndexEntry>();
            foreach (var test in tests)
            {
                var entry = new IndexEntry();
                entry.label = test.Item1.GetTestName();
                entry.name = test.Item1.GetTestName().Replace(" ", "_");
                entry.tags = _schemaUsedInCase[test.Item1].ToArray();
                entry.variants.Add("glTF-Binary", test.Item2.Replace(@"\", "/"));
                entry.variants.Add("test-Json", test.Item3.Replace(@"\", "/"));
                indexData.Add(entry);
            }

            var json = JsonConvert.SerializeObject(indexData, Formatting.Indented);
            
            System.IO.File.WriteAllText(name, json);
        }

        private void CreateTestCaseJsonFile(string name, ITestCase[] testCases, string filename, string glbFileName)
        {
            var jsonOutput = new JsonOutput();
            jsonOutput.glbFileName = glbFileName;
            jsonOutput.name = name;

            var tests = new List<JsonCaseOutput>();
            foreach (var testCase in testCases)
            {
                var test = _testCase[testCase];
                var testCaseOutput = new JsonCaseOutput();
                tests.Add(testCaseOutput);
                testCaseOutput.name = testCase.GetTestName();
                testCaseOutput.description = testCase.GetTestDescription();
                
                var entries = new List<JsonCaseOutput.EntryPoint>();
                foreach (var entry in test.entryNodes)
                {
                    var entryPoint = new JsonCaseOutput.EntryPoint();
                    entryPoint.name = entry.name;
                    entryPoint.nodeId = entry.node.Index;
                    if (entry.delayedExecutionTime.HasValue)
                    {
                        entryPoint.name += " (delayed execution)";
                        entryPoint.delayedExecutionTime = entry.delayedExecutionTime;
                    }
                    if (entry.requiresUserInteraction)
                    {
                        entryPoint.name += " (requires user interaction)";
                        entryPoint.requiresUserInteraction = entry.requiresUserInteraction;
                    }
                    entries.Add(entryPoint);
                }

                testCaseOutput.entryPoints = entries.ToArray();
                testCaseOutput.usedSchemas = _schemaUsedInCase[testCase].OrderBy(s => s).ToArray();

                var subTests = new List<JsonCaseOutput.SubTests>();
                foreach (var check in test.checkBoxes)
                {
                    var subTest = new JsonCaseOutput.SubTests();
                    subTest.name = check.GetText();
                    subTest.resultVarName = check.GetResultVariableName();
                    subTest.resultVarId = check.ResultValueVarId;
                    subTest.expectedResultValue = check.expectedValue;
                    subTest.successResultVarId = check.ResultPassValueVarId;
                    subTest.successResultVarName = check.GetResultPassVariableName();
                    subTests.Add(subTest);
                }

                testCaseOutput.subTests = subTests.ToArray();
            }

            jsonOutput.tests = tests.ToArray();
            jsonOutput.usedSchemas = tests.SelectMany(t => t.usedSchemas).Distinct().OrderBy(s => s).ToArray();
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            string json = JsonConvert.SerializeObject(jsonOutput, Formatting.Indented, new JsonCaseOutput.SubTestsJsonConverter());
          
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            System.IO.File.WriteAllText(filename, json);
            Debug.Log("Test case json file created at: " + filename);
        }

        private void CreateTestCaseReadmeFile(ITestCase testCase, string filename)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Test Sample: " + testCase.GetTestName());
            sb.AppendLine("Description: " + testCase.GetTestDescription());
            sb.AppendLine();
            sb.AppendLine("Tests:");
            var caseContext = _testCase[testCase];
            foreach (var test in caseContext.checkBoxes)
            {
                sb.AppendLine(
                    $"\t**{test.GetText()}** - Result saved in Variable **{test.GetResultVariableName()}** with Id **{test.ResultValueVarId}**");
            }

            sb.AppendLine();
            sb.AppendLine("Schemas used in this test case:");
            foreach (var schema in _schemaUsedInCase[testCase].OrderBy(s => s))
            {
                sb.AppendLine($"\t{schema}");
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            System.IO.File.WriteAllText(filename, sb.ToString());
            Debug.Log("Test case md file created at: " + filename);
        }

        public string ShowDestinationFolderDialog()
        {
            var path = EditorPrefs.GetString("GLTFTestExportPath", "");
            path = UnityEditor.EditorUtility.SaveFolderPanel("Destination Folder", path, "");
            EditorPrefs.SetString("GLTFTestExportPath", path);
            return path;
        }
        
        public void ExportTest(ITestCase[] cases, bool batchExport, string allInOneName, string indexFileName)
        {
            var settings = GLTFSettings.GetDefaultSettings();
            var testFileExporterPLugin = settings.ExportPlugins.FirstOrDefault(ep => ep is TestFileExporterPlugin);
            if (testFileExporterPLugin == null)
            {
                testFileExporterPLugin = new TestFileExporterPlugin();
                settings.ExportPlugins.Add(testFileExporterPLugin);
            }

            testFileExporterPLugin.Enabled = true;

            settings.ExportPlugins.FirstOrDefault(ep => ep is VisualScriptingExportPlugin).Enabled = false;


            var exportContext = new ExportContext(settings);

            var path = EditorPrefs.GetString("GLTFTestExportPath", "");
            if (string.IsNullOrEmpty(path))
            {
                path = ShowDestinationFolderDialog();
            }
            
            _testCase.Clear();
            _schemaUsedInCase.Clear();

            if (batchExport)
            {
                List<(ITestCase, string, string)> tests = new List<(ITestCase, string, string)>();
                foreach (var testCase in cases)
                {
                    try
                    {
                        var export = new GLTFSceneExporter(transform, exportContext);

                        currentTestContext = new TestContext(checkBoxPrefab, caseLabelPrefab, transform);
                        currentTestCases = new[] { testCase };
                        var newCase = currentTestContext.NewTestCase(testCase.GetTestName());
                        testCase.PrepareObjects(currentTestContext);
                        _testCase.Add(testCase, newCase);
                        System.IO.Directory.CreateDirectory(
                            System.IO.Path.GetDirectoryName(System.IO.Path.Combine(path, testCase.GetTestName())));

                        var destinationPath = path;
                        var testName = testCase.GetTestName().Replace(" ", "_");
                        if (testName.Contains("/"))
                        {
                             var splits = testName.Split("/"); 
                             for (int i = 0; i < splits.Length ; i++)
                                    destinationPath = System.IO.Path.Combine(destinationPath, splits[i]);
                             
                             testName = splits[splits.Length - 1];

                        }
                        var glbFileName = (testName + ".glb");
                        var jsonFileName = (testName + ".json");
                        
                        var glbPath = System.IO.Path.Combine(destinationPath, "glTF-Binary");
                        export.SaveGLB(glbPath, glbFileName);

                        var jsonPath = Path.Combine(destinationPath, "test-Json");
                        
                        var jsonFullPathFileName = System.IO.Path.Combine(path, System.IO.Path.Combine(jsonPath, jsonFileName));
                        
                        tests.Add((testCase, glbFileName, jsonFileName));

                        CreateTestCaseReadmeFile(testCase, System.IO.Path.Combine(destinationPath, testName + ".md"));
                        CreateTestCaseJsonFile(testCase.GetTestName(), new[] { testCase }, jsonFullPathFileName, glbFileName);
                        currentTestContext.Dispose();
                    }
                    finally
                    {
                        if (testCase is IDisposable disposable)
                            disposable.Dispose();

                        currentTestContext.Dispose();
                    }
                    
                }
                string fullIndexFilePath = Path.Combine(path, indexFileName + ".json");
                CreateIndexJsonFile(fullIndexFilePath, tests);
            }
            else
            {
                try
                {
                    var export = new GLTFSceneExporter(transform, exportContext);

                    currentTestContext = new TestContext(checkBoxPrefab, caseLabelPrefab, transform);
                    currentTestCases = cases;
                    foreach (var testCase in cases)
                    {
                        var newCase = currentTestContext.NewTestCase(testCase.GetTestName());
                        testCase.PrepareObjects(currentTestContext);
                        _testCase.Add(testCase, newCase);

                        currentTestContext.NewRow();
                    }

                    string glbFileName = allInOneName + ".glb";
                    export.SaveGLB(path, glbFileName);
                    // foreach (var testCase in cases)
                    // {
                    //     CreateTestCaseReadmeFile(testCase, System.IO.Path.Combine(path, glbFileName));
                    // }

                    CreateTestCaseJsonFile(allInOneName, cases, System.IO.Path.Combine(path, allInOneName+".json"), glbFileName);
                }
                finally
                {
                    foreach (var testCase in cases.Where( t => t is IDisposable).Cast<IDisposable>())
                        testCase.Dispose();
                    currentTestContext.Dispose();
                }
            }
        }

        public void OnInteractivityExport(GltfInteractivityExportNodes export)
        {
            currentTestContext.interactivityExportContext = export;
            int index = 0;
            foreach (var testCase in currentTestCases)
            {
                currentTestContext.CurrentCaseIndex = index;
                int lastCount = currentTestContext.interactivityExportContext.nodes.Count;
                testCase.CreateNodes(currentTestContext);

                int newCount = currentTestContext.interactivityExportContext.nodes.Count;
                var schemaUsedInCase = new HashSet<string>();
                for (int i = lastCount; i < newCount; i++)
                {
                    var node = currentTestContext.interactivityExportContext.nodes[i];
                    schemaUsedInCase.Add(node.Schema.Op);
                }

                if (_schemaUsedInCase.ContainsKey(testCase))
                {
                    _schemaUsedInCase[testCase].UnionWith(schemaUsedInCase);
                }
                else
                {
                    _schemaUsedInCase.Add(testCase, schemaUsedInCase);
                }

                index++;
            }
        }
        
        [CustomEditor(typeof(TestExporter))]
        public class Inspector : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                var exporter = target as TestExporter;
                
                var path = EditorPrefs.GetString("GLTFTestExportPath", "");
                GUILayout.Label("Export Path: ");
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(path, GUILayout.ExpandWidth(true));
                EditorGUI.EndDisabledGroup();
                
                var btn = GUILayout.Button("Select Exporter Folder", GUILayout.MinWidth(150));
                EditorGUILayout.EndHorizontal();
                if (btn)
                {
                    exporter.ShowDestinationFolderDialog();
                }
          
            }
        }
    }
}