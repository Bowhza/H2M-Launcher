using System.Windows;

namespace H2MLauncher.UI.View.Controls;

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

    public static void SetSelectionBoxAltTemplate(DependencyObject element, DataTemplate value)
    {
        element.SetValue(SelectionBoxAltTemplateProperty, value);
    }

    public static DataTemplate GetSelectionBoxAltTemplate(DependencyObject element)
    {
        return (DataTemplate)element.GetValue(SelectionBoxAltTemplateProperty);
    }

    public static readonly DependencyProperty SelectionBoxAltTemplateProperty = DependencyProperty.RegisterAttached(
        "SelectionBoxAltTemplate", typeof(DataTemplate), typeof(ComboBoxSelectionBoxAltTemplateBehaviour), new PropertyMetadata(default(DataTemplate)));
}