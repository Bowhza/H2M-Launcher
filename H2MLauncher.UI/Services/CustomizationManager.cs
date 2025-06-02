using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities;

using Nogic.WritableOptions;

namespace H2MLauncher.UI.Services;

public partial class CustomizationManager : ObservableObject
{
    internal const string DefaultBackgroundImagePath = "pack://application:,,,/H2MLauncher.UI;component/Assets/Background.jpg";
    internal const double DefaultBackgroundBlur = 0;

    private readonly IWritableOptions<H2MLauncherSettings> _settings;

    [ObservableProperty]
    private ImageSource _backgroundImage = new BitmapImage(new Uri(DefaultBackgroundImagePath));

    [ObservableProperty]
    private bool _loadingError;

    [ObservableProperty]
    private double _backgroundBlur = 0;

    public CustomizationManager(IWritableOptions<H2MLauncherSettings> settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Load the initial background image from the settings.
    /// </summary>
    public void LoadInitialValues()
    {
        BackgroundBlur = _settings.Value.Customization?.BackgroundBlur ?? DefaultBackgroundBlur;

        if (string.IsNullOrEmpty(_settings.Value.Customization?.BackgroundImagePath))
        {
            LoadDefaultImage();
            return;
        }

        if (!TryLoadImage(_settings.Value.Customization!.BackgroundImagePath, out var image))
        {
            LoadingError = true;
            LoadDefaultImage();
        }
        else
        {
            BackgroundImage = image;
        }        
    }

    public bool LoadImage(string imageFileName)
    {
        if (TryLoadImage(imageFileName, out var image))
        {
            BackgroundImage = image;
            LoadingError = false;

            UpdateCustomizationSettings(settings => settings with
            {
                BackgroundImagePath = imageFileName
            });

            return true;
        }
        else
        {
            LoadingError = true;
            return false;
        }
    }

    public void LoadDefaultImage()
    {
        BackgroundImage = new BitmapImage(new Uri(DefaultBackgroundImagePath));
        LoadingError = false;

        UpdateCustomizationSettings(settings => settings with
        {
            BackgroundImagePath = null
        });
    }

    private static bool TryLoadImage(string path, [NotNullWhen(true)] out ImageSource? image)
    {
        image = null;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            image = bitmap;
            return true;
        }
        catch
        {
            return false;
        }
    }

    partial void OnBackgroundBlurChanged(double value)
    {
        UpdateCustomizationSettings(settings => settings with
        {
            BackgroundBlur = value
        });
    }

    private void UpdateCustomizationSettings(Func<LauncherCustomizationSettings, LauncherCustomizationSettings> update)
    {
        _settings.Update((settings) => settings with
        {
            Customization = update(settings.Customization ?? new())
        });
    }
}
