using SHCDESE.ImMenu.Visualizers;
using SHCDESE.Logging;
using System;
using UnityEngine;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // GameTile
    //
    internal ManagedDetour<GameTile_SetTileColor_Delegate> gameTile_SetTileColor_hook;
    internal delegate void GameTile_SetTileColor_Delegate(gameTile instance, GameMapTile gameMapTile, Vector3Int x, int y);
    internal void GameTile_SetTileColor_Hook(gameTile instance, GameMapTile tile, Vector3Int location, int light)
    {
        try
        {
            DebugGridVisualizer debugVis = DebugGridVisualizer.Instance;
            LogicDebugInfoVisualizer logicVis = LogicDebugInfoVisualizer.Instance;
            KeepProximityVisualizer keepVis = KeepProximityVisualizer.Instance;

            if (debugVis != null && debugVis.IsActive)
            {
                UnityEngine.Color overrideColor;
                if (debugVis.TryGetColorOverride(tile.gameMapX, tile.gameMapY, out overrideColor))
                {
                    tile.tilemapRef.SetColor(location, overrideColor);
                    return;
                }
            }

            if (logicVis != null && logicVis.IsActive)
            {
                UnityEngine.Color overrideColor;
                if (logicVis.TryGetColorOverride(tile.gameMapX, tile.gameMapY, out overrideColor))
                {
                    tile.tilemapRef.SetColor(location, overrideColor);
                    return;
                }
            }

            if (keepVis != null && keepVis.IsActive)
            {
                UnityEngine.Color overrideColor;
                if (keepVis.TryGetColorOverride(tile.gameMapX, tile.gameMapY, out overrideColor))
                {
                    tile.tilemapRef.SetColor(location, overrideColor);
                    return;
                }
            }

            gameTile_SetTileColor_hook.Trampoline(instance, tile, location, light);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error");
            gameTile_SetTileColor_hook.Trampoline(instance, tile, location, light);
        }
    }
}
