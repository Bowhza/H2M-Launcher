using CommunityToolkit.Mvvm.Input;

namespace H2MLauncher.UI.Dialog
{
    public class TextDialogViewModel : DialogViewModelBase
    {
        public required string Title { get; set; }
        public required string Text { get; set; }
    }
}
