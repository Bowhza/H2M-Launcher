using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace H2MLauncher.UI.View.Controls
{
    public partial class FriendsControl : UserControl
    {
        //private Storyboard _pulseStoryboard;

        public FriendsControl()
        {
            InitializeComponent();
            //InitializePulseAnimation();
        }

        private void ListBoxItem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListBoxItem_GotFocus(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void DetailsPopup_Opened(object sender, EventArgs e)
        {
            if (sender is not Popup detailsPopup) return;
            detailsPopup.Child.Focus();
        }

        private void DetailsPopup_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        //[MemberNotNull(nameof(_pulseStoryboard))]
        //private void InitializePulseAnimation()
        //{
        //    // 1. Create the Storyboard
        //    _pulseStoryboard = new Storyboard();

        //    // 2. Create the DoubleAnimation for Opacity
        //    DoubleAnimation opacityAnimation = new DoubleAnimation
        //    {
        //        From = 1.0,  // Fully visible
        //        To = 0.5,    // Half visible (or whatever minimum you like)
        //        Duration = new Duration(TimeSpan.FromSeconds(0.5)), // 0.5 seconds for one half-cycle
        //        AutoReverse = true,  // Go from To back to From
        //        RepeatBehavior = RepeatBehavior.Forever // Repeat indefinitely
        //    };

        //    // 3. Set the target property and target element for the animation
        //    // This tells the animation *what* property to animate and *on which element*.
        //    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(TextBlock.OpacityProperty));
        //    Storyboard.SetTarget(opacityAnimation, envelop); // This links it to your named TextBlock

        //    // 4. Add the animation to the Storyboard
        //    _pulseStoryboard.Children.Add(opacityAnimation);
        //}

        ///// <summary>
        ///// Call this method when an invite is received to start the pulsing.
        ///// </summary>
        //public void StartEnvelopePulse()
        //{
        //    // Ensure the animation is stopped and opacity is reset before starting,
        //    // to avoid abrupt jumps if it was already mid-animation.
        //    _pulseStoryboard.Stop();
        //    envelopeTextBlock.Opacity = 1.0; // Reset to fully visible

        //    _pulseStoryboard.Begin();
        //}

        ///// <summary>
        ///// Call this method when the invite is acknowledged or no longer needs highlighting.
        ///// </summary>
        //public void StopEnvelopePulse()
        //{
        //    _pulseStoryboard.Stop();
        //    envelopeTextBlock.Opacity = 1.0; // Reset to fully visible when stopped
        //}
    }
}
