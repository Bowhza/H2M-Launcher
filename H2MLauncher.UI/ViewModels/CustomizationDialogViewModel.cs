using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using H2MLauncher.Core.Settings;
using H2MLauncher.UI.Dialog;
using H2MLauncher.UI.Services;

using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace H2MLauncher.UI.ViewModels
{
    public partial class CustomizationDialogViewModel : DialogViewModelBase
    {
        private readonly CustomizationManager _customization;

        [ObservableProperty]
        private string? _backgroundImageUrl;

        [ObservableProperty]
        private string? _loadedThemePath;

        public CustomizationManager Customization => _customization;

        public CustomizationDialogViewModel(IOptionsMonitor<H2MLauncherSettings> options, CustomizationManager customization)
        {
            _customization = customization;

            BackgroundImageUrl = options.CurrentValue.Customization?.BackgroundImagePath;
            LoadedThemePath = options.CurrentValue.Customization?.Themes?.FirstOrDefault();
        }

        [RelayCommand]
        public void SelectImage()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select Background Image"
            };

            bool? dialogResult = dlg.ShowDialog();            
            if (dialogResult != true)
            {
                return;
            }

            BackgroundImageUrl = dlg.FileName;

            _customization.LoadImage(dlg.FileName);
        }

        [RelayCommand]
        public void ResetImage()
        {
            _customization.LoadDefaultImage();

            BackgroundImageUrl = null;
        }

        [RelayCommand]
        public void SelectTheme()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "XAML Resource|*.xaml",
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
    }
}
