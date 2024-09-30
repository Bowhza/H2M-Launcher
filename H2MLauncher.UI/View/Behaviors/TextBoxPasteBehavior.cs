using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace H2MLauncher.UI
{
    public class TextBoxPasteBehavior
    {
        public static readonly DependencyProperty PasteCommandProperty =
            DependencyProperty.RegisterAttached(
                "PasteCommand",
                typeof(ICommand),
                typeof(TextBoxPasteBehavior),
                new FrameworkPropertyMetadata(PasteCommandChanged)
            );

        public static ICommand GetPasteCommand(DependencyObject target)
        {
            return (ICommand)target.GetValue(PasteCommandProperty);
        }

        public static void SetPasteCommand(DependencyObject target, ICommand value)
        {
            target.SetValue(PasteCommandProperty, value);
        }

        static void PasteCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var newValue = (ICommand)e.NewValue;

            if (newValue != null)
                textBox.AddHandler(CommandManager.ExecutedEvent, new RoutedEventHandler(CommandExecuted), true);
            else
                textBox.RemoveHandler(CommandManager.ExecutedEvent, new RoutedEventHandler(CommandExecuted));

        }

        static void CommandExecuted(object sender, RoutedEventArgs e)
        {
            if (((ExecutedRoutedEventArgs)e).Command != ApplicationCommands.Paste) return;

            var textBox = (TextBox)sender;
            var command = GetPasteCommand(textBox);

            if (command.CanExecute(textBox.Text))
                command.Execute(textBox.Text);
        }
    }
}
