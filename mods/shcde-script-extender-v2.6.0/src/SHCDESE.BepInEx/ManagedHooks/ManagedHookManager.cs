using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;
using SHCDESE.ManagedHooks.Callbacks;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    private static readonly Lazy<ManagedHookManager> lazy = new Lazy<ManagedHookManager>(() => new ManagedHookManager());

    public static ManagedHookManager Instance { get { return lazy.Value; } }

    private ManagedHookManager()
    {
       
    }

    public void Apply()
    {
        try
        {
            LogHelper.Information($"Applying Managed Detours");
            gameData_getChimpGoldCost_hook = new ManagedDetour<gameData_getChimpGoldCost_delegate>(typeof(GameData).GetMethod(nameof(GameData.getChimpGoldCost), BindingFlags.Public | BindingFlags.Static), GameData_getChimpGoldCosts_Hook);


            mainViewModel_GetVersionString_hook = new Hook(typeof(MainViewModel).GetProperty(nameof(MainViewModel.VersionString), BindingFlags.Public | BindingFlags.Instance).GetGetMethod(), MainViewModel_GetVersionString_Hook);

            mapFileManager_getRadarFromFile_hook = new ManagedDetour<mapFileManager_getRadarFromFileDelegate>(typeof(MapFileManager).GetMethod(nameof(MapFileManager.GetRadarFromFile), BindingFlags.Public | BindingFlags.Instance), MapFileManager_GetRadarFromFile_Hook);

            translate_lookUpText_hook = new ManagedDetour<translate_lookUpTextDelegate>(typeof(Translate).GetMethod(nameof(Translate.lookUpText), BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null), Translate_lookUpText_Hook);
            translate_lookUpText2_hook = new ManagedDetour<translate_lookUpText2Delegate>(typeof(Translate).GetMethod(nameof(Translate.lookUpText), BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string), typeof(int) }, null), Translate_lookUpText2_Hook);
            translate_lookUpTextEx_hook = new ManagedDetour<translate_lookUpTextExDelegate>(typeof(Translate).GetMethod(nameof(Translate.lookUpText), BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Enums.eTextSections), typeof(int) }, null), Translate_lookUpTextEx_Hook);

            hud_MpChatMessages_ReceiveIngameChat_hook = new ManagedDetour<hud_MpChatMessages_ReceiveIngameChatDelegate>(typeof(HUD_MPChatMessages).GetMethod(nameof(HUD_MPChatMessages.recieveIngameChat), BindingFlags.Public | BindingFlags.Instance), HUD_MPChatMessages_ReceiveIngameChat_Hook);

            platform_Multiplayer_ChatHandle_hook = new ManagedDetour<platform_Multiplayer_ChatHandleDelegate>(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.ChatHandle), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_ChatHandle_Hook);
            platform_Multiplayer_SendLobbyChatMessage_hook = new ManagedDetour<platform_Multiplayer_SendLobbyChatMessageDelegate>(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.SendLobbyChatMessage), BindingFlags.Public | BindingFlags.Instance), Platform_Multiplayer_SendLobbyChatMessage_Hook);
            platform_Multiplayer_SendIngameChat_hook = new ManagedDetour<platform_Multiplayer_SendIngameChatDelegate>(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.SendIngameChat), BindingFlags.Public | BindingFlags.Instance), Platform_Multiplayer_SendIngameChat_Hook);
            platform_Multiplayer_receiveLobbyMessages_hook = new ManagedDetour<platform_Multiplayer_ReceiveLobbyMessagesDelegate>(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.receiveLobbyMessages), BindingFlags.NonPublic | BindingFlags.Instance), Platform_Multiplayer_ReceiveLobbyMessages_Hook);
            platform_Multiplayer_SendCustomInfoToMember_hook = new ManagedDetour<platform_Multiplayer_SendCustomInfoToMemberDelegate>(typeof(Platform_Multiplayer).GetMethod(nameof(Platform_Multiplayer.SendCustomInfoToMember), BindingFlags.Public | BindingFlags.Instance), Platform_Multiplayer_SendCustomInfoToMember_Hook);

            fatControler_noesisGuiUpdateChecksInGame_hook = new ManagedDetour<fatControler_noesisGuiUpdateChecksInGameDelegate>(typeof(FatControler).GetMethod(nameof(FatControler.NoesisGUIUpdateChecksInGame), BindingFlags.Public | BindingFlags.Instance), FatControler_NoesisGUIUpdateChecksInGame_Hook);

            hud_Buildings_CTOR_hook = new ManagedDetour<HUD_Buildings_CTORDelegate>(typeof(HUD_Buildings).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, [], null), HUD_Buildings_CTOR_Hook);

            gameMap_interpChimp_hook = new ManagedDetour<GameMap_InterpChimpDelegate>(typeof(GameMap).GetMethod(nameof(GameMap.interpChimp), BindingFlags.Public | BindingFlags.Instance), GameMap_InterpChimpHook);
            gameMap_deleteChimp_hook = new ManagedDetour<GameMap_DeleteChimpDelegate>(typeof(GameMap).GetMethod(nameof(GameMap.deleteChimp), BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Chimp), typeof(bool) }, null), GameMap_DeleteChimpHook);

            myAudioManager_playSFX_hook = new ManagedDetour<myAudioManager_playSFXDelegate>(typeof(MyAudioManager).GetMethod(nameof(MyAudioManager.playSFX), BindingFlags.Public | BindingFlags.Instance), MyAudioManager_PlaySFX_Hook);
            myAudioManager_loadClip_hook = new ManagedDetour<myAudioManager_loadClipDelegate>(typeof(MyAudioManager).GetMethod("LoadClip", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null), MyAudioManager_LoadClip_Hook);

            mapFileManager_updateWorkshopMap_hook = new ManagedDetour<mapFileManager_updateWorkshopMapDelegate>(typeof(MapFileManager).GetMethod(nameof(MapFileManager.UpdateWorkshopMap), BindingFlags.Instance | BindingFlags.NonPublic), MapFileManager_UpdateWorkshopMap_Hook);
            spriteMapping_SetBodySprite_hook = new ManagedDetour<SpriteMapping_SetBodySprite_delegate>(typeof(SpriteMapping).GetMethod(nameof(SpriteMapping.SetBodySprite), BindingFlags.Static | BindingFlags.Public), SpriteMapping_SetBodySprite_Hook);

            mainViewModel_SetVisibleState_hook = new ManagedDetour<mainViewModel_SetVisibleState_Delegate>(typeof(MainViewModel).GetMethod(nameof(MainViewModel.SetVisibleState), BindingFlags.Public | BindingFlags.Instance), MainViewModel_SetVisibleState_Hook);

            keyManager_Update_Hook = new ManagedDetour<KeyManager_Update_Delegate>(typeof(KeyManager).GetMethod(nameof(KeyManager.Update), BindingFlags.NonPublic | BindingFlags.Instance), KeyManager_Update_Hook_Impl);

            customisationFileManager_ProcessExtendedLordFile_hook = new ManagedDetour<customisationFileManager_ProcessExtendedLordFile_Delegate>(typeof(CustomisationFileManager).GetMethod(nameof(CustomisationFileManager.ProcessExtendedLordFile), BindingFlags.NonPublic | BindingFlags.Instance), CustomisationFileManager_ProcessExtendedLordFile_Hook);
            customisationFileManager_GetCustomLordText_hook = new ManagedDetour<customisationFileManager_GetCustomLordText_Delegate>(typeof(CustomisationFileManager).GetMethod(nameof(CustomisationFileManager.GetCustomLordText), BindingFlags.Public | BindingFlags.Instance), CustomisationFileManager_GetCustomLordText_Hook);

            front_Multiplayer_SkirmishAIAddClick_hook = new ManagedDetour<front_Multiplayer_SkirmishAIAddClick_Delegate>(typeof(FRONT_Multiplayer).GetMethod(nameof(FRONT_Multiplayer.SkirmishAIAddClick), BindingFlags.Public | BindingFlags.Instance), FRONT_Multiplayer_SkirmishAIAddClick_Hook);
            front_Multiplayer_AILordLeave_hook = new ManagedDetour<front_Multiplayer_AILordLeave_Delegate>(typeof(FRONT_Multiplayer).GetMethod(nameof(FRONT_Multiplayer.AILordLeave), BindingFlags.Public | BindingFlags.Instance), FRONT_Multiplayer_AILordLeave_Hook);

            mainViewModel_getAIFace_hook = new ManagedDetour<mainViewModel_getAIFace_Delegate>(typeof(MainViewModel).GetMethod(nameof(MainViewModel.getAIFace), BindingFlags.Public | BindingFlags.Instance), MainViewModel_GetAIFace_Hook);

            noesisMediaElement_Setter_hook = new ManagedDetour<noesisMediaElement_SetterDelegate>(typeof(NoesisApp.MediaElement).GetProperty(nameof(NoesisApp.MediaElement.Source), BindingFlags.Public | BindingFlags.Instance).GetSetMethod(false), NoesisMediaElement_Setter_Hook);
            sfxManager_PlaySpeech_hook = new ManagedDetour<SFXManager_PlaySpeech_Delegate>(typeof(SFXManager).GetMethod(nameof(SFXManager.playSpeech), BindingFlags.Public | BindingFlags.Instance), SFXManager_PlaySpeech_Hook);

            NoesisApp.MediaElement.SetCreateMediaPlayerCallback(new NoesisApp.CreateMediaPlayerCallback(NoesisCallbacks.CreateMediaPlayer), null);

            onScreenText_GetComputerName_hook = new ManagedDetour<onScreenText_GetComputerName_Delegate>(typeof(OnScreenText).GetMethod(nameof(OnScreenText.getComputerName), BindingFlags.Public | BindingFlags.Static), OnScreenText_GetComputerName_Hook);

            aivloader_GetAIVData_hook = new ManagedDetour<AIVLoader_GetAIVData_Delegate>(typeof(AIVLoader).GetMethod(nameof(AIVLoader.getAIVData), BindingFlags.Public | BindingFlags.Static), AIVLoader_GetAIVData_Hook);

            platform_Workshop_GetlistOfSubscribedItemPaths_hook = new ManagedDetour<platform_Workshop_GetListOfSubscribedItemsPaths_Delegate>(typeof(Platform_Workshop).GetMethod(nameof(Platform_Workshop.GetListOfSubscribedItemsPaths), BindingFlags.Public | BindingFlags.Instance), Platform_Workshop_GetListOfSubscribedItemsPaths_Hook);

            frontendMenus_ClearUIPanels_hook = new ManagedDetour<frontendMenus_ClearUIPanels_Delegate>(typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.ClearUIPanels), BindingFlags.Public | BindingFlags.Static), FrontendMenus_ClearUIPanels_Hook);
            frontendMenus_OpenFrontEndMenus_hook = new ManagedDetour<frontendMenus_OpenFrontEndMenus_Delegate>(typeof(FrontendMenus).GetMethod(nameof(FrontendMenus.OpenFrontEndMenus), BindingFlags.Public | BindingFlags.Static), FrontendMenus_OpenFrontEndMenus_Hook);

            gameTile_SetTileColor_hook = new ManagedDetour<GameTile_SetTileColor_Delegate>(typeof(gameTile).GetMethod(nameof(gameTile.setTileColour), BindingFlags.NonPublic | BindingFlags.Instance), GameTile_SetTileColor_Hook);

            steamManager_Awake_hook = new ManagedDetour<steamManager_Awake_Delegate>(typeof(SteamManager).GetMethod(nameof(SteamManager.Awake), BindingFlags.NonPublic | BindingFlags.Instance), SteamManager_Awake_Hook);

            director_Awake_hook = new ManagedDetour<director_Awake>(typeof(Director).GetMethod(nameof(Director.Awake), BindingFlags.NonPublic | BindingFlags.Instance), Director_Awake_Hook);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to apply managed hooks: {ex.Message}");
        }
    }

    public void ApplyEarly()
    {
        LogHelper.Information($"Applying Managed Early Detours");

        try
        {
            noesisXamlProvider_loadXaml_hook = new ManagedDetour<noesisXamlProvider_loadXamlDelegate>(typeof(NoesisXamlProvider).GetMethod(nameof(NoesisXamlProvider.LoadXaml), BindingFlags.Public | BindingFlags.Instance), NoesisXamlProvider_LoadXaml);
            noesisTextureProvider_GetTextureInfo_hook = new ManagedDetour<noesisTextureProvider_GetTextureInfoDelegate>(typeof(NoesisTextureProvider).GetMethod(nameof(NoesisTextureProvider.GetTextureInfo), new[] { typeof(Uri) }), NoesisTextureProvider_GetTextureInfo);
            noesisGUI_LoadComponent_Hook = new ManagedDetour<NoesisGUI_LoadComponent_Delegate>(typeof(Noesis.GUI).GetMethod(nameof(Noesis.GUI.LoadComponent), BindingFlags.Public | BindingFlags.Static), NoesisGUI_LoadComponent_Hook);

        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to apply managed early hooks: {ex.Message}");
        }

    }

    internal void Unload()
    {
        LogHelper.Information($"Unloading Managed Hooks");

        try
        {
            noesisXamlProvider_loadXaml_hook?.Hook?.Undo();
            noesisTextureProvider_GetTextureInfo_hook?.Hook?.Undo();
            noesisGUI_LoadComponent_Hook?.Hook?.Undo();
            gameTile_SetTileColor_hook?.Hook?.Undo();
            frontendMenus_OpenFrontEndMenus_hook?.Hook?.Undo();
            frontendMenus_ClearUIPanels_hook?.Hook?.Undo();
            platform_Workshop_GetlistOfSubscribedItemPaths_hook?.Hook?.Undo();
            aivloader_GetAIVData_hook?.Hook?.Undo();
            onScreenText_GetComputerName_hook?.Hook?.Undo();
            sfxManager_PlaySpeech_hook?.Hook?.Undo();
            noesisMediaElement_Setter_hook?.Hook?.Undo();
            mainViewModel_getAIFace_hook?.Hook?.Undo();
            customisationFileManager_ProcessExtendedLordFile_hook?.Hook?.Undo();
            front_Multiplayer_AILordLeave_hook?.Hook?.Undo();
            front_Multiplayer_SkirmishAIAddClick_hook?.Hook?.Undo();
            keyManager_Update_Hook?.Hook?.Undo();
            spriteMapping_SetBodySprite_hook?.Hook?.Undo();
            mapFileManager_updateWorkshopMap_hook?.Hook?.Undo();
            myAudioManager_loadClip_hook?.Hook?.Undo();
            myAudioManager_playSFX_hook?.Hook?.Undo();
            gameMap_interpChimp_hook?.Hook?.Undo();
            hud_Buildings_CTOR_hook?.Hook?.Undo();
            fatControler_noesisGuiUpdateChecksInGame_hook?.Hook?.Undo();
            platform_Multiplayer_SendCustomInfoToMember_hook?.Hook?.Undo();
            platform_Multiplayer_receiveLobbyMessages_hook?.Hook?.Undo();
            platform_Multiplayer_SendIngameChat_hook?.Hook?.Undo();
            platform_Multiplayer_SendLobbyChatMessage_hook?.Hook?.Undo();
            platform_Multiplayer_ChatHandle_hook?.Hook?.Undo();
            hud_MpChatMessages_ReceiveIngameChat_hook?.Hook?.Undo();
            translate_lookUpTextEx_hook?.Hook?.Undo();
            translate_lookUpText2_hook?.Hook?.Undo();
            translate_lookUpText_hook?.Hook?.Undo();
            mapFileManager_getRadarFromFile_hook?.Hook?.Undo();
            mainViewModel_GetVersionString_hook?.Undo();
            gameData_getChimpGoldCost_hook?.Hook?.Undo();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to unload managed hooks: {ex.Message}");
        }
    }
}
