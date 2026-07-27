using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;

namespace Motion.Avalonia;

/// <summary>
/// Selects and evaluates the interpolation used for one target property.
/// </summary>
public abstract class MotionInterpolator : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref="Target"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> TargetProperty =
        AvaloniaProperty.Register<MotionInterpolator, string?>(nameof(Target));

    /// <summary>
    /// Defines the <see cref="Property"/> property.
    /// </summary>
    public static readonly StyledProperty<AvaloniaProperty?> PropertyProperty =
        AvaloniaProperty.Register<MotionInterpolator, AvaloniaProperty?>(nameof(Property));

    /// <summary>
    /// Defines the <see cref="Easing"/> property.
    /// </summary>
    public static readonly StyledProperty<Easing?> EasingProperty =
        AvaloniaProperty.Register<MotionInterpolator, Easing?>(nameof(Easing));

    /// <summary>
    /// Gets or sets the name of the target control.
    /// </summary>
    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the target property.
    /// </summary>
    public AvaloniaProperty? Property
    {
        get => GetValue(PropertyProperty);
        set => SetValue(PropertyProperty, value);
    }

    /// <summary>
    /// Gets or sets an easing override. A null value inherits the pose easing.
    /// </summary>
    public Easing? Easing
    {
        get => GetValue(EasingProperty);
        set => SetValue(EasingProperty, value);
    }

    /// <summary>
    /// Gets the directed edges on which this interpolator applies. An empty collection matches every edge.
    /// </summary>
    public AvaloniaList<MotionEdge> Edges { get; } = [];

    internal abstract object? InterpolateCore(
        AvaloniaProperty property,
        double progress,
        object? oldValue,
        object? newValue);
}

/// <summary>
/// Provides a strongly typed custom interpolation algorithm.
/// </summary>
/// <typeparam name="T">The interpolated value type.</typeparam>
public abstract class MotionInterpolator<T> : MotionInterpolator
{
    /// <summary>
    /// Interpolates between two property values.
    /// </summary>
    public abstract T Interpolate(double progress, T oldValue, T newValue);

    internal sealed override object? InterpolateCore(AvaloniaProperty property, double progress, object? oldValue, object? newValue)
    {
        if (!IsValidValue(oldValue))
        {
            throw CreateTypeException(property, oldValue, "source");
        }

        if (!IsValidValue(newValue))
        {
            throw CreateTypeException(property, newValue, "target");
        }

        return Interpolate(progress, (T)oldValue!, (T)newValue!);
    }

    private static bool IsValidValue(object? value) => value is T || value is null && default(T) is null;

    private static InvalidOperationException CreateTypeException(AvaloniaProperty property, object? value, string endpoint) =>
        new(
            $"{typeof(MotionInterpolator<T>).Name} cannot interpolate the {endpoint} value of property '{property}'. " +
            $"Expected '{typeof(T)}', but received '{value?.GetType()}'.");
}