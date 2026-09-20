using RedBird.Core.Memory.Managed;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SHCDESE.Lua.DocsGen;

/// <summary>
/// Class to generate a quick Lua API reference from attributes
/// and DocFX summaries.
/// </summary>
public static class LuaApiDocGenerator
{
    private static readonly Dictionary<Type, string> TypeAliases = new()
    {
        { typeof(void), "void" },
        { typeof(byte), "byte" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(ushort), "ushort" },
        { typeof(int), "int" },
        { typeof(uint), "uint" },
        { typeof(long), "long" },
        { typeof(ulong), "ulong" },
        { typeof(float), "float" },
        { typeof(double), "double" },
        { typeof(decimal), "decimal" },
        { typeof(bool), "bool" },
        { typeof(string), "string" },
        { typeof(object), "object" }
    };

    [Conditional("DEBUG")]
    public static void Generate(string? outputPath = null)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string assemblyFileName = Path.GetFileNameWithoutExtension(assembly.Location);
        string xmlDocPath = Path.Combine(Path.GetDirectoryName(assembly.Location), assemblyFileName + ".xml");
        if (!File.Exists(xmlDocPath))
        {
            LogHelper.Error($"XML Documentation not found at: {xmlDocPath}");
            return;
        }

        LogHelper.Information($"Found assembly: {assembly.FullName} at {xmlDocPath}");
        string defaultOutputPath = outputPath ?? "lua-reference.md";
        Generate(assembly, xmlDocPath, defaultOutputPath);
    }

    public static void Generate(Assembly assembly, string xmlDocPath, string outputPath)
    {
        XDocument xmlDocs = XDocument.Load(xmlDocPath);
        StringBuilder stringBuilder = new StringBuilder();

        SortedDictionary<string, List<MemberInfo>> apiSections = new SortedDictionary<string, List<MemberInfo>>();
        IEnumerable<Type> exportedTypes = assembly.GetTypes()
              .Where(t =>
                  t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Any(m => m.IsDefined(typeof(LuaApiExportAttribute), false)) ||
                  t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Any(f => f.IsDefined(typeof(LuaApiExportAttribute), false)) ||
                  t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Any(p => p.IsDefined(typeof(LuaApiExportAttribute), false))
              );

        foreach (Type type in exportedTypes)
        {
            IEnumerable<MemberInfo> methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.IsDefined(typeof(LuaApiExportAttribute), false))
                .Cast<MemberInfo>();

            IEnumerable<MemberInfo> fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(f => f.IsDefined(typeof(LuaApiExportAttribute), false))
                .Cast<MemberInfo>();

            IEnumerable<MemberInfo> properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(p => p.IsDefined(typeof(LuaApiExportAttribute), false))
                .Cast<MemberInfo>();

            List<MemberInfo> members = methods.Concat(fields).Concat(properties)
                .OrderBy(m => m.GetCustomAttribute<LuaApiExportAttribute>().ResolveName(m.DeclaringType))
                .ToList();

            if (members.Any())
            {
                apiSections[type.Name] = members;
            }
        }

        stringBuilder.AppendLine("# Lua API Reference");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("## Table of Contents");
        foreach (KeyValuePair<string, List<MemberInfo>> section in apiSections)
        {
            string className = section.Key;
            stringBuilder.AppendLine($"* **[{className}](#{CreateAnchorLink(className)})**");
            foreach (MemberInfo member in section.Value)
            {
                LuaApiExportAttribute exportAttribute = member.GetCustomAttribute<LuaApiExportAttribute>();
                string luaName = exportAttribute.ResolveName(member.DeclaringType);
                stringBuilder.AppendLine($"  * [`{luaName}`](#{CreateAnchorLink(luaName)})");
            }
        }
        stringBuilder.AppendLine();

        foreach (KeyValuePair<string, List<MemberInfo>> section in apiSections)
        {
            string className = section.Key;
            List<MemberInfo> members = section.Value;
            stringBuilder.AppendLine($"## {className}");
            foreach (MemberInfo member in members)
            {
                LuaApiExportAttribute exportAttribute = member.GetCustomAttribute<LuaApiExportAttribute>();
                string resolvedName = exportAttribute.ResolveName(member.DeclaringType);
                string summary = GetSummary(xmlDocs, member);

                stringBuilder.AppendLine($"### `{resolvedName}`");

                if (member is MethodInfo method)
                {
                    List<(string Name, string Type, string Description)> parameters = GetParameters(xmlDocs, method);
                    string returns = GetReturns(xmlDocs, method);

                    stringBuilder.AppendLine($"**Summary:** {summary}");
                    stringBuilder.AppendLine();

                    if (parameters.Any())
                    {
                        stringBuilder.AppendLine("**Parameters:**");
                        stringBuilder.AppendLine();
                        stringBuilder.AppendLine("| Name | Type | Description |");
                        stringBuilder.AppendLine("|---|---|---|");
                        foreach ((string Name, string Type, string Description) param in parameters)
                        {
                            stringBuilder.AppendLine($"| `{param.Name}` | `{param.Type}` | {param.Description} |");
                        }
                        stringBuilder.AppendLine(); // Ensure separation after table
                    }

                    if (!string.IsNullOrEmpty(returns))
                    {
                        stringBuilder.AppendLine($"**Returns:** `{GetFriendlyTypeName(method.ReturnType)}` - {returns}");
                        stringBuilder.AppendLine();
                    }
                }
                else if (member is FieldInfo field)
                {
                    stringBuilder.AppendLine($"**Type:** `{GetFriendlyTypeName(field.FieldType)}`");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"**Summary:** {summary}");
                    stringBuilder.AppendLine();
                }
                else if (member is PropertyInfo property)
                {
                    bool isManagedValue = IsManagedValueType(property.PropertyType);
                    string innerTypeName = isManagedValue
                        ? GetFriendlyTypeName(property.PropertyType.GetGenericArguments()[0])
                        : GetFriendlyTypeName(property.PropertyType);

                    stringBuilder.AppendLine(isManagedValue
                        ? $"**Type:** `ManagedValue<{innerTypeName}>`"
                        : $"**Type:** `{GetFriendlyTypeName(property.PropertyType)}`");
                    stringBuilder.AppendLine();

                    stringBuilder.AppendLine("**Lua Usage:**");
                    stringBuilder.AppendLine();

                    if (isManagedValue)
                    {
                        // Flat Push/Pop/Set/GetValue globals registered by RegisterExportedMethods
                        stringBuilder.AppendLine($"* Push value: `{resolvedName}_Push({innerTypeName.ToLowerInvariant()})`");
                        stringBuilder.AppendLine($"* Pop value:  `{resolvedName}_Pop()`");
                        stringBuilder.AppendLine($"* Set value:  `{resolvedName}_SetValue({innerTypeName.ToLowerInvariant()})`");
                        stringBuilder.AppendLine($"* Get value:  `{resolvedName}_GetValue()` → `{innerTypeName}`");
                    }
                    else
                    {
                        bool hasGetter = property.GetGetMethod(nonPublic: true) != null;
                        bool hasSetter = property.GetSetMethod(nonPublic: true) != null;
                        if (hasGetter) stringBuilder.AppendLine($"* Getter: `Get{resolvedName}()`");
                        if (hasSetter) stringBuilder.AppendLine($"* Setter: `Set{resolvedName}(value)`");
                    }

                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"**Summary:** {summary}");
                    stringBuilder.AppendLine();
                }


                stringBuilder.AppendLine("---");
            }
        }
        File.WriteAllText(outputPath, stringBuilder.ToString());
        Console.WriteLine($"Successfully generated Lua API documentation at: {outputPath}");
    }

    private static string CreateAnchorLink(string text)
    {
        text = text.Replace("`", "").Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"\s", "-");
        text = Regex.Replace(text, @"[^\w\-]", "");
        return text;
    }

    private static string CleanXmlText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Flatten whitespace: replaces newlines/tabs with single space
        string clean = Regex.Replace(input, @"\s+", " ").Trim();

        // Escape pipes to prevent breaking Markdown tables
        clean = clean.Replace("|", "\\|");

        // If text ends with a backslash (e.g. "path\"), it escapes the Markdown table cell delimiter.
        // Append a space to prevent the table row from breaking.
        if (clean.EndsWith("\\"))
        {
            clean += " ";
        }

        return clean;
    }

    private static string GetSummary(XDocument xmlDocs, MemberInfo member)
    {
        string memberName = GetXmlMemberName(member);
        XElement memberElement = xmlDocs.Descendants("member").FirstOrDefault(e => e.Attribute("name")?.Value == memberName);
        if (memberElement == null) return "No summary provided.";

        XElement summaryElement = memberElement.Element("summary");
        if (summaryElement == null) return "No summary provided.";

        return CleanXmlText(summaryElement.Value);
    }

    private static string GetReturns(XDocument xmlDocs, MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
            return string.Empty;

        string memberName = GetXmlMemberName(method);
        XElement returnsElement = xmlDocs.Descendants("member")
            .FirstOrDefault(e => e.Attribute("name")?.Value == memberName)
            ?.Element("returns");

        if (returnsElement == null) return "No description provided.";

        return CleanXmlText(returnsElement.Value);
    }

    private static List<(string Name, string Type, string Description)> GetParameters(XDocument xmlDocs, MethodInfo method)
    {
        string memberName = GetXmlMemberName(method);
        IEnumerable<XElement>? paramDocs = xmlDocs.Descendants("member")
            .FirstOrDefault(e => e.Attribute("name")?.Value == memberName)
            ?.Elements("param");

        return method.GetParameters().Select(p =>
        {
            string doc = paramDocs?.FirstOrDefault(e => e.Attribute("name")?.Value == p.Name)?.Value;
            doc = string.IsNullOrWhiteSpace(doc) ? "No description." : CleanXmlText(doc);

            return (p.Name, GetFriendlyTypeName(p.ParameterType), doc);
        }).ToList();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        // Handle Ref/Out parameters (e.g., ref int)
        if (type.IsByRef)
        {
            type = type.GetElementType() ?? type;
        }

        // Handle Nullable types (e.g. int?)
        if (Nullable.GetUnderlyingType(type) is Type underlyingType)
        {
            return $"{GetFriendlyTypeName(underlyingType)}?";
        }

        // Handle Generics (e.g. List<T>)
        if (type.IsGenericType)
        {
            string genericTypeName = type.GetGenericTypeDefinition().Name;
            int backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex > 0)
                genericTypeName = genericTypeName.Substring(0, backtickIndex);

            string genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{genericTypeName}<{genericArgs}>";
        }

        // Handle Aliases (int, float, bool, etc.)
        if (TypeAliases.TryGetValue(type, out string? alias))
        {
            return alias;
        }

        return type.Name;
    }

    private static string GetXmlMemberName(MemberInfo member)
    {
        string prefix = member.MemberType switch
        {
            MemberTypes.Method => "M",
            MemberTypes.Field => "F",
            MemberTypes.Property => "P",
            _ => throw new NotSupportedException($"Unsupported member type: {member.MemberType}")
        };

        string declaringTypeName = member.DeclaringType?.FullName?.Replace('+', '.') ?? member.DeclaringType?.Name ?? "";

        if (member is MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string memberName = method.Name;

            if (parameters.Length == 0)
            {
                return $"{prefix}:{declaringTypeName}.{memberName}";
            }

            IEnumerable<string> paramTypeNames = parameters.Select(p => GetXmlTypeName(p.ParameterType));
            return $"{prefix}:{declaringTypeName}.{memberName}({string.Join(",", paramTypeNames)})";
        }

        return $"{prefix}:{declaringTypeName}.{member.Name}";
    }

    private static string GetXmlTypeName(Type type)
    {
        if (type == null) return string.Empty;

        if (type.IsByRef)
        {
            return GetXmlTypeName(type.GetElementType()!) + "@";
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            string genericTypeName = type.GetGenericTypeDefinition().FullName!;
            int backtickIndex = genericTypeName.IndexOf('`');
            if (backtickIndex > 0)
                genericTypeName = genericTypeName.Substring(0, backtickIndex);

            string genericArgs = string.Join(",", type.GetGenericArguments().Select(GetXmlTypeName));
            return $"{genericTypeName}{{{genericArgs}}}";
        }

        return type.FullName ?? type.Name;
    }

    private static bool IsManagedValueType(Type type)
    {
        Type? current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ManagedValue<>))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}