using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

    public static class ScrollToTopBehavior
    {
        public static readonly DependencyProperty ScrollToTopProperty =
            DependencyProperty.RegisterAttached
            (
                "ScrollToTop",
                typeof(bool),
                typeof(ScrollToTopBehavior),
                new UIPropertyMetadata(false, OnScrollToTopPropertyChanged)
            );
        public static bool GetScrollToTop(DependencyObject obj)
        {
            return (bool)obj.GetValue(ScrollToTopProperty);
        }
        public static void SetScrollToTop(DependencyObject obj, bool value)
        {
            obj.SetValue(ScrollToTopProperty, value);
        }
        private static void OnScrollToTopPropertyChanged(DependencyObject dpo, DependencyPropertyChangedEventArgs e)
        {
            ItemsControl? itemsControl = dpo as ItemsControl;
            if (itemsControl is not null)
            {
                DependencyPropertyDescriptor dependencyPropertyDescriptor =
                        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ItemsControl));
                if (dependencyPropertyDescriptor != null)
                {
                    if ((bool)e.NewValue == true)
                    {
                        dependencyPropertyDescriptor.AddValueChanged(itemsControl, ItemsSourceChanged);
                    }
                    else
                    {
                        dependencyPropertyDescriptor.RemoveValueChanged(itemsControl, ItemsSourceChanged);
                    }
                }
            }
        }
        static void ItemsSourceChanged(object? sender, EventArgs e)
        {
            if (sender is not ItemsControl itemsControl) return;

            EventHandler? eventHandler = null;
            eventHandler = new(delegate
            {
                if (itemsControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    ScrollViewer? scrollViewer = GetVisualChild<ScrollViewer>(itemsControl) as ScrollViewer;
                    scrollViewer?.ScrollToTop();
                    itemsControl.ItemContainerGenerator.StatusChanged -= eventHandler;
                }
            });
            itemsControl.ItemContainerGenerator.StatusChanged += eventHandler;
        }

        private static T? GetVisualChild<T>(DependencyObject parent) where T : Visual
        {
            T? child = default;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
    }
}
