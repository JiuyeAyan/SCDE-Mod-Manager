namespace SHCDESE.Interop.Enums;

/// <summary>
/// Native macro-path connection classes. Only class 1 and the two gatehouse classes currently have semantic names.
/// </summary>
public enum PathConnectionClass : int
{
    Unknown = 0,
    LadderClimb = 1,
    Connection2 = 2,
    GatehouseBig = 3,
    GatehouseSmall = 4,
    Connection5 = 5,
    Connection6 = 6
}