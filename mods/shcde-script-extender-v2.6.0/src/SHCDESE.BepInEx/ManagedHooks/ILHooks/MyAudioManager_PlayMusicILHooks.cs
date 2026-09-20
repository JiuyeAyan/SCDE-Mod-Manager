using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // MyAudioManager
    //
    internal ILHook? myAudioManager_PlayMusic;
    internal void MyAudioManager_PlayMusic_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Ldarg_1),
            x => x.Match(OpCodes.Ldarg_2),
            x => x.Match(OpCodes.Ldarg_3),
            x => x.Match(OpCodes.Ldarg_S),
            x => x.Match(OpCodes.Ldarg_S)
            ))
        {
            LogHelper.Error($"Target not found!");
            return;
        }

        ILLabel normalPlay = c.DefineLabel();

        c.Emit(OpCodes.Ldarg_1);
        c.EmitDelegate(static (string soundName) =>
        {
            // For sounds with prefix: "se://<ID>" we will play them in a custom way.
            return soundName.StartsWith(GameSoundManagerAPI.CUSTOM_SOUND_ID_PREFIX, StringComparison.InvariantCultureIgnoreCase);
        });
        c.Emit(OpCodes.Brfalse, normalPlay);
        c.Emit(OpCodes.Ldarg_1);
        c.Emit(OpCodes.Ldarg_2);
        c.Emit(OpCodes.Ldarg_3);
        c.Emit(OpCodes.Ldarg_S, (byte)4);
        c.Emit(OpCodes.Ldarg_S, (byte)5);
        c.EmitDelegate(LoadCustomMusicClip);
        c.Emit(OpCodes.Ret);
        c.MarkLabel(normalPlay);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal static void LoadCustomMusicClip(string soundName, float gameVolume, float soundVolume, bool loop, bool followon)
    {
        try
        {
            if (!int.TryParse(soundName[5..], out int parsedId)) {
                LogHelper.Error($"Failed to parse id from: {soundName}");
                return;
            }
            if (SFXManager.instance.play_list.Count < parsedId)
            {
                LogHelper.Error($"Id is out of bounds: {parsedId}");
                return;
            }

            if (!followon)
            {
                MyAudioManager.Instance.musicClip = SFXManager.instance.play_list[parsedId].clip; //await LoadClip(path);
                MyAudioManager.Instance.music_GameVolume = gameVolume;
                MyAudioManager.Instance.music_SoundVolume = soundVolume;
                MyAudioManager.Instance.musicSource.loop = loop;
                MyAudioManager.Instance.musicSource.volume = gameVolume * soundVolume * ConfigSettings.Settings_MusicVolume * MyAudioManager.GetMasterVolume() * MyAudioManager.Instance.getFadedMusicVolume();
                MyAudioManager.Instance.musicSource.clip = MyAudioManager.Instance.musicClip;
                MyAudioManager.Instance.musicSource.Play();
                MyAudioManager.Instance.musicAboutToLoop = false;
                MyAudioManager.Instance.musicAboutToLoopTime = DateTime.MinValue;
                MyAudioManager.Instance.musicMode = 2;
            }
            else
            {
                MyAudioManager.Instance.nextMusicLoop = loop;
                MyAudioManager.Instance.nextMusic_SoundVolume = soundVolume;
                MyAudioManager.Instance.nextMusicClip = SFXManager.instance.play_list[parsedId].clip; //await LoadClip(path);
            }
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during custom music clip load, soundName={soundName}");
        }
    }

    internal ILHook? myAudioManager_LoadClip2_hook;

    /// <summary>
    /// TODO: This ILHook breaks english copies? Just by itself?
    /// </summary>
    /// <param name="ctx"></param>
    internal void MyAudioManager_LoadClip2_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        // extract folderField
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Stelem_Ref),
            x => x.Match(OpCodes.Dup),
            x => x.Match(OpCodes.Ldc_I4_5)
            ))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        object folderField = c.Instrs[c.Index].Operand;
        c.Index = 0;

        // extract soundName
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Stelem_Ref),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Stloc_2)
            ))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }
        object soundNameField = c.Instrs[c.Index].Operand;
        c.Index = 0;

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Brtrue_S),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Brtrue_S)
            ))
        {
            LogHelper.Error($"Target 3 not found!");
            return;
        }

        ILLabel seSpecialHandling = c.DefineLabel();

        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldfld, folderField);
        c.EmitDelegate(static (string folderName) =>
        {
            LogHelper.Debug($"folderName: {folderName}");

            // For folderName with "_SE_" we will modify the path
            return folderName.StartsWith("_SE_", StringComparison.InvariantCultureIgnoreCase);
        });
        c.Emit(OpCodes.Brtrue, seSpecialHandling);
        
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Ldstr),
            x => x.Match(OpCodes.Ldarg_0),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Ldarg_0)
            ))
        {
            LogHelper.Error($"Target 4 not found!");
            return;
        }

        c.MarkLabel(seSpecialHandling);
        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldfld, soundNameField);
        c.EmitDelegate(static (string path) =>
        {
            LogHelper.Debug($"soundName: {path}");
            return path;
        });
        c.Emit(OpCodes.Stloc_2);

        LogHelper.Verbose($"IL=[{ctx.ToString()}]");
    }
}
