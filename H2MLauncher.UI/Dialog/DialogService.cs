using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.Dialog.Views;

namespace H2MLauncher.UI.Dialog
{
    public class DialogService
    {
        private static DialogWindow CreateDialog(Control content)
        {
            return new DialogWindow
            {
                Owner = Application.Current.MainWindow,
                Content = content,
            };
        }

        private static void PrepareDialogWindow(IDialogViewModel viewModel, DialogWindow dialogWindow)
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
                dialogWindow.Loaded -= onLoaded;
            }

            void onLoaded(object? sender, RoutedEventArgs args)
            {
                if (viewModel.LoadedCommand.CanExecute(null))
                {
                    viewModel.LoadedCommand.Execute(null);
                }
            }

            viewModel.CloseRequested += onCloseRequested;
            dialogWindow.Closed += onClosed;
            dialogWindow.Loaded += onLoaded;
        }

        public static bool? ShowDialog(IDialogViewModel viewModel, DialogWindow dialogWindow)
        {
            PrepareDialogWindow(viewModel, dialogWindow);

            return dialogWindow.ShowDialog();
        }

        public static bool? OpenDialog(IDialogViewModel viewModel, Control dialogContent)
        {
            DialogWindow dialogWindow = CreateDialog(dialogContent);

            return ShowDialog(viewModel, dialogWindow);
        }

        public bool? OpenTextDialog(string title, string text, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            return OpenDialog<TextDialogView>(
                new TextDialogViewModel(buttons)
                {
                    Title = title,
                    Text = text,
                });
        }

        public bool? OpenTextDialog(string title, string text, string acceptButtonText, string cancelButtonText = "Cancel")
        {
            MessageBoxButton buttons = string.IsNullOrEmpty(cancelButtonText) ? MessageBoxButton.OK : MessageBoxButton.OKCancel;

            return OpenDialog<TextDialogView>(
                new TextDialogViewModel(buttons)
                {
                    Title = title,
                    Text = text,
                    AcceptButtonText = acceptButtonText,
                    CancelButtonText = cancelButtonText
                });
        }

        public string? OpenInputDialog(string title, string text, string? initialInput = null,
            string acceptButtonText = "OK", string cancelButtonText = "Cancel")
        {
            MessageBoxButton buttons = string.IsNullOrEmpty(cancelButtonText) ? MessageBoxButton.OK : MessageBoxButton.OKCancel;
            InputTextDialogViewModel viewModel = new(buttons)
            {
                Title = title,
                Text = text,
                Input = initialInput,
                AcceptButtonText = acceptButtonText,
                CancelButtonText = cancelButtonText
            };

            bool? result = OpenDialog<InputTextDialogView>(viewModel);
            if (result == true)
            {
                return viewModel.Input;
            }

            return null;
        }

        public bool? OpenDialog<TDialog>(IDialogViewModel viewModel) where TDialog : Control, new()
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogWindow = CreateDialog(new TDialog()
                {
                    DataContext = viewModel
                });

                return ShowDialog(viewModel, dialogWindow);
            });
        }

        public Task<bool?> ShowDialogAsync<TDialog>(IDialogViewModel viewModel)
            where TDialog : Control, new()
        {
            var dialogWindow = Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogWindow = CreateDialog(new TDialog()
                {
                    DataContext = viewModel
                });

                PrepareDialogWindow(viewModel, dialogWindow);

                return dialogWindow;
            });

            return dialogWindow.ShowDialogAsync();
        }
    }
}
