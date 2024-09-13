using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class QueueDialogView : UserControl
    {
        private Storyboard? _marqueeStoryboard;

        public QueueDialogView()
        {
            InitializeComponent();
        }

        private void ServerNameTextBox_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AnimateServerNameMarquee();
        }

        private void ServerNameTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // when actual width is set after text bound
            AnimateServerNameMarquee();
        }

        void AnimateServerNameMarquee()
        {
            double scrollSpan = serverTextContainer.Width - serverName.ActualWidth;
            if (scrollSpan >= 0)
            {
                // center text
                Canvas.SetLeft(serverName, scrollSpan / 2.0);

                // Text is smaller that container, dont animate
                _marqueeStoryboard?.Stop();
                _marqueeStoryboard = null;
                serverName.RenderTransform = new TranslateTransform();
                return;
            }

            Canvas.SetLeft(serverName, 0);
            TimeSpan duration = TimeSpan.FromSeconds(4) * Math.Abs(scrollSpan) / 160.0;
            TimeSpan delay = TimeSpan.FromSeconds(3);
            TimeSpan initialDelay = TimeSpan.FromSeconds(3);
            TimeSpan endDelay = TimeSpan.FromSeconds(5);

            _marqueeStoryboard = new Storyboard();
            _marqueeStoryboard.Children.Add(
                new DoubleAnimation()
                {
                    From = 0,
                    To = scrollSpan,
                    Duration = duration,
                });

            _marqueeStoryboard.Children.Add(
                new DoubleAnimation()
                {
                    From = scrollSpan,
                    To = 0,
                    Duration = duration,
                    BeginTime = duration + delay,
                });

            _marqueeStoryboard.BeginTime = initialDelay;
            _marqueeStoryboard.Duration = duration * 2 + delay + endDelay;

            foreach (var animation in _marqueeStoryboard.Children)
            {
                Storyboard.SetTarget(animation, serverName);
                Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            }

            _marqueeStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _marqueeStoryboard.Begin();
        }

        private void NumberTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = NumberRegex();
            e.Handled = regex.IsMatch(e.Text);
        }

        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NumberRegex();
    }
}
