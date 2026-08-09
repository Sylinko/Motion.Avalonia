using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Represents one runtime-owned realization of a motion layout source.
/// </summary>
/// <remarks>
/// A layout may use an Avalonia control tree, compute its frame directly, or combine both
/// approaches. The owning runtime drives measurement and arrangement and disposes the layout
/// when that runtime is replaced. Implementations that attach a layout root through
/// <see cref="MotionCompilationContext.AttachLayoutRoot"/> must release the returned lifetime
/// from <see cref="IDisposable.Dispose"/>.
/// </remarks>
public interface IMotionLayout : IDisposable
{
    /// <summary>
    /// Gets the desired size produced by the latest measure pass.
    /// </summary>
    Size DesiredSize { get; }

    /// <summary>
    /// Gets whether the latest measure result is still valid.
    /// </summary>
    bool IsMeasureValid { get; }

    /// <summary>
    /// Gets whether the latest arrange result is still valid.
    /// </summary>
    bool IsArrangeValid { get; }

    /// <summary>
    /// Occurs when one or more existing endpoint values change without changing the compiled
    /// channel set.
    /// </summary>
    /// <remarks>
    /// This is a layout-wide invalidation signal. The owning runtime may reevaluate all of its
    /// tracks and lets each track suppress an unchanged output.
    /// </remarks>
    event EventHandler? EndpointChanged;

    /// <summary>
    /// Occurs when the endpoint channel set changes and the owning motion must be recompiled.
    /// </summary>
    event EventHandler? EndpointStructureChanged;

    /// <summary>
    /// Measures the layout against the panel's current available size.
    /// </summary>
    /// <param name="availableSize">The available size in device-independent pixels.</param>
    void Measure(Size availableSize);

    /// <summary>
    /// Arranges the layout into the panel's current bounds.
    /// </summary>
    /// <param name="finalRect">The final layout rectangle in device-independent pixels.</param>
    void Arrange(Rect finalRect);

    /// <summary>
    /// Captures the latest reusable frame represented by this layout.
    /// </summary>
    /// <returns>The current layout frame.</returns>
    MotionLayoutFrame CaptureLayout();
}
