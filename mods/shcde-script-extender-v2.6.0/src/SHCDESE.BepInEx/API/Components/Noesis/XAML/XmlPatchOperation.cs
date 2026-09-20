using System;

namespace SHCDESE.API.Components.Noesis.XAML;

/// <summary>
/// Represents a single modification action to be applied to a specific XAML file.
/// </summary>
/// <remarks>
/// Operations are defined in XML patch files and executed sequentially by the <see cref="XamlPatcher"/>.
/// </remarks>
public class XmlPatchOperation
{
    /// <summary>
    /// Gets or sets the type of modification to perform (e.g., Add, Remove, Replace).
    /// </summary>
    public XmlPatchType Type { get; set; }

    /// <summary>
    /// Gets or sets the XPath expression used to locate the target element(s) within the XAML document.
    /// </summary>
    /// <remarks>
    /// Use the prefix <c>n:</c> to match default XAML presentation elements (e.g., <c>//n:StackPanel</c>).
    /// Use the prefix <c>x:</c> to match elements with x:Keys (e.g., <c>//*[@x:Key='MyResource']</c>).
    /// </remarks>
    public string XPath { get; set; }

    /// <summary>
    /// Gets or sets the name of the attribute to modify.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Type"/> is <see cref="XmlPatchType.SetAttribute"/> or <see cref="XmlPatchType.AddNamespace"/>.
    /// </remarks>
    public string AttributeName { get; set; }

    /// <summary>
    /// Gets or sets the value to assign to an attribute or namespace.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="Type"/> is <see cref="XmlPatchType.SetAttribute"/> or <see cref="XmlPatchType.AddNamespace"/>.
    /// </remarks>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the raw XML content block to inject.
    /// </summary>
    /// <remarks>
    /// Used for <see cref="XmlPatchType.Add"/>, <see cref="XmlPatchType.InsertBefore"/>, 
    /// <see cref="XmlPatchType.InsertAfter"/>, and <see cref="XmlPatchType.Replace"/>.
    /// The content is automatically sanitized to use the root document's namespaces.
    /// </remarks>
    public string Content { get; set; }
}