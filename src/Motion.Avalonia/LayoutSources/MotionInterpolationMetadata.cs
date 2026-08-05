using Avalonia.Animation.Easings;
using Avalonia.Data;

namespace Motion.Avalonia;

/// <summary>
/// Stores interpolation policy with explicit missing and null semantics.
/// </summary>
/// <remarks>
/// An empty optional means that the current scope does not override its fallback. An optional that
/// contains <see langword="null"/> explicitly selects the default policy at that scope.
/// </remarks>
public readonly record struct MotionInterpolationMetadata(
    Optional<Easing?> Easing,
    Optional<MotionInterpolator?> Interpolator
);