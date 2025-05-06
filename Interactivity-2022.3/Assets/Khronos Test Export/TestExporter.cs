using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.VisualScripting;


// TODO FIX: exptected value "+unendlich"

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
            public class EntryPoint
            {
                public string name;
                public int nodeId;
            }

            public EntryPoint[] entryPoints;

            [Serializable]
            public class SubTests
            {
                public string name;
                public string resultVarName;
                public int resultVarId;
                public string expectedResultValue;
            }

            public SubTests[] subTests;
        }

        private void CreateTestCaseJsonFile(string name, ITestCase[] testCases, string fileName, string glbFileName)
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
                    entryPoint.name = entry.caseName;
                    entryPoint.nodeId = entry.node.Index;
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
                    subTest.expectedResultValue = check.expectedValue.ToString();
                    subTests.Add(subTest);
                }

                testCaseOutput.subTests = subTests.ToArray();
            }

            jsonOutput.tests = tests.ToArray();
            jsonOutput.usedSchemas = tests.SelectMany(t => t.usedSchemas).Distinct().OrderBy(s => s).ToArray();

            var json = JsonUtility.ToJson(jsonOutput);

              System.IO.File.WriteAllText(fileName, json);
            Debug.Log("Test case json file created at: " + fileName);
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


            System.IO.File.WriteAllText(filename, sb.ToString());
            Debug.Log("Test case md file created at: " + filename);
        }

        public void ShowDestinationFolderDialog()
        {
            var path = EditorPrefs.GetString("GLTFTestExportPath", "");
            path = UnityEditor.EditorUtility.SaveFolderPanel("Destination Folder", path, "");
            EditorPrefs.SetString("GLTFTestExportPath", path);
        }

        public void ExportTest(ITestCase[] cases, bool batchExport = false, string allInOneName = null)
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
                path = UnityEditor.EditorUtility.SaveFolderPanel("Destination Folder", path, "");
            EditorPrefs.SetString("GLTFTestExportPath", path);

            _testCase.Clear();
            _schemaUsedInCase.Clear();

            if (batchExport)
            {
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
                        var testName = testCase.GetTestName();
                        if (testName.Contains("/"))
                        {
                             var splits = testName.Split("/"); 
                             for (int i = 0; i < splits.Length ; i++)
                                    destinationPath = System.IO.Path.Combine(destinationPath, splits[i]);
                             
                             testName = splits[splits.Length - 1];

                        }
                        var glbFileName = testName + ".glb";
                        
                        var individualPath = System.IO.Path.Combine(path, glbFileName);

                        export.SaveGLB(destinationPath, glbFileName);
                            
                        CreateTestCaseReadmeFile(testCase, System.IO.Path.Combine(destinationPath, testName + ".md"));
                        CreateTestCaseJsonFile(testCase.GetTestName(), new[] { testCase },
                            System.IO.Path.Combine(path, System.IO.Path.Combine(destinationPath, testName+".json")), glbFileName);
                        currentTestContext.Dispose();
                    }
                    finally
                    {
                        currentTestContext.Dispose();
                    }
                }
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
                    currentTestContext.Dispose();
                }
            }
        }

        public void OnInteractivityExport(GltfInteractivityExportNodes export)
        {
            currentTestContext.interactivityExportContext = export;
            foreach (var testCase in currentTestCases)
            {
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
            }
        }
    }
}