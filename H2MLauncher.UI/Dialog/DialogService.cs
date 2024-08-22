using System.Windows;

namespace H2MLauncher.UI.Dialog
{
    public class DialogService
    {
        private static DialogWindow s_dialogWindow;
        private readonly DialogViewModel _dialogViewModel;

        public DialogService(DialogViewModel dialogViewModel)
        {
            _dialogViewModel = dialogViewModel ?? throw new ArgumentNullException(nameof(dialogViewModel));
        }

        private static void InstantiateDialog()
        {
            s_dialogWindow = new DialogWindow
            {
                Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive)
            };
        }

        public static void CloseDialogWindow()
        {
            s_dialogWindow.Close();
        }

        public void OpenTextDialog(string title, string text)
        {
            InstantiateDialog();
            _dialogViewModel.DisplayTextDialogCommand.Execute(new DialogContent() { Title = title, Text = text });
            s_dialogWindow.ShowDialog();
            _dialogViewModel.ClearViewModels();  
        }
    }
}
