using System.Reflection;
using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.Dialog.Views;

namespace H2MLauncher.UI.Dialog
{
    public class DialogService
    {
        private DialogWindow? _dialogWindow;

        private DialogWindow CreateDialog(UserControl content)
        {
            _dialogWindow = new DialogWindow
            {
                Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive),
                Content = content,
            };

            return _dialogWindow;
        }

        public void CloseDialogWindow()
        {
            _dialogWindow?.Close();
        }

        public void OpenTextDialog(string title, string text)
        {
            OpenDialog<TextDialogView>(
                new TextDialogViewModel(new DialogContent()
                {
                    Title = title,
                    Text = text
                }));
        }

        public void OpenDialog<T>(IDialogViewModel viewModel) where T : UserControl, new()
        {
            var dialogWindow = CreateDialog(new T()
            {
                DataContext = viewModel
            });

            void onCloseRequested(object? sender, RequestCloseEventArgs e)
            {
                dialogWindow.DialogResult = e.DialogResult;
                viewModel.CloseRequested -= onCloseRequested;
            }

            viewModel.CloseRequested += onCloseRequested;

            _dialogWindow?.ShowDialog();
        }
    }
}
