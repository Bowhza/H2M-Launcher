using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public abstract class DialogViewModelBase : ObservableObject, IDialogViewModel
    {
        public IRelayCommand LoadedCommand { get; }
        public IRelayCommand<bool?> CloseCommand { get; set; }

        public event EventHandler<RequestCloseEventArgs>? CloseRequested;

        public DialogViewModelBase()
        {
            LoadedCommand = new AsyncRelayCommand(OnLoaded);
            CloseCommand = new RelayCommand<bool?>((result) =>
            {
                if (result is not bool boolResult)
                {
                    boolResult = false;
                }

                CloseRequested?.Invoke(this, new(boolResult));
            });
        }

        protected virtual Task OnLoaded()
        {
            return Task.CompletedTask;
        }
    }
}