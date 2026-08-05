using Avalonia;
using Avalonia.VisualTree;

namespace Motion.Avalonia;

/// <summary>
/// Provides logical-tree motion invalidation for declarative motion elements.
/// </summary>
/// <remarks>
/// Motion definitions are read when their owning motion is compiled. Invalidating an element
/// walks its logical ancestors to the owning <see cref="Motion"/> and marks only the current
/// host realization dirty. Detached definitions need no retained dirty state because a host
/// always compiles them when they are attached.
/// </remarks>
public abstract class MotionElement : StyledElement
{
    /// <summary>
    /// Invalidates the active runtime realization of the motion that owns this element.
    /// </summary>
    public void InvalidateMotion()
    {
        InvalidateMotionTree(this);
    }

    /// <summary>
    /// Invalidates the nearest motion or motion panel that owns a changed motion definition.
    /// </summary>
    /// <remarks>
    /// The walk deliberately uses Avalonia's logical parent chain instead of retaining
    /// subscriptions from every definition element. This keeps mutable authoring objects
    /// cheap and lets the normal logical-tree lifetime release the invalidation path.
    /// </remarks>
    internal static void InvalidateMotionTree(StyledElement source)
    {
        ArgumentNullException.ThrowIfNull(source);

        StyledElement? current = source;
        while (current is not null)
        {
            if (current is Motion motion)
            {
                motion.InvalidateHost();
                return;
            }

            if (current is MotionPanel panel)
            {
                panel.InvalidateMotionIdentity(source);
                return;
            }

            current = current.Parent;
        }

        // ItemsControl containers are owned logically by the ItemsControl, while their visual
        // parent is the MotionPanel items host. Fall back to Avalonia's visual tree for those
        // generated direct targets without retaining a second subscription graph.
        if (source is Visual visual)
        {
            Visual? currentVisual = visual;
            while (currentVisual is not null)
            {
                if (currentVisual is MotionPanel panel)
                {
                    panel.InvalidateMotionIdentity(source);
                    return;
                }

                currentVisual = currentVisual.GetVisualParent();
            }
        }
    }

    /// <summary>
    /// Registers Avalonia properties whose changes invalidate their owning motion definition.
    /// </summary>
    /// <typeparam name="T">The motion element that owns the properties.</typeparam>
    /// <param name="properties">The properties that affect the motion runtime structure.</param>
    protected static void AffectsMotion<T>(params ReadOnlySpan<AvaloniaProperty> properties) where T : MotionElement
    {
        foreach (var property in properties)
        {
            property.Changed.AddClassHandler<T>(static (element, _) => element.InvalidateMotion());
        }
    }
}