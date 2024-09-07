using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

using NHotkey.Wpf;

namespace H2MLauncher.UI.ViewModels;

public partial class ShortcutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutDisplay))]
    [NotifyPropertyChangedFor(nameof(IsKeySet))]
    [NotifyPropertyChangedFor(nameof(KeyGesture))]
    private Key _key = Key.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortcutDisplay))]
    [NotifyPropertyChangedFor(nameof(IsKeySet))]
    [NotifyPropertyChangedFor(nameof(KeyGesture))]
    private ModifierKeys _modifiers = ModifierKeys.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHotkeyRegistered))]
    private bool _isHotkey = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHotkeyRegistered))]
    private bool _isHotkeyEnabled = true;

    [ObservableProperty]
    private bool _isEditing;

    public KeyGesture KeyGesture => new(Key, Modifiers);

    public bool IsKeySet => Key > 0 || Modifiers > 0;

    /// <summary>
    /// Whether the hotkey is currently registered in the system.
    /// </summary>
    public bool IsHotkeyRegistered => IsHotkey && IsHotkeyEnabled;

    public string ShortcutDisplay
    {
        get
        {
            if (!IsKeySet)
            {
                return IsEditing ? "" : "No hotkey set";
            }

            List<string> keys = [];

            if ((Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keys.Add("Ctrl");
            if ((Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keys.Add("Alt");
            if ((Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keys.Add("Shift");
            if ((Modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                keys.Add("⊞ Win");

            if (Key > 0)
            {
                keys.Add(Key.ToString());
            }

            // Combine modifiers and key
            string shortcut = string.Join(" + ", keys);
            if (!string.IsNullOrEmpty(shortcut))
            {
                return shortcut;
            }

            return "No hotkey set";
        }
    }
}
 