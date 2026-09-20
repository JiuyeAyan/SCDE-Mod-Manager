using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;

namespace SHCDESE.EventAPI.Buildings;

public class BuildingSpawnEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public NativePointer<GameBuildingManager> BuildingManager { get; }
    public int PlayerId { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public Int16 HeightElevation { get; set; }
    public eStructs Building { get; set; }
    public int BuildingScale { get; set; }
    public int VisualPlayerId { get; set; }
    public int SpriteVariationIndex { get; set; } 

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public BuildingSpawnEventArgs(EventHookPhase phase, NativePointer<GameBuildingManager> pBuildingManager, int playerId, int tileX, int tileY, Int16 heightElevation, eStructs building, int buildingScale, int visualPlayerId, int spriteVariationIndex)
    {
        Phase = phase;
        BuildingManager = pBuildingManager;
        PlayerId = playerId;
        TileX = tileX;
        TileY = tileY;
        HeightElevation = heightElevation;
        Building = building;
        BuildingScale = buildingScale;
        VisualPlayerId = visualPlayerId;
        SpriteVariationIndex = spriteVariationIndex;
    }
}
