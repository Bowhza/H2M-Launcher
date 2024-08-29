using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI
{
    public partial class PasswordViewModel : DialogViewModelBase
    {
        [ObservableProperty]
        private string? _password = null;
    }
}
