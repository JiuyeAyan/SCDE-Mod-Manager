using Noesis;
using SHCDESE.Logging;

namespace SHCDESE.UI;

/// <summary>
/// Selects one of the SE tooltip size presets from the current Unity screen height. 
/// It is exposed as an attached property so a normal XAML style can opt into automatic scaling without requiring a custom tooltip control.
/// </summary>
public static class ToolTipResolutionScale
{
    private const int FOUR_K_MIN_HEIGHT = 2100;
    private const int FOURTEEN_FORTY_MIN_HEIGHT = 1300;
    private static int _lastLoggedScreenHeight = -1;

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(ToolTipResolutionScale), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ToolTip toolTip)
            return;

        toolTip.Opened -= OnToolTipOpened;

        if (!(bool)args.NewValue)
            return;

        toolTip.Opened += OnToolTipOpened;
        ApplyCurrentPreset(toolTip);
    }

    private static void OnToolTipOpened(object sender, RoutedEventArgs args)
    {
        if (sender is ToolTip toolTip)
        {
            ApplyCurrentPreset(toolTip);
        }
    }

    private static void ApplyCurrentPreset(ToolTip toolTip)
    {
        int screenHeight = UnityEngine.Screen.height;
        string presetName;

        if (screenHeight >= FOUR_K_MIN_HEIGHT)
        {
            ApplyPreset(toolTip, 36f, 920.0f, 26.0f, 20.0f, 5.0f);
            presetName = "4K";
        }
        else if (screenHeight >= FOURTEEN_FORTY_MIN_HEIGHT)
        {
            ApplyPreset(toolTip, 20f, 680.0f, 20.0f, 15.0f, 4.0f);
            presetName = "1440p";
        }
        else
        {
            ApplyPreset(toolTip, 12f, 520.0f, 16.0f, 12.0f, 3.0f);
            presetName = "1080p";
        }

        if (_lastLoggedScreenHeight == screenHeight)
            return;

        _lastLoggedScreenHeight = screenHeight;
        LogHelper.Debug($"SE tooltip auto-scale selected the {presetName} preset for {UnityEngine.Screen.width}x{screenHeight}.");
    }

    private static void ApplyPreset(ToolTip toolTip, float fontSize, float maxWidth, float horizontalPadding, float verticalPadding, float borderThickness)
    {
        toolTip.FontSize = fontSize;
        toolTip.MaxWidth = maxWidth;
        toolTip.Padding = new Thickness(horizontalPadding, verticalPadding, horizontalPadding, verticalPadding);
        toolTip.BorderThickness = new Thickness(borderThickness);
    }
}
