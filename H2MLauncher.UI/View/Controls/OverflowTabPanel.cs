using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace H2MLauncher.UI.View.Controls
{
    public class OverflowTabPanel : TabPanel
    {
        // Dependency Property or event to notify the TabControl about overflowed tabs
        public static readonly DependencyProperty OverflowedItemsProperty =
            DependencyProperty.Register("OverflowedItems", typeof(IEnumerable<TabItem>), typeof(OverflowTabPanel), new PropertyMetadata(null));

        public IEnumerable<TabItem> OverflowedItems
        {
            get { return (IEnumerable<TabItem>)GetValue(OverflowedItemsProperty); }
            set { SetValue(OverflowedItemsProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double totalDesiredWidth = 0;
            double maxHeight = 0;
            List<TabItem> visibleTabs = new List<TabItem>();
            List<TabItem> overflowedTabs = new List<TabItem>();

            // First pass: Measure all children to determine their desired sizes
            // and the total desired width if all were visible.
            foreach (UIElement child in InternalChildren)
            {
                TabItem? tabItem = child as TabItem;
                if (tabItem is not null)
                {
                    // Measure with infinite width to get the tab's natural desired width
                    child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                    totalDesiredWidth += child.DesiredSize.Width;
                    maxHeight = Math.Max(maxHeight, child.DesiredSize.Height); // Keep track of max height for the panel
                }
            }

            // Second pass: Determine which tabs are visible and which are overflowed
            // based on the availableSize.Width
            double currentWidth = 0;
            foreach (UIElement child in InternalChildren)
            {
                TabItem? tabItem = child as TabItem;
                if (tabItem is not null)
                {
                    // If the tab fits within the available width, add it to visible tabs.
                    // Special handling for the case where availableSize.Width is infinite (e.g., in a ScrollViewer)
                    // In a TabControl header, availableSize.Width typically won't be infinite unless
                    // it's hosted in something like a horizontal StackPanel without limits.
                    // For a TabControl, the TabPanel will typically be limited by the TabControl's width.

                    if (double.IsInfinity(availableSize.Width) || (currentWidth + child.DesiredSize.Width <= availableSize.Width))
                    {
                        visibleTabs.Add(tabItem);
                        currentWidth += child.DesiredSize.Width;
                    }
                    else
                    {
                        overflowedTabs.Add(tabItem);
                    }
                }
            }

            // Set the OverflowedItems DP
            OverflowedItems = overflowedTabs;

            // The desired size of the panel should be the width required for the visible tabs,
            // or the full desired width if availableSize.Width is infinite.
            // It must be a finite value.
            return new Size(double.IsInfinity(availableSize.Width) ? totalDesiredWidth : currentWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double x = 0;
            foreach (UIElement child in InternalChildren)
            {
                TabItem? tabItem = child as TabItem;
                if (tabItem is not null)
                {
                    // Only arrange if it's a visible tab (based on your MeasureOverride logic)
                    if (OverflowedItems is null || !OverflowedItems.Contains(tabItem)) // Simplified check
                    {
                        child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                        x += child.DesiredSize.Width;
                    }
                    else
                    {
                        // Do not arrange overflowed tabs
                        child.Arrange(new Rect(0, 0, 0, 0)); // Or collapse visibility
                    }
                }
            }
            return finalSize;
        }
    }
}
