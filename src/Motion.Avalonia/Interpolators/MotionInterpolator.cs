using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Collections;

namespace Motion.Avalonia;

/// <summary>
/// Selects and evaluates the interpolation used for one motion value.
/// </summary>
/// <remarks>
/// Interpolators are matched by <see cref="Target"/> and <see cref="Property"/>. The property
/// may identify an ordinary target property or a built-in <see cref="MotionLayout"/> channel.
/// An edge-specific match takes precedence over a general match whose <see cref="Edges"/>
/// collection is empty. <see cref="Easing"/> changes progress for this value only.
/// </remarks>
/// <example>
/// This override keeps Avalonia's normal opacity interpolation but gives it a separate curve:
/// <code><![CDATA[
/// <motion:MotionPanel.Interpolators>
///   <motion:MotionAvaloniaInterpolator
///       Target="Card"
///       Property="Opacity"
///       Easing="SineEaseInOut"/>
/// </motion:MotionPanel.Interpolators>
/// ]]></code>
/// </example>
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
    /// <remarks>
    /// Omit the target only when <see cref="Property"/> is
    /// <see cref="MotionLayout.DesiredSizeProperty"/>, which belongs to the panel.
    /// </remarks>
    public string? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    /// Gets or sets the target property or built-in layout channel.
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
/// <example>
/// <code><![CDATA[
/// public sealed class OvershootDoubleInterpolator : MotionInterpolator<double>
/// {
///     public override double Interpolate(double progress, double oldValue, double newValue)
///     {
///         var wave = Math.Sin(progress * Math.PI) * 0.08;
///         return oldValue + ((newValue - oldValue) * (progress + wave));
///     }
/// }
/// ]]></code>
/// Register the instance in <see cref="MotionPanel.Interpolators"/> with a matching target and
/// property.
/// </example>
public abstract class MotionInterpolator<T> : MotionInterpolator
{
    /// <summary>
/// Interpolates between two motion values.
    /// </summary>
    /// <param name="progress">The effective progress. It may be outside zero to one when an easing overshoots.</param>
    /// <param name="oldValue">The source endpoint value.</param>
    /// <param name="newValue">The destination endpoint value.</param>
    /// <returns>The interpolated value.</returns>
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
