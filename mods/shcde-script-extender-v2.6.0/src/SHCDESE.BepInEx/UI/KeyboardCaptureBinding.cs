using Noesis;
using SHCDESE.API;

namespace SHCDESE.UI;

/// <summary>
/// Allows TextBoxes to request keyboard input from Noesis.
/// </summary>
public static class KeyboardCaptureBinding
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(KeyboardCaptureBinding),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;

        if ((bool)e.NewValue)
        {
            tb.GotFocus += OnGotFocus;
            tb.LostFocus += OnLostFocus;
        }
        else
        {
            tb.GotFocus -= OnGotFocus;
            tb.LostFocus -= OnLostFocus;
        }
    }

    private static void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        bool previous = GamePlayerManagerAPI.Instance.GetNoesisHasKeyboard();
        tb.SetValue(PreviousKeyboardStateProperty, previous);
        GamePlayerManagerAPI.Instance.SetNoesisHasKeyboard(true);
    }

    private static void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        bool previous = (bool)tb.GetValue(PreviousKeyboardStateProperty);
        GamePlayerManagerAPI.Instance.SetNoesisHasKeyboard(previous);
    }

    // Stores the pre-focus keyboard state on the TextBox itself
    private static readonly DependencyProperty PreviousKeyboardStateProperty =
        DependencyProperty.RegisterAttached(
            "PreviousKeyboardState",
            typeof(bool),
            typeof(KeyboardCaptureBinding),
            new PropertyMetadata(false));
}