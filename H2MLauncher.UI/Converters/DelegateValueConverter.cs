using System.Globalization;
using System.Windows.Data;

namespace H2MLauncher.UI.Converters;

public class DelegateValueConverter<T> : IValueConverter
{
    private readonly Func<T, object?> _convert;
    private readonly Func<object?, T>? _convertBack;
    private readonly Func<object?, object?> _fallback;
    public DelegateValueConverter(Func<T, object?> convert, Func<object?, T>? convertBack = null, Func<object?, object?>? fallback = null)
    {
        _convert = convert;
        _convertBack = convertBack;
        _fallback = fallback ?? (_ => null);
    }

    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is T specificValue
            ? _convert(specificValue)
            : _fallback(value);
    }

    public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return _convertBack is not null
            ? (object?)_convertBack(value)
            : throw new NotImplementedException();
    }
}
