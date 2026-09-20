using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// The game unit information for a selected unit, including its ID and type.
/// Used by GetSelectedChimps to return a structured array of selected units.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SelectedUnitInfo
{
    public int UnitId;
    public int UnitType;
}