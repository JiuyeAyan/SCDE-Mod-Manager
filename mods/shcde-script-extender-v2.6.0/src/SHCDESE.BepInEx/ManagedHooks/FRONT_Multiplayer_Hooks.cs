using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.AI;
using SHCDESE.Logging;
using System;
using System.ComponentModel;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    internal static ManagedDetour<front_Multiplayer_SkirmishAIAddClick_Delegate> front_Multiplayer_SkirmishAIAddClick_hook;
    internal delegate void front_Multiplayer_SkirmishAIAddClick_Delegate(FRONT_Multiplayer instance, string param);

    internal static ManagedDetour<front_Multiplayer_AILordLeave_Delegate> front_Multiplayer_AILordLeave_hook;
    internal delegate void front_Multiplayer_AILordLeave_Delegate(FRONT_Multiplayer instance, string param);

    private ListView? customLordDetailsList;
    private FRONT_Multiplayer? customLordDetailsOwner;
    private MainViewModel? customLordDetailsViewModel;
    private bool customLordDetailsOwned;
    private int customLordDetailsGeneration;

    internal void FRONT_Multiplayer_SkirmishAIAddClick_Hook(FRONT_Multiplayer instance, string param)
    {
        bool opensCustomLordPicker = string.Equals(param, "98", StringComparison.Ordinal);
        if (opensCustomLordPicker)
        {
            try
            {
                DetachCustomLordDetailsHandler(restoreVanilla: true);
            }
            catch (Exception ex)
            {
                // Detail cleanup must never prevent Vanilla from opening the picker.
                LogHelper.Error(ex, "Exception while resetting custom lord details");
                DetachCustomLordDetailsHandler();
            }
        }

        front_Multiplayer_SkirmishAIAddClick_hook.Trampoline(instance, param);

        if (!opensCustomLordPicker || instance.RefCustomLordList == null)
            return;

        try
        {
            customLordDetailsOwner = instance;
            customLordDetailsList = instance.RefCustomLordList;
            customLordDetailsViewModel = MainViewModel.Instance;
            instance.RefCustomLordList.SelectionChanged += CustomLordList_SelectionChanged;
            customLordDetailsViewModel.PropertyChanged += CustomLordDetailsViewModel_PropertyChanged;
            UpdateSelectedCustomLordDetails(instance);

            // Noesis can realize the initially highlighted row after this click handler returns.
            // Recheck once on the next UI tick without retaining a stale picker instance.
            int generation = customLordDetailsGeneration;
            ListView list = instance.RefCustomLordList;
            UnityMainThreadDispatcher.Instance.EnqueueDeferred(() =>
                UpdateInitialCustomLordDetails(instance, list, generation));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception while initializing custom lord details");
        }
    }

    internal void FRONT_Multiplayer_AILordLeave_Hook(FRONT_Multiplayer instance, string param)
    {
        try
        {
            // The XAML passes "-1" for every lord-button MouseLeave and Vanilla ignores the
            // parameter. Hiding the picker button must not clear the active custom-lord panel.
            if (IsTrackedCustomLordPickerVisible(instance))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            // On any tracking failure, retain Vanilla's leave behavior.
            LogHelper.Error(ex, "Exception while checking custom lord detail visibility");
        }

        front_Multiplayer_AILordLeave_hook.Trampoline(instance, param);
    }

    private void CustomLordList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        try
        {
            FRONT_Multiplayer? instance = customLordDetailsOwner;
            if (instance == null ||
                !ReferenceEquals(sender, customLordDetailsList) ||
                !ReferenceEquals(sender, instance.RefCustomLordList))
            {
                return;
            }

            UpdateSelectedCustomLordDetails(instance);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception while displaying custom lord details");
        }
    }

    private void CustomLordDetailsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        bool panelVisibilityChanged = string.Equals(
                                          args.PropertyName,
                                          nameof(MainViewModel.Show_AddAIPanel_Custom),
                                          StringComparison.Ordinal) ||
                                      string.Equals(
                                          args.PropertyName,
                                          nameof(MainViewModel.Show_AddAIPanel),
                                          StringComparison.Ordinal);
        if (!panelVisibilityChanged)
        {
            return;
        }

        try
        {
            MainViewModel? viewModel = customLordDetailsViewModel;
            if (viewModel == null ||
                !ReferenceEquals(sender, viewModel) ||
                (viewModel.Show_AddAIPanel && viewModel.Show_AddAIPanel_Custom))
            {
                return;
            }

            FRONT_Multiplayer? owner = customLordDetailsOwner;
            DetachCustomLordDetailsHandler();
            // The picker is closed, so restore Vanilla's delayed rollover clear.
            owner?.AILordLeave("-1");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception while closing custom lord details");
            DetachCustomLordDetailsHandler();
        }
    }

    internal void DetachCustomLordDetailsHandler(bool restoreVanilla = false)
    {
        customLordDetailsGeneration++;

        ListView? list = customLordDetailsList;
        FRONT_Multiplayer? owner = customLordDetailsOwner;
        MainViewModel? viewModel = customLordDetailsViewModel;
        bool owned = customLordDetailsOwned;

        customLordDetailsOwner = null;
        customLordDetailsList = null;
        customLordDetailsViewModel = null;
        customLordDetailsOwned = false;

        if (list != null)
            list.SelectionChanged -= CustomLordList_SelectionChanged;
        if (viewModel != null)
            viewModel.PropertyChanged -= CustomLordDetailsViewModel_PropertyChanged;

        if (restoreVanilla && owned && owner != null)
            owner.AILordEnter("98");
    }

    private bool IsTrackedCustomLordPickerVisible(FRONT_Multiplayer instance)
    {
        return customLordDetailsOwned &&
               ReferenceEquals(instance, customLordDetailsOwner) &&
               customLordDetailsList != null &&
               ReferenceEquals(customLordDetailsList, instance.RefCustomLordList) &&
               (customLordDetailsList.SelectedItem as FileRow)?.lord != null &&
               customLordDetailsViewModel?.Show_AddAIPanel == true &&
               customLordDetailsViewModel?.Show_AddAIPanel_Custom == true;
    }

    private void UpdateInitialCustomLordDetails(
        FRONT_Multiplayer instance,
        ListView list,
        int generation)
    {
        try
        {
            if (generation != customLordDetailsGeneration ||
                !ReferenceEquals(instance, customLordDetailsOwner) ||
                !ReferenceEquals(list, customLordDetailsList) ||
                !ReferenceEquals(list, instance.RefCustomLordList))
            {
                return;
            }

            UpdateSelectedCustomLordDetails(instance);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception while displaying initial custom lord details");
        }
    }

    private void UpdateSelectedCustomLordDetails(FRONT_Multiplayer instance)
    {
        CustomisationFileManager.CustomLord? lord =
            (instance.RefCustomLordList.SelectedItem as FileRow)?.lord;
        if (lord == null || string.IsNullOrWhiteSpace(lord.lordName))
        {
            RestoreVanillaCustomLordDetails(instance);
            return;
        }

        // Clear every field first so switching between registered lords cannot retain stale values.
        instance.AILordEnter("98");

        GameAIManagerAPI api = GameAIManagerAPI.Instance;
        bool registered = api.TryGetLordDetails(lord.lordName, out LordDetails? details);
        MainViewModel viewModel = MainViewModel.Instance;
        string displayName = !string.IsNullOrWhiteSpace(lord.lordDisplayName)
            ? lord.lordDisplayName
            : lord.lordName ?? string.Empty;
        if (registered &&
            api.TryGetDisplayName(lord.lordName, out string? localizedName) &&
            !string.IsNullOrWhiteSpace(localizedName) &&
            !string.Equals(localizedName, "not-set", StringComparison.Ordinal))
        {
            displayName = localizedName;
        }

        viewModel.SkirmishLordRolloverName = displayName;
        viewModel.SkirmishLordRolloverName2 = string.Empty;
        viewModel.SkirmishLordRolloverDesc = details?.Description ?? string.Empty;
        int lordPower = GetLordPower(lord);
        string difficultyRating = details?.DifficultyRating ?? string.Empty;
        viewModel.SkirmishLordRolloverRating = string.IsNullOrWhiteSpace(difficultyRating)
            ? $"({lordPower})"
            : $"({lordPower}) {difficultyRating}";
        viewModel.SkirmishLordRolloverTroops = details?.FavouriteTroops ?? string.Empty;
        viewModel.SkirmishLordRolloverCastle = details?.Castles ?? string.Empty;
        viewModel.SkirmishLordRolloverStyle = details?.PlayStyle ?? string.Empty;
        viewModel.SkirmishLordRolloverSaying = details?.FavouriteSaying ?? string.Empty;
        viewModel.SkirmishLordRolloverSayingOpacity =
            string.IsNullOrWhiteSpace(details?.FavouriteSaying) ? 0f : 1f;

        ImageSource? portrait = null;
        if (registered && api.TryGetFace(lord.lordName, out ImageSource? face) && face != null)
            portrait = face;
        else if (lord.image != null)
            portrait = lord.image; // Vanilla's validated 144x144 avatar.png.

        if (portrait != null)
            viewModel.SkirmishLordRolloverFace = portrait;

        viewModel.Show_AddAIPanel_Rollover = true;
        customLordDetailsOwned = true;
    }

    private static int GetLordPower(CustomisationFileManager.CustomLord lord)
    {
        // Vanilla selects configs[0] when a custom lord is added to the player list.
        return lord.configs != null && lord.configs.Count > 0
            ? lord.configs[0].lordData.lord_power_display_level
            : 0;
    }

    private void RestoreVanillaCustomLordDetails(FRONT_Multiplayer instance)
    {
        if (!customLordDetailsOwned)
            return;

        instance.AILordEnter("98");
        customLordDetailsOwned = false;
    }
}
