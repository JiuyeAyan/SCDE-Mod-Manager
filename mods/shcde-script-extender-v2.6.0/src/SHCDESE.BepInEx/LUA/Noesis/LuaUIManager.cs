using Noesis;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using static Enums;

namespace SHCDESE.LUA.Noesis;

public class LuaUIManagerAPI
{
    private static readonly Lazy<LuaUIManagerAPI> lazy = new Lazy<LuaUIManagerAPI>(() => new LuaUIManagerAPI());
    public static LuaUIManagerAPI Instance { get { return lazy.Value; } }

    // We track the injected elements so we can remove them when the map unloads
    private List<FrameworkElement> _injectedElements = new List<FrameworkElement>();

    private LuaUIManagerAPI() { }

    public LuaViewModel CreateViewModel() => new LuaViewModel();

    /// <summary>
    /// Injects a XAML snippet into the live game UI.
    /// </summary>
    /// <param name="anchorName">The x:Name of an existing element to use as an anchor (e.g., "MainHUD").</param>
    /// <param name="xamlFragment">The string XAML content.</param>
    /// <param name="viewModel">The data container.</param>
    /// <param name="mode">0 = Child, 1 = Replace, 2 = AddToParent</param>
    internal bool InjectXamlInternal(string anchorName, string xamlFragment, LuaViewModel viewModel, int mode = 0)
    {
        try
        {
            LuaNoesisInjectionMode injectionMode = (LuaNoesisInjectionMode)mode;

            // Find the Anchor
            FrameworkElement? target = GameXAMLManagerAPI.Instance.FindGlobalElement(anchorName);
            if (target == null)
            {
                LogHelper.Error($"Anchor element [{anchorName}] not found");
                return false;
            }

            // Resolve the actual container based on mode
            FrameworkElement container = target;

            if (injectionMode == LuaNoesisInjectionMode.AddToParent)
            {
                if (target.Parent is FrameworkElement parentFe)
                {
                    container = parentFe;
                    LogHelper.Debug($"Anchored to [{anchorName}], injecting into parent [{parentFe.GetType().Name}]");
                }
                else
                {
                    LogHelper.Error($"Anchor [{anchorName}] has no valid parent");
                    return false;
                }
            }

            // Parse XAML
            object parsed = GUI.ParseXaml(xamlFragment);
            if (parsed is not FrameworkElement newElement)
            {
                LogHelper.Error("XAML fragment must be a FrameworkElement");
                return false;
            }

            // Bind Data
            if (viewModel != null)
            {
                newElement.DataContext = viewModel;
            }

            // Add to Container
            bool success = false;

            if (container is Panel panel)
            {
                panel.Children.Add(newElement);
                success = true;
            }
            else if (container is Decorator decorator)
            {
                decorator.Child = newElement;
                success = true;
            }
            else if (container is ContentControl contentControl)
            {
                contentControl.Content = newElement;
                success = true;
            }
            else
            {
                LogHelper.Error($"Container type [{container.GetType().Name}] is not supported for injection");
            }

            if (success)
            {
                _injectedElements.Add(newElement);
                LogHelper.Information($"Injected successfully near [{anchorName}]");
            }

            return success;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Injection Exception");
            return false;
        }
    }

    /// <summary>
    /// Injects a XAML snippet into the live game UI.
    /// </summary>
    /// <param name="anchorName">The x:Name of an existing element to use as an anchor (e.g., "MainHUD").</param>
    /// <param name="xamlFragment">The string XAML content.</param>
    /// <param name="viewModel">The data container.</param>
    /// <param name="mode">0 = Child, 1 = Replace, 2 = AddToParent</param>
    public void InjectXaml(string anchorName, string xamlFragment, LuaViewModel viewModel, int mode = 0)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            InjectXamlInternal(anchorName, xamlFragment, viewModel, mode);
        });
    }

    /// <summary>
    /// Removes all elements injected by Lua. Call this on Map Unload.
    /// </summary>
    public void ClearAll()
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            ClearAllInternal();
        });
    }

    /// <summary>
    /// Removes all elements injected by Lua. Call this on Map Unload.
    /// </summary>
    internal void ClearAllInternal()
    {
        foreach (FrameworkElement element in _injectedElements)
        {
            if (element == null)
                continue;

            if (element.Parent is Panel panel)
            {
                panel.Children.Remove(element);
            }
            else if (element.Parent is ContentControl cc)
            {
                cc.Content = null;
            }
            else if (element.Parent is Decorator dec)
            {
                dec.Child = null;
            }
        }
        _injectedElements.Clear();
        LogHelper.Information("Cleared all injected elements.");
    }
}