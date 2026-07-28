using System.Collections.Specialized;
using Avalonia;
using Avalonia.LogicalTree;

namespace Motion.Avalonia;

/// <summary>
/// Mirrors the selector-relevant identity of one real element in an isolated motion style tree.
/// </summary>
/// <remarks>
/// The proxy carries the real style key, name, classes, pseudo-classes, and logical hierarchy,
/// but only receives motion shadow properties. Original properties are never registered on it.
/// </remarks>
internal sealed class MotionStyleProxy : StyledElement, IDisposable
{
    public StyledElement Target { get; }

    protected override Type StyleKeyOverride { get; }

    private bool _disposed;

    public MotionStyleProxy(StyledElement target)
    {
        Target = target;
        StyleKeyOverride = target.StyleKey;
        Name = target.Name;
        SynchronizeClasses();
        target.Classes.CollectionChanged += OnTargetClassesChanged;
    }

    public void AddChild(MotionStyleProxy child) => LogicalChildren.Add(child);

    public void ApplyStylesRecursively()
    {
        ApplyStyling();

        foreach (var child in ((ILogical)this).LogicalChildren.OfType<MotionStyleProxy>())
        {
            child.ApplyStylesRecursively();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Target.Classes.CollectionChanged -= OnTargetClassesChanged;

        foreach (var child in ((ILogical)this).LogicalChildren.OfType<MotionStyleProxy>().ToArray())
        {
            child.Dispose();
        }

        LogicalChildren.Clear();
    }

    private void OnTargetClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SynchronizeClasses();

    private void SynchronizeClasses()
    {
        foreach (var item in Classes.ToArray())
        {
            if (item.StartsWith(':'))
            {
                PseudoClasses.Remove(item);
            }
            else
            {
                Classes.Remove(item);
            }
        }

        foreach (var item in Target.Classes)
        {
            if (item.StartsWith(':'))
            {
                PseudoClasses.Add(item);
            }
            else
            {
                Classes.Add(item);
            }
        }
    }
}