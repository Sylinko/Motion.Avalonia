using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Animators;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace Motion.Avalonia;

/// <summary>
/// Interpolates motion values using Avalonia's registered animators.
/// </summary>
/// <remarks>
/// <see cref="MotionPanel"/> uses a shared instance as its fallback. Add an explicit instance
/// when a target property needs a property-specific easing or edge filter. Registered
/// <see cref="Animator{T}"/> implementations are reused where possible; brushes, effects, and
/// render transforms receive compatible specialized handling. Unsupported value pairs switch
/// discretely at halfway.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:MotionAvaloniaInterpolator
///     Target="Card"
///     Property="CornerRadius"
///     Easing="CubicEaseOut"/>
/// ]]></code>
/// </example>
public sealed class MotionAvaloniaInterpolator : MotionInterpolator
{
    internal static MotionAvaloniaInterpolator Default { get; } = new();

    private static readonly Dictionary<AvaloniaProperty, IMotionValueInterpolator> Cache = [];

    internal override object? InterpolateCore(AvaloniaProperty property, double progress, object? oldValue, object? newValue)
    {
        if (!Cache.TryGetValue(property, out var interpolator))
        {
            interpolator = CreateInterpolator(property);
            Cache.Add(property, interpolator);
        }

        return interpolator.Interpolate(progress, oldValue, newValue);
    }

    private static IMotionValueInterpolator CreateInterpolator(AvaloniaProperty property)
    {
        // Avalonia's BaseBrushAnimator and EffectAnimator select a concrete subtype animator in
        // Apply(). Motion calls Animator<T>.Interpolate directly, where both base animators only
        // provide a discrete fallback. Select equivalent value interpolators once from the
        // property's declared type; they inspect both concrete endpoint values when interpolating.
        if (typeof(IBrush).IsAssignableFrom(property.PropertyType))
        {
            return BrushValueInterpolator.Shared;
        }

        if (typeof(IEffect).IsAssignableFrom(property.PropertyType))
        {
            return EffectValueInterpolator.Shared;
        }

        // Avalonia's TransformAnimator targets scalar properties owned by Transform objects; it
        // is not an ITransform value interpolator. Normalize RenderTransform values to
        // TransformOperations so Motion can interpolate the property itself.
        if (property == Visual.RenderTransformProperty)
        {
            return TransformValueInterpolator.Shared;
        }

        var registration = GetAnimatorType(null, property);
        if (registration is not { } registered)
        {
            return DiscreteValueInterpolator.Shared;
        }

        var animator = registered.Factory();
        var valueType = FindAnimatorValueType(animator.GetType());
        if (valueType is null)
        {
            return DiscreteValueInterpolator.Shared;
        }

        var adapterType = typeof(AvaloniaAnimatorAdapter<>).MakeGenericType(valueType);
        return (IMotionValueInterpolator)Activator.CreateInstance(adapterType, animator)!;
    }

    private static Type? FindAnimatorValueType(Type animatorType)
    {
        for (var current = animatorType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Animator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetAnimatorType")]
    private static extern (Type Type, Func<IAnimator> Factory)? GetAnimatorType(Animation? klass, AvaloniaProperty property);

    private interface IMotionValueInterpolator
    {
        object? Interpolate(double progress, object? oldValue, object? newValue);
    }

    private sealed class AvaloniaAnimatorAdapter<T>(IAnimator animator) : IMotionValueInterpolator
    {
        private readonly Animator<T> _animator = (Animator<T>)animator;

        public object? Interpolate(double progress, object? oldValue, object? newValue) =>
            _animator.Interpolate(progress, (T)oldValue!, (T)newValue!);
    }

    private sealed class BrushValueInterpolator : IMotionValueInterpolator
    {
        public static BrushValueInterpolator Shared { get; } = new();

        private static readonly ISolidColorBrushAnimator SolidColorAnimator = new();
        private static readonly GradientBrushAnimator GradientAnimator = new();

        public object? Interpolate(double progress, object? oldValue, object? newValue)
        {
            if (oldValue is ISolidColorBrush oldSolid && newValue is ISolidColorBrush newSolid)
            {
                return SolidColorAnimator.Interpolate(progress, oldSolid, newSolid);
            }

            if (!TryNormalizeGradients(oldValue, newValue, out var oldGradient, out var newGradient))
            {
                return DiscreteValueInterpolator.InterpolateCore(progress, oldValue, newValue);
            }

            return GradientAnimator.Interpolate(progress, oldGradient, newGradient);
        }

        private static bool TryNormalizeGradients(
            object? oldValue,
            object? newValue,
            out IGradientBrush? oldGradient,
            out IGradientBrush? newGradient)
        {
            oldGradient = oldValue as IGradientBrush;
            newGradient = newValue as IGradientBrush;

            if (oldGradient is not null && newValue is ISolidColorBrush newSolid)
            {
                newGradient = GradientBrushAnimator.ConvertSolidColorBrushToGradient(oldGradient, newSolid);
            }
            else if (newGradient is not null && oldValue is ISolidColorBrush oldSolid)
            {
                oldGradient = GradientBrushAnimator.ConvertSolidColorBrushToGradient(newGradient, oldSolid);
            }

            return oldGradient is not null && newGradient is not null;
        }
    }

    private sealed class EffectValueInterpolator : IMotionValueInterpolator
    {
        public static EffectValueInterpolator Shared { get; } = new();

        private static readonly BlurEffectAnimator BlurAnimator = new();
        private static readonly DropShadowEffectAnimator DropShadowAnimator = new();

        public object? Interpolate(double progress, object? oldValue, object? newValue)
        {
            if (oldValue is IBlurEffect oldBlur && newValue is IBlurEffect newBlur)
            {
                return BlurAnimator.Interpolate(progress, oldBlur, newBlur);
            }

            if (oldValue is IDropShadowEffect oldShadow && newValue is IDropShadowEffect newShadow)
            {
                return DropShadowAnimator.Interpolate(progress, oldShadow, newShadow);
            }

            return DiscreteValueInterpolator.InterpolateCore(progress, oldValue, newValue);
        }
    }

    private sealed class TransformValueInterpolator : IMotionValueInterpolator
    {
        public static TransformValueInterpolator Shared { get; } = new();

        private static readonly TransformOperationsAnimator Animator = new();

        public object Interpolate(double progress, object? oldValue, object? newValue)
        {
            var oldOperations = oldValue is ITransform oldTransform ?
                TransformOperationsAnimator.EnsureOperations(oldTransform) :
                TransformOperations.Identity;
            var newOperations = newValue is ITransform newTransform ?
                TransformOperationsAnimator.EnsureOperations(newTransform) :
                TransformOperations.Identity;

            return Animator.Interpolate(progress, oldOperations, newOperations);
        }
    }

    private sealed class DiscreteValueInterpolator : IMotionValueInterpolator
    {
        public static DiscreteValueInterpolator Shared { get; } = new();

        public object? Interpolate(double progress, object? oldValue, object? newValue) =>
            InterpolateCore(progress, oldValue, newValue);

        public static object? InterpolateCore(double progress, object? oldValue, object? newValue) =>
            progress >= 0.5 ? newValue : oldValue;
    }
}
