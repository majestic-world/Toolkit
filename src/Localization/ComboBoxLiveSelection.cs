using Avalonia;
using Avalonia.Controls;

namespace L2Toolkit.Localization;

/// <summary>
/// What the closed ComboBox shows (presenter of the shared ComboBox theme in <c>App.axaml</c>). Avalonia copies the
/// selected <see cref="ComboBoxItem"/>'s <c>Content</c> into <see cref="ComboBox.SelectionBoxItem"/> only when the
/// selection changes, so a <c>{DynamicResource}</c> item text would keep the old language after a live switch.
/// <see cref="ContentProperty"/> tracks the selected item's current <c>Content</c> when it is plain data (text) and
/// falls back to <see cref="ComboBox.SelectionBoxItem"/> otherwise (items from <c>ItemsSource</c>, visual content).
/// Only this attached property is written: selection and <c>SelectionChanged</c> are never touched.
/// </summary>
public static class ComboBoxLiveSelection
{
    public static readonly AttachedProperty<object?> ContentProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, object?>("Content", typeof(ComboBoxLiveSelection));

    private static bool _registered;

    public static object? GetContent(ComboBox comboBox) => comboBox.GetValue(ContentProperty);

    /// <summary>Hooks every ComboBox in the app; call once before any window is created.</summary>
    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;

        ComboBox.SelectedItemProperty.Changed.AddClassHandler<ComboBox>((comboBox, _) => Update(comboBox));
        ComboBox.SelectionBoxItemProperty.Changed.AddClassHandler<ComboBox>((comboBox, _) => Update(comboBox));
        // Parent (logical), not ItemsControlFromItemContainer: the latter needs the popup realized, and a selected
        // item of a never-opened ComboBox must update too.
        ContentControl.ContentProperty.Changed.AddClassHandler<ComboBoxItem>((item, _) =>
        {
            if (item.Parent is ComboBox comboBox && ReferenceEquals(comboBox.SelectedItem, item))
                Update(comboBox);
        });
    }

    private static void Update(ComboBox comboBox) =>
        comboBox.SetValue(ContentProperty,
            comboBox.SelectedItem is ComboBoxItem { Content: { } content and not Control }
                ? content
                : comboBox.SelectionBoxItem);
}
