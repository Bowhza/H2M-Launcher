using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

using static System.Net.Mime.MediaTypeNames;

namespace H2MLauncher.UI.ViewModels
{
    public partial class ThemeViewModel : ObservableObject
    {
        public required string ThemeName { get; init; }
        public string? ThemeFile { get; init; }
        public string? Icon { get; init; }
        public string? Author { get; init; }
        public string? Description { get; init; }

        public bool IsDefault { get; init; }

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isLoadingError;

        public IRelayCommand? DeleteCommand { get; set; }

        public IRelayCommand? OpenFolderCommand { get; set; }
    }

    public partial class CustomizationDialogViewModel : DialogViewModelBase
    {
        private readonly CustomizationManager _customization;
        private readonly IOptionsMonitor<H2MLauncherSettings> _options;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly DialogService _dialogService;
        private readonly ILogger<CustomizationDialogViewModel> _logger;

        [ObservableProperty]
        private string? _backgroundImageUrl;

        [ObservableProperty]
        private string? _loadedThemePath;

        private readonly ThemeViewModel _defaultThemeViewModel;

        public ObservableCollection<ThemeViewModel> Themes { get; } = [];

        [ObservableProperty]
        private ThemeViewModel? _activeTheme;

        [ObservableProperty]
        private bool _loadingThemes;

        public CustomizationManager Customization => _customization;

        public CustomizationDialogViewModel(
            IOptionsMonitor<H2MLauncherSettings> options,
            CustomizationManager customization,
            IErrorHandlingService errorHandlingService,
            DialogService dialogService,
            ILogger<CustomizationDialogViewModel> logger)
        {
            _options = options;
            _customization = customization;

            BackgroundImageUrl = options.CurrentValue.Customization?.BackgroundImagePath;
            LoadedThemePath = options.CurrentValue.Customization?.Themes?.FirstOrDefault();

            _defaultThemeViewModel = new()
            {
                ThemeName = "Default Theme",
                Description = "Default",
                IsDefault = true
            };
            _errorHandlingService = errorHandlingService;
            _dialogService = dialogService;

            LoadThemesCommand.Execute(null);
            _logger = logger;
        }


