using SHCDESE.Interop;

namespace SHCDESE.EventAPI.Buildings;
public class BuildingPlacementValidationEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int PlayerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public eMappers Mappers { get; set; }
    public int Unknown1 { get; set; }
    public byte Unknown2 { get; set; }

    // Custom parameters
    public bool CustomValidationRules { get; set; } = false;
    public bool ForceBlockPlacementState { get; set; } = true;

    public BuildingPlacementValidationEventArgs(EventHookPhase phase, int playerId, int tileX, int tileY, eMappers mv, int a6, byte a7)
    {
        Phase = phase;
        PlayerId = playerId;
        TileX = tileX;
        TileY = tileY;
        Mappers = mv;
        Unknown1 = a6;
        Unknown2 = a7;
    }
}
