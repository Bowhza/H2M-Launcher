using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI
{
    public partial class PasswordDialog : UserControl
    {
        public PasswordDialog()
        {
            InitializeComponent();

        }
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is not PasswordViewModel viewModel)
                return;

            viewModel.Password = PasswordInput.Password;

            Window.GetWindow(this).DialogResult = true;
            Window.GetWindow(this).Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).DialogResult = false;
            Window.GetWindow(this).Close();
        }
    }
}
