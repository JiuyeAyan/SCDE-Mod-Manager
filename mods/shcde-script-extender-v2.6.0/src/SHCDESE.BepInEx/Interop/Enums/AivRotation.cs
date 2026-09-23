namespace SHCDESE.Interop.Enums;

/// <summary>
/// Cardinal rotation values accepted by the native AIV layout system.
/// These values share the game's eight-way direction encoding.
/// </summary>
public enum AivRotation : int
{
    South = 0,
    East = 2,
    North = 4,
    West = 6,
    Default = 15 // Seems to be used for player keep rotation
}
