using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace H2MLauncher.UI.View.Controls
{
    public partial class RecentPlayersControl : UserControl
    {
        public RecentPlayersControl()
        {
            InitializeComponent();
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
