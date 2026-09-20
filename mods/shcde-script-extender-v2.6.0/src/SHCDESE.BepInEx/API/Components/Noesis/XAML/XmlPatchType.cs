using System;

namespace SHCDESE.API.Components.Noesis.XAML;

/// <summary>
/// Defines the available operations for modifying XAML content at runtime.
/// </summary>
public enum XmlPatchType
{

    /// <summary>
    /// Appends the new content as the last child of the target element.
    /// </summary>
    Add,

    /// <summary>
    /// Inserts the new content immediately before the target element (as a sibling).
    /// </summary>
    InsertBefore,

    /// <summary>
    /// Inserts the new content immediately after the target element (as a sibling).
    /// </summary>
    InsertAfter,

    /// <summary>
    /// Completely replaces the target element with the new content.
    /// </summary>
    Replace,

    /// <summary>
    /// Removes the target element from the document.
    /// </summary>
    Remove,

    /// <summary>
    /// Adds or modifies an attribute value on the target element (e.g., changing <c>Width="100"</c>).
    /// </summary>
    SetAttribute,

    /// <summary>
    /// Adds a new namespace declaration (xmlns) to the root element of the document.
    /// </summary>
    AddNamespace
}