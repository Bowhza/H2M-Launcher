using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace H2MLauncher.UI.View;

// From this sample: https://github.com/bstollnitz/old-wpf-blog/tree/master/46-DragDropListBox
public class InsertionAdorner : Adorner
{
    private readonly bool _isSeparatorHorizontal;
    public bool IsInFirstHalf { get; set; }
    private readonly AdornerLayer _adornerLayer;
    private readonly static Pen Pen;
    private readonly static PathGeometry Triangle;

    // Create the pen and triangle in a static constructor and freeze them to improve performance.
    static InsertionAdorner()
    {
        Pen = new Pen { Brush = Brushes.Gray, Thickness = 2 };
        Pen.Freeze();

        LineSegment firstLine = new LineSegment(new Point(0, -5), false);
        firstLine.Freeze();
        LineSegment secondLine = new LineSegment(new Point(0, 5), false);
        secondLine.Freeze();

        PathFigure figure = new PathFigure { StartPoint = new Point(5, 0) };
        figure.Segments.Add(firstLine);
        figure.Segments.Add(secondLine);
        figure.Freeze();

        Triangle = new PathGeometry();
        Triangle.Figures.Add(figure);
        Triangle.Freeze();
    }

    public InsertionAdorner(bool isSeparatorHorizontal, bool isInFirstHalf, UIElement adornedElement, AdornerLayer adornerLayer)
        : base(adornedElement)
    {
        this._isSeparatorHorizontal = isSeparatorHorizontal;
        this.IsInFirstHalf = isInFirstHalf;
        this._adornerLayer = adornerLayer;
        this.IsHitTestVisible = false;

        this._adornerLayer.Add(this);
    }

    // This draws one line and two triangles at each end of the line.
    protected override void OnRender(DrawingContext drawingContext)
    {
        Point startPoint;
        Point endPoint;

        CalculateStartAndEndPoint(out startPoint, out endPoint);
        drawingContext.DrawLine(Pen, startPoint, endPoint);

        if (this._isSeparatorHorizontal)
        {
            DrawTriangle(drawingContext, startPoint, 0);
            DrawTriangle(drawingContext, endPoint, 180);
        }
        else
        {
            DrawTriangle(drawingContext, startPoint, 90);
            DrawTriangle(drawingContext, endPoint, -90);
        }
    }

    private void DrawTriangle(DrawingContext drawingContext, Point origin, double angle)
    {
        drawingContext.PushTransform(new TranslateTransform(origin.X, origin.Y));
        drawingContext.PushTransform(new RotateTransform(angle));

        drawingContext.DrawGeometry(Pen.Brush, null, Triangle);

        drawingContext.Pop();
        drawingContext.Pop();
    }

    private void CalculateStartAndEndPoint(out Point startPoint, out Point endPoint)
    {
        startPoint = new Point();
        endPoint = new Point();

        double width = this.AdornedElement.RenderSize.Width;
        double height = this.AdornedElement.RenderSize.Height;

        if (this._isSeparatorHorizontal)
        {
            endPoint.X = width;
            if (!this.IsInFirstHalf)
            {
                startPoint.Y = height;
                endPoint.Y = height;
            }
        }
        else
        {
            endPoint.Y = height;
            if (!this.IsInFirstHalf)
            {
                startPoint.X = width;
                endPoint.X = width;
            }
        }
    }

    public void Detach()
    {
        this._adornerLayer.Remove(this);
    }

}

public class DraggedAdorner : Adorner
{
    private readonly ContentPresenter _contentPresenter;
    private double _left;
    private double _top;
    private readonly AdornerLayer _adornerLayer;

    public DraggedAdorner(object dragDropData, DataTemplate dragDropTemplate, UIElement adornedElement, AdornerLayer adornerLayer)
        : base(adornedElement)
    {
        this._adornerLayer = adornerLayer;

        this._contentPresenter = new ContentPresenter();
        this._contentPresenter.Content = dragDropData;
        this._contentPresenter.ContentTemplate = dragDropTemplate;
        this._contentPresenter.Opacity = 0.7;

        this._adornerLayer.Add(this);
    }

