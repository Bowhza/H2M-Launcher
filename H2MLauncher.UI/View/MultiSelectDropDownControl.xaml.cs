using System.Windows;
using System.Windows.Controls;

namespace H2MLauncher.UI.View.Controls
{
    public partial class MultiSelectDropDownControl : UserControl
    {
        public Dictionary<string, object> ItemsSource
        {
            get => (Dictionary<string, object>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public Dictionary<string, object> SelectedItems
        {
            get => (Dictionary<string, object>)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string DefaultText
        {
            get => (string)GetValue(DefaultTextProperty);
            set => SetValue(DefaultTextProperty, value);
        }

        public MultiSelectDropDownControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(Dictionary<string, object>),
            typeof(MultiSelectDropDownControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemsProperty =
           DependencyProperty.Register("SelectedItems", typeof(Dictionary<string, object>),
           typeof(MultiSelectDropDownControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty TextProperty =
           DependencyProperty.Register("Text", typeof(string), 
               typeof(MultiSelectDropDownControl), new UIPropertyMetadata(string.Empty));

        public static readonly DependencyProperty DefaultTextProperty =
            DependencyProperty.Register("DefaultText", typeof(string),
            typeof(MultiSelectDropDownControl), new UIPropertyMetadata(string.Empty));
    }
}