        private static ValueTask<ThemeMetadata?> DeserializeThemeMetadata(Stream metadataFileStream)
        {
            return JsonSerializer.DeserializeAsync<ThemeMetadata>(
                metadataFileStream,
                new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        }


        [RelayCommand]
        private async Task LoadThemes()
        {
            LoadingThemes = true;
            try
            {
                Themes.Clear();
                Themes.Add(_defaultThemeViewModel);

                if (Directory.Exists(Constants.ThemesDir))
                {
                    foreach (string themeDir in Directory.EnumerateDirectories(Constants.ThemesDir))
                    {
                        try
                        {
                            ThemeMetadata? metadata = null;

                            // find metadata file
                            string metadataFileName = Path.Combine(themeDir, "metadata.json");
                            if (File.Exists(metadataFileName))
                            {
                                using FileStream metadataFileStream = File.OpenRead(metadataFileName);
                                metadata = await DeserializeThemeMetadata(metadataFileStream);
                                if (metadata is null)
                                {
                                    // invalid metadata
                                    continue;
                                }
                            }

                            // find theme file
                            List<string> validThemeFileNames = ["theme", $"{Path.GetDirectoryName(themeDir)}"];
                            if (metadata is not null)
                            {
                                validThemeFileNames.Add(metadata.Id);
                            }

                            string? themeFile = null;
                            foreach ((string xamlFile, int i) in Directory.EnumerateFiles(themeDir, "*.xaml").Select((file, i) => (file, i)))
                            {
                                if (validThemeFileNames.Contains(Path.GetFileNameWithoutExtension(xamlFile)))
                                {
                                    themeFile = xamlFile;
                                    break;
                                }

                                // if there's a single xaml file, accept that too
                                if (i == 0)
                                {
                                    themeFile = xamlFile;
                                }
                                else
                                {
                                    themeFile = null;
                                }
                            }

                            if (themeFile is null)
                            {
                                // theme file not found
                                continue;
                            }

                            if (Themes.Any(t => t.ThemeFile == themeFile))
                            {
                                // theme already added
                                continue;
                            }

                            string themeName = metadata?.Name ?? Path.GetFileNameWithoutExtension(themeFile);
                            string? iconFile = Directory.EnumerateFiles(themeDir, "icon.*").FirstOrDefault();

                            AddTheme(themeName, themeFile, iconFile, metadata?.Author, metadata?.Description);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while loading theme at {themeFolder}", themeDir);
                        }
                    }
                }

                // Determine active theme
                string? currentThemeFilePath = _options.CurrentValue.Customization?.Themes?.FirstOrDefault();

                ThemeViewModel currentTheme = string.IsNullOrEmpty(currentThemeFilePath)
                    ? _defaultThemeViewModel
                    : Themes.FirstOrDefault(t =>
                            t.ThemeFile is not null && string.Equals(
                                Path.GetFullPath(t.ThemeFile).TrimEnd('\\'),
                                Path.GetFullPath(currentThemeFilePath).TrimEnd('\\'),
                                StringComparison.InvariantCultureIgnoreCase)
                      ) ?? _defaultThemeViewModel;

                currentTheme.IsActive = true;
                currentTheme.IsLoadingError = _customization.ThemeLoadingError;

                ActiveTheme = currentTheme;
            }
            catch
            {

            }
            finally
            {
                LoadingThemes = false;
            }
        }

        private void AddTheme(string name, string xamlFile, string? iconFile, string? author, string? description)
        {
            ThemeViewModel theme = new()
            {
                ThemeName = name,
                ThemeFile = xamlFile,
                Icon = iconFile,
                Author = author,
                Description = description ?? Path.GetFileName(xamlFile)
            };

            theme.DeleteCommand = new RelayCommand(() =>
            {
                if (ActiveTheme == theme)
                {
                    ActiveTheme = _defaultThemeViewModel;
                }                

                try
                {
                    bool? delete = _dialogService.OpenTextDialog(
                        title: "Delete Theme Pack",
                        text: $"Do you want to delete the theme '{theme.ThemeName}' with all it's files?",
                        acceptButtonText: "Delete");

                    if (delete != true)
                    {
                        return;
                    }

                    Themes.Remove(theme);

                    string? themeFolder = Path.GetDirectoryName(theme.ThemeFile);
                    if (themeFolder is null)
                    {
                        File.Delete(theme.ThemeFile);
                        return;
                    }

                    Directory.Delete(themeFolder, true);
                }
                catch (Exception ex)
                {
                    _errorHandlingService.HandleException(ex, "Could not delete theme");
                }
            });

            theme.OpenFolderCommand = new RelayCommand(() =>
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "explorer.exe",
                    Arguments = Path.GetDirectoryName(theme.ThemeFile),
                    UseShellExecute = true,
                });
            });

