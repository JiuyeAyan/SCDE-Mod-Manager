using SHCDESE.Logging;
using System.Numerics;

namespace SHCDESE;

public unsafe static class CoordinateConverter
{
    public static Vector2 ConvertLocalTileToCameraWorld(Vector2 localTilePosition)
    {
        GameMap gameMap = GameMap.instance;

        if (gameMap == null || CameraControls2D.instance == null)
        {
            LogHelper.Warning("Critical game instance(s) not found for conversion (GameMap or CameraControls2D).");
            return Vector2.Zero;
        }

        // Convert the local game tile coordinates to the internal tilemap coordinates.
        int tileMapX, tileMapY;
        gameMap.mapGameTileToTilemapCoord((int)localTilePosition.X, (int)localTilePosition.Y, out tileMapX, out tileMapY);

        // Get the specific GameMapTile object to access its terrain height.
        GameMapTile mapTile = gameMap.getMapTile(tileMapX, tileMapY);
        if (mapTile == null)
        {
            LogHelper.Warning($"MapTile at converted tilemap coordinates ({tileMapX}, {tileMapY}) is null. Input local tile ({localTilePosition.X}, {localTilePosition.Y}) might be out of bounds or invalid.");
            return Vector2.Zero; // Cannot proceed without a valid tile.
        }

        // Get the base world position vector from the tilemap coordinates (without height adjustment initially).
        // The last few parameters (tileX, tileY, objectID, halfPixelX, halfPixelY) default to 0 for a tile center.
        UnityEngine.Vector3 v3 = gameMap.getSpritePosVector(tileMapX, tileMapY, 0, 0, 0, 0, 0);
        Vector3* worldPosition = (Vector3*)&v3;

        // Adjust the world Y position by the terrain's height, unless in flattened landscape mode.
        // This is crucial for correctly placing the camera in 3D isometric space.
        if (!EngineInterface.FlattenedLandscape)
        {
            worldPosition->Y += mapTile.height;
        }

        // Apply the final small offsets used by the game internal function for precise centering on the sprite.
        worldPosition->X += 0.5f;
        worldPosition->Y -= 0.5f;

        // Return the final calculated Unity World Space camera position.
        return new Vector2(worldPosition->X, worldPosition->Y);
    }
}
