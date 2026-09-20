using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameSoundManagerAPI functionality to the Lua scripting environment.
/// </summary>
[LuaApiNamespace("Sound")]
public static class LuaSoundAPI
{
    /// <summary>
    /// Registers all sound and music-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameSoundManagerAPI.Instance);
        lua.RegisterExportedStaticMethods(typeof(LuaSoundAPI));
    }

    /// <summary>
    /// Lua version of <see cref="GameSoundManagerAPI.RegisterSoundFromArchive"/>
    /// </summary>
    /// <param name="archivePath">Relative path within the archive (e.g., "sounds/mysound.wav").</param>
    /// <param name="onCompleteCallback">Called with the new sound ID, or -1 on failure.</param>
    /// <param name="initialVolume">Default volume for this sound.</param>
    /// <param name="initialPosition">Audio position/priority value.</param>
    /// <param name="temporary">If true, will be auto-removed on map unload.</param>
    [LuaApiExport("RegisterFromArchiveAsync")]
    public static void RegisterSoundFromArchive(string archivePath, LuaFunction onCompleteCallback, float initialVolume = 1f, int initialPosition = 64, bool temporary = true)
    {
        GameSoundManagerAPI.Instance.RegisterSoundFromArchive(archivePath, result =>
        {
            try 
            {
                onCompleteCallback?.Call(result); 
            }
            catch (Exception ex) 
            { 
                LogHelper.Error($"callback error: {ex}"); 
            }
        }, initialVolume, initialPosition, temporary);
    }

    /// <summary>
    /// Lua version of <see cref="GameSoundManagerAPI.RegisterAmbient"/>
    /// </summary>
    /// <param name="firstBufferNo">The sound ID of the first sound in the ambient group.</param>
    /// <param name="maxVariants">The number of sound variations in this group (e.g., if you have bird1.wav, bird2.wav, set this to 2).</param>
    /// <param name="temporary">If true, will delete the ambient group on any MapUnload event automatically, this is primarily for the lua-side.</param>
    /// <param name="onCompleteCallback">Called with the new ambient group ID, or -1 on failure.</param>
    [LuaApiExport("RegisterAmbientGroupAsync")]
    public static void RegisterAmbient(int firstBufferNo, int maxVariants, bool temporary, LuaFunction onCompleteCallback)
    {
        GameSoundManagerAPI.Instance.RegisterAmbient(firstBufferNo, maxVariants, temporary, result =>
        {
            try 
            { 
                onCompleteCallback?.Call(result); 
            }
            catch (Exception ex) 
            { 
                LogHelper.Error($"callback error: {ex}"); 
            }
        });
    }

    /// <summary>
    /// Lua version of <see cref="GameSoundManagerAPI.TryRemoveAmbient"/>
    /// </summary>
    /// <param name="ambientId">The ID of the ambient group to remove.</param>
    /// <param name="onCompleteCallback">Called with true if the group was successfully removed, false otherwise.</param>
    [LuaApiExport("UnregisterAmbientGroupAsync")]
    public static void TryRemoveAmbient(int ambientId, LuaFunction onCompleteCallback)
    {
        GameSoundManagerAPI.Instance.TryRemoveAmbient(ambientId, result =>
        {
            try 
            { 
                onCompleteCallback?.Call(result); 
            }
            catch (Exception ex) 
            { 
                LogHelper.Error($"callback error: {ex}"); 
            }
        });
    }
}
