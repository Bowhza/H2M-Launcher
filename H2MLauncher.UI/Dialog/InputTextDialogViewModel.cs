using System.Windows;

namespace H2MLauncher.UI.Dialog
{
    public class InputTextDialogViewModel : TextDialogViewModel
    {
        public override bool HasInput => true;
        public string? Input { get; init; }
        public bool AcceptOnPaste { get; init; } = true;

        public InputTextDialogViewModel(MessageBoxButton buttons = MessageBoxButton.OK) : base(buttons) { }
    }
}
