using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class CustomizationDialogView : UserControl
    {
        public CustomizationDialogView()
        {
            InitializeComponent();
        }

        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (!decimal.TryParse(e.Text, NumberStyles.AllowDecimalPoint | NumberStyles.Float, CultureInfo.CurrentCulture, out _))
            {
                e.Handled = true;
            }
        }        
    }
}
