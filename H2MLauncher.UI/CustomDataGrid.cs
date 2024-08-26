using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace H2MLauncher.UI
{
    public class CustomDataGrid : DataGrid
    {
        static CustomDataGrid()
        {
            //DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomDataGrid), new FrameworkPropertyMetadata(typeof(CustomDataGrid)));

            // Override Coerce of ItemsSourceProperty 
            ItemsSourceProperty.OverrideMetadata(typeof(CustomDataGrid), new FrameworkPropertyMetadata(null, OnCoercItemsSource));
        }

        private static object OnCoercItemsSource(DependencyObject d, object basevalue)
        {
            // DataGrid messes up sorting changing the ItemsSource. Overriding this method
            // to do nothing fixes that issue, and keeps column sorting intact when changing ItemsSource.
            return basevalue;
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            // Keep the old filter by reapplying it to the new collection view
            ICollectionView oldCollectionView = CollectionViewSource.GetDefaultView(oldValue);
            ICollectionView newCollectionView = CollectionViewSource.GetDefaultView(newValue);
            if (oldCollectionView is not null && newCollectionView is not null)
            {
                newCollectionView.Filter = oldCollectionView.Filter;
            }
            base.OnItemsSourceChanged(oldValue, newValue);
        }
    }
}