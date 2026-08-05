using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;

namespace Motion.Avalonia;

/// <summary>
/// Defines a declarative source of live motion for one <see cref="MotionPanel"/>.
/// </summary>
/// <remarks>
/// A motion is hosted as a logical child, so bindings, inherited data context, resource lookup,
/// and lifetime follow normal Avalonia rules. Derive from <see cref="PlayerDrivenMotion"/> when
/// the motion owns a frame-aligned clock.
/// </remarks>
public abstract partial class Motion : MotionElement
{
    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<Motion, Easing?>(nameof(Easing));

    /// <summary>
    /// Gets or sets the default easing for segments that do not provide a more specific value.
    /// A null value resolves to linear easing.
    /// </summary>
    /// <remarks>
    /// Sequence and timeline motions capture this value when they compile. State motions sample
    /// it when a transition begins, together with that transition's duration and edge override.
    /// </remarks>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    /// <summary>
    /// Gets the property-specific interpolation overrides used by this motion runtime.
    /// </summary>
    /// <remarks>
    /// Entries are authoring data. A motion runtime copies their current configuration and
    /// does not observe later collection or property changes.
    /// </remarks>
    public AvaloniaList<MotionInterpolation> Interpolations { get; } = [];

    internal void InvalidateHost()
    {
        if (Parent is MotionPanel panel)
        {
            panel.InvalidateMotion(this);
        }
    }

    /// <summary>
    /// Creates the runtime graph for one stable host topology.
    /// </summary>
    /// <remarks>
    /// The default implementation creates the standard <see cref="MotionRuntime"/>. Derived
    /// motions may return a <see cref="MotionRuntime"/> subclass. The subclass declares its
    /// definition-level layout sources while this method runs; the runtime base realizes those
    /// sources and calls its second construction phase to create tracks after this method returns.
    /// </remarks>
    /// <param name="context">The panel-bound target and layout services for this realization.</param>
    /// <returns>The runtime graph that owns the compiled tracks and subscriptions.</returns>
    protected virtual MotionRuntime CreateRuntime(MotionCompilationContext context) => new(context);

    internal MotionRuntime CompileCore(MotionCompilationContext context)
    {
        MotionRuntime? runtime = null;
        try
        {
            runtime = CreateRuntime(context);
            runtime.Build();
            return runtime;
        }
        catch
        {
            runtime?.Dispose();
            context.Dispose();
            throw;
        }
    }
}