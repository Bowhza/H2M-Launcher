using System.Globalization;
using System.Windows.Data;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.Converters;

public sealed class MapConverter : IValueConverter
{
    private Dictionary<string, string>? _mapMap = null;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (_mapMap is null)
        {
            IOptions<ResourceSettings> options = App.ServiceProvider.GetService<IOptions<ResourceSettings>>()!;
            _mapMap = [];
            foreach (IW4MObjectMap oMap in options.Value.MapPacks.SelectMany(mappack => mappack.Maps))
            {
                _mapMap!.TryAdd(oMap.Name, oMap.Alias);
            }
        }

        string map = (string)value;

        return !_mapMap.TryGetValue(map, out string? alias) 
            ? throw new Exception($"Map: {map} not found") 
            : alias;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
