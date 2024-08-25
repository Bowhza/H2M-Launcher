using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
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
                    boolResult = false;
                }

                CloseRequested?.Invoke(this, new(boolResult));
            });
        }
    }
}