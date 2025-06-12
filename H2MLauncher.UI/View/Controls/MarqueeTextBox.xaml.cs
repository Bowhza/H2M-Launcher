using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace H2MLauncher.UI.View.Controls;

public partial class MarqueeTextBox : ContentControl
{
    private Storyboard? _marqueeStoryboard;

    public MarqueeTextBox()
    {
        InitializeComponent();
    }

    private void TextBox_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        AnimateServerNameMarquee();
    }

    private void TextBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // when actual width is set after text bound
        AnimateServerNameMarquee();
    }
    private void Container_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AnimateServerNameMarquee();
    }

    private FrameworkElement FindTextBox() => (FrameworkElement)Template.FindName("textBox", this);
    private Canvas FindContainer() => (Canvas)Template.FindName("container", this);

    void AnimateServerNameMarquee()
    {
        FrameworkElement textBox = FindTextBox();
        Canvas container = FindContainer();

        double scrollSpan = container.Width - textBox.ActualWidth;
        if (scrollSpan >= 0)
        {
            // center text
            Canvas.SetLeft(textBox, scrollSpan / 2.0);

            // Text is smaller that container, dont animate
            _marqueeStoryboard?.Stop();
            _marqueeStoryboard = null;
            textBox.RenderTransform = new TranslateTransform();
            return;
        }

        if (!double.IsRealNumber(scrollSpan)) {
            return;
        }

        Canvas.SetLeft(textBox, 0);
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
            Storyboard.SetTarget(animation, textBox);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        }

        _marqueeStoryboard.RepeatBehavior = RepeatBehavior.Forever;
        _marqueeStoryboard.Begin();
    }

    public string Text
    {
        get { return (string)GetValue(TextProperty); }
        set { SetValue(TextProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Text.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(MarqueeTextBox), new PropertyMetadata(null));


    public IEnumerable<Inline> Inlines
    {
        get { return (IEnumerable<Inline>)GetValue(InlinesProperty); }
        set { SetValue(InlinesProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Inlines.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty InlinesProperty =
        DependencyProperty.Register("Inlines", typeof(IEnumerable<Inline>), typeof(MarqueeTextBox), new PropertyMetadata(null));
}
