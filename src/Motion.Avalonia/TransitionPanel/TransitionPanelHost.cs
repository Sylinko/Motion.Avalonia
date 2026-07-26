using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

internal sealed class TransitionPanelHost : Decorator
{
    public TransitionPanelState State { get; }

    private readonly TransitionPanel _owner;

    public TransitionPanelHost(TransitionPanel owner, TransitionPanelState state)
    {
        _owner = owner;
        State = state;
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = false;
        Rebuild();
    }

    public void Rebuild()
    {
        Child = State.Template?.Build(_owner.DataContext);
        InvalidateMeasure();
    }

    public Size MeasureTarget(string? target, Size availableSize) =>
        _owner.MeasureTarget(target, availableSize);

    public MotionLayout CaptureLayout()
    {
        var entries = new Dictionary<string, MotionLayoutEntry>(_owner.TargetCount, StringComparer.Ordinal);
        if (Child is TransitionPanelSlot rootSlot)
        {
            AddSlot(rootSlot, entries);
        }

        if (Child is not null)
        {
            foreach (var descendant in Child.GetVisualDescendants())
            {
                if (descendant is TransitionPanelSlot slot)
                {
                    AddSlot(slot, entries);
                }
            }
        }

        return new MotionLayout(DesiredSize, entries);
    }

    private void AddSlot(TransitionPanelSlot slot, Dictionary<string, MotionLayoutEntry> entries)
    {
        var target = slot.Target;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException(
                $"Every {nameof(TransitionPanelSlot)} in state '{State.Key}' must specify a non-empty target.");
        }

        if (!entries.TryAdd(target, new MotionLayoutEntry(GetLayoutBounds(slot), slot.Opacity)))
        {
            throw new InvalidOperationException($"State '{State.Key}' contains more than one slot for layout morph target '{target}'.");
        }
    }

    private Rect GetLayoutBounds(TransitionPanelSlot slot)
    {
        var position = slot.Bounds.Position;
        var current = slot.GetVisualParent();

        while (current is not null && current != this)
        {
            position += (Vector)current.Bounds.Position;
            current = current.GetVisualParent();
        }

        if (current != this)
        {
            throw new InvalidOperationException($"Motion slot '{slot.Target}' is not a descendant of its state scene.");
        }

        return new Rect(position, slot.Bounds.Size);
    }
}

internal sealed record MotionLayout(Size DesiredSize, IReadOnlyDictionary<string, MotionLayoutEntry> Entries);

internal readonly record struct MotionLayoutEntry(Rect Bounds, double Opacity);