using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public interface IDialogViewModel
    {

        public event EventHandler<RequestCloseEventArgs>? CloseRequested;
    }

    public abstract class DialogViewModelBase : ObservableObject, IDialogViewModel
    {
        public IRelayCommand<bool?> CloseCommand { get; set; }

        public event EventHandler<RequestCloseEventArgs>? CloseRequested;

        public DialogViewModelBase()
        {
            CloseCommand = new RelayCommand<bool?>((result) =>
            {
                if (result is not bool boolResult)
                {
                    boolResult = true;
                }

                CloseRequested?.Invoke(this, new(boolResult));
            });
        }
    }

    public class RequestCloseEventArgs : EventArgs
    {
        public RequestCloseEventArgs(bool dialogResult)
        {
            this.DialogResult = dialogResult;
        }

        public bool DialogResult { get; private set; }
    }
}