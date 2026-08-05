using Avalonia;

namespace Motion.Avalonia;

/// <summary>
/// Interpolates values in a property's value space.
/// </summary>
/// <remarks>
/// This type deliberately contains no target, property, timing, or edge selection. Use
/// <see cref="MotionInterpolation"/> to associate an interpolator with an authored channel.
/// </remarks>
public abstract class MotionInterpolator : AvaloniaObject
{
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
///     protected override double Interpolate(double progress, double oldValue, double newValue)
///     {
///         var wave = Math.Sin(progress * Math.PI) * 0.08;
///         return oldValue + ((newValue - oldValue) * (progress + wave));
///     }
/// }
/// ]]></code>
/// Place the instance in a <see cref="MotionInterpolation"/> to associate it with a property.
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
    protected abstract T Interpolate(double progress, T oldValue, T newValue);

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