using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Provides a stable visual layout boundary for one logical child of a <see cref="TransitionPanel"/>.
/// </summary>
internal sealed class TransitionPanelItemContainer : Control
{
    public static readonly DirectProperty<TransitionPanelItemContainer, Point> PositionProperty =
        AvaloniaProperty.RegisterDirect<TransitionPanelItemContainer, Point>(
            nameof(Position),
            container => container.Position,
            (container, value) => container.Position = value);

    public Point Position
    {
        get;
        set => SetAndRaise(PositionProperty, ref field, value);
    }

    public Control Child { get; }

    static TransitionPanelItemContainer()
    {
        PositionProperty.Changed.AddClassHandler<TransitionPanelItemContainer>(static (container, _) => container.InvalidateParentArrange());
        WidthProperty.Changed.AddClassHandler<TransitionPanelItemContainer>(static (container, _) => container.InvalidateParentMeasure());
        HeightProperty.Changed.AddClassHandler<TransitionPanelItemContainer>(static (container, _) => container.InvalidateParentMeasure());
    }

    public TransitionPanelItemContainer(Control child)
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

    private void InvalidateParentArrange() => this.GetVisualParent<TransitionPanel>()?.InvalidateArrange();

    private void InvalidateParentMeasure() => this.GetVisualParent<TransitionPanel>()?.InvalidateMeasure();
}