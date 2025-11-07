using System;
using System.Collections.Generic;
using GLTF.Schema;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Interactivity;
using UnityGLTF.Interactivity.Export;
using UnityGLTF.Interactivity.Schema;
using Object = UnityEngine.Object;

namespace Khronos_Test_Export
{
    public class PointerSetGetTest : ITestCase, IDisposable
    {
   
        public abstract class PointerTest
        {
            public string Extension = null;
            
            public virtual string TestName { get; }
            
            public abstract IEnumerable<(object value, int gltfType, string template, string label)> subTests { get; }
            
            public virtual Func<GameObject> CustomObjectCreator { get; } = null;
        }

        public class LightPointerTest : PointerTest
        {
            public string template;

            public override string TestName { get => template; }

            public object value;
            public int GltfTypeIndex => GltfTypes.TypeIndex(value.GetType());

            public LightType LightType = LightType.Directional;
            public override Func<GameObject> CustomObjectCreator => GetGameObject;
            private GameObject GetGameObject() 
            {
                var go = new GameObject("LightPointerTest"+Guid.NewGuid());
                var light = go.AddComponent<Light>();
                light.type = LightType;
                return go;
            }

            public override IEnumerable<(object value, int gltfType, string template, string label)> subTests 
            {
                get
                {
                    yield return new() { gltfType = GltfTypeIndex, value = value, template = template, label = template};
                }
            }      
        }
        
        public class SinglePointerTest : PointerTest
        {
            public string template;
            
            public override string TestName { get => template; }
            
            public object value;
            public int GltfTypeIndex => GltfTypes.TypeIndex(value.GetType());

            public override IEnumerable<(object value, int gltfType, string template, string label)> subTests 
            {
                get
                {
                    yield return new() { gltfType = GltfTypeIndex, value = value, template = template, label = template};
                }
            }
        }

        public class MaterialProperty
        {
            public string name;
            public object value;

            public MaterialProperty(string name, object value)
            {
                this.name = name;
                this.value = value;
            }
        }

        public class PbrMaterialPointerTest : MaterialPointerTest
        {
            public override string materialTemplate => "/materials/{"+PointersHelper.IdPointerMaterialIndex+"}/pbrMetallicRoughness/";
        }   
        
        public class MaterialPointerTest : PointerTest
        {
            public virtual string materialTemplate =>
                "/materials/{"+PointersHelper.IdPointerMaterialIndex+"}/" 
                + (string.IsNullOrEmpty(Extension)
                    ? ""
                    : $"extensions/{Extension}/");
            
            public MaterialProperty[] materialProperties;
            public string[] textures;
            public override string TestName { get => materialTemplate; }

            public override IEnumerable<(object value, int gltfType, string template, string label)> subTests
            {
                get
                {
                    if (materialProperties != null)
                        foreach (var m in materialProperties)
                            yield return new()
                            {
                                value = m.value, gltfType = GltfTypes.TypeIndex(m.value.GetType()),
                                template = materialTemplate + m.name,
                                label = Extension + "/" + m.name
                            };

                    if (textures != null)
                        foreach (var t in textures)
                        {
                            yield return new()
                            {
                                gltfType = GltfTypes.TypeIndex(typeof(Vector2)),
                                template = materialTemplate + t + "/extensions/KHR_texture_transform/offset",
                                value = new Vector2(2f, 3f),
                                label = Extension + "/" + t + " texture offset"
                            };
                            yield return new()
                            {
                                gltfType = GltfTypes.TypeIndex(typeof(float)),
                                template = materialTemplate + t + "/extensions/KHR_texture_transform/rotation",
                                value = 45f,
                                label = Extension + "/" + t + " texture rotation"
                            };
                            yield return new()
                            {
                                gltfType = GltfTypes.TypeIndex(typeof(Vector2)),
                                template = materialTemplate + t + "/extensions/KHR_texture_transform/scale",
                                value = new Vector2(2f, 3f),
                                label = Extension + "/" + t + " texture scale"

                            };
                        }
                            
                }
            }
        }
        /* TODO pointers:
        
        /nodes/{}/extensions/EXT_lights_ies/multiplier
        /nodes/{}/extensions/EXT_lights_ies/color
        /extensions/EXT_lights_ies/lights.length

        /extensions/EXT_lights_image_based/lights/{}/rotation
        /extensions/EXT_lights_image_based/lights/{}/intensity
        /extensions/EXT_lights_image_based/lights.lengt
        
        /materials/{}/extensions/ADOBE_materials_clearcoat_specular/clearcoatIor
        /materials/{}/extensions/ADOBE_materials_clearcoat_specular/clearcoatSpecularFactor
        /materials/{}/extensions/ADOBE_materials_clearcoat_specular/clearcoatSpecularTexture
        /materials/{}/extensions/ADOBE_materials_clearcoat_tint/clearcoatTintFacto
        /materials/{}/extensions/ADOBE_materials_clearcoat_tint/clearcoatTintTexture
         *
         * 
         */

