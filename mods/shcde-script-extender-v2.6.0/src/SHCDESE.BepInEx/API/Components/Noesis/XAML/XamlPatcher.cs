using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace SHCDESE.API.Components.Noesis.XAML;

/// <summary>
/// A utility class responsible for applying runtime modifications to XAML assets using XPath-based operations.
/// </summary>
internal static class XamlPatcher
{
    /// <summary>
    /// Applies a sequence of patch operations to a raw XAML string.
    /// </summary>
    /// <remarks>
    /// This method parses the XAML into an <see cref="XDocument"/>, resolves standard Noesis namespaces, 
    /// and executes operations (Add, Remove, Replace) sequentially.
    /// Errors during individual patch operations are logged but do not halt the entire process.
    /// </remarks>
    /// <param name="originalXml">The original XAML content loaded from the game assets.</param>
    /// <param name="operations">A list of <see cref="XmlPatchOperation"/> defined by mods.</param>
    /// <returns>The modified XAML string, or the original string if parsing failed.</returns>
    public static string ApplyPatches(string originalXml, List<XmlPatchOperation> operations)
    {
        if (string.IsNullOrEmpty(originalXml)) return originalXml;

        try
        {
            XDocument doc = XDocument.Parse(originalXml, LoadOptions.PreserveWhitespace);
            XmlNamespaceManager nsManager = CreateNamespaceManager(doc);

            // Capture the exact Namespace objects from the Root.
            // These are the "Keys" to the document. Using these ensures no 'p1' prefixes.
            XNamespace rootDefaultNs = doc.Root.GetDefaultNamespace();
            XNamespace rootXamlNs = doc.Root.GetNamespaceOfPrefix("x") ?? "http://schemas.microsoft.com/winfx/2006/xaml";

            foreach (XmlPatchOperation op in operations)
            {
                try
                {
                    LogHelper.Information($"Applying patch: {op.Type}, xpath: [{op.XPath}]");
                    ProcessOperation(doc, op, nsManager, rootDefaultNs, rootXamlNs);
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, $"Failed to apply patch operation: {op.Type} on XPath: {op.XPath}");
                }
            }

            // Properly save-back the xml with preserved whitespace, no indent, etc.
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = false,
                NewLineHandling = NewLineHandling.None,
                NewLineOnAttributes = false,
            };

