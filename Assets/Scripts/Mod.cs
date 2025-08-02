using Assets.Scripts.Flight;
using Assets.Scripts.Vizzy.Sockets;
using Assets.Scripts.Vizzy.SocketsService;
using Assets.Scripts.Vizzy.UI;
using HarmonyLib;
using ModApi.Craft.Program;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using System.IO;

namespace Assets.Scripts
{
    //[HarmonyPatch(typeof(FirstPersonCameraController), "SetVantageScript")]
    //public class ModifyTargetPatch
    //{
    //    // 后置补丁，在 SetVantageScript 方法执行后调用
    //    static void Postfix(FirstPersonCameraController __instance, CameraVantageScript cameraVantage)
    //    {
    //        // 仅在配置中要求观察指挥舱时才修改 _target
    //        if (cameraVantage != null && cameraVantage.LookAtCommandPod)
    //        {
    //            // 使用 Harmony 提供的 AccessTools 获取私有字段 _target 的 FieldInfo
    //            var targetField = AccessTools.Field(typeof(FirstPersonCameraController), "_target");
    //            if (targetField != null)
    //            {
    //                // 示例：使用 GameObject.Find 查找场景中名称为 "NewTargetObject" 的对象，并获取其 Transform
    //                Transform newTarget = GameObject.Find("NewTargetObject")?.transform;
    //                if (newTarget != null)
    //                {
    //                    // 修改 _target 字段的值
    //                    targetField.SetValue(__instance, newTarget);
    //                    Debug.Log("Successfully changed _target to the Transform of NewTargetObject");
    //                }
    //                else
    //                {
    //                    Debug.LogWarning("Could not find an object named NewTargetObject");
    //                }
    //            }
    //            else
    //            {
    //                Debug.LogWarning("Could not get information about the _target field");
    //            }
    //        }
    //    }
    //}
    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : ModApi.Mods.GameMod
    {

        static Mod()
        {
            Harmony harmony = new Harmony("Sockets service for Vizzy");
            harmony.PatchAll();
            RegisterCustomNodes();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();

        protected override void OnModInitialized()
        {
            base.OnModInitialized();
        }

        private static readonly Dictionary<string, (Type, Func<ProgramNode>)> ModNodes = new()
        {
            ["StartSockets"] = (typeof(StartSocketsInstruction), () => new StartSocketsInstruction()),
            ["SentSockets"] = (typeof(SentSocketsInstruction), () => new SentSocketsInstruction()),
            ["StopSockets"] = (typeof(StopSocketsInstruction), () => new StopSocketsInstruction())
        };

        // 核心注册方法
        public static void RegisterCustomNodes()
        {
            // 获取 ProgramNodeCreator 类型，使用 AccessTools.Inner 来获取嵌套类型
            var programNodeCreatorType = AccessTools.Inner(typeof(ProgramSerializer), "ProgramNodeCreator");
            if (programNodeCreatorType == null)
            {
                Debug.LogError("Registration failed: Could not find ProgramNodeCreator type");
                return;
            }

            var creatorConstructor = programNodeCreatorType.GetConstructor(new[] { typeof(string), typeof(Type), typeof(Func<ProgramNode>) });
            if (creatorConstructor == null)
            {
                Debug.LogError("Registration failed: Could not find ProgramNodeCreator constructor");
                return;
            }

            // 获取序列化器内部字典
            var typeNameLookup = AccessTools.Field(typeof(ProgramSerializer), "_typeNameLookup")?.GetValue(null) as IDictionary;
            var xmlNameLookup = AccessTools.Field(typeof(ProgramSerializer), "_xmlNameLookup")?.GetValue(null) as IDictionary;

            if (typeNameLookup == null || xmlNameLookup == null)
            {
                Debug.LogError("Registration failed: Could not get internal dictionaries of the serializer");
                return;
            }

            // 动态注册每个节点
            foreach (var (xmlName, (nodeType, constructor)) in ModNodes)
            {
                var creator = creatorConstructor.Invoke(new object[] { xmlName, nodeType, constructor });
                typeNameLookup[nodeType.Name] = creator;
                xmlNameLookup[xmlName] = creator;

                Debug.Log($"Successfully registered custom node: {xmlName}");
            }
        }

        public static void Initialize()
        {
            var userInterface = Game.Instance.UserInterface;
            //userInterface.AddBuildUserInterfaceXmlAction(
            //    UserInterfaceIds.Vizzy,
            //    OnBuildVizzyUI);
        }

        // 在 ExitFlightScene 方法执行后运行的 Harmony 补丁
        [HarmonyPatch(typeof(FlightSceneScript), "ExitFlightScene")]
        public static class ExitFlightScenePatch
        {
            // 后置补丁，在 ExitFlightScene 方法执行后调用
            static void Postfix()
            {

                SocketsServiceManager.CloseAllServers();
                //Debug.Log("The ExitFlightScene method has been executed");
            }
        }

        [HarmonyPatch(typeof(VizzyToolbox))]
        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(XElement), typeof(bool) })]

        public static class VizzyToolboxCtorPatch
        {
            // 前缀方法，在构造函数执行前调用
            static void Prefix(ref XElement xml, ref bool showMfdCategory)
            {
                Debug.Log("VizzyToolbox rewrite start");
                ApplySocketsToolboxConfiguration(xml);
            }
        }

        private static void ApplySocketsToolboxConfiguration(XElement toolboxXml)
        {
            try
            {
                var xmlConfigAsset = Instance.ResourceLoader.LoadAsset<TextAsset>("Assets/Scripts/Vizzy/SocketsServer/SocketsVizzyToolbox.xml");
                if (xmlConfigAsset == null)
                {
                    Debug.LogWarning("SocketsVizzyToolbox.xml not found in mod resources");
                    return;
                }

                var configDocument = XDocument.Parse(xmlConfigAsset.text);
                var configRoot = configDocument.Root;
                XNamespace xmlNamespace = toolboxXml.Name.Namespace;

                // 应用颜色配置
                ApplyColorConfigurations(toolboxXml, configRoot, xmlNamespace);
                
                // 应用样式配置
                ApplyStyleConfigurations(toolboxXml, configRoot, xmlNamespace);
                
                // 应用分类配置
                ApplyCategoryConfigurations(toolboxXml, configRoot, xmlNamespace);

                Debug.Log("SocketsVizzyToolbox configuration applied successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to apply SocketsVizzyToolbox configuration: {ex.Message}");
            }
        }

        private static void ApplyColorConfigurations(XElement toolboxXml, XElement configRoot, XNamespace xmlNamespace)
        {
            var colorsElement = toolboxXml.Element(xmlNamespace + "Colors");
            var colorsConfig = configRoot.Element("Colors");
            
            if (colorsElement != null && colorsConfig != null)
            {
                foreach (var colorDefinition in colorsConfig.Elements("Color"))
                {
                    colorsElement.Add(new XElement(xmlNamespace + "Color",
                        new XAttribute("id", colorDefinition.Attribute("id")?.Value ?? ""),
                        new XAttribute("color", colorDefinition.Attribute("color")?.Value ?? "")));
                }
            }
        }

        private static void ApplyStyleConfigurations(XElement toolboxXml, XElement configRoot, XNamespace xmlNamespace)
        {
            var stylesElement = toolboxXml.Element(xmlNamespace + "Styles");
            var stylesConfig = configRoot.Element("Styles");
            
            if (stylesElement != null && stylesConfig != null)
            {
                foreach (var styleDefinition in stylesConfig.Elements("Style"))
                {
                    stylesElement.Add(new XElement(xmlNamespace + "Style",
                        new XAttribute("id", styleDefinition.Attribute("id")?.Value ?? ""),
                        new XAttribute("color", styleDefinition.Attribute("color")?.Value ?? ""),
                        new XAttribute("format", styleDefinition.Attribute("format")?.Value ?? ""),
                        new XAttribute("tooltip", styleDefinition.Attribute("tooltip")?.Value ?? "")));
                }
            }
        }

        private static void ApplyCategoryConfigurations(XElement toolboxXml, XElement configRoot, XNamespace xmlNamespace)
        {
            var categoriesElement = toolboxXml.Element(xmlNamespace + "Categories");
            var categoryConfig = configRoot.Element("Category");
            
            if (categoriesElement != null && categoryConfig != null)
            {
                var categoryName = categoryConfig.Attribute("name")?.Value ?? "Socket";
                
                var socketCategory = categoriesElement.Elements(xmlNamespace + "Category")
                    .FirstOrDefault(category => (string)category.Attribute("name") == categoryName);
                    
                if (socketCategory == null)
                {
                    socketCategory = new XElement(xmlNamespace + "Category",
                        new XAttribute("name", categoryName),
                        new XAttribute("icon", categoryConfig.Attribute("icon")?.Value ?? ""));
                    categoriesElement.Add(socketCategory);
                }

                // 添加配置中定义的所有元素
                foreach (var elementDefinition in categoryConfig.Elements())
                {
                    var newElement = new XElement(xmlNamespace + elementDefinition.Name.LocalName);
                    
                    // 复制所有属性
                    foreach (var attribute in elementDefinition.Attributes())
                    {
                        newElement.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
                    }

                    // 添加子元素（如Constant）
                    foreach (var childElement in elementDefinition.Elements())
                    {
                        var newChildElement = new XElement(xmlNamespace + childElement.Name.LocalName);
                        foreach (var childAttribute in childElement.Attributes())
                        {
                            newChildElement.SetAttributeValue(childAttribute.Name.LocalName, childAttribute.Value);
                        }
                        newElement.Add(newChildElement);
                    }

                    socketCategory.Add(newElement);
                }
            }
        }
    }
}