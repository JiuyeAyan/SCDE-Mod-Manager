using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using System;
using System.Data;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // MapFileManager
    //

    internal ManagedDetour<mapFileManager_getRadarFromFileDelegate> mapFileManager_getRadarFromFile_hook;
    internal delegate byte[] mapFileManager_getRadarFromFileDelegate(MapFileManager instance, string path);
    internal byte[] MapFileManager_GetRadarFromFile_Hook(MapFileManager instance, string path)
    {
        try
        {
            LogHelper.Debug($"Attempting to retrieve from [{path}]");
            using (MapArchive archive = new MapArchive(path))
            {
                if (!archive.IsValid)
                    return mapFileManager_getRadarFromFile_hook.Trampoline(instance, path);

                byte[]? previewBytes = archive.TryReadBinaryFile(GameMapArchiveManagerAPI.DEFAULT_PREVIEW_FILE_NAME);
                if (previewBytes == null)
                {
                    LogHelper.Debug($"No custom preview found.");
                    return mapFileManager_getRadarFromFile_hook.Trampoline(instance, path);
                }

                // Prepare preview picture.
                // The games radar preview consumer calls SetPixelData into a BGRA32 texture,
                // so we need to return raw bytes in BGRA order.
                //
                // We use GetPixels32() rather than GetRawTextureData() for two reasons:
                // - GetRawTextureData() sucks
                // - LoadImage() applies a linear color space conversion that corrupts mid-tone
                //    values when using GetRawTextureData(). GetPixels32() always returns reliable
                //    sRGB RGBA values regardless of the textures internal format or color space settings.
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(previewBytes);

                Color32[] pixels = tex.GetPixels32();
                byte[] bgra = new byte[pixels.Length * 4];
                for (int i = 0; i < pixels.Length; i++)
                {
                    bgra[i * 4 + 0] = pixels[i].b;
                    bgra[i * 4 + 1] = pixels[i].g;
                    bgra[i * 4 + 2] = pixels[i].r;
                    bgra[i * 4 + 3] = pixels[i].a;
                }
                return bgra;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during retrieval attempt");
        }
        return mapFileManager_getRadarFromFile_hook.Trampoline(instance, path);
    }

    internal ManagedDetour<mapFileManager_updateWorkshopMapDelegate> mapFileManager_updateWorkshopMap_hook;
    internal delegate void mapFileManager_updateWorkshopMapDelegate(MapFileManager instance, string file, string realFileName);
    internal void MapFileManager_UpdateWorkshopMap_Hook(MapFileManager instance, string file, string realFileName)
    {
        try
        {
            LogHelper.Information($"Attempting to retrieve from file=[{file}], realFileName=[{realFileName}]");
            if (MapArchive.TryLoad(file, out MapArchive? archive))
            {
                if (!archive.IsValid || archive.Info?.Manifest == null)
                {
                    mapFileManager_updateWorkshopMap_hook.Trampoline(instance, file, realFileName);
                    return;
                }

                // Skip any BepInEx maps.
                LogHelper.Information($"Map manifest: of [file]: [{archive.Info?.Manifest}]");
                if (archive.Info?.Manifest == ModManifest.BepInEx)
                {
                    return;
                }

            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during map query attempt");
        }
        mapFileManager_updateWorkshopMap_hook.Trampoline(instance, file, realFileName);
        return;
    }
}
