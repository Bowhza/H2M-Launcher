using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
    }
}
