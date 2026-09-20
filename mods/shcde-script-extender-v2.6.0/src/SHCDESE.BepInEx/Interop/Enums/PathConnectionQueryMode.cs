namespace SHCDESE.Interop.Enums;

/// <summary>
/// Selects which macro-path connection classes may participate in a route query.
/// </summary>
public enum PathConnectionQueryMode : int
{
    ExcludeLadderClimb = 0,
    IncludeAll = 1,
    LadderClimbOnly = 2
}
