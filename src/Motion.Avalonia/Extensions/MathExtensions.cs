using System.Numerics;
using Avalonia;

namespace Motion.Avalonia.Extensions;

internal static class MathExtensions
{
    public static bool IsCloseTo(this double value, double target, double tolerance = 0.0001) =>
        value.IsCloseTo<double>(target, tolerance);

    public static bool IsCloseTo(this float value, float target, float tolerance = 0.0001f) =>
        value.IsCloseTo<float>(target, tolerance);

    public static T Lerp<T>(this T from, T to, T progress) where T : IFloatingPointIeee754<T> =>
        from + (to - from) * progress;

    public static T LerpClamped<T>(this T from, T to, T progress) where T : IFloatingPointIeee754<T> =>
        from.Lerp(to, T.Clamp(progress, T.Zero, T.One));

    public static T SmoothStep<T>(this T from, T to, T progress) where T : IFloatingPointIeee754<T>
    {
        progress = T.Clamp(progress, T.Zero, T.One);
        progress = progress * progress * (T.CreateChecked(3) - T.CreateChecked(2) * progress);
        return from.Lerp(to, progress);
    }

    public static Size Lerp(this Size from, Size to, double progress) => new(
        from.Width.Lerp(to.Width, progress),
        from.Height.Lerp(to.Height, progress));

    public static Rect Lerp(this Rect from, Rect to, double progress) => new(
        from.X.Lerp(to.X, progress),
        from.Y.Lerp(to.Y, progress),
        from.Width.Lerp(to.Width, progress),
        from.Height.Lerp(to.Height, progress));

    private static bool IsCloseTo<T>(this T value, T target, T tolerance) where T : IFloatingPointIeee754<T> =>
        T.Abs(value - target) < tolerance;
}