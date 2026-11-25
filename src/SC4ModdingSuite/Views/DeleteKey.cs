using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SC4ModdingSuite.Views;

/// <summary>
/// Makes the DEL key run a list's own delete/remove action (user request: "DEL key would
/// delete stuff, it can speed up stuff"). Hooked on the list/grid itself, so it only fires
/// while that list has focus, and never while the user is typing in a text field inside it
/// (a DataGrid cell being edited, a NumericUpDown...) - DEL keeps deleting text there.
/// </summary>
public static class DeleteKey
{
    public static void Attach(Control list, Action onDelete)
    {
        list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Delete || e.KeyModifiers != KeyModifiers.None || IsInTextInput(e.Source))
            {
                return;
            }

            e.Handled = true;
            onDelete();
        };
    }

    /// <summary>Same as <see cref="Attach(Control, Action)"/> for the existing REMOVE/DELETE button's command.</summary>
    public static void Attach(Control list, Func<ICommand> command) =>
        Attach(list, () =>
        {
            var cmd = command();
            if (cmd.CanExecute(null))
            {
                cmd.Execute(null);
            }
        });

    private static bool IsInTextInput(object? source) =>
        source is TextBox || (source as Avalonia.Visual)?.FindAncestorOfType<TextBox>() is not null;
}
