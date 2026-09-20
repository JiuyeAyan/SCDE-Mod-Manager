using SHCDESE.API;
using SHCDESE.Logging;
using System;
using UnityEngine;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // Director
    //
    internal ManagedDetour<director_Awake> director_Awake_hook;
    internal delegate void director_Awake(Director instance);
    internal void Director_Awake_Hook(Director instance)
    {
        director_Awake_hook.Trampoline(instance);
        try
        {
            string[] cursorFields = [
                nameof(Director.swordCursor), 
                nameof(Director.scimitarCursor),
                nameof(Director.handCursor),
                nameof(Director.deleteCursor),
                nameof(Director.deleteNotCursor),
                nameof(Director.waitCursor),
                nameof(Director.swordCursorX),
                nameof(Director.scimitarCursorX),
                nameof(Director.handCursorX),
                nameof(Director.deleteCursorX),
                nameof(Director.deleteNotCursorX),
                nameof(Director.waitCursorX),
                nameof(Director.swordCursor32),
                nameof(Director.scimitarCursor32),
                nameof(Director.handCursor32),
                nameof(Director.deleteCursor32),
                nameof(Director.deleteNotCursor32),
                nameof(Director.waitCursor32),
                nameof(Director.deleteOldCursor)
                ];

            foreach (string cursorField in cursorFields)
            {
                Texture2D? textureField = typeof(Director).GetField(cursorField, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(instance) as Texture2D;
                if (textureField == null)
                {
                    LogHelper.Warning($"Cursor texture field [{cursorField}] is null in Director instance.");
                    continue;
                }

                string texturePath = $"Assets/Resources/sprites/cursors/{cursorField}.png";
                if (!GameAssetManagerAPI.Instance.TryLoadTexture(texturePath, out Texture2D? loadedTexture))
                {
                    LogHelper.Verbose($"Failed to load texture for cursor field [{cursorField}]");
                    continue;
                }
                LogHelper.Information($"Successfully loaded texture for cursor field [{cursorField}] from path [{texturePath}]");
                Graphics.CopyTexture(loadedTexture, textureField);
            }

        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during director awake");
        }
    }
}
