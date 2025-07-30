using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using ModApi.Craft.Program;

namespace Assets.Scripts.Editor
{
    public class MethodWhitelistGenerator
    {
        [MenuItem("SimpleRockets 2/Generate Method Whitelist XML")]
        public static void GenerateMethodWhitelist()
        {
            try
            {
                Debug.Log("Starting method whitelist generation...");
                
                var xmlDoc = new XDocument();
                var root = new XElement("MethodWhitelist",
                    new XAttribute("GeneratedTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new XAttribute("Version", "1.0"));
                
                // 扫描 IThreadContext 相关的方法
                ScanIThreadContextMethods(root);
                
                xmlDoc.Add(root);
                
                // 保存到指定路径
                string outputPath = Path.Combine(Application.dataPath, "Scripts", "Vizzy", "MethodWhitelist.xml");
                string directory = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                xmlDoc.Save(outputPath);
                
                Debug.Log($"Method whitelist generated successfully at: {outputPath}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to generate method whitelist: {ex.Message}");
            }
        }
        
        private static void ScanIThreadContextMethods(XElement root)
        {
            // 获取 IThreadContext 类型
            Type threadContextType = typeof(IThreadContext);
            if (threadContextType == null)
            {
                Debug.LogWarning("IThreadContext type not found");
                return;
            }
            
            var namespaceGroup = new XElement("Namespace", new XAttribute("name", "ModApi.Craft.Program"));
            
            // 扫描 IThreadContext 本身的方法
            var threadContextClass = new XElement("Class", new XAttribute("name", "IThreadContext"));
            var methods = threadContextType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && !IsSystemMethod(m))
                .OrderBy(m => m.Name);
            
            foreach (var method in methods)
            {
                var methodElement = CreateMethodElement(method, "context");
                threadContextClass.Add(methodElement);
            }
            
            namespaceGroup.Add(threadContextClass);
            
            // 扫描 IThreadContext 的属性和子对象
            ScanThreadContextProperties(namespaceGroup, threadContextType);
            
            // 扫描 context.Craft 相关方法
            ScanCraftMethods(namespaceGroup);
            
            root.Add(namespaceGroup);
        }
        
        private static void ScanThreadContextProperties(XElement namespaceGroup, Type threadContextType)
        {
            try
            {
                // 获取 IThreadContext 的所有属性
                var properties = threadContextType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                
                foreach (var property in properties)
                {
                    if (property.PropertyType.IsInterface || property.PropertyType.IsClass)
                    {
                        // 扫描属性类型的方法，比如 context.Craft
                        ScanPropertyMethods(namespaceGroup, property);
                    }
                }
                
                // 手动添加一些已知的链式调用
                AddKnownChainedMethods(namespaceGroup);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error scanning thread context properties: {ex.Message}");
            }
        }
        
        private static void ScanPropertyMethods(XElement namespaceGroup, PropertyInfo property)
        {
            try
            {
                Type propertyType = property.PropertyType;
                string propertyPath = $"context.{property.Name}";
                
                var classGroup = new XElement("Class", 
                    new XAttribute("name", propertyType.Name),
                    new XAttribute("accessPath", propertyPath));
                
                var methods = propertyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && !IsSystemMethod(m))
                    .OrderBy(m => m.Name);
                
                foreach (var method in methods)
                {
                    var methodElement = CreateMethodElement(method, propertyPath);
                    classGroup.Add(methodElement);
                }
                
                // 递归扫描属性的属性（比如 context.Craft.ExecutingPart）
                var subProperties = propertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var subProperty in subProperties)
                {
                    if (subProperty.PropertyType.IsInterface || subProperty.PropertyType.IsClass)
                    {
                        ScanSubPropertyMethods(classGroup, subProperty, $"{propertyPath}.{subProperty.Name}");
                    }
                }
                
                if (classGroup.HasElements)
                {
                    namespaceGroup.Add(classGroup);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error scanning property {property.Name}: {ex.Message}");
            }
        }
        
        private static void ScanSubPropertyMethods(XElement parentClass, PropertyInfo property, string fullPath)
        {
            try
            {
                Type propertyType = property.PropertyType;
                
                var subClassGroup = new XElement("SubClass", 
                    new XAttribute("name", propertyType.Name),
                    new XAttribute("accessPath", fullPath));
                
                var methods = propertyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName && !IsSystemMethod(m))
                    .OrderBy(m => m.Name);
                
                foreach (var method in methods)
                {
                    var methodElement = CreateMethodElement(method, fullPath);
                    subClassGroup.Add(methodElement);
                }
                
                if (subClassGroup.HasElements)
                {
                    parentClass.Add(subClassGroup);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error scanning sub-property {property.Name}: {ex.Message}");
            }
        }
        
        private static void AddKnownChainedMethods(XElement namespaceGroup)
        {
            // 添加一些已知的链式调用模式
            var knownChains = new[]
            {
                "context.GetOrCreateGlobalVariable().Value.Set",
                "context.GetOrCreateGlobalVariable().Value.Get",
                "context.Craft.BroadcastMessage",
                "context.Craft.ExecutingPart.Activated",
                "context.Craft.ExecutingPart.IsDestroyed",
                "context.Log.LogError",
                "context.Log.LogWarning",
                "context.Log.LogInfo"
            };
            
            var chainedGroup = new XElement("Class", 
                new XAttribute("name", "KnownChainedMethods"),
                new XAttribute("description", "Common chained method calls"));
            
            foreach (var chain in knownChains)
            {
                var chainElement = new XElement("Method",
                    new XAttribute("name", chain),
                    new XAttribute("enabled", "false"),
                    new XAttribute("category", "Chained"),
                    new XAttribute("description", $"Chained method call: {chain}"),
                    new XAttribute("fullPath", chain)
                );
                chainedGroup.Add(chainElement);
            }
            
            namespaceGroup.Add(chainedGroup);
        }
        
