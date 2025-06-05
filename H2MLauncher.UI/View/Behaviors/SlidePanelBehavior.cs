namespace H2MLauncher.UI.View;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

public static class SlidePanelBehavior
{
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.RegisterAttached("IsExpanded", typeof(bool), typeof(SlidePanelBehavior),
                                            new PropertyMetadata(false, OnIsExpandedChanged));

    public static bool GetIsExpanded(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsExpandedProperty);
    }

    public static void SetIsExpanded(DependencyObject obj, bool value)
    {
        obj.SetValue(IsExpandedProperty, value);
    }

    public static readonly DependencyProperty TargetColumnNameProperty =
        DependencyProperty.RegisterAttached("TargetColumnName", typeof(string), typeof(SlidePanelBehavior), new PropertyMetadata(null));

    public static string GetTargetColumnName(DependencyObject obj)
    {
        return (string)obj.GetValue(TargetColumnNameProperty);
    }

    public static void SetTargetColumnName(DependencyObject obj, string value)
    {
        obj.SetValue(TargetColumnNameProperty, value);
    }

    public static readonly DependencyProperty CollapsedWidthProperty =
        DependencyProperty.RegisterAttached("CollapsedWidth", typeof(double), typeof(SlidePanelBehavior), new PropertyMetadata(0.0));

    public static double GetCollapsedWidth(DependencyObject obj)
    {
        return (double)obj.GetValue(CollapsedWidthProperty);
    }

    public static void SetCollapsedWidth(DependencyObject obj, double value)
    {
        obj.SetValue(CollapsedWidthProperty, value);
    }

    public static readonly DependencyProperty ExpandedWidthProperty =
        DependencyProperty.RegisterAttached("ExpandedWidth", typeof(double), typeof(SlidePanelBehavior), new PropertyMetadata(290.0)); // Default to your control's width

    public static double GetExpandedWidth(DependencyObject obj)
    {
        return (double)obj.GetValue(ExpandedWidthProperty);
    }

    public static void SetExpandedWidth(DependencyObject obj, double value)
    {
        obj.SetValue(ExpandedWidthProperty, value);
    }    

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element) // Element this behavior is attached to (your SocialOverviewControl)
        {
            bool isExpanded = (bool)e.NewValue;

            // 1. Get/Create the TranslateTransform for the element itself
            TranslateTransform? translateTransform = element.RenderTransform as TranslateTransform;
            if (translateTransform is null)
            {
                translateTransform = new TranslateTransform();
                element.RenderTransform = translateTransform;
                // Ensure initial state is set for the transform
                translateTransform.X = GetExpandedWidth(element); // Off-screen initially
            }

            // 2. Find the target ColumnDefinition
            Grid? parentGrid = FindAncestor<Grid>(element);
            ColumnDefinition? targetColumn = null;
            string targetColumnName = GetTargetColumnName(element);

            if (parentGrid is not null && !string.IsNullOrEmpty(targetColumnName))
            {
                foreach (ColumnDefinition colDef in parentGrid.ColumnDefinitions)
                {
                    if (colDef.ReadLocalValue(FrameworkElement.NameProperty) as string == targetColumnName)
                    {
                        targetColumn = colDef;
                        break;
                    }
                }
            }

            if (targetColumn is null)
            {
                // This behavior needs a target column to animate its width
                // Log an error or return if not found.
                // Console.WriteLine($"Warning: Could not find ColumnDefinition named '{targetColumnName}'.");
                return;
            }

            // --- Start the animations ---
            Storyboard sb = new Storyboard();
            Duration animationDuration = TimeSpan.FromSeconds(0.2);
            IEasingFunction easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            IEasingFunction easeIn = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            // Animation for the Column Width
            GridLengthAnimation columnWidthAnimation = new GridLengthAnimation()
            {
                Duration = animationDuration,
                From = new GridLength(isExpanded ? GetCollapsedWidth(element) : GetExpandedWidth(element)),
                EasingFunction = isExpanded ? easeOut : easeIn,
                To = new GridLength(isExpanded ? GetExpandedWidth(element) : GetCollapsedWidth(element)),
            };
            
            Storyboard.SetTarget(columnWidthAnimation, targetColumn);
            Storyboard.SetTargetProperty(columnWidthAnimation, new PropertyPath(ColumnDefinition.WidthProperty)); // Target GridLength.Value

            sb.Children.Add(columnWidthAnimation);

            // Animation for the TranslateTransform.X
            DoubleAnimation translateAnimation = new DoubleAnimation
            {
                Duration = animationDuration,
                EasingFunction = isExpanded ? easeOut : easeIn,
                To = isExpanded ? 0 : GetExpandedWidth(element) // Slide to 0 (visible) or to its width (off-screen)
            };
            Storyboard.SetTarget(translateAnimation, translateTransform);
            Storyboard.SetTargetProperty(translateAnimation, new PropertyPath(TranslateTransform.XProperty));

            sb.Children.Add(translateAnimation);

            sb.Begin();

            // Set visibility during the animation for interactive controls
            // You might want to control visibility more tightly depending on the exact effect.
            // If the Visibility binding is still there, this might override it temporarily.
            // Consider if you need a converter that delays Visibility.Collapsed until animation is done.
            if (isExpanded)
            {
                element.Visibility = Visibility.Visible;
            }
            else
            {
                // To hide after animation is complete:
                sb.Completed += (s, args) => element.Visibility = Visibility.Collapsed;
            }
        }
    }

    // Helper to find ancestor (useful for finding the Grid containing the ColumnDefinition)
    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        do
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T ancestor)
            {
                return ancestor;
            }
        } while (current is not null);
        return null;
    }
}

