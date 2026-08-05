using System.Runtime.CompilerServices;
#if NET8_0
using System.Reflection;
#endif
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Core;

namespace Motion.Avalonia;

/// <summary>
/// Evaluates an Avalonia binding as a read-only motion endpoint on its real target.
/// </summary>
/// <remarks>
/// The expression is observed without attaching it to the target's value store, so Self,
/// DataContext, ancestor, and templated-parent lookup retain their native target context.
/// </remarks>
internal static class MotionBindingEvaluator
{
#if NET8_0
    private static readonly MethodInfo Net8ToObservableMethod =
        typeof(UntypedBindingExpressionBase).GetMethod("ToObservable", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new MissingMethodException(typeof(UntypedBindingExpressionBase).FullName, "ToObservable");
#endif

    public static IDisposable Observe(BindingBase binding, AvaloniaObject target, AvaloniaProperty property, Action<object?> publish)
    {
        if (GetBindingMode(binding) == BindingMode.OneWayToSource)
        {
            throw new NotSupportedException($"{nameof(MotionSlot)} endpoint bindings do not support {BindingMode.OneWayToSource}.");
        }

        var expression = CreateInstance(binding, target, property, null);
        if (expression is not UntypedBindingExpressionBase untyped)
        {
            expression.Dispose();
            throw new NotSupportedException($"Binding type '{binding.GetType()}' does not expose an observable endpoint expression.");
        }

#if NET10_0_OR_GREATER
        var observable = (IObservable<object?>)ToObservable(untyped, target);
#else
        // UnsafeAccessorType is a .NET 10 runtime feature. Keep the compatibility reflection
        // isolated to binding creation on the net8.0 target; it is never used during animation.
        var observable = (IObservable<object?>)Net8ToObservableMethod.Invoke(untyped, [target])!;
#endif
        var subscription = observable.Subscribe(new Observer(publish));
        return new Subscription(subscription, expression);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "CreateInstance")]
    private static extern BindingExpressionBase CreateInstance(
        BindingBase binding,
        AvaloniaObject target,
        AvaloniaProperty? targetProperty,
        object? anchor);

#if NET10_0_OR_GREATER
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ToObservable")]
    [return: UnsafeAccessorType("Avalonia.Reactive.IAvaloniaSubject`1[[System.Object, System.Private.CoreLib]], Avalonia.Base")]
    private static extern object ToObservable(UntypedBindingExpressionBase expression, AvaloniaObject? target);
#endif

    private static BindingMode GetBindingMode(BindingBase binding) =>
        binding switch
        {
            ReflectionBinding reflection => reflection.Mode,
            CompiledBinding compiled => compiled.Mode,
            TemplateBinding template => template.Mode,
            MultiBinding multi => multi.Mode,
            _ => BindingMode.Default,
        };

    private sealed class Observer(Action<object?> publish) : IObserver<object?>
    {
        public void OnNext(object? value) => publish(value);

        public void OnError(Exception error) => throw error;

        public void OnCompleted()
        {
        }
    }

    private sealed class Subscription(IDisposable subscription, BindingExpressionBase expression) : IDisposable
    {
        public void Dispose()
        {
            subscription.Dispose();
            expression.Dispose();
        }
    }
}