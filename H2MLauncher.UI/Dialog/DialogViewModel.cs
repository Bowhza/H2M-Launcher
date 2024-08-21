using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public partial class DialogViewModel : DialogViewModelBase
    {
        private TextDialogViewModel _textDialogViewModel;

        [ObservableProperty]
        private DialogViewModelBase _activeDialogViewModel;

        public IRelayCommand<DialogContent> DisplayTextDialogCommand { get; private set; }

        public DialogViewModel()
        {
            DisplayTextDialogCommand = new RelayCommand<DialogContent>(DoDisplayTextDialogCommand);
        }

        public void ClearViewModels()
        {
            ActiveDialogViewModel = null;
            _textDialogViewModel = null;
        }

        private void DoDisplayTextDialogCommand(DialogContent dialogContent)
        {
            ActiveDialogViewModel = (_textDialogViewModel = new TextDialogViewModel(dialogContent));
        }
    }
}