public class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation));

    public GridLength From
    {
        get { return (GridLength)GetValue(FromProperty); }
        set { SetValue(FromProperty, value); }
    }

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation));

    public GridLength To
    {
        get { return (GridLength)GetValue(ToProperty); }
        set { SetValue(ToProperty, value); }
    }

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register("EasingFunction", typeof(IEasingFunction), typeof(GridLengthAnimation));

    public IEasingFunction EasingFunction
    {
        get { return (IEasingFunction)GetValue(EasingFunctionProperty); }
        set { SetValue(EasingFunctionProperty, value); }
    }

    public override Type TargetPropertyType
    {
        get { return typeof(GridLength); }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        // Ensure the animation has a progress value
        if (!animationClock.CurrentProgress.HasValue)
            return GridLength.Auto; // Or a sensible default if animation hasn't started/ended

        double progress = animationClock.CurrentProgress.Value;

        // Apply easing function if one is set
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        GridLength fromValue = From; // Use the Dependency Property
        GridLength toValue = To;     // Use the Dependency Property

        // Handle GridUnitType.Auto cases separately if necessary.
        // Animating to/from Auto is not directly supported by linear interpolation.
        // For simplicity, if either is Auto, we'll snap to the 'To' value at the end.
        if (fromValue.GridUnitType is GridUnitType.Auto or GridUnitType.Auto)
        {
            if (progress >= 1.0)
            {
                return toValue;
            }
            else
            {
                return fromValue;
            }
        }

        // Extract the numerical values for interpolation
        double fromVal = fromValue.Value;
        double toVal = toValue.Value;

        // Determine the target GridUnitType based on the 'From' value.
        // This assumes From and To will have the same unit type for a smooth animation.
        // If you need to animate between Pixel and Star, more complex logic is required
        // that understands the actual pixel width of the grid at the start of the animation.
        GridUnitType targetUnitType = fromValue.GridUnitType;

        double animatedValue;

        // Your provided logic for interpolation
        if (fromVal > toVal)
        {
            animatedValue = (1 - progress) * (fromVal - toVal) + toVal;
        }
        else
        {
            animatedValue = progress * (toVal - fromVal) + fromVal;
        }

        return new GridLength(animatedValue, targetUnitType);
    }
}
