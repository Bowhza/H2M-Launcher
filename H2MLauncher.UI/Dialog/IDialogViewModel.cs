using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public interface IDialogViewModel
    {
        IRelayCommand LoadedCommand { get; set; }

        event EventHandler<RequestCloseEventArgs>? CloseRequested;
    }
}