        // Color without Alpha channel
        private static Vector3 ColorRGB(Color col)
        {
            return new Vector3(col.r, col.g, col.b);
        }

        private PointerTest[] tests = new PointerTest[]
        {
            new LightPointerTest()
            {
                template = "/extensions/KHR_lights_punctual/lights/{"+PointersHelper.IdPointerLightIndex+"}/color",
                value = ColorRGB(Color.red),
                LightType = LightType.Spot
            },
            new LightPointerTest()
            {
                template = "/extensions/KHR_lights_punctual/lights/{"+PointersHelper.IdPointerLightIndex+"}/intensity",
                value = 4f
            },
            new LightPointerTest()
            {
                template = "/extensions/KHR_lights_punctual/lights/{"+PointersHelper.IdPointerLightIndex+"}/range",
                value = 9f
            },
            new LightPointerTest()
            {
                template = "/extensions/KHR_lights_punctual/lights/{"+PointersHelper.IdPointerLightIndex+"}/spot/innerConeAngle",
                value = 2f,
                LightType = LightType.Spot
            },
            new LightPointerTest()
            {
                template = "/extensions/KHR_lights_punctual/lights/{"+PointersHelper.IdPointerLightIndex+"}/spot/outerConeAngle",
                value = 5f,
                LightType = LightType.Spot
            },
  

            new MaterialPointerTest()
            {
                materialProperties = new[]
                    {
                        new MaterialProperty("alphaCutoff", 0.5f),
                        new MaterialProperty("emissiveFactor", ColorRGB(Color.red)),
                        new MaterialProperty("normalTexture/scale", 0.5f),
                        new MaterialProperty("occlusionTexture/strength", 0.5f),
                    },
                textures = new[] { "normalTexture", "occlusionTexture", "emissiveTexture" }
            },
            new PbrMaterialPointerTest()
            {
                Extension = "pbrMetallicRoughness",
                materialProperties = new[]
                    {
                        new MaterialProperty("baseColorFactor", Color.blue),
                        new MaterialProperty("metallicFactor", 0.5f),
                        new MaterialProperty("roughnessFactor", 0.5f),
                        
                    },
                textures = new[] { "baseColorTexture", "metallicRoughnessTexture" }
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_anisotropy",
                materialProperties = new[]
                    {
                        new MaterialProperty("anisotropyStrength", 2f),
                        new MaterialProperty("anisotropyRotation", 30f),
                    },
                textures = new[]{"anisotropyTexture"}
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_clearcoat",
                materialProperties = new MaterialProperty[]
                {
                },
                textures = new[]{"clearcoatTexture", "clearcoatRoughnessTexture"}
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_dispersion",
                materialProperties = new[]
                {
                    new MaterialProperty("dispersion", 2f),
                },
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_emissive_strength",
                materialProperties = new[]
                {
                    new MaterialProperty("emissiveStrength", 2f),
                },
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_ior",
                materialProperties = new[]
                {
                    new MaterialProperty("ior", 3f),
                },
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_iridescence",
                materialProperties = new[]
                {
                    new MaterialProperty("iridescenceFactor", 1.2f),
                    new MaterialProperty("iridescenceIor", 2.3f),
                    new MaterialProperty("iridescenceThicknessMinimum", 0.5f),
                    new MaterialProperty("iridescenceThicknessMaximum", 1.2f),
                },
                textures = new[]{"iridescenceTexture", "iridescenceThicknessTexture" }
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_sheen",
                materialProperties = new[]
                {
                    new MaterialProperty("sheenColorFactor",  ColorRGB(Color.blue)),
                    new MaterialProperty("sheenRoughnessFactor", 2.3f),
                },
                textures = new[]{"sheenColorTexture", "sheenRoughnessTexture" }
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_specular",
                materialProperties = new[]
                {
                    new MaterialProperty("specularFactor", 1.2f),
                    new MaterialProperty("specularColorFactor", ColorRGB(Color.red)),
                },
                textures = new[]{"specularTexture", "specularColorTexture" }
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_transmission",
                materialProperties = new[]
                {
                    new MaterialProperty("transmissionFactor", 1.2f),
                },
                textures = new[]{"transmissionTexture"}
            },
            new MaterialPointerTest()
            {
                Extension = "KHR_materials_volume",
                materialProperties = new[]
                {
                    new MaterialProperty("thicknessFactor", 1.2f),
                    new MaterialProperty("attenuationDistance", 2.2f),
                    new MaterialProperty("attenuationColor", ColorRGB(Color.red)),
                },
                textures = new[]{"thicknessTexture"}
            },           
        };
        

