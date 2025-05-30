using System.Windows;
using System.Windows.Controls;

namespace H2MLauncher.UI.View.Controls
{
    public partial class FriendRequestsControl : UserControl
    {
        public FriendRequestsControl()
        {
            InitializeComponent();
        }

        private void ResetSearchTextButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
        }
    }
}
