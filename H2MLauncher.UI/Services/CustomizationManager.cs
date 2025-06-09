using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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

    // Callback from the UI that the media element has loaded the source
    private TaskCompletionSource _backgroundVideoLoadCompletionSource = new();

    [ObservableProperty]
    private ImageSource _backgroundImage = new BitmapImage(new Uri(DefaultBackgroundImagePath));

    [ObservableProperty]
    private Uri? _backgroundVideo;

    [ObservableProperty]
    private MediaElement? _previewBackgroundVideo;

    [ObservableProperty]
    private bool _backgroundImageLoadingError;

    [ObservableProperty]
    private double _backgroundBlur = 0;

    [ObservableProperty]
    private bool _themeLoadingError;

    [ObservableProperty]
    private bool _hotReloadThemes;


    public CustomizationManager(IWritableOptions<H2MLauncherSettings> settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Load the initial background image from the settings.
    /// </summary>
    public async Task LoadInitialValues()
    {
        LauncherCustomizationSettings? customizationSettings = _settings.Value.Customization;

        // Background blur
        BackgroundBlur = customizationSettings?.BackgroundBlur ?? DefaultBackgroundBlur;
        SetResourceInBaseTheme(Constants.BackgroundImageBlurRadiusKey, BackgroundBlur);

        // Background media
        if (string.IsNullOrEmpty(customizationSettings?.BackgroundImagePath))
        {
            // no custom image or video set
            SetDefaultBackgroundImage(resetSetting: false);
        }
        else if (TryLoadImage(customizationSettings.BackgroundImagePath, out ImageSource? image))
        {
            SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, image);
            BackgroundImage = image;
        }
        else if (await TryLoadBackgroundVideo(customizationSettings.BackgroundImagePath))
        {
            SetResourceInBaseTheme(Constants.BackgroundVideoSourceKey, customizationSettings.BackgroundImagePath);
            BackgroundVideo = new Uri(customizationSettings.BackgroundImagePath);
        }
        else
        {
            // something went wrong trying to load it
            BackgroundImageLoadingError = true;
        }

        // Theme
        if (customizationSettings?.Themes is not null &&
            customizationSettings.Themes.Count > 0)
        {
            LoadTheme(customizationSettings.Themes[0]);
        }

        HotReloadThemes = customizationSettings?.HotReloadThemes ?? false;
    }

    public async Task<bool> LoadMedia(string mediaFileName)
    {        
        if (TryLoadImage(mediaFileName, out ImageSource? image))
        {
            SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, image);
            SetResourceInBaseTheme(Constants.BackgroundVideoSourceKey, null);

            BackgroundImage = image;
            BackgroundImageLoadingError = false;

            UpdateCustomizationSettings(settings => settings with
            {
                BackgroundImagePath = mediaFileName
            });

            return true;
        }
        else if (await TryLoadBackgroundVideo(mediaFileName))
        {
            SetResourceInBaseTheme(Constants.BackgroundVideoSourceKey, mediaFileName);
            SetDefaultBackgroundImage(resetSetting: false);
            
            BackgroundVideo = new Uri(mediaFileName);
            BackgroundImageLoadingError = false;

            UpdateCustomizationSettings(settings => settings with
            {
                BackgroundImagePath = mediaFileName
            });

            return true;
        }
        else
        {
            BackgroundImageLoadingError = true;
            return false;
        }
    }

    public void ResetBackgroundMedia()
    {
        // Reset video
        SetResourceInBaseTheme(Constants.BackgroundVideoSourceKey, null);
        BackgroundVideo = null;

        // Reset image
        SetDefaultBackgroundImage(resetSetting: true);
    }

    private void SetDefaultBackgroundImage(bool resetSetting = true)
    {
        BackgroundImage = new BitmapImage(new Uri(DefaultBackgroundImagePath));
        BackgroundImageLoadingError = false;
        SetResourceInBaseTheme(Constants.BackgroundImageSourceKey, BackgroundImage);

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

    private async Task<bool> TryLoadBackgroundVideo(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            // Cancel previous loading callback and create new
            _backgroundVideoLoadCompletionSource.TrySetCanceled();
            _backgroundVideoLoadCompletionSource = new();
            
            using CancellationTokenSource timeoutCancellation = new(TimeSpan.FromSeconds(15));
            using CancellationTokenRegistration reg = timeoutCancellation.Token.Register(() => _backgroundVideoLoadCompletionSource.TrySetCanceled());

            // Set the resource so the MediaElement starts loading
            SetResourceInBaseTheme(Constants.BackgroundVideoSourceKey, path);  

            // Wait for the media to be opened in the UI
            await _backgroundVideoLoadCompletionSource.Task;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void OnBackgroundMediaLoaded()
    {
        _backgroundVideoLoadCompletionSource.TrySetResult();
    }

    public void OnBackgroundMediaFailed(Exception exception)
    {
        _backgroundVideoLoadCompletionSource.TrySetException(exception);
    }

    public bool LoadTheme(string path)
    {
        try
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                ResourceDictionary resourceDictionary = new()
                {
                    Source = new Uri(path, UriKind.Absolute)
                };

                // remove old one
                if (Application.Current.Resources.MergedDictionaries.Count > 1)
                {
                    Application.Current.Resources.MergedDictionaries.RemoveAt(1);
                }
                
                // add new one
                Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);

                UpdateCustomizationSettings(settings => settings with
                {
                    Themes = [path]
                });

                ThemeLoadingError = false;
                return true;
            });
        }
        catch (Exception)
        {
            ThemeLoadingError = true;
            GC.Collect(); // force garbage collection to free locked file resource
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

    partial void OnHotReloadThemesChanged(bool value)
    {
        UpdateCustomizationSettings(settings => settings with
        {
            HotReloadThemes = value
        });
    }

    private void UpdateCustomizationSettings(Func<LauncherCustomizationSettings, LauncherCustomizationSettings> update)
    {
        _settings.Update((settings) => settings with
        {
            Customization = update(settings.Customization ?? new())
        });
    }

    private static void SetResourceInBaseTheme(object resourceKey, object? value)
    {
        Application.Current.Resources.MergedDictionaries[0][resourceKey] = value;
    }
}
