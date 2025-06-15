using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace H2MLauncher.UI.ViewModels
{
    public partial class ThemeViewModel : ObservableObject
    {
        public required string ThemeName { get; init; }
        public string? ThemeFile { get; init; }
        public string? Icon { get; init; }
        public string? Author { get; init; }
        public string? Description { get; init; }

        public bool IsInternal => ThemeFile is null || ThemeFile.StartsWith(Constants.EmbeddedThemesAbsolutePath);
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
        private const string MetadataFileName = "metadata.json";
        private const string IconName = "icon";

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
            LoadedThemePath = Path.GetFileName(options.CurrentValue.Customization?.Themes?.FirstOrDefault());

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

        #region Theme Loading

        private static string? TryFindThemeFile(string? themeFolderName, ThemeMetadata? metadata, IEnumerable<string> xamlFiles)
        {
            List<string> validThemeFileNames = GetValidThemeFileNames(themeFolderName, metadata).ToList();

            string? themeFile = null;
            foreach ((string xamlFile, int i) in xamlFiles.Select((file, i) => (file, i)))
            {
                if (validThemeFileNames.Contains(xamlFile.ToLowerInvariant()))
                {
                    themeFile = xamlFile;
                    return themeFile;
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

            return themeFile;
        }

        private async Task<bool> LoadAndAddThemeFromResource(string themeFolderName, IEnumerable<string> files)
        {
            try
            {
                ThemeMetadata? metadata = null;

                // find metadata
                string? metadataResourceName = files.FirstOrDefault(r => r.Equals(MetadataFileName, StringComparison.InvariantCultureIgnoreCase));
                if (metadataResourceName is not null)
                {
                    using Stream? metadataStream = ResourceHelper.GetWpfResourceStream(
                        $"{Constants.EmbeddedThemesRelativePath}/{themeFolderName}/{metadataResourceName}");

                    if (metadataStream is not null)
                    {
                        metadata = await DeserializeThemeMetadata(metadataStream);
                        if (metadata is null)
                        {
                            // invalid metadata
                            return false;
                        }
                    }
                }

                // find theme file (only files directly in this folder count)
                string? themeFileName = TryFindThemeFile(themeFolderName, metadata, files.Where(r => r.EndsWith(".xaml") && !r.Contains('/')));
                if (themeFileName is null)
                {
                    return false;
                }

                // create the absolute theme file path
                string themeFile = $"{Constants.EmbeddedThemesAbsolutePath}/{themeFolderName}/{themeFileName}";

                if (Themes.Any(t => t.ThemeFile == themeFile))
                {
                    // theme already added
                    return false;
                }

                string themeName = metadata?.Name ?? Path.GetFileNameWithoutExtension(themeFile);
                string? iconFile = files.Where(r => r.StartsWith(IconName + ".") && !r.Contains('/')).FirstOrDefault();

                if (iconFile is not null)
                    iconFile = $"{Constants.EmbeddedThemesAbsolutePath}/{themeFolderName}/{iconFile}";

                AddTheme(themeName, themeFile, iconFile, metadata?.Author, metadata?.Description);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading embedded theme at {themeFolder}", themeFolderName);
                return false;
            }
        }

        private async Task<bool> LoadAndAddTheme(string themeDir)
        {
            try
            {
                ThemeMetadata? metadata = null;

                // find metadata file
                string metadataFileName = Path.Combine(themeDir, MetadataFileName);
                if (File.Exists(metadataFileName))
                {
                    using FileStream metadataFileStream = File.OpenRead(metadataFileName);
                    metadata = await DeserializeThemeMetadata(metadataFileStream);
                    if (metadata is null)
                    {
                        // invalid metadata
                        return false;
                    }
                }

                // find theme file
                string? themeFile = TryFindThemeFile(
                    Path.GetDirectoryName(themeDir),
                    metadata,
                    Directory.EnumerateFiles(themeDir, "*.xaml").Select(path => Path.GetFileName(path)));

                if (themeFile is null)
                {
                    // theme file not found
                    return false;
                }

                themeFile = Path.Combine(themeDir, themeFile);

                if (Themes.Any(t => t.ThemeFile == themeFile))
                {
                    // theme already added
                    return false;
                }

                string themeName = metadata?.Name ?? Path.GetFileNameWithoutExtension(themeFile);
                string? iconFile = Directory.EnumerateFiles(themeDir, $"{IconName}.*").FirstOrDefault();

                AddTheme(themeName, themeFile, iconFile, metadata?.Author, metadata?.Description);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading theme at {themeFolder}", themeDir);
                return false;
            }
        }


        [RelayCommand]
        private async Task LoadThemes()
        {
            try
            {
                Themes.Clear();
                Themes.Add(_defaultThemeViewModel);

                // First load the embedded themes
                IEnumerable<IGrouping<string, string>> resourcesGroupedByThemeFolder =
                ResourceHelper
                    .GetResourcesUnder(Constants.EmbeddedThemesRelativePath)
                    .GroupBy(r => r[..r.IndexOf('/')], r => r.Substring(r.IndexOf('/') + 1));

                foreach (IGrouping<string, string> grp in resourcesGroupedByThemeFolder)
                {
                    string themeFolderName = grp.Key;
                    await LoadAndAddThemeFromResource(themeFolderName, grp);
                }

                // Then the ones from the users themes folder
                if (Directory.Exists(Constants.ThemesDir))
                {
                    foreach (string themeDir in Directory.EnumerateDirectories(Constants.ThemesDir))
                    {
                        await LoadAndAddTheme(themeDir);
                    }
                }

                // Determine active theme
                string? currentThemeFilePath = _options.CurrentValue.Customization?.Themes?.FirstOrDefault();


                ThemeViewModel? currentTheme = string.IsNullOrEmpty(currentThemeFilePath)
                    ? _defaultThemeViewModel
                    : Themes.FirstOrDefault(t =>
                            t.ThemeFile is not null && string.Equals(
                                Path.GetFullPath(t.ThemeFile).TrimEnd('\\'),
                                Path.GetFullPath(currentThemeFilePath).TrimEnd('\\'),
                                StringComparison.InvariantCultureIgnoreCase)
                      ) ?? null;

                if (currentTheme is not null)
                {
                    currentTheme.IsActive = true;
                }

                ActiveTheme = currentTheme;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading themes");
            }
        }

        #endregion

        private void AddTheme(string name, string xamlFile, string? iconFile, string? author, string? description)
        {
            ThemeViewModel theme = new()
            {
                ThemeName = name,
                ThemeFile = xamlFile,
                Icon = iconFile,
                Author = author,
                Description = description ?? $"{Path.GetFileName(Path.GetDirectoryName(xamlFile))}\\{Path.GetFileName(xamlFile)}"
            };

            theme.DeleteCommand = new RelayCommand(() =>
            {
                try
                {
                    bool? delete = _dialogService.OpenTextDialog(
                        title: "Delete Theme Pack",
                        text: $"Are you sure you want to delete the theme '{theme.ThemeName}' with all it's files ({Path.GetDirectoryName(xamlFile)}\\)?",
                        acceptButtonText: "Delete");

                    if (delete != true)
                    {
                        return;
                    }

                    if (ActiveTheme == theme)
                    {
                        ActiveTheme = _defaultThemeViewModel;
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

        #region Theme Import

        private static IEnumerable<string> GetValidThemeFileNames(string? themeFolderName, ThemeMetadata? metadata)
        {
            yield return "theme.xaml";

            if (themeFolderName is not null)
                yield return $"{themeFolderName}.xaml";

            if (metadata is not null)
                yield return $"{metadata.Id}.xaml";
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

            List<ZipArchiveEntry> rootXamlFiles = zipArchive.Entries
                .Where(e => e.FullName.EndsWith(".xaml") && e.FullName == e.Name)
                .ToList();
            if (rootXamlFiles.Count == 0)
            {
                _errorHandlingService.HandleError("Invalid theme pack: no xaml file found.");
                return false;
            }

            string themeFolderName = Path.GetFileNameWithoutExtension(packFile);
            List<string> validThemeFileNames = GetValidThemeFileNames(themeFolderName, metadata).ToList();

            if (rootXamlFiles.Count > 1 && !rootXamlFiles.Any(e => validThemeFileNames.Contains(e.Name)))
            {
                _errorHandlingService.HandleError(
                    $"Invalid theme pack: no xaml file with valid name (must be any of {string.Join(',', validThemeFileNames)}).");
                return false;
            }

            string targetFolder = Path.Combine(Constants.ThemesDir, themeFolderName);
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

        #endregion

        private record ThemeMetadata
        {
            public required string Id { get; init; }

            public required string Name { get; init; }

            public string? Author { get; init; }

            public string? Description { get; init; }
        }
    }
}
