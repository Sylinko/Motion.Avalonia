using Avalonia.Data.Converters;

namespace Motion.Avalonia.Demo.Converters;

/// <summary>
/// Provides binding conversions used by the motion controller demos.
/// </summary>
public static class MotionConverters
{
    /// <summary>
    /// Converts between a timeline <see cref="TimeSpan"/> and Slider seconds.
    /// </summary>
    public static IValueConverter TimeSpanSeconds { get; } =
        new FuncValueConverter<TimeSpan, double>(
            static time => time.TotalSeconds,
            static seconds => TimeSpan.FromSeconds(Math.Max(seconds, 0)));

    /// <summary>
    /// Formats a timeline position for the compact transport display.
    /// </summary>
    public static IValueConverter TimelineTimestamp { get; } =
        new FuncValueConverter<TimeSpan, string>(
            static time => time.ToString(@"mm\:ss\.f"));

    /// <summary>
    /// Returns whether a player is currently playing.
    /// </summary>
    public static IValueConverter IsPlaying { get; } =
        new FuncValueConverter<MotionPlayerState, bool>(
            static state => state == MotionPlayerState.Playing);

    /// <summary>
    /// Returns whether a player is not currently playing.
    /// </summary>
    public static IValueConverter IsNotPlaying { get; } =
        new FuncValueConverter<MotionPlayerState, bool>(
            static state => state != MotionPlayerState.Playing);
}
