using NLua;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameTileManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Tile")]
public static class LuaTileAPI
{
    /// <summary>
    /// Registers all tile-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameTileManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaTileAPI));

    }

    /// <summary>
    /// Retrieves an array of TileIDs for all tiles within a specified rectangular area. Exposed to Lua.
    /// </summary>
    /// <param name="tileX">The starting X coordinate of the rectangle.</param>
    /// <param name="tileY">The starting Y coordinate of the rectangle.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    /// <returns>An array of integer TileIDs within the specified rectangle.</returns>
    /// <example>
    /// The following Lua code gets all tile IDs in a 10x10 box, prints them and sets their height to 20.
    /// <code>
    /// local tileIds = Tile_FindInRect(400, 400, 10, 10)
    /// for i=1, tileIds.Length, 1 do
    ///     print("Found tile ID: " .. tileIds[i-1])
    ///     SetTileHeight(tileIds[i-1], 20)
    /// end
    /// </code>
    /// </example>
    [LuaApiExport("FindInRect")]
    public static int[] GetTilesInRect(int tileX, int tileY, int width, int height)
    {
        List<int> result = GameTileManagerAPI.Instance.GetTilesInRect(tileX, tileY, width, height);
        return [.. result];
    }

    /// <summary>
    /// Retrieves an array of TileIDs for all tiles within a circular range of a central point. Exposed to Lua.
    /// </summary>
    /// <param name="centerX">The X coordinate of the center point.</param>
    /// <param name="centerY">The Y coordinate of the center point.</param>
    /// <param name="range">The radius of the circle (in tiles).</param>
    /// <returns>An array of integer TileIDs within the specified range.</returns>
    /// <example>
    /// The following Lua code gets all tile IDs within a 5-tile radius of a point.
    /// <code>
    /// local tileIds = Tile_FindInRange(400, 400, 5)
    /// print("Found " .. tileIds.Length .. " tiles in range.")
    /// </code>
    /// </example>
    [LuaApiExport("FindInRange")]
    public static int[] GetTilesInRange(int centerX, int centerY, int range)
    {
        List<int> result = GameTileManagerAPI.Instance.GetTilesInRange(centerX, centerY, range);
        return [.. result];
    }


    /// <summary>
    /// Gets a random tile within a rectangle, optionally filtering by a Lua function.
    /// </summary>
    /// <param name="tileX">Top-left X.</param>
    /// <param name="tileY">Top-left Y.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="validator">Optional Lua function(tileId) -> bool. If provided, the tile must return true to be picked.</param>
    /// <returns>The selected x, y coordinates in a <see cref="UnmanagedVector2{UInt16}"/>. Returns default if not found.</returns>
    /// <example>
    /// <code>
    /// -- Get any random tile
    /// local pos = Tile_GetRandomInRect(10, 10, 20, 20)
    /// print(pos.x, pos.y)
    /// 
    /// -- Get random tile that is NOT Rock (Condition)
    /// local pos2 = Tile_GetRandomInRect(10, 10, 20, 20, function(id) 
    ///     return Tile_HasPropertyFlag(id, eTilePropertyFlag.ImpassableEdge)
    /// end)
    /// </code>
    /// </example>
    [LuaApiExport("GetRandomInRect")]
    public static object GetRandomInRect(int tileX, int tileY, int width, int height, LuaFunction validator = null)
    {
        System.Predicate<int> predicate = null;
        if (validator != null)
        {
            predicate = (tileId) =>
            {
                // Call the Lua function. It returns an array of objects.
                object[] res = validator.Call(tileId);
                // Check if result exists and is true
                if (res != null && res.Length > 0 && res[0] is bool b)
                {
                    return b;
                }
                return false;
            };
        }

        return GameTileManagerAPI.Instance.GetRandomTileInRect(tileX, tileY, width, height, predicate);
    }

    /// <summary>
    /// Gets a random tile within a sphere (circle), optionally filtering by a Lua function.
    /// </summary>
    /// <param name="centerX">Center X.</param>
    /// <param name="centerY">Center Y.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="validator">Optional Lua function(tileId) -> bool.</param>
    /// <returns>The selected x, y coordinates in a <see cref="UnmanagedVector2{UInt16}"/>.</returns>
    /// <example>
    /// <code>
    /// -- Get random walkable tile in range 15
    /// local pos = Tile_GetRandomInSphere(100, 100, 15, function(id)
    ///     return Tile_IsWalkableAndUnoccupied(id)
    /// end)
    /// </code>
    /// </example>
    [LuaApiExport("GetRandomInSphere")]
    public static UnmanagedVector2<UInt16> GetRandomInSphere(int centerX, int centerY, int radius, LuaFunction validator = null)
    {
        System.Predicate<int> predicate = null;

        if (validator != null)
        {
            predicate = (tileId) =>
            {
                object[] res = validator.Call(tileId);
                if (res != null && res.Length > 0 && res[0] is bool b)
                {
                    return b;
                }
                return false;
            };
        }

        return GameTileManagerAPI.Instance.GetRandomTileInSphere(centerX, centerY, radius, predicate);
    }

    /// <summary>
    /// Exports an inclusive rectangular map area as a SEMA asset owned by a registered asset provider.
    /// </summary>
    /// <param name="startX">First X coordinate, inclusive.</param>
    /// <param name="startY">First Y coordinate, inclusive.</param>
    /// <param name="endX">Second X coordinate, inclusive.</param>
    /// <param name="endY">Second Y coordinate, inclusive.</param>
    /// <param name="assetProviderGuid">GUID from the asset provider's <c>info.json</c>.</param>
    /// <param name="relativeAssetPath">Provider-relative <c>.sema</c> path.</param>
    /// <returns><c>true</c> when the complete area was serialized and saved.</returns>
    /// <example>
    /// <code>
    /// Tile_ExportMapArea(390, 390, 410, 410, "My.Mod", "MapAreas/castle.sema")
    /// </code>
    /// </example>
    [LuaApiExport("ExportMapArea")]
    public static bool ExportMapArea(int startX, int startY, int endX, int endY, string assetProviderGuid, string relativeAssetPath)
    {
        return GameTileManagerAPI.Instance.ExportMapArea(startX, startY, endX, endY, assetProviderGuid, relativeAssetPath);
    }

    /// <summary>
    /// Imports a provider-relative SEMA asset at the supplied top-left map coordinate.
    /// </summary>
    /// <param name="targetX">Destination X coordinate.</param>
    /// <param name="targetY">Destination Y coordinate.</param>
    /// <param name="assetProviderGuid">GUID from the asset provider's <c>info.json</c>.</param>
    /// <param name="relativeAssetPath">Provider-relative <c>.sema</c> path.</param>
    /// <param name="force">Skips destination occupancy validation. Existing entities are not removed.</param>
    /// <returns><c>true</c> when the asset was found, validated, and imported.</returns>
    /// <example>
    /// <code>
    /// Tile_ImportMapArea(450, 390, "My.Mod", "MapAreas/castle.sema")
    /// </code>
    /// </example>
    [LuaApiExport("ImportMapArea")]
    public static bool ImportMapArea(int targetX, int targetY, string assetProviderGuid, string relativeAssetPath, bool force = false)
    {
        return GameTileManagerAPI.Instance.ImportMapArea(targetX, targetY, assetProviderGuid, relativeAssetPath, force);
    }
}
