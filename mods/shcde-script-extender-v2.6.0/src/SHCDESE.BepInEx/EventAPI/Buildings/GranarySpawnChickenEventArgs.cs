using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Buildings;

public class GranarySpawnChickenEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TileX { get; set; }
    public int TileY { get; set; }
    public eChimps Chimp { get; set; }
    public int PlayerId { get; set; }
    public int HeightElevation { get; set; }

    public GranarySpawnChickenEventArgs(EventHookPhase phase, int tileX, int tileY, eChimps chimp, int playerId, int heightElevation)
    {
        Phase = phase;
        TileX = tileX;
        TileY = tileY;
        Chimp = chimp;
        PlayerId = playerId;
        HeightElevation = heightElevation;
    }
}
