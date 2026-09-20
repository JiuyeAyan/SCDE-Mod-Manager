using CrusaderDE;
using Noesis;
using SHCDESE.API;
using System;

namespace SHCDESE.UI;

/// <summary>
/// Adds the host's advertised mod list to the multiplayer lobby list.
/// </summary>
public static class LobbyModToolTip
{
    private const int MAX_TREE_DEPTH = 50;

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(LobbyModToolTip), new PropertyMetadata(false, OnEnabledChanged));

    private static readonly DependencyProperty HoveredLobbyProperty = DependencyProperty.RegisterAttached("HoveredLobby", typeof(string), typeof(LobbyModToolTip), new PropertyMetadata(string.Empty));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ListView listView)
        {
            return;
        }

        listView.MouseMove -= OnMouseMove;
        listView.MouseLeave -= OnMouseLeave;

        if (!(bool)args.NewValue)
        {
            listView.ToolTip = null;
            return;
        }

        ToolTip toolTip = new ToolTip
        {
            Content = string.Empty,
            Visibility = Visibility.Collapsed,
        };
        if (listView.TryFindResource("SE_ToolTip_Scale") is Style style)
        {
            toolTip.Style = style;
        }

        listView.ToolTip = toolTip;
        listView.MouseMove += OnMouseMove;
        listView.MouseLeave += OnMouseLeave;
    }

    private static void OnMouseMove(object sender, MouseEventArgs args)
    {
        if (sender is not ListView listView || listView.ToolTip is not ToolTip toolTip)
        {
            return;
        }

        Visual? hit = (Visual?)VisualTreeHelper.HitTest(listView, Mouse.GetPosition(listView)).VisualHit;
        FileRow? row = FindLobbyRow(hit, listView);
        if (row?.lobby == null)
        {
            ClearToolTip(listView, toolTip);
            return;
        }

        string lobbyId = row.lobby.id.ToString();
        if (string.Equals((string)listView.GetValue(HoveredLobbyProperty), lobbyId, StringComparison.Ordinal))
        {
            return;
        }

        listView.SetValue(HoveredLobbyProperty, lobbyId);
        toolTip.Content = GameNetworkAPI.GetLobbyModsToolTip(row.lobby.id);
        toolTip.Visibility = Visibility.Visible;
    }

    private static void OnMouseLeave(object sender, MouseEventArgs args)
    {
        if (sender is ListView listView && listView.ToolTip is ToolTip toolTip)
        {
            ClearToolTip(listView, toolTip);
        }
    }

    private static FileRow? FindLobbyRow(Visual? source, ListView owner)
    {
        Visual? current = source;
        for (int depth = 0; current != null && depth < MAX_TREE_DEPTH; depth++)
        {
            if (current is ListViewItem item)
            {
                return item.DataContext as FileRow;
            }

            if (ReferenceEquals(current, owner))
            {
                break;
            }

            current = VisualTreeHelper.GetParent(current) as Visual;
        }

        return null;
    }

    private static void ClearToolTip(ListView listView, ToolTip toolTip)
    {
        listView.SetValue(HoveredLobbyProperty, string.Empty);
        toolTip.Content = string.Empty;
        toolTip.Visibility = Visibility.Collapsed;
    }
}
