using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI.ViewModels
{
    public partial class PasswordViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string? _password = null;
    }
}
