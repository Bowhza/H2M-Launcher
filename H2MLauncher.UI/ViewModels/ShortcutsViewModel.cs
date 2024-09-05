using System.Collections.ObjectModel;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ShortcutsViewModel : ObservableObject
    {
        public ShortcutViewModel RefreshServers { get; } = new()
        {
            Name = "RefreshServers",
            Key = Key.F5
        };

        public ShortcutViewModel Reconnect { get; } = new()
        {
            Name = "Reconnect",
            Key = Key.R,
            Modifiers = ModifierKeys.Control | ModifierKeys.Alt,
            IsHotkey = true
        };

        public ShortcutViewModel SaveFavourites { get; } = new()
        {
            Name = "SaveFavourites",
            Key = Key.S,
            Modifiers = ModifierKeys.Control
        };

        public ObservableCollection<ShortcutViewModel> Shortcuts { get; private set; }

        public ShortcutsViewModel()
        {
            Shortcuts = [RefreshServers, SaveFavourites, Reconnect];
        }

        public Dictionary<string, string> ToDictionary()
        {
            var converter = new KeyGestureConverter();

            return Shortcuts
                .Where(_ => _.IsKeySet)
                .DistinctBy(_ => _.Name.ToLower())
                .ToDictionary(_ => _.Name, _ => converter.ConvertToString(_.KeyGesture) ?? "");
        }

        public void ResetViewModel(IReadOnlyDictionary<string, string> keyBindings)
        {
            var converter = new KeyGestureConverter();

            Shortcuts = [Reconnect, RefreshServers];

            foreach ((string name, string gestureString) in keyBindings)
            {
                var gesture = (KeyGesture?)converter.ConvertFromString(gestureString);
                if (gesture is null)
                {
                    return;
                }

                ShortcutViewModel? existing = Shortcuts.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.Key = gesture.Key;
                    existing.Modifiers = gesture.Modifiers;
                }
                else
                {
                    Shortcuts.Add(new ShortcutViewModel()
                    {
                        Name = name,
                        Key = gesture.Key,
                        Modifiers = gesture.Modifiers
                    });
                }
            }
        }
    }
}
