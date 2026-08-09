using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace Motion.Avalonia;

/// <summary>
/// Provides the visual envelope used by a <see cref="MotionPanel"/> for one motion target.
/// </summary>
/// <remarks>
/// The container is a <see cref="ContentPresenter"/> so an <see cref="ItemsControl"/> can use
/// Avalonia's normal item-template preparation without adding another presenter below the motion
/// envelope. For a control that is already materialized, the panel uses the direct-child path and
/// keeps the control's logical parent unchanged. Position, size, and presence are attached to the
/// materialized child; this envelope only projects presence into a composited visual opacity and
/// provides the stable visual boundary used for layout and paint order.
///
/// The type is public so applications can target it from an
/// <see cref="ItemsControl.ItemContainerTheme"/>. Its content and motion lifecycle remain owned
/// by <see cref="MotionPanel"/>; applications should customize it through normal Avalonia styles
/// and themes rather than creating or reparenting its generated child.
/// </remarks>
public sealed class MotionItemContainer : ContentPresenter
{
    private Control? _directChild;

    /// <summary>
    /// Gets the control that receives ordinary property tracks.
    /// </summary>
    internal Control? MotionChild => _directChild ?? Child;

    static MotionItemContainer()
    {
        Motion.PositionProperty.Changed.AddClassHandler<Control>(static (s, _) => FindContainer(s)?.InvalidateParentArrange());
        Motion.SizeProperty.Changed.AddClassHandler<Control>(static (s, _) => FindContainer(s)?.InvalidateParentMeasure());
        Motion.PresenceProperty.Changed.AddClassHandler<Control>(static (s, e) => FindContainer(s)?.UpdatePresence((double)e.NewValue!));
        ChildProperty.Changed.AddClassHandler<MotionItemContainer>(static (s, _) => s.OnContentChildChanged());
    }

    /// <summary>
    /// Initializes an empty item container for an <see cref="ItemsControl"/>.
    /// </summary>
    public MotionItemContainer()
    {
        Focusable = false;
        UpdatePresence(GetValue(Motion.PresenceProperty));
    }

    /// <summary>
    /// Ensures that an item-template child has been created before the panel resolves endpoints.
    /// </summary>
    internal Control? GetOrCreateMotionChild()
    {
        if (_directChild is not null || Child is not null || Content is null)
        {
            return MotionChild;
        }

        UpdateChild();
        return Child;
    }

    /// <summary>
    /// Attaches an already materialized child through the visual tree only.
    /// </summary>
    internal void AttachDirectChild(Control child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (_directChild is not null || Child is not null)
        {
            throw new InvalidOperationException($"{nameof(MotionItemContainer)} already has a child.");
        }

        _directChild = child;
        VisualChildren.Add(child);
        UpdatePresence(child.GetValue(Motion.PresenceProperty));
    }

    /// <summary>
    /// Detaches the child created through <see cref="AttachDirectChild"/>.
    /// </summary>
    internal void DetachDirectChild()
    {
        if (_directChild is not { } child)
        {
            return;
        }

        VisualChildren.Remove(child);
        _directChild = null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (MotionChild is { } child)
        {
            child.Measure(availableSize);
            return default;
        }

        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (MotionChild is { } child)
        {
            child.Arrange(new Rect(finalSize));
            return finalSize;
        }

        return base.ArrangeOverride(finalSize);
    }

    private void OnContentChildChanged()
    {
        UpdatePresence(MotionChild?.GetValue(Motion.PresenceProperty) ?? 1);
        (VisualParent as MotionItemsPanel)?.InvalidateMotionIdentity(this);
    }

    private static MotionItemContainer? FindContainer(Control target) =>
        target as MotionItemContainer ?? target.VisualParent as MotionItemContainer;

    private void InvalidateParentArrange()
    {
        if (VisualParent is MotionItemsPanel panel)
        {
            panel.InvalidateArrange();
            panel.Owner?.Presenter?.InvalidateArrange();
            panel.Owner?.InvalidateArrange();
        }
    }

    private void InvalidateParentMeasure()
    {
        if (VisualParent is MotionItemsPanel panel)
        {
            panel.InvalidateMeasure();
            panel.Owner?.Presenter?.InvalidateMeasure();
            panel.Owner?.InvalidateMeasure();
        }
    }

    private void UpdatePresence(double value)
    {
        var presence = Math.Clamp(value, 0, 1);
        Opacity = presence;
        IsHitTestVisible = presence > 0.0001;
    }
}