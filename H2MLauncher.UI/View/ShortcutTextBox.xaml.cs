using System.Windows.Controls;
using System.Windows.Input;

using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI.View.Controls
{
    public partial class ShortcutTextBox : UserControl
    {
        public ShortcutTextBox()
        {
            InitializeComponent();
        }
        private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (textBox.DataContext is not ShortcutViewModel shortcutViewModel)
            {
                return;
            }

            if (!shortcutViewModel.IsEditing)
            {
                return;
            }

            e.Handled = true;

            shortcutViewModel.Key = GetRealKey(e.Key, e.SystemKey);
            shortcutViewModel.Modifiers = Keyboard.Modifiers;
        }

        private static Key GetRealKey(Key key, Key systemKey = Key.None)
        {
            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LWin:
                case Key.RWin:
                    return Key.None;
                case Key.System:
                    return GetRealKey(systemKey);
                default:
                    return key;
            };
        }

        private void ShortcutTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (textBox.DataContext is not ShortcutViewModel shortcutViewModel)
            {
                return;
            }

            if (!shortcutViewModel.IsEditing)
            {
                return;
            }

            shortcutViewModel.IsEditing = false;
        }

        private void ShortcutTextBox_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (textBox.DataContext is not ShortcutViewModel shortcutViewModel)
            {
                return;
            }

            if (!shortcutViewModel.IsEditing)
            {
                return;
            }

            try
            {
                if (Keyboard.Modifiers is ModifierKeys.None &&
                    (shortcutViewModel.Key is Key.None ||
                    !Keyboard.GetKeyStates(shortcutViewModel.Key).HasFlag(KeyStates.Down)))
                {
                    shortcutViewModel.IsEditing = false;
                }
            }
            catch { }
        }
    }
}