        private List<(PointerTest test, CheckBox[] checkBoxes)> testCheckboxes = new();
        private Dictionary<PointerTest, Material> testMaterials = new();
        private Dictionary<PointerTest, Light> testLights = new();
        private Material material;
        private List<GameObject> dummyObjects = new List<GameObject>();
        
        public string GetTestName()
        {
            return "pointer/set and get";
        }

        public string GetTestDescription()
        {
            return "";
        }

        private void CreateMaterialForTest(TestContext context, PointerTest test)
        {
            var shader = Shader.Find("UnityGLTF/PBRGraph");
            material = new Material(shader);
            material.name = "PointerTestMaterial-"+test.TestName;
            material.EnableKeyword("_TEXTURE_TRANSFORM_ON");
            var kw = material.shader.keywordSpace.keywords;
            foreach (var k in kw)
            {
                if (k.name == "INSTANCING_ON") continue;
                
                material.EnableKeyword(k);
            }
            
            // TODO: only enable keywords that are used in the test
            
            material.SetFloat("_ANISOTROPY", 1f);
            material.SetFloat("_VOLUME_ON", 1f);
            
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummyObjects.Add(cube);
            cube.name = $"pointer-tests [{testMaterials.Count}]";
            cube.transform.SetParent(context.Root);
            cube.transform.localPosition = new Vector3(1, 1, 1);
            cube.transform.localScale = Vector3.zero;
            
            var rend =cube.GetComponent<MeshRenderer>();
            rend.sharedMaterial = material;        
            
            testMaterials.Add(test, material);
        }