        private static void ScanCraftMethods(XElement namespaceGroup)
        {
            try
            {
                // 尝试获取 Craft 相关的类型
                var craftTypes = new[]
                {
                    "ModApi.Craft.Program.Craft.ICraftScript",
                    "ModApi.Craft.Program.Craft.ICraft",
                    "ModApi.Craft.Program.Craft.IProgrammablePart"
                };
                
                foreach (var typeName in craftTypes)
                {
                    Type type = Type.GetType(typeName);
                    if (type == null)
                    {
                        // 尝试从已加载的程序集中查找
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            type = assembly.GetType(typeName);
                            if (type != null) break;
                        }
                    }
                    
                    if (type != null)
                    {
                        var classGroup = new XElement("Class", new XAttribute("name", type.Name));
                        
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .Where(m => !m.IsSpecialName && !IsSystemMethod(m))
                            .OrderBy(m => m.Name);
                        
                        foreach (var method in methods)
                        {
                            var methodElement = CreateMethodElement(method, $"context.Craft.{type.Name}");
                            classGroup.Add(methodElement);
                        }
                        
                        if (classGroup.HasElements)
                        {
                            namespaceGroup.Add(classGroup);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error scanning craft methods: {ex.Message}");
            }
        }
        
        private static XElement CreateMethodElement(MethodInfo method, string accessPath = "")
        {
            var methodElement = new XElement("Method",
                new XAttribute("name", method.Name),
                new XAttribute("enabled", "false"), // 默认禁用
                new XAttribute("returnType", GetFriendlyTypeName(method.ReturnType))
            );
            
            // 添加访问路径信息
            if (!string.IsNullOrEmpty(accessPath))
            {
                methodElement.Add(new XAttribute("accessPath", $"{accessPath}.{method.Name}"));
            }
            
            // 添加参数信息
            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                var parametersElement = new XElement("Parameters");
                foreach (var param in parameters)
                {
                    var paramElement = new XElement("Parameter",
                        new XAttribute("name", param.Name),
                        new XAttribute("type", GetFriendlyTypeName(param.ParameterType)),
                        new XAttribute("isOptional", param.IsOptional)
                    );
                    
                    if (param.HasDefaultValue)
                    {
                        paramElement.Add(new XAttribute("defaultValue", param.DefaultValue?.ToString() ?? "null"));
                    }
                    
                    parametersElement.Add(paramElement);
                }
                methodElement.Add(parametersElement);
            }
            
            // 添加分类信息
            string category = CategorizeMethod(method.Name);
            methodElement.Add(new XAttribute("category", category));
            
            // 添加描述
            methodElement.Add(new XAttribute("description", GenerateMethodDescription(method)));
            
            return methodElement;
        }
        
        private static string CategorizeMethod(string methodName)
        {
            if (methodName.StartsWith("Get") || methodName.StartsWith("Is") || methodName.StartsWith("Has"))
                return "Read";
            else if (methodName.StartsWith("Set") || methodName.StartsWith("Update") || methodName.StartsWith("Modify"))
                return "Write";
            else if (methodName.StartsWith("Create") || methodName.StartsWith("Add") || methodName.StartsWith("New"))
                return "Create";
            else if (methodName.StartsWith("Delete") || methodName.StartsWith("Remove") || methodName.StartsWith("Destroy"))
                return "Delete";
            else if (methodName.StartsWith("Start") || methodName.StartsWith("Stop") || methodName.StartsWith("Enable") || methodName.StartsWith("Disable"))
                return "Control";
            else
                return "Other";
        }
        
        private static string GenerateMethodDescription(MethodInfo method)
        {
            var sb = new StringBuilder();
            sb.Append($"{GetFriendlyTypeName(method.ReturnType)} {method.Name}(");
            
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{GetFriendlyTypeName(parameters[i].ParameterType)} {parameters[i].Name}");
            }
            
            sb.Append(")");
            return sb.ToString();
        }
        
        private static string GetFriendlyTypeName(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            
            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();
                
                if (genericTypeDef == typeof(List<>))
                    return $"List<{GetFriendlyTypeName(genericArgs[0])}>";
                if (genericTypeDef == typeof(IEnumerable<>))
                    return $"IEnumerable<{GetFriendlyTypeName(genericArgs[0])}>";
                if (genericTypeDef == typeof(IReadOnlyList<>))
                    return $"IReadOnlyList<{GetFriendlyTypeName(genericArgs[0])}>";
            }
            
            return type.Name;
        }
        
        private static bool IsSystemMethod(MethodInfo method)
        {
            // 排除系统方法
            var systemMethods = new[] 
            { 
                "GetHashCode", "Equals", "ToString", "GetType",
                "Finalize", "MemberwiseClone"
            };
            
            return systemMethods.Contains(method.Name) || 
                   method.DeclaringType == typeof(object) ||
                   method.Name.StartsWith("get_") || 
                   method.Name.StartsWith("set_") ||
                   method.Name.StartsWith("add_") ||
                   method.Name.StartsWith("remove_");
        }
    }
}
