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
        private bool _isLoadingError;

        public CustomizationManager Customization => _customization;

        public CustomizationDialogViewModel(IOptionsMonitor<H2MLauncherSettings> options, CustomizationManager customization)
        {
            _customization = customization;

            BackgroundImageUrl = options.CurrentValue.Customization?.BackgroundImagePath;
            IsLoadingError = customization.LoadingError;
        }

        [RelayCommand]
        public void SelectImage()
        {
            OpenFileDialog dlg = new()
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Select Background Image"
            };

            if (dlg.ShowDialog() == true &&
                _customization.LoadImage(dlg.FileName))
            {
                BackgroundImageUrl = dlg.FileName;
                IsLoadingError = false;
                
            }
            else
            {
                IsLoadingError = true;
            }
        }

        [RelayCommand]
        public void ResetImage()
        {
            _customization.LoadDefaultImage();

            BackgroundImageUrl = null;
            IsLoadingError = _customization.LoadingError;
        }
    }
}
