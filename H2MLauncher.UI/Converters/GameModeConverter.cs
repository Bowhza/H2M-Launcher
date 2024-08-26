using System.Globalization;
using System.Windows.Data;

using H2MLauncher.Core.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace H2MLauncher.UI.Converters;

public sealed class GameModeConverter : IValueConverter
{
    private Dictionary<string, string>? _gameModeMap = null;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (_gameModeMap is null)
        {
            ResourceSettings resourceSettings = App.ServiceProvider.GetService<IOptions<ResourceSettings>>()!.Value;
            _gameModeMap = [];
            foreach (IW4MObjectMap oMap in resourceSettings.GameTypes)
            {
                _gameModeMap!.TryAdd(oMap.Name, oMap.Alias);
            }
        }

        string gamemode = (string)value;

        return !_gameModeMap.TryGetValue(gamemode, out string? alias)
            ? throw new Exception($"GameMode: {gamemode} not found")
            : alias;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
