using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace H2MLauncher.UI.View.Controls
{
    public partial class FriendsControl : UserControl
    {
        public FriendsControl()
        {
            InitializeComponent();
        }

        private void ListBoxItem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListBoxItem_GotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void DetailsPopup_Opened(object sender, EventArgs e)
        {
            if (sender is not Popup detailsPopup) return;
            detailsPopup.Child.Focus();
        }

        private void DetailsPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
