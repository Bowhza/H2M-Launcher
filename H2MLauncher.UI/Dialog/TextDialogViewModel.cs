using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public class TextDialogViewModel : DialogViewModelBase
    {
        public DialogContent DialogContent { get; }
        public IRelayCommand ContinueCommand { get; private set; }

        public TextDialogViewModel(DialogContent dialogContent)
        {
            DialogContent = dialogContent ?? throw new ArgumentNullException(nameof(dialogContent));
            ContinueCommand = new RelayCommand(DoContinueCommand);
        }

        private void DoContinueCommand()
        {
            DialogService.CloseDialogWindow();
        }
    }
}
