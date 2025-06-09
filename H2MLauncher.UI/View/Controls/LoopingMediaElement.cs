using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace H2MLauncher.UI.View.Controls;

public sealed class LoopingMediaElement : MediaElement
{
    /// <summary>
    /// Whether the media should be looped.
    /// </summary>
    public bool Loop
    {
        get { return (bool)GetValue(LoopProperty); }
        set { SetValue(LoopProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Loop.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LoopProperty =
        DependencyProperty.Register("Loop", typeof(bool), typeof(LoopingMediaElement), new PropertyMetadata(true, OnLoopChanged));    

    public LoopingMediaElement()
    {
        MediaEnded += OnMediaEnded;
        UnloadedBehavior = MediaState.Manual;
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (Loop && UnloadedBehavior is MediaState.Manual)
        {
            // loop the video
            Position = Source.AbsolutePath.EndsWith(".gif", StringComparison.InvariantCultureIgnoreCase)
                ? TimeSpan.FromMilliseconds(1)
                : TimeSpan.Zero;
            Play();
        }
    }

    private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LoopingMediaElement mediaElement) return;
        if (mediaElement.UnloadedBehavior is not MediaState.Manual) return;
        if (e.NewValue is bool boolValue && boolValue == true)
        {
            mediaElement.Play();
        }
    }
}
