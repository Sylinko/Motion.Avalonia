namespace Motion.Avalonia;

/// <summary>
/// Creates one live layout realization for each owning motion runtime.
/// </summary>
/// <remarks>
/// States and keyframes intentionally remain distinct concepts. This interface lets custom
/// motions provide template-backed or computed layouts without exposing their implementation to
/// the owning motion panel. A source is a reusable definition and must not store one runtime's
/// mutable layout instance on itself.
/// </remarks>
public interface IMotionLayoutSource
{
    /// <summary>
    /// Creates a runtime-owned realization of this layout source.
    /// </summary>
    /// <param name="context">The panel-bound services available to this realization.</param>
    /// <returns>A new layout whose lifetime is owned by the requesting runtime.</returns>
    IMotionLayout CreateLayout(MotionCompilationContext context);

    /// <summary>
    /// Gets a concise description used by validation diagnostics.
    /// </summary>
    string Description { get; }
}