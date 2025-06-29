using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace H2MLauncher.UI.View.Controls
{
    public class DropDownButton : ToggleButton
    {
        public DropDownButton()
        {
            // Bind the ToogleButton.IsChecked property to the drop-down's IsOpen property
            Binding binding = new Binding("Menu.IsOpen");
            binding.Source = this;
            binding.Mode = BindingMode.OneWay;
            this.SetBinding(IsCheckedProperty, binding);
            DataContextChanged += (sender, args) =>
            {
                if (Menu != null)
                    Menu.DataContext = DataContext;
            };
        }
        
        public Popup Menu
        {
            get { return (Popup)GetValue(MenuProperty); }
            set { SetValue(MenuProperty, value); }
        }
        public static readonly DependencyProperty MenuProperty = DependencyProperty.Register("Menu",
            typeof(Popup), typeof(DropDownButton), new UIPropertyMetadata(null, OnMenuChanged));

        private static void OnMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var dropDownButton = (DropDownButton)d;
            var contextMenu = (Popup)e.NewValue;
            contextMenu.DataContext = dropDownButton.DataContext;
            
            if (contextMenu.PlacementTarget is null)
            {
                contextMenu.PlacementTarget = dropDownButton;
            }

            contextMenu.Placement = PlacementMode.Bottom;
        }

        protected override void OnClick()
        {
            if (Menu != null)
            {
                Menu.IsOpen = true;
            }
        }
    }
}
