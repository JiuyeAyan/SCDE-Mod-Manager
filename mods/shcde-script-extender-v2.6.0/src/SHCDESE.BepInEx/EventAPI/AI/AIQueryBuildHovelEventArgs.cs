using SHCDESE.Interop;

namespace SHCDESE.EventAPI.AI;

public class AIQueryBuildHovelEventArgs : EventHookBase
{
    public int PlayerId { get; set; }
    public eMappers Mappers { get; set; }

    public bool ReturnValue { get; set; }

    public AIQueryBuildHovelEventArgs(EventHookPhase phase, int playerId, eMappers mappers)
    {
        Phase = phase;
        PlayerId = playerId;
        Mappers = mappers;
    }
}
