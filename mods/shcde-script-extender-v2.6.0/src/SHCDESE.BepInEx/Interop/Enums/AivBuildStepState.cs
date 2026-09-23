namespace SHCDESE.Interop.Enums;

/// <summary>
/// Observed states of one native AIV construction step.
/// </summary>
public enum AivBuildStepState : byte
{
    Inactive = 0,
    Pending = 1,
    Unknown2 = 2,
    Built = 3,
    Abandoned = 4,
    RetryPending = 5
}
