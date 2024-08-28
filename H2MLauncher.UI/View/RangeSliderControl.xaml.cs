using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace H2MLauncher.UI.View.Controls;

public partial class RangeSliderControl : UserControl
{
    public RangeSliderControl()
    {
        InitializeComponent();
        this.Loaded += RangeSlider_Loaded;
    }

    void RangeSlider_Loaded(object sender, RoutedEventArgs e)
    {
        lowerSlider.ValueChanged += LowerSlider_ValueChanged;
        upperSlider.ValueChanged += UpperSlider_ValueChanged;
    }

    private void LowerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        upperSlider.Value = Math.Max(upperSlider.Value, lowerSlider.Value);
    }

    private void UpperSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        lowerSlider.Value = Math.Min(upperSlider.Value, lowerSlider.Value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register("Minimum", typeof(double), typeof(RangeSliderControl), new UIPropertyMetadata(0d));
    public static readonly DependencyProperty LowerValueProperty =
        DependencyProperty.Register("LowerValue", typeof(double), typeof(RangeSliderControl), new UIPropertyMetadata(0d, null));
    public static readonly DependencyProperty UpperValueProperty =
        DependencyProperty.Register("UpperValue", typeof(double), typeof(RangeSliderControl), new UIPropertyMetadata(1d, null));
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register("Maximum", typeof(double), typeof(RangeSliderControl), new UIPropertyMetadata(1d));
    public static readonly DependencyProperty IsSnapToTickEnabledProperty =
        DependencyProperty.Register("IsSnapToTickEnabled", typeof(bool), typeof(RangeSliderControl), new UIPropertyMetadata(false));
    public static readonly DependencyProperty TickFrequencyProperty =
        DependencyProperty.Register("TickFrequency", typeof(double), typeof(RangeSliderControl), new UIPropertyMetadata(0.1d));
    public static readonly DependencyProperty TickPlacementProperty =
        DependencyProperty.Register("TickPlacement", typeof(TickPlacement), typeof(RangeSliderControl), new UIPropertyMetadata(TickPlacement.None));
    public static readonly DependencyProperty TicksProperty =
        DependencyProperty.Register("Ticks", typeof(DoubleCollection), typeof(RangeSliderControl), new UIPropertyMetadata(null));

    public double Minimum
    {
        get { return (double)GetValue(MinimumProperty); }
        set { SetValue(MinimumProperty, value); }
    }

    public double LowerValue
    {
        get { return (double)GetValue(LowerValueProperty); }
        set { SetValue(LowerValueProperty, value); }
    }

    public double UpperValue
    {
        get { return (double)GetValue(UpperValueProperty); }
        set { SetValue(UpperValueProperty, value); }
    }

    public double Maximum
    {
        get { return (double)GetValue(MaximumProperty); }
        set { SetValue(MaximumProperty, value); }
    }

    public bool IsSnapToTickEnabled
    {
        get { return (bool)GetValue(IsSnapToTickEnabledProperty); }
        set { SetValue(IsSnapToTickEnabledProperty, value); }
    }

    public double TickFrequency
    {
        get { return (double)GetValue(TickFrequencyProperty); }
        set { SetValue(TickFrequencyProperty, value); }
    }

    public TickPlacement TickPlacement
    {
        get { return (TickPlacement)GetValue(TickPlacementProperty); }
        set { SetValue(TickPlacementProperty, value); }
    }

    public DoubleCollection Ticks
    {
        get { return (DoubleCollection)GetValue(TicksProperty); }
        set { SetValue(TicksProperty, value); }
    }

    private static object LowerValueCoerceValueCallback(DependencyObject target, object valueObject)
    {
        RangeSliderControl targetSlider = (RangeSliderControl)target;
        double value = (double)valueObject;

        return Math.Min(value, targetSlider.UpperValue);
    }

    private static object UpperValueCoerceValueCallback(DependencyObject target, object valueObject)
    {
        RangeSliderControl targetSlider = (RangeSliderControl)target;
        double value = (double)valueObject;

        return Math.Max(value, targetSlider.LowerValue);
    }
}
public class ComboBoxSelectionBoxAltTemplateBehaviour
{




    public static object? GetSelectionBoxAltContent(DependencyObject obj)
    {
        return (object?)obj.GetValue(SelectionBoxAltContentProperty);
    }

    public static void SetSelectionBoxAltContent(DependencyObject obj, object? value)
    {
        obj.SetValue(SelectionBoxAltContentProperty, value);
    }

    // Using a DependencyProperty as the backing store for SelectionBoxAltContent.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SelectionBoxAltContentProperty =
        DependencyProperty.RegisterAttached("SelectionBoxAltContent", typeof(object), typeof(ComboBoxSelectionBoxAltTemplateBehaviour), new PropertyMetadata(null));




    public static bool GetIsHitTestVisible(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsHitTestVisibleProperty);
    }

    public static void SetIsHitTestVisible(DependencyObject obj, bool value)
    {
        obj.SetValue(IsHitTestVisibleProperty, value);
    }

    // Using a DependencyProperty as the backing store for IsHitTestVisible.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsHitTestVisibleProperty =
        DependencyProperty.RegisterAttached("IsHitTestVisible", typeof(bool), typeof(ComboBoxSelectionBoxAltTemplateBehaviour), new PropertyMetadata(false));




    public static readonly DependencyProperty SelectionBoxAltTemplateProperty = DependencyProperty.RegisterAttached(
        "SelectionBoxAltTemplate", typeof(DataTemplate), typeof(ComboBoxSelectionBoxAltTemplateBehaviour), new PropertyMetadata(default(DataTemplate)));

    public static void SetSelectionBoxAltTemplate(DependencyObject element, DataTemplate value)
    {
        element.SetValue(SelectionBoxAltTemplateProperty, value);
    }

    public static DataTemplate GetSelectionBoxAltTemplate(DependencyObject element)
    {
        return (DataTemplate)element.GetValue(SelectionBoxAltTemplateProperty);
    }

}