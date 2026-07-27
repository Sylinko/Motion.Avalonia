using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Provides a stable visual layout boundary for one logical child of a <see cref="MotionPanel"/>.
/// </summary>
internal sealed class MotionPanelItemContainer : Control
{
    public static readonly DirectProperty<MotionPanelItemContainer, Point> PositionProperty =
        AvaloniaProperty.RegisterDirect<MotionPanelItemContainer, Point>(
            nameof(Position),
            container => container.Position,
            (container, value) => container.Position = value);

    public Point Position
    {
        get;
        set => SetAndRaise(PositionProperty, ref field, value);
    }

    public Control Child { get; }

    static MotionPanelItemContainer()
    {
        PositionProperty.Changed.AddClassHandler<MotionPanelItemContainer>(static (container, _) => container.InvalidateParentArrange());
        WidthProperty.Changed.AddClassHandler<MotionPanelItemContainer>(static (container, _) => container.InvalidateParentMeasure());
        HeightProperty.Changed.AddClassHandler<MotionPanelItemContainer>(static (container, _) => container.InvalidateParentMeasure());
    }

    public MotionPanelItemContainer(Control child)
    {
        Child = child;
        Focusable = false;
        VisualChildren.Add(child);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child.Measure(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public void DetachChild() => VisualChildren.Remove(Child);

    private void InvalidateParentArrange() => this.GetVisualParent<MotionPanel>()?.InvalidateArrange();

    private void InvalidateParentMeasure() => this.GetVisualParent<MotionPanel>()?.InvalidateMeasure();
}