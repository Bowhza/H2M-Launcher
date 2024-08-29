using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class FilterDialogView : UserControl
    {
        public FilterDialogView()
        {
            InitializeComponent();
        }

        private void TextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!ExcludeFilterComboBox.HasItems)
            {
                return;
            }
            ExcludeFilterComboBox.IsDropDownOpen = true;
        }

        private void NewExcludeFilterTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DataContext is not ServerFilterViewModel viewModel)
            {
                return;
            }

            if (sender is not TextBox textBox)
            {
                return;
            }

            if (e.Key is not System.Windows.Input.Key.Enter)
            {
                return;
            }

            if (viewModel.AddNewExcludeKeywordCommand.CanExecute(textBox.Text))
            {
                viewModel.AddNewExcludeKeywordCommand.Execute(textBox.Text);                
                textBox.Text = "";

                if (!ExcludeFilterComboBox.IsDropDownOpen)
                {
                    ExcludeFilterComboBox.IsDropDownOpen = true;
                }
            }

            e.Handled = true;
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ExcludeFilterComboBox.IsDropDownOpen = true;
        }
    }
}