        public void PrepareObjects(TestContext context)
        {
            testCheckboxes.Clear();
            testLights.Clear();
            testMaterials.Clear();
            for (int i = 0; i < tests.Length; i++)
            {
                var test = tests[i];
                var boxes = new List<CheckBox>();
                foreach (var sub in test.subTests)
                {
                    var formattedLabel = sub.label;
                    // Remove all {..} from label
                    while (formattedLabel.Contains("{"))
                    {
                        int startIndex = formattedLabel.IndexOf("{");
                        int endIndex = formattedLabel.IndexOf("}", startIndex);
                        if (endIndex > startIndex)
                        {
                            formattedLabel = formattedLabel.Remove(startIndex, endIndex - startIndex + 1);
                            formattedLabel = formattedLabel.Insert(startIndex, "[]");
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    var newCheckBox = context.AddCheckBox(formattedLabel);
                    boxes.Add(newCheckBox);
                }
                testCheckboxes.Add(new () {checkBoxes = boxes.ToArray(), test = test});
                if (i < tests.Length - 1)
                    context.NewRow();

                if (test.CustomObjectCreator != null)
                {
                    var newGo = test.CustomObjectCreator();
                    newGo.transform.SetParent(context.Root);
                    if (newGo.GetComponentInChildren<Light>())
                        testLights.Add(test, newGo.GetComponentInChildren<Light>());
                    dummyObjects.Add(newGo);
                }
                
                if (test is MaterialPointerTest mTest)
                {
                    CreateMaterialForTest(context, test);
                    if (mTest.textures != null)
                    {
                        foreach (var tex in mTest.textures)
                        {
                            if (material.HasTexture(tex))
                                material.SetTexture(tex, Texture2D.redTexture);
                        }
                    }
                }
            }
        }

        private void AddMaterialExtension(GLTFSceneExporter exporter, int materialIndex, string extensionName)
        {
            if (string.IsNullOrEmpty(extensionName))
                return;
       
            if (exporter.GetRoot().Materials[materialIndex].Extensions == null)
                exporter.GetRoot().Materials[materialIndex].Extensions = new Dictionary<string, IExtension>();

            exporter.DeclareExtensionUsage(extensionName);

            var ext = exporter.GetRoot().Materials[materialIndex].Extensions;
            if (ext.ContainsKey(extensionName))
                return;
            
            switch (extensionName)
            {
                case KHR_materials_specular_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_specular());
                    break;
                case KHR_materials_anisotropy_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_anisotropy());
                    break;
                case KHR_materials_clearcoat_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_clearcoat());
                    break;
                case KHR_materials_dispersion_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_dispersion());
                    break;
                case KHR_materials_emissive_strength_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_emissive_strength());
                    break;
                case KHR_materials_ior_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_ior());
                    break;
                case KHR_materials_iridescence_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_iridescence());
                    break;
                case KHR_materials_sheen_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_sheen());
                    break;
                case KHR_materials_volume_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_volume());
                    break;
                case KHR_materials_transmission_Factory.EXTENSION_NAME:
                    ext.Add(extensionName, new KHR_materials_transmission());
                    break;
                case "pbrMetallicRoughness":
                    break;
                // case KHR_materials_pbrSpecularGlossinessExtensionFactory.EXTENSION_NAME:
                //     ext.Add(extensionName, new KHR_materials_pbrSpecularGlossinessExtension(
                //         ));
                //     break;
                default:
                    Debug.LogWarning("Unknown material extension: " + extensionName);
                    break;
            }
            
        }

        public void CreateNodes(TestContext context)
        {
            var exporter = context.interactivityExportContext.Context.exporter;
            exporter.DeclareExtensionUsage(ExtTextureTransformExtensionFactory.EXTENSION_NAME);
            
            int materialIndex = -1;
            int lightIndex = -1;
            foreach (var check in testCheckboxes)
            {
                lightIndex = -1;
                materialIndex = -1;
                if (check.test is MaterialPointerTest matTest)
                {
                    if (!testMaterials.ContainsKey(check.test))
                    {
                        Debug.LogWarning("Material for test " + check.test + " not found.");
                        continue;
                    }
                    materialIndex = context.interactivityExportContext.Context.exporter.GetMaterialIndex(testMaterials[check.test]);
                    AddMaterialExtension(exporter, materialIndex, check.test.Extension);
                }
                
                if (testLights.TryGetValue(check.test, out var l))
                    lightIndex = context.interactivityExportContext.Context.exporter.GetLightIndex(l);
                
                if (!string.IsNullOrEmpty(check.test.Extension))
                {
                    exporter.DeclareExtensionUsage(check.test.Extension);
                }
                
                int subIndex = 0;
                foreach (var sub in check.test.subTests)
                {
                    context.NewEntryPoint(check.checkBoxes[subIndex].GetText());

                    var pSet = context.interactivityExportContext.CreateNode<Pointer_SetNode>();
                    PointersHelper.AddPointerConfig(pSet, sub.template, sub.gltfType);
                    context.AddToCurrentEntrySequence(pSet.FlowIn());
                    pSet.ValueIn(Pointer_SetNode.IdValue).SetValue(sub.value);

                    var pGet = context.interactivityExportContext.CreateNode<Pointer_GetNode>();
                    PointersHelper.AddPointerConfig(pGet, sub.template, sub.gltfType);

                    var pointerString = sub.template;
                    if (sub.template.Contains(PointersHelper.IdPointerMaterialIndex))
                    {
                        pSet.ValueIn(PointersHelper.IdPointerMaterialIndex).SetValue(materialIndex);
                        pGet.ValueIn(PointersHelper.IdPointerMaterialIndex).SetValue(materialIndex);
                        var gltfMaterial = context.interactivityExportContext.Context.exporter.GetRoot().Materials[materialIndex];
                        gltfMaterial.AlphaMode = AlphaMode.MASK;
                        gltfMaterial.AlphaCutoff = 1f;
                        pointerString = pointerString.Replace("{"+PointersHelper.IdPointerMaterialIndex+"}", materialIndex.ToString());
                    }
                    
                    if (sub.template.Contains(PointersHelper.IdPointerLightIndex))
                    {
                        pSet.ValueIn(PointersHelper.IdPointerLightIndex).SetValue(lightIndex);
                        pGet.ValueIn(PointersHelper.IdPointerLightIndex).SetValue(lightIndex);
                        pointerString = pointerString.Replace("{"+PointersHelper.IdPointerMaterialIndex+"}", lightIndex.ToString());
                    }
                    
                    context.AddLog("ERROR! Flow-[err] on Set pointer: " + pointerString + " with " + sub.value+ " can't be set.", out var logErrFlowIn, out _);
                    
                    pSet.FlowOut(Pointer_SetNode.IdFlowOutError).ConnectToFlowDestination(logErrFlowIn);
                    
                    check.checkBoxes[subIndex].SetupCheck(pGet.FirstValueOut(), pSet.FlowOut(), sub.value);

                    subIndex++;
                }
            }
        }

        public void Dispose()
        {
            testCheckboxes.Clear();
            testMaterials.Clear();
            foreach (var d in dummyObjects)
                Object.DestroyImmediate(d);
            dummyObjects.Clear();
        }
    }
}