            using StringWriter sw = new StringWriter();
            using (XmlWriter xw = XmlWriter.Create(sw, settings))
            {
                doc.Save(xw);
            }
            return sw.ToString();
            //return doc.ToString(SaveOptions.DisableFormatting);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to parse original XAML for patching.");
            return originalXml;
        }
    }

    private static void ProcessOperation(XDocument doc, XmlPatchOperation op, XmlNamespaceManager nsManager, XNamespace rootDefaultNs, XNamespace rootXamlNs)
    {
        if (op.Type == XmlPatchType.AddNamespace)
        {
            if (doc.Root != null)
            {
                doc.Root.Add(new XAttribute(XNamespace.Xmlns + op.AttributeName, op.Value));
                nsManager.AddNamespace(op.AttributeName, op.Value);
            }
            return;
        }

        List<XElement> targets = doc.XPathSelectElements(op.XPath, nsManager).ToList();
        if (targets.Count == 0)
        {
            LogHelper.Warning($"Patch XPath yielded no results: {op.XPath}");
            return;
        }

        XElement GetCleanContent()
        {
            if (string.IsNullOrWhiteSpace(op.Content)) 
                return null;

            // Parse the string into a "Dirty" element (likely has p1:Key, xmlns attributes, etc.)
            XElement dirtyElement;
            XmlParserContext context = new XmlParserContext(null, nsManager, null, XmlSpace.Default);
            using (XmlReader reader = XmlReader.Create(new StringReader(op.Content), new XmlReaderSettings(), context))
            {
                dirtyElement = XElement.Load(reader);
            }

            // Rebuild the element cleanly using the Root Document's namespaces
            return RebuildElement(dirtyElement, rootDefaultNs, rootXamlNs);
        }

        foreach (XElement target in targets)
        {
            // Must create a new copy for each target
            XElement content = GetCleanContent();

            switch (op.Type)
            {
                case XmlPatchType.Add:
                    target.Add(content);
                    break;
                case XmlPatchType.InsertBefore:
                    target.AddBeforeSelf(content);
                    break;
                case XmlPatchType.InsertAfter:
                    target.AddAfterSelf(content);
                    break;
                case XmlPatchType.Replace:
                    target.ReplaceWith(content);
                    break;
                case XmlPatchType.Remove:
                    target.Remove();
                    break;
                case XmlPatchType.SetAttribute:
                    target.SetAttributeValue(ResolveAttributeName(op.AttributeName, nsManager), op.Value);
                    break;
            }
        }
    }

    /// <summary>
    /// Resolves prefixed XAML attributes such as <c>ui:Behavior.Enabled</c> against the
    /// namespaces known to the patch. Plain attributes remain in the empty namespace.
    /// </summary>
    private static XName ResolveAttributeName(string attributeName, XmlNamespaceManager nsManager)
    {
        int separator = attributeName.IndexOf(':');
        if (separator <= 0 || separator == attributeName.Length - 1)
        {
            return attributeName;
        }

        string prefix = attributeName.Substring(0, separator);
        string localName = attributeName.Substring(separator + 1);
        string? namespaceName = nsManager.LookupNamespace(prefix);
        if (string.IsNullOrEmpty(namespaceName))
        {
            throw new InvalidOperationException($"Unknown namespace prefix '{prefix}' in attribute '{attributeName}'.");
        }

        return XNamespace.Get(namespaceName) + localName;
    }



    /// <summary>
    /// Recursively rebuilds an XElement to strip specific namespace declarations.
    /// </summary>
    /// <remarks>
    /// When parsing partial XML snippets, .NET often adds explicit namespaces (e.g., <c>xmlns=""</c> or <c>p1:Key</c>).
    /// This method forces the element to adopt the Root Document's default and XAML namespaces, ensuring 
    /// the injected XML merges cleanly without validation errors.
    /// </remarks>
    private static XElement RebuildElement(XElement dirty, XNamespace defaultNs, XNamespace xNs)
    {
        // Create new element using the Document's Default Namespace
        // (This removes xmlns="" or xmlns="p1")
        XElement clean = new XElement(defaultNs + dirty.Name.LocalName);

        // Copy Attributes (Sanitized)
        foreach (XAttribute attr in dirty.Attributes())
        {
            if (attr.IsNamespaceDeclaration) continue; // SKIP all xmlns declarations

            XName cleanAttrName;

            // Check if this attribute belongs to the XAML namespace (http://.../2006/xaml)
            if (attr.Name.NamespaceName == xNs.NamespaceName)
            {
                // Force it to use the Root's 'x' namespace object. 
                // This ensures it prints as 'x:Key' and not 'p1:Key'
                cleanAttrName = xNs + attr.Name.LocalName;
            }
            else if (attr.Name.Namespace == XNamespace.None)
            {
                // Standard attribute like UriSource="...", keep as-is (no namespace)
                cleanAttrName = attr.Name.LocalName;
            }
            else
            {
                // Other namespaces? Keep them as is, but this is rare in your case.
                cleanAttrName = attr.Name;
            }

            clean.Add(new XAttribute(cleanAttrName, attr.Value));
        }

        // Recurse for Children
        foreach (XNode node in dirty.Nodes())
        {
            if (node is XElement childEl)
            {
                clean.Add(RebuildElement(childEl, defaultNs, xNs));
            }
            else
            {
                // Comments, Text, etc. just copy over
                clean.Add(node);
            }
        }

        return clean;
    }

    internal static XmlNamespaceManager CreateNamespaceManager(XDocument doc)
    {
        XmlReader reader = doc.CreateReader();
        return CreateNamespaceManager((NameTable)reader.NameTable);
    }

    /// <summary>
    /// Creates an <see cref="XmlNamespaceManager"/> pre-populated with standard Noesis and game-specific namespaces.
    /// </summary>
    /// <remarks>
    /// Registered prefixes for XPath usage:
    /// <list type="bullet">
    /// <item><c>n</c>: Standard Presentation namespace (http://schemas.microsoft.com/winfx/2006/xaml/presentation)</item>
    /// <item><c>x</c>: XAML namespace (http://schemas.microsoft.com/winfx/2006/xaml)</item>
    /// <item><c>noesis</c>: NoesisGUI Extensions</item>
    /// <item><c>local</c>: Game local namespace (clr-namespace:CrusaderDE)</item>
    /// </list>
    /// </remarks>
    internal static XmlNamespaceManager CreateNamespaceManager(NameTable nameTable)
    {
        XmlNamespaceManager nsManager = new XmlNamespaceManager(nameTable);

        // Default Namespace (Presentation) mapped to 'n'
        // Modders must use //n:StackPanel
        nsManager.AddNamespace("n", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        // Standard XAML/Noesis Namespaces
        nsManager.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
        nsManager.AddNamespace("b", "http://schemas.microsoft.com/xaml/behaviors");
        nsManager.AddNamespace("i", "http://schemas.microsoft.com/expression/2010/interactivity");
        nsManager.AddNamespace("d", "http://schemas.microsoft.com/expression/blend/2008");
        nsManager.AddNamespace("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
        nsManager.AddNamespace("noesis", "clr-namespace:NoesisGUIExtensions;assembly=Noesis.GUI.Extensions");

        // Local namespace (Game specific)
        nsManager.AddNamespace("local", "clr-namespace:CrusaderDE");

        // This allows the parser to recognize 'se:' during the file load process
        nsManager.AddNamespace("se", "clr-namespace:SHCDESE.ViewModels;assembly=SHCDESE");

        return nsManager;
    }
}
