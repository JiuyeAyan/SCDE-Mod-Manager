using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.DebugMenu.ImMenu;
using SHCDESE.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using static UnityEngine.GraphicsBuffer;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    private static readonly Lazy<ILHooksManager> lazy = new Lazy<ILHooksManager>(() => new ILHooksManager());

    public static ILHooksManager Instance { get { return lazy.Value; } }

    private ILHooksManager()
    {

    }

    public void Apply()
    {
        LogHelper.Information($"Applying Managed IL Detours");

        configSettings_maxGameSpeed = new ILHook(typeof(ConfigSettings).GetMethod(nameof(ConfigSettings.loadSettingsFromString), BindingFlags.NonPublic | BindingFlags.Static), ConfigSettings_MaxGameSpeed_ILHook);
        director_IncreaseFrameRate = new ILHook(typeof(Director).GetMethod(nameof(Director.IncreaseFrameRate), BindingFlags.Public | BindingFlags.Instance), Director_IncreaseFrameRate_ILHook);
        director_DecreaseFrameRate = new ILHook(typeof(Director).GetMethod(nameof(Director.DecreaseFrameRate), BindingFlags.Public | BindingFlags.Instance), Director_DecreaseFrameRate_ILHook);
        director_SetEngineFrameRate = new ILHook(typeof(Director).GetMethod(nameof(Director.SetEngineFrameRate), BindingFlags.Public | BindingFlags.Instance), Director_SetEngineFrameRate_ILHook);

        mainViewModel_buttonEnterCreateTroop = new ILHook(typeof(MainViewModel).GetMethod(nameof(MainViewModel.ButtonEnterCreateTroop), BindingFlags.Public | BindingFlags.Instance), MainViewModel_ButtonEnterCreateTroop_ILHook);
        
        mapFileManager_mapFleSizeLimit = new ILHook(typeof(MapFileManager).GetMethod(nameof(MapFileManager.GetFileInfoFromFileName), BindingFlags.Public | BindingFlags.Instance), MapFileManager_GetFileInfoFromFileName_ILHook);
        hud_inGameMenu_ilhook = new ILHook(typeof(HUD_IngameMenu).GetMethod(nameof(HUD_IngameMenu.ButtonIngameMenuFunction), BindingFlags.Public | BindingFlags.Instance), HUD_InGameMenu_ButtonIngameMenuFunction_ILHook);
        
        front_multiplayer_update = new ILHook(typeof(FRONT_Multiplayer).GetMethod(nameof(FRONT_Multiplayer.Update), BindingFlags.Public | BindingFlags.Instance), FRONT_Multiplayer_Update_ILHook);
        front_multiplayer_buttonclicked = new ILHook(typeof(FRONT_Multiplayer).GetMethod(nameof(FRONT_Multiplayer.ButtonClicked), BindingFlags.Public | BindingFlags.Instance), FRONT_Multiplayer_ButtonClicked_ILHook);
        platform_multiplayer_processMessage = new ILHook(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.processMessage), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_ProcessMessage_ILHook);
        
        if (Plugin.Instance.LobbyIsolation.Value)
        {
            platform_multiplayer_CreateLobbyResult = new ILHook(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.CreateLobbyResult), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_CreateLobbyResult_ILHook);
            platform_multiplayer_JoinLobbyResult = new ILHook(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.JoinLobbyResult), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_JoinLobbyResult_ILHook);
            platform_multiplayer_GetLobbies = new ILHook(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.GetLobbies), BindingFlags.Public | BindingFlags.Instance), Platform_Multiplayer_GetLobbies_ILHook);
            platform_multiplayer_RequestLobbyListResult = new ILHook(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.RequestLobbyListResult), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_RequestLobbyListResult_ILHook);
        }
        gameMap_AddUpdateChimp = new ILHook(typeof(GameMap).GetMethod(nameof(GameMap.addUpdateChimp), BindingFlags.Public | BindingFlags.Instance), GameMap_AddUpdateChimp_ILHook);
        myAudioManager_PlayMusic = new ILHook(typeof(MyAudioManager).GetMethod(nameof(MyAudioManager.PlayMusic), BindingFlags.Public | BindingFlags.Instance), MyAudioManager_PlayMusic_ILHook);

        Type stateMachine = typeof(MyAudioManager).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance).First(t => t.Name.StartsWith("<LoadClip>d__"));
        myAudioManager_LoadClip2_hook = new ILHook(stateMachine.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance), MyAudioManager_LoadClip2_ILHook);

        // Hook SpriteLoader IEnumerator
        MethodInfo spriteLoadMethod = typeof(spriteLoader).GetMethod(nameof(spriteLoader.SpriteLoad), BindingFlags.Instance | BindingFlags.NonPublic);
        IteratorStateMachineAttribute iteratorAttr = spriteLoadMethod.GetCustomAttribute<IteratorStateMachineAttribute>();
        Type stateMachineType = iteratorAttr.StateMachineType;
        spriteLoader_LoadSprites = new ILHook(stateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic), SpriteLoader_LoadSprites_ILHook);

        onScreenText_UpdateOST_ilhook = new ILHook(typeof(OnScreenText).GetMethod(nameof(OnScreenText.updateOST), BindingFlags.Public | BindingFlags.Instance), OnScreenText_UpdateOST_ILHook);

        engineInterface_CopyPlayStateStruct_ilhook = new ILHook(typeof(EngineInterface).GetMethod(nameof(EngineInterface.CopyPlayStateStruct), BindingFlags.Public | BindingFlags.Static), EngineInterface_CopyPlayStateStruct_ILHook);

        if (Plugin.Instance.AllowChatInNonMultiplayer.Value)
        {
            keyManager_update_ilhook = new ILHook(typeof(KeyManager).GetMethod(nameof(KeyManager.Update), BindingFlags.NonPublic | BindingFlags.Instance), KeyManager_Update_ILHook);
        }

        if (Plugin.Instance.AllowMultipleInstances.Value)
        {
            steamManager_Awake_ilhook = new ILHook(typeof(SteamManager).GetMethod(nameof(SteamManager.Awake), BindingFlags.NonPublic | BindingFlags.Instance), SteamManager_Awake_ILHook);
        }

        gameData_setGameStatew_ilhook = new ILHook(typeof(GameData).GetMethod(nameof(GameData.setGameState), BindingFlags.Public | BindingFlags.Instance), gameData_setGameState_ILHook);
    }

    public void ApplyEarly()
    {
        LogHelper.Information($"Applying Managed Early IL Detours");
        
    }

    internal void Unload()
    {
        LogHelper.Information($"Unloading IL Hooks");

        engineInterface_CopyPlayStateStruct_ilhook?.Undo();
        onScreenText_UpdateOST_ilhook?.Undo();
        spriteLoader_LoadSprites?.Undo();
        myAudioManager_LoadClip2_hook?.Undo();
        myAudioManager_PlayMusic?.Undo();
        gameMap_AddUpdateChimp?.Undo();
        platform_multiplayer_RequestLobbyListResult?.Undo();
        platform_multiplayer_GetLobbies?.Undo();
        platform_multiplayer_JoinLobbyResult?.Undo();
        platform_multiplayer_CreateLobbyResult?.Undo();
        platform_multiplayer_processMessage?.Undo();
        front_multiplayer_buttonclicked?.Undo();
        front_multiplayer_update?.Undo();
        hud_inGameMenu_ilhook?.Undo();
        mapFileManager_mapFleSizeLimit?.Undo();
        mainViewModel_buttonEnterCreateTroop?.Undo();
        director_SetEngineFrameRate?.Undo();
        director_DecreaseFrameRate?.Undo();
        director_IncreaseFrameRate?.Undo();
        configSettings_maxGameSpeed?.Undo();
    }
}