            Themes.Add(theme);
        }

        partial void OnActiveThemeChanged(ThemeViewModel? oldValue, ThemeViewModel? newValue)
        {
            if (newValue is null)
            {
                ActiveTheme = _defaultThemeViewModel;
                return;
            }

            if (newValue.IsActive == true)
                return;

            if (oldValue is not null)
                oldValue.IsActive = false;

            if (newValue.IsDefault)
            {
                newValue.IsLoading = true;
                _customization.ResetTheme();
                newValue.IsActive = true;
                newValue.IsLoading = false;
                newValue.IsLoadingError = false;
                LoadedThemePath = null;

                return;
            }
            if (string.IsNullOrEmpty(newValue.ThemeFile))
            {
                ActiveTheme = oldValue ?? _defaultThemeViewModel;
                return;
            }


            newValue.IsLoading = true;
            if (_customization.LoadTheme(newValue.ThemeFile))
            {
                newValue.IsActive = true;
                newValue.IsLoading = false;
                newValue.IsLoadingError = false;
            }
            else
            {
                newValue.IsLoading = false;
                newValue.IsLoadingError = true;
            }
            LoadedThemePath = Path.GetFileName(newValue.ThemeFile);
        }

        [RelayCommand]
        public Task SelectBackgroundMedia()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "All Supported Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.wmv;*.avi|Image Files|*.png;*.jpg;*.jpeg;*.bmp|GIF Files|*.gif|Video Files|*.mp4;*.wmv;*.avi",
                Title = "Select Background Media"
            };

            bool? dialogResult = dlg.ShowDialog();
            if (dialogResult != true)
            {
                return Task.CompletedTask;
            }

            BackgroundImageUrl = dlg.FileName;

            return _customization.LoadMedia(dlg.FileName);
        }

        [RelayCommand]
        public void ResetBackgroundMedia()
        {
            _customization.ResetBackgroundMedia();

            BackgroundImageUrl = null;
        }

        [RelayCommand]
        public void SelectTheme()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "XAML Dictionary|*.xaml",
                Title = "Select Resource File"
            };

            bool? dialogResult = dlg.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }

            _customization.LoadTheme(dlg.FileName);
            LoadedThemePath = Path.GetFileName(dlg.FileName);
        }

        [RelayCommand]
        public void ResetTheme()
        {
            _customization.ResetTheme();
            LoadedThemePath = null;
        }

        private static bool ImportXamlTheme(string xamlFile)
        {
            string name = Path.GetFileNameWithoutExtension(xamlFile);

            for (int i = 0; i < 99; i++)
            {
                string suffix = i == 0 ? "" : i.ToString();
                string dir = Path.Combine(Constants.ThemesDir, $"{name}{suffix}");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);

                    File.Copy(xamlFile, Path.Combine(dir, Path.GetFileName(xamlFile)));

                    return true;
                }
            }

            return false;
        }

        private async Task<bool> ImportThemePack(string packFile)
        {
            ZipArchive zipArchive = ZipFile.OpenRead(packFile);
            ZipArchiveEntry? metadataFile = zipArchive.Entries.FirstOrDefault(d => d.FullName == "metadata.json");
            if (metadataFile is null)
            {
                _errorHandlingService.HandleError("Invalid theme pack: no metadata found.");
                return false;
            }

            using Stream metadataFileStream = metadataFile.Open();
            ThemeMetadata? metadata = await DeserializeThemeMetadata(metadataFileStream);
            if (metadata is null)
            {
                _errorHandlingService.HandleError("Invalid theme pack metadata.");
                return false;
            }

            string targetFolder = Path.Combine(Constants.ThemesDir, Path.GetFileNameWithoutExtension(packFile));
            if (Directory.Exists(targetFolder))
            {
                ThemeViewModel? existingTheme = Themes.FirstOrDefault(t => t.ThemeFile is not null && t.ThemeFile.StartsWith(targetFolder));
                string text = existingTheme is not null
                    ? $"There already exists a theme pack ('{existingTheme.ThemeName}') at {targetFolder}. Do you want to overwrite it?"
                    : $"There already exists a theme pack at {targetFolder}. Do you want to overwrite it?";

                bool? overwrite = _dialogService.OpenTextDialog(
                    title: "Import Theme Pack",
                    text: text,
                    acceptButtonText: "Overwrite");

                if (overwrite != true)
                {
                    return false;
                }
            }

            zipArchive.ExtractToDirectory(targetFolder, true);
            return true;
        }

        [RelayCommand]
        public async Task ImportTheme()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "All Supported Files|*.png;*.jpg;*.zip;*.xaml|Theme Pack|*.zip|XAML Dictionary|*.xaml",
                Title = "Select Resource File"
            };

            bool? dialogResult = dlg.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }

            try
            {
                string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                bool success = ext switch
                {
                    ".xaml" => ImportXamlTheme(dlg.FileName),
                    ".zip" => await ImportThemePack(dlg.FileName),
                    _ => false
                };

                if (success)
                {
                    LoadThemesCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.HandleException(ex, "Error while importing theme");
            }
        }

        private record ThemeMetadata
        {
            public required string Id { get; init; }

            public required string Name { get; init; }

            public string? Author { get; init; }

            public string? Description { get; init; }
        }
    }
}
