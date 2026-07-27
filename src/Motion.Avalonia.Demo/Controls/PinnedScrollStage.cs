using Avalonia;
using Avalonia.Controls;

namespace Motion.Avalonia.Demo.Controls;

/// <summary>
/// Provides a fixed scroll extent while keeping its child pinned inside the viewport.
/// </summary>
public sealed class PinnedScrollStage : Panel
{
    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<PinnedScrollStage, double>(
            nameof(ScrollOffset),
            validate: static value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<double> ViewportHeightProperty =
        AvaloniaProperty.Register<PinnedScrollStage, double>(
            nameof(ViewportHeight),
            680,
            validate: static value => double.IsFinite(value) && value >= 0);

    public static readonly StyledProperty<double> TransitionDistanceProperty =
        AvaloniaProperty.Register<PinnedScrollStage, double>(
            nameof(TransitionDistance),
            900,
            validate: static value => double.IsFinite(value) && value >= 0);

    static PinnedScrollStage()
    {
        AffectsArrange<PinnedScrollStage>(ScrollOffsetProperty);
        AffectsMeasure<PinnedScrollStage>(ViewportHeightProperty, TransitionDistanceProperty);
    }

    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    public double ViewportHeight
    {
        get => GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    public double TransitionDistance
    {
        get => GetValue(TransitionDistanceProperty);
        set => SetValue(TransitionDistanceProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 360;
        var viewportSize = new Size(width, ViewportHeight);

        foreach (var child in Children)
        {
            child.Measure(viewportSize);
        }

        return new Size(width, ViewportHeight + TransitionDistance);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pinnedOffset = Math.Min(ScrollOffset, TransitionDistance);
        var viewportSize = new Size(finalSize.Width, ViewportHeight);

        foreach (var child in Children)
        {
            child.Arrange(new Rect(new Point(0, pinnedOffset), viewportSize));
        }

        return finalSize;
    }
}
