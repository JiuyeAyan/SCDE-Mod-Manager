using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines how a unit was selected.
/// This is not a game known enum. This is a script extender exclusive for internal use only.
/// </summary>
public enum UnitSelectionType : UInt32
{
    /// <summary>No selection type</summary>
    None = 0,
    /// <summary>Selected via selection-rect</summary>
    SelectionRect = 1,
    /// <summary>Selected via mouse-click</summary>
    Click = 2
}