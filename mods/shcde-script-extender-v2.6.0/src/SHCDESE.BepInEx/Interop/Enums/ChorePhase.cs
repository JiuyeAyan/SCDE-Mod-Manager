namespace SHCDESE.Interop.Enums;

public enum ChorePhase : int
{
    Unpack = 0,
    Pack = 1,

    /// <summary>
    /// Receive-side sizing pass. The handler must publish the number of payload bytes that the native chore manager should copy into the scheduled slot.
    /// No fields should be transferred during this phase.
    /// </summary>
    Measure = 2,
}
