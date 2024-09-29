using System.Windows;

using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public class TextDialogViewModel : DialogViewModelBase
    {
        public required string Title { get; init; }
        public required string Text { get; init; }

        public IRelayCommand AcceptCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public bool HasCancelButton { get; }

        public string AcceptButtonText { get; init; } = "OK";

        public string CancelButtonText { get; init; } = "Cancel";

        public TextDialogViewModel(MessageBoxButton buttons = MessageBoxButton.OK)
        {
            AcceptCommand = new RelayCommand(() => CloseCommand.Execute(true), () => CloseCommand.CanExecute(true));
            CancelCommand = new RelayCommand(() => CloseCommand.Execute(false), () => CloseCommand.CanExecute(false));

            HasCancelButton = buttons is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel or MessageBoxButton.YesNo;
            AcceptButtonText = buttons is MessageBoxButton.OK or MessageBoxButton.OKCancel ? "OK" : "Yes";
            CancelButtonText = buttons is MessageBoxButton.YesNo ? "No" : "Cancel";
        }
    }
}
