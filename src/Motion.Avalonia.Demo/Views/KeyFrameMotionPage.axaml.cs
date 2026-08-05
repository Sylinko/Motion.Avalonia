using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Motion.Avalonia.Demo.Views;

public partial class KeyFrameMotionPage : UserControl
{
    private static readonly string[] SequenceFrameNames = ["Orbit", "Columns", "Focus", "Stack"];

    private static readonly TimelineFrame[] TimelineFrames =
    [
        new("Seed", TimeSpan.Zero),
        new("Constellation", TimeSpan.FromSeconds(1.4)),
        new("Stream", TimeSpan.FromSeconds(3.2)),
        new("Focus", TimeSpan.FromSeconds(5.2)),
        new("Seed", TimeSpan.FromSeconds(7)),
    ];

    public KeyFrameMotionPage()
    {
        InitializeComponent();

        TimelineMotion.PropertyChanged += OnTimelineMotionPropertyChanged;

        UpdateSequenceFrame(SequenceSlider.Value);
        UpdateTimelineFrame(TimelineMotion.Time);
    }

    private void OnSequenceSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        UpdateSequenceFrame(e.NewValue);

    private void OnTimelineMotionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == global::Motion.Avalonia.TimelineMotion.TimeProperty)
        {
            UpdateTimelineFrame(TimelineMotion.Time);
        }
    }

    private void UpdateSequenceFrame(double progress) =>
        SequenceFrameLabel.Text = SequenceFrameNames[GetSequenceFrameIndex(progress)];

    private static int GetSequenceFrameIndex(double progress) =>
        Math.Clamp(
            (int)Math.Round(progress * (SequenceFrameNames.Length - 1)),
            0,
            SequenceFrameNames.Length - 1);

    private void UpdateTimelineFrame(TimeSpan time) =>
        TimelineFrameLabel.Text = GetTimelineFrameLabel(time);

    private static string GetTimelineFrameLabel(TimeSpan time)
    {
        for (var i = 1; i < TimelineFrames.Length; i++)
        {
            var destination = TimelineFrames[i];
            if (time == destination.Time)
            {
                return destination.Name;
            }

            if (time < destination.Time)
            {
                return $"{TimelineFrames[i - 1].Name} → {destination.Name}";
            }
        }

        return TimelineFrames[^1].Name;
    }

    private readonly record struct TimelineFrame(string Name, TimeSpan Time);
}
