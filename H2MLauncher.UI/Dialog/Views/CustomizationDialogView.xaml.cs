using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class CustomizationDialogView : UserControl
    {
        public CustomizationDialogView()
        {
            InitializeComponent();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!decimal.TryParse(e.Text, NumberStyles.AllowDecimalPoint | NumberStyles.Float, CultureInfo.CurrentCulture, out _))
            {
                e.Handled = true;
            }
        }

        private void ListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Suppress selection on single click
            var item = VisualUpwardSearch<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (item != null && e.ClickCount == 1)
            {
                e.Handled = true;
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = VisualUpwardSearch<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void ListBoxItem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListBoxItem_GotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private static T? VisualUpwardSearch<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null && !(source is T))
                source = VisualTreeHelper.GetParent(source);

            return source as T;
        }
    }
}