    public void SetPosition(double left, double top)
    {
        // -1 and +13 align the dragged adorner with the dashed rectangle that shows up
        // near the mouse cursor when dragging.
        this._left = left - 1;
        this._top = top + 13;
        if (this._adornerLayer != null)
        {
            this._adornerLayer.Update(this.AdornedElement);
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        this._contentPresenter.Measure(constraint);
        return this._contentPresenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        this._contentPresenter.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override Visual GetVisualChild(int index)
    {
        return this._contentPresenter;
    }

    protected override int VisualChildrenCount
    {
        get { return 1; }
    }

    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
    {
        GeneralTransformGroup result = new GeneralTransformGroup();
        result.Children.Add(base.GetDesiredTransform(transform));
        result.Children.Add(new TranslateTransform(this._left, this._top));

        return result;
    }

    public void Detach()
    {
        this._adornerLayer.Remove(this);
    }

}

public class DragDropHelper
{
    // source and target
    private readonly DataFormat _format = DataFormats.GetDataFormat("DragDropItemsControl");
    private Point _initialMousePosition;
    private Vector _initialMouseOffset;
    private object? _draggedData;
    private DraggedAdorner? _draggedAdorner;
    private InsertionAdorner? _insertionAdorner;
    private Window? _topWindow;
    // source
    private ItemsControl? _sourceItemsControl;
    private FrameworkElement? _sourceItemContainer;
    // target
    private ItemsControl? _targetItemsControl;
    private FrameworkElement? _targetItemContainer;
    private bool _hasVerticalOrientation;
    private int _insertionIndex;
    private bool _isInFirstHalf;
    // singleton
    private static DragDropHelper? _instance;
    private static DragDropHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new DragDropHelper();
            }
            return _instance;
        }
    }

    public static bool GetIsDragSource(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDragSourceProperty);
    }

    public static void SetIsDragSource(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDragSourceProperty, value);
    }

    public static readonly DependencyProperty IsDragSourceProperty =
        DependencyProperty.RegisterAttached("IsDragSource", typeof(bool), typeof(DragDropHelper), new UIPropertyMetadata(false, IsDragSourceChanged));


    public static bool GetIsDropTarget(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDropTargetProperty);
    }

    public static void SetIsDropTarget(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDropTargetProperty, value);
    }

    public static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached("IsDropTarget", typeof(bool), typeof(DragDropHelper), new UIPropertyMetadata(false, IsDropTargetChanged));

    public static DataTemplate GetDragDropTemplate(DependencyObject obj)
    {
        return (DataTemplate)obj.GetValue(DragDropTemplateProperty);
    }

    public static void SetDragDropTemplate(DependencyObject obj, DataTemplate value)
    {
        obj.SetValue(DragDropTemplateProperty, value);
    }

    public static readonly DependencyProperty DragDropTemplateProperty =
        DependencyProperty.RegisterAttached("DragDropTemplate", typeof(DataTemplate), typeof(DragDropHelper), new UIPropertyMetadata(null));

    private static void IsDragSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
        var dragSource = obj as ItemsControl;
        if (dragSource != null)
        {
            if (Object.Equals(e.NewValue, true))
            {
                dragSource.PreviewMouseLeftButtonDown += Instance.DragSource_PreviewMouseLeftButtonDown;
                dragSource.PreviewMouseLeftButtonUp += Instance.DragSource_PreviewMouseLeftButtonUp;
                dragSource.PreviewMouseMove += Instance.DragSource_PreviewMouseMove;
            }
            else
            {
                dragSource.PreviewMouseLeftButtonDown -= Instance.DragSource_PreviewMouseLeftButtonDown;
                dragSource.PreviewMouseLeftButtonUp -= Instance.DragSource_PreviewMouseLeftButtonUp;
                dragSource.PreviewMouseMove -= Instance.DragSource_PreviewMouseMove;
            }
        }
    }

    private static void IsDropTargetChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
        var dropTarget = obj as ItemsControl;
        if (dropTarget != null)
        {
            if (Object.Equals(e.NewValue, true))
            {
                dropTarget.AllowDrop = true;
                dropTarget.PreviewDrop += Instance.DropTarget_PreviewDrop;
                dropTarget.PreviewDragEnter += Instance.DropTarget_PreviewDragEnter;
                dropTarget.PreviewDragOver += Instance.DropTarget_PreviewDragOver;
                dropTarget.PreviewDragLeave += Instance.DropTarget_PreviewDragLeave;
            }
            else
            {
                dropTarget.AllowDrop = false;
                dropTarget.PreviewDrop -= Instance.DropTarget_PreviewDrop;
                dropTarget.PreviewDragEnter -= Instance.DropTarget_PreviewDragEnter;
                dropTarget.PreviewDragOver -= Instance.DropTarget_PreviewDragOver;
                dropTarget.PreviewDragLeave -= Instance.DropTarget_PreviewDragLeave;
            }
        }
    }

    // DragSource

    private void DragSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this._sourceItemsControl = (ItemsControl)sender;
        Visual? visual = e.OriginalSource as Visual;

        this._topWindow = Window.GetWindow(this._sourceItemsControl);
        this._initialMousePosition = e.GetPosition(this._topWindow);

        this._sourceItemContainer = _sourceItemsControl.ContainerFromElement(visual) as FrameworkElement;
        if (this._sourceItemContainer != null)
        {
            this._draggedData = this._sourceItemContainer.DataContext;
        }
    }

    // Drag = mouse down + move by a certain amount
    private void DragSource_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (this._draggedData != null && this._topWindow != null && this._sourceItemContainer != null)
        {
            // Only drag when user moved the mouse by a reasonable amount.
            if (Utilities.IsMovementBigEnough(this._initialMousePosition, e.GetPosition(this._topWindow)))
            {
                this._initialMouseOffset = this._initialMousePosition - this._sourceItemContainer.TranslatePoint(new Point(0, 0), this._topWindow);

                DataObject data = new DataObject(this._format.Name, this._draggedData);

                // Adding events to the window to make sure dragged adorner comes up when mouse is not over a drop target.
                bool previousAllowDrop = this._topWindow.AllowDrop;
                this._topWindow.AllowDrop = true;
                this._topWindow.DragEnter += TopWindow_DragEnter;
                this._topWindow.DragOver += TopWindow_DragOver;
                this._topWindow.DragLeave += TopWindow_DragLeave;

                DragDropEffects effects = DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);

                // Without this call, there would be a bug in the following scenario: Click on a data item, and drag
                // the mouse very fast outside of the window. When doing this really fast, for some reason I don't get 
                // the Window leave event, and the dragged adorner is left behind.
                // With this call, the dragged adorner will disappear when we release the mouse outside of the window,
                // which is when the DoDragDrop synchronous method returns.
                RemoveDraggedAdorner();

                this._topWindow.AllowDrop = previousAllowDrop;
                this._topWindow.DragEnter -= TopWindow_DragEnter;
                this._topWindow.DragOver -= TopWindow_DragOver;
                this._topWindow.DragLeave -= TopWindow_DragLeave;

                this._draggedData = null;
            }
        }
    }

    private void DragSource_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        this._draggedData = null;
    }

    // DropTarget

    private void DropTarget_PreviewDragEnter(object sender, DragEventArgs e)
    {
        this._targetItemsControl = (ItemsControl)sender;
        object draggedItem = e.Data.GetData(this._format.Name);

        DecideDropTarget(e);
        if (draggedItem != null)
        {
            // Dragged Adorner is created on the first enter only.
            ShowDraggedAdorner(e.GetPosition(this._topWindow));
            CreateInsertionAdorner();
        }
        e.Handled = true;
    }

    private void DropTarget_PreviewDragOver(object sender, DragEventArgs e)
    {
        object draggedItem = e.Data.GetData(this._format.Name);

        DecideDropTarget(e);
        if (draggedItem != null)
        {
            // Dragged Adorner is only updated here - it has already been created in DragEnter.
            ShowDraggedAdorner(e.GetPosition(this._topWindow));
            UpdateInsertionAdornerPosition();
        }
        e.Handled = true;
    }

    private void DropTarget_PreviewDrop(object sender, DragEventArgs e)
    {
        if (this._sourceItemsControl is null)
        {
            return;
        }

        if (this._targetItemsControl is null)
        {
            return;
        }

        object draggedItem = e.Data.GetData(this._format.Name);
        int indexRemoved = -1;

        if (draggedItem != null)
        {
            int sourceIndex = this._sourceItemsControl.ItemContainerGenerator.IndexFromContainer(this._sourceItemContainer);

            if (this._sourceItemsControl == this._targetItemsControl && 
                (this._insertionIndex == sourceIndex || this._insertionIndex == sourceIndex + 1))
            {
                // If the insertion point is before or after the same item in the same control, no change
                RemoveDraggedAdorner();
                RemoveInsertionAdorner();
                e.Handled = true;
                return;
            }
                
            bool wasSelected = (this._sourceItemsControl is Selector selector && selector.SelectedItem == draggedItem);            

            if ((e.Effects & DragDropEffects.Move) != 0)
            {
                indexRemoved = Utilities.RemoveItemFromItemsControl(this._sourceItemsControl, draggedItem);
            }
            // This happens when we drag an item to a later position within the same ItemsControl.
            if (indexRemoved != -1 && this._sourceItemsControl == this._targetItemsControl && indexRemoved < this._insertionIndex)
            {
                this._insertionIndex--;
            }
            Utilities.InsertItemInItemsControl(this._targetItemsControl, draggedItem, this._insertionIndex);

            if (wasSelected)
            {
                ((Selector)this._sourceItemsControl).SelectedItem = draggedItem;
            }

            RemoveDraggedAdorner();
            RemoveInsertionAdorner();
        }
        e.Handled = true;
    }

    private void DropTarget_PreviewDragLeave(object sender, DragEventArgs e)
    {
        // Dragged Adorner is only created once on DragEnter + every time we enter the window. 
        // It's only removed once on the DragDrop, and every time we leave the window. (so no need to remove it here)
        object draggedItem = e.Data.GetData(this._format.Name);

        if (draggedItem != null)
        {
            RemoveInsertionAdorner();
        }
        e.Handled = true;
    }

    // If the types of the dragged data and ItemsControl's source are compatible, 
    // there are 3 situations to have into account when deciding the drop target:
    // 1. mouse is over an items container
    // 2. mouse is over the empty part of an ItemsControl, but ItemsControl is not empty
    // 3. mouse is over an empty ItemsControl.
    // The goal of this method is to decide on the values of the following properties: 
    // targetItemContainer, insertionIndex and isInFirstHalf.
    private void DecideDropTarget(DragEventArgs e)
    {
        if (this._targetItemsControl is null)
        {
            return;
        }

        int targetItemsControlCount = this._targetItemsControl.Items.Count;
        object draggedItem = e.Data.GetData(this._format.Name);

        if (IsDropDataTypeAllowed(draggedItem))
        {
            if (targetItemsControlCount > 0)
            {
                this._hasVerticalOrientation = Utilities.HasVerticalOrientation(this._targetItemsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement);
                this._targetItemContainer = _targetItemsControl.ContainerFromElement((DependencyObject)e.OriginalSource) as FrameworkElement;

                if (this._targetItemContainer != null)
                {
                    Point positionRelativeToItemContainer = e.GetPosition(this._targetItemContainer);
                    this._isInFirstHalf = Utilities.IsInFirstHalf(this._targetItemContainer, positionRelativeToItemContainer, this._hasVerticalOrientation);
                    this._insertionIndex = this._targetItemsControl.ItemContainerGenerator.IndexFromContainer(this._targetItemContainer);

                    if (!this._isInFirstHalf)
                    {
                        this._insertionIndex++;
                    }
                }
                else
                {
                    this._targetItemContainer = this._targetItemsControl.ItemContainerGenerator.ContainerFromIndex(targetItemsControlCount - 1) as FrameworkElement;
                    this._isInFirstHalf = false;
                    this._insertionIndex = targetItemsControlCount;
                }
            }
            else
            {
                this._targetItemContainer = null;
                this._insertionIndex = 0;
            }
        }
        else
        {
            this._targetItemContainer = null;
            this._insertionIndex = -1;
            e.Effects = DragDropEffects.None;
        }
    }

    // Can the dragged data be added to the destination collection?
    // It can if destination is bound to IList<allowed type>, IList or not data bound.
    private bool IsDropDataTypeAllowed(object draggedItem)
    {
        if (this._targetItemsControl is null)
        {
            return false;
        }

        bool isDropDataTypeAllowed;
        IEnumerable collectionSource = this._targetItemsControl.ItemsSource;
        if (draggedItem != null)
        {
            if (collectionSource != null)
            {
                Type draggedType = draggedItem.GetType();
                Type collectionType = collectionSource.GetType();

                Type? genericIListType = collectionType.GetInterface("IList`1");
                if (genericIListType != null)
                {
                    Type[] genericArguments = genericIListType.GetGenericArguments();
                    isDropDataTypeAllowed = genericArguments[0].IsAssignableFrom(draggedType);
                }
                else if (typeof(IList).IsAssignableFrom(collectionType))
                {
                    isDropDataTypeAllowed = true;
                }
                else
                {
                    isDropDataTypeAllowed = false;
                }
            }
            else // the ItemsControl's ItemsSource is not data bound.
            {
                isDropDataTypeAllowed = true;
            }
        }
        else
        {
            isDropDataTypeAllowed = false;
        }
        return isDropDataTypeAllowed;
    }

    // Window

    private void TopWindow_DragEnter(object sender, DragEventArgs e)
    {
        ShowDraggedAdorner(e.GetPosition(this._topWindow));
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void TopWindow_DragOver(object sender, DragEventArgs e)
    {
        ShowDraggedAdorner(e.GetPosition(this._topWindow));
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void TopWindow_DragLeave(object sender, DragEventArgs e)
    {
        RemoveDraggedAdorner();
        e.Handled = true;
    }

    // Adorners

    // Creates or updates the dragged Adorner. 
    private void ShowDraggedAdorner(Point currentPosition)
    {
        if (this._draggedAdorner == null)
        {
            if (this._draggedData is null || this._sourceItemsControl is null || this._sourceItemContainer is null)
            {
                return;
            }

            var adornerLayer = AdornerLayer.GetAdornerLayer(this._sourceItemsControl);
            this._draggedAdorner = new DraggedAdorner(this._draggedData, GetDragDropTemplate(this._sourceItemsControl), this._sourceItemContainer, adornerLayer);
        }
        this._draggedAdorner.SetPosition(currentPosition.X - this._initialMousePosition.X + this._initialMouseOffset.X, currentPosition.Y - this._initialMousePosition.Y + this._initialMouseOffset.Y);
    }

    private void RemoveDraggedAdorner()
    {
        if (this._draggedAdorner != null)
        {
            this._draggedAdorner.Detach();
            this._draggedAdorner = null;
        }
    }

    private void CreateInsertionAdorner()
    {
        if (this._targetItemContainer != null)
        {
            // Here, I need to get adorner layer from targetItemContainer and not targetItemsControl. 
            // This way I get the AdornerLayer within ScrollContentPresenter, and not the one under AdornerDecorator (Snoop is awesome).
            // If I used targetItemsControl, the adorner would hang out of ItemsControl when there's a horizontal scroll bar.
            var adornerLayer = AdornerLayer.GetAdornerLayer(this._targetItemContainer);
            this._insertionAdorner = new InsertionAdorner(this._hasVerticalOrientation, this._isInFirstHalf, this._targetItemContainer, adornerLayer);
        }
    }

    private void UpdateInsertionAdornerPosition()
    {
        if (this._insertionAdorner != null)
        {
            this._insertionAdorner.IsInFirstHalf = this._isInFirstHalf;
            this._insertionAdorner.InvalidateVisual();
        }
    }

    private void RemoveInsertionAdorner()
    {
        if (this._insertionAdorner != null)
        {
            this._insertionAdorner.Detach();
            this._insertionAdorner = null;
        }
    }
}
