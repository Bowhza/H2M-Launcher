using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Converters;

using H2MLauncher.UI.Dialog.Views;

namespace H2MLauncher.UI.Dialog
{
    public class DialogService
    {
        private DialogWindow? _dialogWindow;

        private DialogWindow CreateDialog(Control content)
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
                new TextDialogViewModel()
                {
                    Title = title,
                    Text = text
                });
        }

        public static bool? OpenDialog(IDialogViewModel viewModel, Control dialogContent)
        {
            var dialogWindow = new DialogWindow
            {
                Owner = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive),
                Content = dialogContent,
            };

            return ShowDialog(viewModel, dialogWindow);
        }

        public static bool? ShowDialog(IDialogViewModel viewModel, DialogWindow dialogWindow)
        {
            bool isClosed = false;

            void onCloseRequested(object? sender, RequestCloseEventArgs e)
            {
                if (isClosed)
                {
                    return;
                }

                dialogWindow.DialogResult = e.DialogResult;
                viewModel.CloseRequested -= onCloseRequested;
            }

            void onClosed(object? sender, EventArgs args)
            {
                isClosed = true;
                dialogWindow.Closed -= onClosed;
            }

            viewModel.CloseRequested += onCloseRequested;
            dialogWindow.Closed += onClosed;

            return dialogWindow.ShowDialog();
        }

        public bool? OpenDialog<TDialog>(IDialogViewModel viewModel) where TDialog : Control, new()
        {
            var dialogWindow = CreateDialog(new TDialog()
            {
                DataContext = viewModel
            });

            return ShowDialog(viewModel, dialogWindow);
        }

        public Task<bool?> ShowDialogAsync<TDialog>(IDialogViewModel viewModel)
            where TDialog : Control, new()
        {
            var dialogWindow = CreateDialog(new TDialog()
            {
                DataContext = viewModel
            });

            return dialogWindow.ShowDialogAsync();
        }
    }
}
