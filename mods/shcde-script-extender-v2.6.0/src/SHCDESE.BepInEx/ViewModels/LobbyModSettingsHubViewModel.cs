using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.Logging;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using UnityEngine;

namespace SHCDESE.ViewModels;

public class LobbyModSettingsHubViewModel : LobbyModSettingsBaseViewModel
{
    private ObservableCollection<LobbyModSettingsEntry> RegisteredModTabs => GameXAMLManagerAPI.Instance.RegisteredModSettings;

    /// <summary>Tabs matching <see cref="SearchText"/>, displayed by the sidebar.</summary>
    public ObservableCollection<LobbyModSettingsEntry> ModTabs { get; } = new();

    private LobbyModSettingsEntry? _selectedTab;
    public LobbyModSettingsEntry? SelectedTab
    {
        get => _selectedTab;
        set { _selectedTab = value; OnPropertyChanged(nameof(SelectedTab)); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            string next = value ?? string.Empty;
            if (string.Equals(_searchText, next, StringComparison.Ordinal))
                return;

            _searchText = next;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SearchHintVisibility));
            RefreshFilteredTabs();
        }
    }

    public Visibility SearchHintVisibility => string.IsNullOrEmpty(SearchText)
        ? Visibility.Visible
        : Visibility.Collapsed;

    private Visibility _windowVisibility = Visibility.Collapsed;
    public Visibility WindowVisibility
    {
        get => _windowVisibility;
        set { _windowVisibility = value; OnPropertyChanged(nameof(WindowVisibility)); }
    }
    public ICommand ToggleWindowCommand { get; }

    /// <summary>
    /// Re-evaluates <see cref="LobbyModSettingsBaseViewModel.IsHost"/> on this hub and on every registered mod ViewModel, so host-only UI reflects the current lobby role.
    /// </summary>
    public void RefreshHostStateOnAllTabs()
    {
        System_RefreshHostState();

        foreach (LobbyModSettingsEntry entry in RegisteredModTabs)
        {
            if (entry.ViewModel is LobbyModSettingsBaseViewModel vm)
                vm.System_RefreshHostState();
        }
    }

    public LobbyModSettingsHubViewModel()
    {
        RefreshFilteredTabs();

        // Mods normally register after the hub is constructed.
        RegisteredModTabs.CollectionChanged += (s, e) => RefreshFilteredTabs();
        InputR3EventHooks.OnKeyDown.Observable.Subscribe(HandleKeyDown);

        ToggleWindowCommand = new RelayCommand(() =>
        {
            if (WindowVisibility == Visibility.Visible)
            {
                CloseWindow();
                return;
            }

            WindowVisibility = Visibility.Visible;

            // Refresh selection when opening if nothing is selected
            if (SelectedTab == null && ModTabs.Count > 0)
                SelectedTab = ModTabs[0];

            RefreshHostStateOnAllTabs();
        });
    }

    private void CloseWindow() => WindowVisibility = Visibility.Collapsed;

    private void HandleKeyDown(UnityInputEventArgs args)
    {
        if (args.Phase != EventHookPhase.Pre ||
            args.Key != KeyCode.Escape ||
            WindowVisibility != Visibility.Visible)
        {
            return;
        }

        // Consume Escape so the lobby screen underneath the modal cannot react to it.
        args.Result = false;
        CloseWindow();
    }

    private void RefreshFilteredTabs()
    {
        LobbyModSettingsEntry? previousSelection = SelectedTab;
        string filter = SearchText.Trim();

        ModTabs.Clear();
        foreach (LobbyModSettingsEntry entry in RegisteredModTabs)
        {
            if (filter.Length == 0 || entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                ModTabs.Add(entry);
        }

        if (previousSelection != null && ModTabs.Contains(previousSelection))
        {
            SelectedTab = previousSelection;
        }
        else
        {
            SelectedTab = ModTabs.Count > 0 ? ModTabs[0] : null;
        }
    }
}
