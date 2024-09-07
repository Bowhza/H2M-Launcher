using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Collections;
using Microsoft.Xaml.Behaviors;

namespace H2MLauncher.UI
{
    public class CustomSortBehaviour
    {
        public static readonly DependencyProperty CustomSorterProperty = DependencyProperty.RegisterAttached("CustomSorter", typeof(IComparer), typeof(CustomSortBehaviour), new PropertyMetadata(default(IComparer)));
        public static readonly DependencyProperty CustomSorterTypeProperty = DependencyProperty.RegisterAttached("CustomSorterType", typeof(Type), typeof(CustomSortBehaviour), new PropertyMetadata(default(Type), CustomSorterTypePropertyChangedCallback));
        public static readonly DependencyProperty AllowCustomSortProperty =
            DependencyProperty.RegisterAttached("AllowCustomSort", typeof(bool),
            typeof(CustomSortBehaviour), new UIPropertyMetadata(false, OnAllowCustomSortChanged));

        public static readonly DependencyProperty UseMemberValueProperty =
            DependencyProperty.RegisterAttached("UseMemberValue", typeof(bool),
            typeof(CustomSortBehaviour), new UIPropertyMetadata(false));



        public static SortDescription GetDefaultSortDescription(DependencyObject obj)
        {
            return (SortDescription)obj.GetValue(DefaultSortDescriptionProperty);
        }

        public static void SetDefaultSortDescription(DependencyObject obj, SortDescription value)
        {
            obj.SetValue(DefaultSortDescriptionProperty, value);
        }

        // Using a DependencyProperty as the backing store for DefaultSortDescription.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DefaultSortDescriptionProperty =
            DependencyProperty.RegisterAttached("DefaultSortDescription", typeof(SortDescription), typeof(CustomSortBehaviour), new PropertyMetadata(null));



        #region Getters and Setters

        public static IComparer GetCustomSorter(DependencyObject element)
        {
            return (IComparer)element.GetValue(CustomSorterProperty);
        }

        public static void SetCustomSorter(DependencyObject element, IComparer value)
        {
            element.SetValue(CustomSorterProperty, value);
        }

        public static bool GetAllowCustomSort(DependencyObject element)
        {
            return (bool)element.GetValue(AllowCustomSortProperty);
        }

        public static void SetAllowCustomSort(DependencyObject element, bool value)
        {
            element.SetValue(AllowCustomSortProperty, value);
        }

        public static Type GetCustomSorterType(DependencyObject element)
        {
            return (Type)element.GetValue(CustomSorterTypeProperty);
        }

        public static void SetCustomSorterType(DependencyObject element, Type value)
        {
            element.SetValue(CustomSorterTypeProperty, value);
        }

        public static bool GetUseMemberValue(DependencyObject element)
        {
            return (bool)element.GetValue(UseMemberValueProperty);
        }

        public static void SetUseMemberValue(DependencyObject element, bool value)
        {
            element.SetValue(UseMemberValueProperty, value);
        }

        #endregion

        private static void OnAllowCustomSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var existing = d as DataGrid;
            if (existing == null) return;

            var oldAllow = (bool)e.OldValue;
            var newAllow = (bool)e.NewValue;

            if (!oldAllow && newAllow)
            {
                existing.Sorting += HandleCustomSorting;
            }
            else
            {
                existing.Sorting -= HandleCustomSorting;
            }
        }

        /// <summary>The callback for the <see cref="CustomSorterTypeProperty"/>.</summary>
        /// <param name="d">The dependency object.</param>
        /// <param name="e">The event args.</param>
        private static void CustomSorterTypePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Type newValue = (Type)e.NewValue;

            var sorter = Activator.CreateInstance(newValue);
            if (sorter is not IComparer comparer)
            {
                throw new ArgumentException("Custom sorter type must be IComparer.");
            }

            SetCustomSorter(d, comparer);
        }

        private static void HandleCustomSorting(object sender, DataGridSortingEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid == null || !GetAllowCustomSort(dataGrid)) return;
            
            var listColView = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource) as ListCollectionView;
            if (listColView == null)
                throw new Exception("The DataGrid's ItemsSource property must be of type, ListCollectionView");

            // Sanity check
            var sorter = GetCustomSorter(e.Column);
            if (sorter == null) return;
            
            listColView.CustomSort = new ColumnComparer(sorter, e.Column, GetUseMemberValue(e.Column));

            e.Handled = true;
        }

        private class ColumnComparer : IComparer
        {
            private readonly DataGridColumn _column;
            private readonly Func<object?, object?, int> _compareMethod;
            private readonly List<PropertyDescriptor> _propertyDescriptors;
            private readonly bool _useMemberValue;

            public ColumnComparer(IComparer valueComparer, DataGridColumn column, bool useMemberValue)
            {
                _column = column;
                _propertyDescriptors = [];
                _useMemberValue = useMemberValue;

                // switching from DESC to ACS (or initial null)
                if (column.SortDirection != ListSortDirection.Ascending)
                {
                    column.SortDirection = ListSortDirection.Ascending;
                    _compareMethod = valueComparer.Compare;
                }
                else
                {
                    column.SortDirection = ListSortDirection.Descending;
                    _compareMethod = (x, y) => valueComparer.Compare(y, x);
                }
            }

            public int Compare(object? x, object? y)
            {
                if (x == y)
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                if (!_useMemberValue || string.IsNullOrEmpty(_column.SortMemberPath))
                {
                    return _compareMethod(x, y);
                }

                if (_propertyDescriptors.Count == 0)
                {
                    string sortMemberPath = _column.SortMemberPath;

                    PopulatePropertyDescriptorForNestedSortMemberPath(x, sortMemberPath);

                    if (_propertyDescriptors.Count == 0)
                    {
                        // Try with other item.
                        PopulatePropertyDescriptorForNestedSortMemberPath(y, sortMemberPath);
                    }

                    if (_propertyDescriptors.Count == 0)
                    {
                        // If still null return anything, will try on next iteration.
                        return -1;
                    }
                }

                object? xSortValue = GetNestedValue(x);
                object? ySortValue = GetNestedValue(y);

                return _compareMethod(xSortValue, ySortValue);
            }

            private static PropertyDescriptor? GetPropertyDescriptor(object obj, string propertyName)
            {
                return TypeDescriptor.GetProperties(obj.GetType()).Find(propertyName, false);
            }

            private void PopulatePropertyDescriptorForNestedSortMemberPath(object? obj, string propertyName)
            {
                string[] split = propertyName.Split('.');

                foreach (string singlePropertyName in split)
                {
                    if (obj == null)
                    {
                        _propertyDescriptors.Clear();
                        return;
                    }

                    PropertyDescriptor? descriptor = GetPropertyDescriptor(obj, singlePropertyName);
                    if (descriptor == null)
                    {
                        _propertyDescriptors.Clear();
                        return;
                    }

                    _propertyDescriptors.Add(descriptor);

                    obj = descriptor.GetValue(obj);
                }
            }

            private object? GetNestedValue(object? obj)
            {
                foreach (PropertyDescriptor descriptor in _propertyDescriptors)
                {
                    obj = descriptor.GetValue(obj);
                }

                return obj;
            }
        }
    }
}
