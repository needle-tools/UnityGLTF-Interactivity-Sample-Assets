using System;
using System.Collections.Generic;
using System.Linq;
using GLTF.Schema;
using Khronos_Test_Export.Core;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class TestCreator : MonoBehaviour
{
    [Serializable]
    public class TestCaseEntry
    {
        public bool Enabled = true;
        public string Name = "TestCase";
        public string Description = "TestCase Description";

        [HideInInspector]
        public string typeFullName;

        public Type type
        {
            get
            {
                if (string.IsNullOrEmpty(typeFullName))
                {
                    return null;
                }
                
                var type = Type.GetType(typeFullName);
                if (type == null)
                {
                    Debug.LogError($"Type {typeFullName} not found.");
                    return null;
                }
                
                return type;
            }
        }

        public ITestCase instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = (ITestCase)System.Activator.CreateInstance(type);
                }
                return _instance;
            }
        }
        
        private ITestCase _instance;
        
    }
    
    [SerializeField] protected TestExporter testExporter;
    [SerializeField] protected TestCaseEntry[] testCases;
    
    public string testName = "TestName";
    
    public class IgnoreTestCaseAttribute : Attribute
    {
        public IgnoreTestCaseAttribute()
        {
        }
    }
    
    [ContextMenu("Generate Test List")]
    protected virtual void GenerateTestList()
    {
        // Find all classes with ITestCase interface
        var testCases = new List<TestCaseEntry>(this.testCases ?? new TestCaseEntry[0]);
        var testCaseTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where( t => !t.HasAttribute(typeof(IgnoreTestCaseAttribute)));
       
        testCases.Where( tc => !testCaseTypes.Contains(tc.type)).ToList().ForEach(tc => testCases.Remove(tc));
        
        foreach (var type in testCaseTypes)
        {
            if (type.IsClass && !type.IsAbstract && typeof(ITestCase).IsAssignableFrom(type))
            {
                
                var testCase = (ITestCase)System.Activator.CreateInstance(type);
                if (testCases.Exists(tc => tc.typeFullName == type.FullName))
                {
                    var existing = testCases.FirstOrDefault(tc => tc.typeFullName == type.FullName);
                    existing.Name = testCase.GetTestName();
                    existing.Description = testCase.GetTestDescription();
                    continue;
                }
                testCases.Add(new TestCaseEntry()
                {
                    Name = testCase.GetTestName(),
                    Description = testCase.GetTestDescription(),
                    typeFullName = testCase.GetType().FullName,
                });
            }
        }
        
        // Sort test cases by name
        testCases.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));
        this.testCases = testCases.ToArray();
    }

    protected virtual ITestCase[] GetTests()
    {
        return testCases.Where(tc => tc.Enabled).Select(tc => tc.instance).ToArray();
        
    }
    
    public virtual void ExportTests(bool exportAllInOne = true, bool exportIndividual = true)
    {
        if (testExporter == null)
        {
            Debug.LogError("Test Exporter is not set.");
            return;
        }

        var cases = GetTests();
        
        testExporter.ShowDestinationFolderDialog();
        
        if (exportAllInOne)
        {
            testExporter.ExportTest(cases, false, testName);
        }
        
        if (exportIndividual)
        {
            testExporter.ExportTest(cases, true);
        }
    }
    
    [CustomEditor(typeof(TestCreator))]
    public class Inspector : UnityEditor.Editor
    {
        public bool exportAllInOne = true;
        public bool exportIndividual = true;
        
        public void OnEnable()
        {
            ((TestCreator)target).GenerateTestList();
        }

        public override void OnInspectorGUI()
        {
            var testCreator = (TestCreator)target;

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TestCreator.testExporter)));

    
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Test Cases", EditorStyles.boldLabel);
            foreach (var testCase in testCreator.testCases)
            {
                testCase.Enabled = GUILayout.Toggle(testCase.Enabled, $"{testCase.Name} ({testCase.Description})");
            }
            GUILayout.EndVertical();
            
            exportIndividual =  GUILayout.Toggle(exportIndividual, "Export Individual Tests");
            exportAllInOne = GUILayout.Toggle(exportAllInOne, "Export All In One");
            if (exportAllInOne)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TestCreator.testName)));
                EditorGUI.indentLevel--;
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
            
            if (GUILayout.Button("Export Tests"))
            {
                ((TestCreator)target).ExportTests(exportAllInOne, exportIndividual);
            }
        }
    }
}
