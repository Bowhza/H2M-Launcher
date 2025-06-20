using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Microsoft.Xaml.Behaviors;

namespace H2MLauncher.UI
{
    public class ScrollIntoViewBehavior : Behavior<Selector>
    {
        /// <summary>
        ///  When Behavior is attached
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += AssociatedObject_SelectionChanged;
        }

        /// <summary>
        /// On Selection Changed
        /// </summary>
        void AssociatedObject_SelectionChanged(object sender,
                                               SelectionChangedEventArgs e)
        {
            if (sender is not Selector selector)
            {
                return;
            }

            if (selector.SelectedItem is null)
            {
                return;
            }

            selector.Dispatcher.BeginInvoke(() =>
            {
                selector.UpdateLayout();
                if (selector.SelectedItem is null) return;

                switch (selector)
                {
                    case DataGrid dataGrid: 
                        dataGrid.ScrollIntoView(selector.SelectedItem);
                        break;
                    case ListBox listBox:
                        listBox.ScrollIntoView(selector.SelectedItem);
                        break;
                }                
            });
        }

        /// <summary>
        /// When behavior is detached
        /// </summary>
        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.SelectionChanged -= AssociatedObject_SelectionChanged;
        }
    }
}
