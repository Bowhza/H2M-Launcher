using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI.View.Controls
{
    public partial class MagicNoSelectDropDownControl : UserControl
    {
        public Dictionary<string, MapPackItem> ItemsSource
        {
            get => (Dictionary<string, MapPackItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public MagicNoSelectDropDownControl()
        {
            InitializeComponent();
        }

        public bool IsDropDownOpen
        {
            get { return (bool)GetValue(IsDropDownOpenProperty); }
            set { SetValue(IsDropDownOpenProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsDropDownOpen.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register("IsDropDownOpen", typeof(bool), typeof(MagicNoSelectDropDownControl), new PropertyMetadata(false));



        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(Dictionary<string, MapPackItem>),
            typeof(MagicNoSelectDropDownControl), new UIPropertyMetadata(null));

        public static readonly DependencyProperty TextProperty =
           DependencyProperty.Register("Text", typeof(string), 
               typeof(MagicNoSelectDropDownControl), new UIPropertyMetadata(string.Empty));



        public int MaxDropDownHeight
        {
            get { return (int)GetValue(MaxDropDownHeightProperty); }
            set { SetValue(MaxDropDownHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MaxDropDownHeight.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxDropDownHeightProperty =
            DependencyProperty.Register("MaxDropDownHeight", typeof(int), typeof(MagicNoSelectDropDownControl), new PropertyMetadata(100));


        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemTemplate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(MagicNoSelectDropDownControl));
    }
}
