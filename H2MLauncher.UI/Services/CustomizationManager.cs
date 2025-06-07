using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
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
    private bool _backgroundImageLoadingError;

    [ObservableProperty]
    private double _backgroundBlur = 0;

    [ObservableProperty]
    private bool _themeLoadingError;


    public CustomizationManager(IWritableOptions<H2MLauncherSettings> settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Load the initial background image from the settings.
    /// </summary>
    public void LoadInitialValues()
    {
        LauncherCustomizationSettings? customizationSettings = _settings.Value.Customization;

        BackgroundBlur = customizationSettings?.BackgroundBlur ?? DefaultBackgroundBlur;
        SetResourceInBaseTheme(Constants.BackgroundImageBlurRadiusKey, BackgroundBlur);

        // Background image
        if (string.IsNullOrEmpty(customizationSettings?.BackgroundImagePath))
        {
            LoadDefaultImage(resetSetting: false);
            return;
        }

        if (!TryLoadImage(customizationSettings!.BackgroundImagePath, out var image))
        {
            LoadDefaultImage(resetSetting: false);
            BackgroundImageLoadingError = true;
        }
        else
        {
            SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, image);
            BackgroundImage = image;
        }

        // Theme
        if (customizationSettings.Themes is not null &&
            customizationSettings.Themes.Count > 0)
        {
            LoadTheme(customizationSettings.Themes[0]);
        }
    }

    public bool LoadImage(string imageFileName)
    {
        if (TryLoadImage(imageFileName, out ImageSource? image))
        {
            SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, image);

            BackgroundImage = image;
            BackgroundImageLoadingError = false;

            UpdateCustomizationSettings(settings => settings with
            {
                BackgroundImagePath = imageFileName
            });


            return true;
        }
        else
        {
            BackgroundImageLoadingError = true;
            return false;
        }
    }

    public void LoadDefaultImage(bool resetSetting = true)
    {
        BackgroundImage = new BitmapImage(new Uri(DefaultBackgroundImagePath));
        SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, BackgroundImage);

        BackgroundImageLoadingError = false;

        if (!resetSetting)
        {
            return;
        }

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

    public bool LoadTheme(string path)
    {
        try
        {
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary()
                {
                    Source = new Uri(path, UriKind.Absolute)
                });

            UpdateCustomizationSettings(settings => settings with
            {
                Themes = [path]
            });

            ThemeLoadingError = false;
            return true;
        }
        catch (Exception)
        {
            ThemeLoadingError = true;
            return false;
        }
    }

    public void ResetTheme()
    {
        if (Application.Current.Resources.MergedDictionaries.Count > 1)
        {
            Application.Current.Resources.MergedDictionaries.RemoveAt(1);

            UpdateCustomizationSettings(settings => settings with
            {
                Themes = []
            });

            ThemeLoadingError = false;
        }
    }

    partial void OnBackgroundBlurChanged(double value)
    {
        SetResourceInBaseTheme(Constants.BackgroundImageBlurRadiusKey, value);

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

    private static void SetResourceInBaseTheme(object resourceKey, object value)
    {
        Application.Current.Resources.MergedDictionaries[0][resourceKey] = value;
    }
}
