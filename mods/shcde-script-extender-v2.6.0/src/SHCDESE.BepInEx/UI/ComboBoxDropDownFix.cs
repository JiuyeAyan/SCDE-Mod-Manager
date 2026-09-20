using Noesis;
using System;

namespace SHCDESE.UI;

/// <summary>
/// Works around a Noesis popup placement issue that makes a <see cref="ComboBox"/> drop-down
/// open and immediately close again.
/// <para>
/// When the drop-down does not fit below the ComboBox, Noesis nudges the popup back into the
/// view bounds, which can place it directly underneath the cursor. The mouse-release of the
/// very same click then lands on a popup item, selecting it and closing the drop-down. The
/// symptom is position dependent: the same ComboBox behaves normally near the top of a view
/// and misbehaves further down.
/// </para>
/// <para>
/// The fix is to shrink <c>MaxDropDownHeight</c> to the space actually available below the
/// control just before the drop-down opens, so the popup always stays below the click point
/// and is never repositioned. The value authored in XAML is remembered and used as an upper
/// bound, so this only ever shrinks a drop-down, never grows one.
/// </para>
/// </summary>
public static class ComboBoxDropDownFix
{
    /// <summary>Gap left between the bottom of the drop-down and the bottom of the view.</summary>
    private const float BOTTOM_MARGIN = 12.0f;

    /// <summary>Guard against pathological trees, mirrors GameXAMLManagerAPI.FindElementByName.</summary>
    private const int MAX_TREE_DEPTH = 50;

    /// <summary>
    /// Opt-out switch. Set <c>ui:ComboBoxDropDownFix.Enabled="False"</c> on a ComboBox to keep
    /// its authored <c>MaxDropDownHeight</c> untouched.
    /// </summary>
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(ComboBoxDropDownFix),
            new PropertyMetadata(true));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    /// <summary>
    /// Stores the height the mod author asked for, captured the first time we touch a ComboBox.
    /// Without this we would keep clamping against our own previously written value and the
    /// drop-down could never grow back when space becomes available again.
    /// </summary>
    private static readonly DependencyProperty AuthoredMaxHeightProperty =
        DependencyProperty.RegisterAttached(
            "AuthoredMaxDropDownHeight",
            typeof(float),
            typeof(ComboBoxDropDownFix),
            new PropertyMetadata(float.NaN));

    /// <summary>
    /// Applies the fix to every <see cref="ComboBox"/> inside <paramref name="root"/>, including
    /// ones generated later by item templates. Call once per view, after it has been loaded.
    /// </summary>
    /// <param name="root">Root of the view to protect, e.g. a registered mod settings view.</param>
    public static void Attach(FrameworkElement root)
    {
        if (root == null)
            return;

        // Tunnelling event, so this runs before the ComboBox toggles its drop-down open,
        // which is exactly when MaxDropDownHeight still has an effect on placement.
        root.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Re-walking the tree on each click (instead of hooking each ComboBox once) keeps
        // template-generated ComboBoxes covered without any bookkeeping.
        if (sender is FrameworkElement root)
            FitAll(root, 0);
    }

    private static void FitAll(DependencyObject parent, int depth)
    {
        if (parent == null || depth > MAX_TREE_DEPTH)
            return;

        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is ComboBox combo)
            {
                Fit(combo);
                continue; // no need to descend into the ComboBox's own template
            }

            FitAll(child, depth + 1);
        }
    }

    private static void Fit(ComboBox combo)
    {
        // Already open means this click is closing it, nothing to place.
        if (combo.IsDropDownOpen || !GetEnabled(combo))
            return;

        float authored = GetAuthoredMaxHeight(combo);

        FrameworkElement? root = GetVisualRoot(combo);
        if (root == null)
            return;

        float rootHeight = root.ActualHeight;
        float comboHeight = combo.ActualHeight;

        // Not laid out yet, leave the authored value alone rather than guessing.
        if (rootHeight <= 0.0f || comboHeight <= 0.0f)
            return;

        Point bottom = combo.TranslatePoint(new Point(0.0f, comboHeight), root);
        float available = rootHeight - bottom.Y - BOTTOM_MARGIN;

        combo.MaxDropDownHeight = available > 0.0f ? Math.Min(authored, available) : authored;
    }

    private static float GetAuthoredMaxHeight(ComboBox combo)
    {
        float stored = (float)combo.GetValue(AuthoredMaxHeightProperty);
        if (!float.IsNaN(stored))
            return stored;

        float authored = combo.MaxDropDownHeight;

        // No usable limit set in XAML: treat it as unbounded so we clamp purely to what fits.
        if (float.IsNaN(authored) || authored <= 0.0f)
            authored = float.PositiveInfinity;

        combo.SetValue(AuthoredMaxHeightProperty, authored);
        return authored;
    }

    /// <summary>
    /// Walks up to the topmost FrameworkElement so available space is measured against the
    /// whole Noesis view rather than the settings panel. This is the surface Noesis itself
    /// clamps popups to, and it adapts to resolution, UI scale and scroll position for free.
    /// </summary>
    private static FrameworkElement? GetVisualRoot(Visual visual)
    {
        FrameworkElement? root = visual as FrameworkElement;
        Visual current = visual;

        for (int i = 0; i < MAX_TREE_DEPTH; i++)
        {
            Visual? parent = VisualTreeHelper.GetParent(current) as Visual;
            if (parent == null)
                break;

            if (parent is FrameworkElement fe)
                root = fe;

            current = parent;
        }

        return root;
    }
}
