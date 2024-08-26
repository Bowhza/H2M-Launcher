using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.Dialog;

namespace H2MLauncher.UI
{
    public class DialogBehavior : FrameworkElement
    {
        public IDialogViewModel ViewModel
        {
            get { return (IDialogViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(IDialogViewModel), typeof(DialogBehavior),
                new PropertyMetadata(new PropertyChangedCallback(OnViewModelPropertyChanged)));

        public Type Dialog
        {
            get { return (Type)GetValue(DialogProperty); }
            set { SetValue(DialogProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DialogProperty =
            DependencyProperty.Register("Dialog", typeof(Type), typeof(DialogBehavior));

        private static void OnViewModelPropertyChanged(
            DependencyObject inDependencyObject, DependencyPropertyChangedEventArgs inEventArgs)
        {
            if (inDependencyObject is not DialogBehavior dialogBehavior)
            {
                return;
            }

            if (dialogBehavior.Dialog == null) { return; }

            if (dialogBehavior.ViewModel is null)
            {
                //dialogBehavior.Dialog.Close();
                return;
            }

            if (!(dialogBehavior.Dialog.BaseType?.IsAssignableTo(typeof(Control)) ?? false))
            {
                return;
            }

            var dialogContent = Activator.CreateInstance(dialogBehavior.Dialog) as Control;
            if (dialogContent is null)
            {
                return;
            }

            dialogContent.DataContext = dialogBehavior.ViewModel;

            _ = Task.Factory.StartNew(() =>
            {
                DialogService.OpenDialog(dialogBehavior.ViewModel, dialogContent);
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
