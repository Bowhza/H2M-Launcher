﻿using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class BooleanToEditConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "Stop" : "Edit";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}