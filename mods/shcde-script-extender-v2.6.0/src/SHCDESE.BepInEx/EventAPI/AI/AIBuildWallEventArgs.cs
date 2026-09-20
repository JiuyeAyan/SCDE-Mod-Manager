using SHCDESE.Interop;

namespace SHCDESE.EventAPI.AI;

public class AIBuildWallEventArgs : EventHookBase
{
    public int PlayerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public eMappers Mappers { get; set; }

    public AIBuildWallEventArgs(EventHookPhase phase, int playerId, int tileX, int tileY, eMappers mappers)
    {
        Phase = phase;
        PlayerId = playerId;
        TileX = tileX;
        TileY = tileY;
        Mappers = mappers;
    }
}
