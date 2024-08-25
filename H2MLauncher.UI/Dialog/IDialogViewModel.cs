using System.Windows.Input;

namespace H2MLauncher.UI.Dialog
{
    public interface IDialogViewModel
    {

        public event EventHandler<RequestCloseEventArgs>? CloseRequested;
    }
}