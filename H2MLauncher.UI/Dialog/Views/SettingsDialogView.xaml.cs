using System.Windows.Controls;

using NHotkey.Wpf;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class SettingsDialogView : UserControl
    {
        public SettingsDialogView()
        {
            InitializeComponent();

            this.Loaded += SettingsDialogView_Loaded;
            this.Unloaded += SettingsDialogView_Unloaded;
        }

        private void SettingsDialogView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // disable hotkeys when settings are open to not interfer with chaning key bindings
            HotkeyManager.Current.IsEnabled = false;
        }

        private void SettingsDialogView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            HotkeyManager.Current.IsEnabled = true;
        }
    }
}
