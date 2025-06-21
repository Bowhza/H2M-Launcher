using System.Windows;
using System.Windows.Controls;

namespace H2MLauncher.UI.View.Controls
{
    public class CustomExpander : Expander
    {
        public static readonly DependencyProperty ShowExpandArrowProperty =
            DependencyProperty.Register(nameof(ShowExpandArrow), typeof(bool), typeof(CustomExpander), new PropertyMetadata(true));

        /// <summary>
        /// Whether to show the expand arrow or not. Does not influence whether the expander can be toggled.
        /// </summary>
        public bool ShowExpandArrow
        {
            get { return (bool)GetValue(ShowExpandArrowProperty); }
            set { SetValue(ShowExpandArrowProperty, value); }
        }

        static CustomExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomExpander),
                new FrameworkPropertyMetadata(typeof(CustomExpander)));
        }
    }
}