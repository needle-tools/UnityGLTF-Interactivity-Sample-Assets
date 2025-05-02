using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Khronos_Test_Export;
using Khronos_Test_Export.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGLTF;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.VisualScripting;


// TODO FIX: exptected value "+unendlich"

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
        public string dDescription;
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

    private void CreateTestCaseJsonFile(string name, ITestCase[] testCases, string path,  string glbFileName)
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
            testCaseOutput.dDescription = testCase.GetTestDescription();


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
        jsonOutput.usedSchemas = tests.SelectMany( t => t.usedSchemas).Distinct().OrderBy(s => s).ToArray();

        var json = JsonUtility.ToJson(jsonOutput);

        var jsonPath = System.IO.Path.GetDirectoryName(path);
        System.IO.Directory.CreateDirectory(jsonPath);
        jsonPath = System.IO.Path.Combine(jsonPath, System.IO.Path.GetFileNameWithoutExtension(glbFileName) + ".json");
        System.IO.File.WriteAllText(jsonPath, json);
        Debug.Log("Test case json file created at: " + jsonPath);
    }

    private void CreateTestCaseReadmeFile(ITestCase testCase, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Test Sample: " + testCase.GetTestName());
        sb.AppendLine("Description: " + testCase.GetTestDescription());
        sb.AppendLine("Tests:");
        var caseContext = _testCase[testCase];
        foreach (var test in caseContext.checkBoxes)
        {
            sb.AppendLine(
                $"\t**{test.GetText()}** - Result save in Variable {test.GetResultVariableName()} with Id {test.ResultValueVarId}");
        }

        sb.AppendLine("Schemas used in this test case:");
        foreach (var schema in _schemaUsedInCase[testCase].OrderBy(s => s))
        {
            sb.AppendLine($"\t{schema}");
        }


        var readmePath = path + "/" + testCase.GetTestName() + ".md";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(readmePath));
        System.IO.File.WriteAllText(readmePath, sb.ToString());
        Debug.Log("Test case md file created at: " + readmePath);
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
                  
                    var glbFileName = testCase.GetTestName() + ".glb";

                    var individualPath = System.IO.Path.Combine(path, glbFileName);
                    
                    export.SaveGLB(System.IO.Path.GetDirectoryName(individualPath), System.IO.Path.GetFileName(individualPath));
                    CreateTestCaseReadmeFile(testCase, path);
                    CreateTestCaseJsonFile(testCase.GetTestName(), new[] { testCase }, System.IO.Path.Combine(path, glbFileName), glbFileName);
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
                foreach (var testCase in cases)
                {
                    CreateTestCaseReadmeFile(testCase, path);
                }

                CreateTestCaseJsonFile(allInOneName, cases, System.IO.Path.Combine(path, glbFileName), glbFileName);
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