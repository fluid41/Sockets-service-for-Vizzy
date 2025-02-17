using Assets.Scripts.Flight;
using Assets.Scripts.Vizzy.Sockets;
using Assets.Scripts.Vizzy.SocketsService;
using Assets.Scripts.Vizzy.UI;
using HarmonyLib;
using ModApi.Craft.Program;
using ModApi.Flight;
using ModApi.Ui.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Xml.Linq;
using UnityEngine;

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
            ["SentSockets"] = (typeof(SentSocketsInstruction), () => new SentSocketsInstruction())
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

            var harmony = new Harmony("com.yourname.vizzypatch");
            harmony.PatchAll();
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
                Debug.Log("VizzyToolbox rewite start");
                //showMfdCategory = true;

                XNamespace ns = xml.Name.Namespace;
                XElement colorsElement = xml.Element(ns + "Colors");
                if (colorsElement != null)
                {
                    colorsElement.Add(new XElement(ns + "Color",
                        new XAttribute("id", "Test1Color"),
                        new XAttribute("color", "#373737")));
                }
                XElement stylesElement = xml.Element(ns + "Styles");
                if (stylesElement != null)
                {
                    stylesElement.Add(new XElement(ns + "Style",
                        new XAttribute("id", "StartSockets"),
                        new XAttribute("color", "Test1Color"),
                        new XAttribute("format", "start sockets server on port (0)"),
                        new XAttribute("tooltip", "127.0.0.1")));

                    stylesElement.Add(new XElement(ns + "Style",
                        new XAttribute("id", "SentSockets"),
                        new XAttribute("color", "Test1Color"),
                        new XAttribute("format", "send list (0) on port (1)"),
                        new XAttribute("tooltip", "Must be list, otherwise a null value is sent")));
                }

                XElement categoriesElement = xml.Element(ns + "Categories");
                if (categoriesElement != null)
                {
                    XElement eventsCategory = categoriesElement.Elements(ns + "Category")
                        .FirstOrDefault(x => (string)x.Attribute("name") == "Events" && (string)x.Attribute("icon") == "Ui/Vizzy/ToolboxIconEvents");
                    if (eventsCategory != null)
                    {
                        // 创建StartSockets元素并添加子Constant
                        XElement startSockets = new XElement(ns + "StartSockets",
                            new XAttribute("style", "StartSockets"));
                        //startSockets.Add(new XElement(ns + "Constant",
                        //    new XAttribute("text", "172.0.0.1")));
                        startSockets.Add(new XElement(ns + "Constant",
                            new XAttribute("text", "10809")));
                        eventsCategory.Add(startSockets);
                        XElement SentSockets = new XElement(ns + "SentSockets",
                            new XAttribute("style", "SentSockets"));
                        SentSockets.Add(new XElement(ns + "Constant",
                            new XAttribute("text", "data")));
                        SentSockets.Add(new XElement(ns + "Constant",
                            new XAttribute("text", "10809")));
                        eventsCategory.Add(SentSockets);
                    }
                }
            }
        }
    }
}