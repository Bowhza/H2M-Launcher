using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace H2MLauncher.UI.Dialog.Views
{
    public partial class QueueDialogView : UserControl
    {
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
                return;
            }

            Canvas.SetLeft(serverName, 0);
            TimeSpan duration = TimeSpan.FromSeconds(4) * Math.Abs(scrollSpan) / 160.0;
            TimeSpan delay = TimeSpan.FromSeconds(3);
            TimeSpan initialDelay = TimeSpan.FromSeconds(3);
            TimeSpan endDelay = TimeSpan.FromSeconds(5);

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(
                new DoubleAnimation()
                {
                    From = 0,
                    To = scrollSpan,
                    Duration = duration,
                });

            storyboard.Children.Add(
                new DoubleAnimation()
                {
                    From = scrollSpan,
                    To = 0,
                    Duration = duration,
                    BeginTime = duration + delay,
                });

            storyboard.BeginTime = initialDelay;
            storyboard.Duration = duration * 2 + delay + endDelay;

            foreach (var animation in storyboard.Children)
            {
                Storyboard.SetTarget(animation, serverName);
                Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            }

            storyboard.RepeatBehavior = RepeatBehavior.Forever;
            storyboard.Begin();
        }
    }
}
