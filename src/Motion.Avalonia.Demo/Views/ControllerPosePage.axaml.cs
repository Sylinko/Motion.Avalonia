using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Motion.Avalonia.Demo.Views;

public partial class ControllerPosePage : UserControl
{
    private static readonly string[] SequenceStateNames = ["Orbit", "Columns", "Focus", "Stack"];

    private static readonly TimelineStop[] TimelineStops =
    [
        new("Seed", TimeSpan.Zero),
        new("Constellation", TimeSpan.FromSeconds(1.4)),
        new("Stream", TimeSpan.FromSeconds(3.2)),
        new("Focus", TimeSpan.FromSeconds(5.2)),
        new("Seed", TimeSpan.FromSeconds(7)),
    ];

    public ControllerPosePage()
    {
        InitializeComponent();

        TimelineController.PropertyChanged += OnTimelineControllerPropertyChanged;

        UpdateSequenceState(PoseSlider.Value);
        UpdateTimelineState(TimelineController.Time);
    }

    private void OnPoseSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        UpdateSequenceState(e.NewValue);

    private void OnTimelineControllerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TimelineMotionController.TimeProperty)
        {
            UpdateTimelineState(TimelineController.Time);
        }
    }

    private void UpdateSequenceState(double progress) =>
        PoseStateLabel.Text = SequenceStateNames[GetSequenceStateIndex(progress)];

    private static int GetSequenceStateIndex(double progress) =>
        Math.Clamp(
            (int)Math.Round(progress * (SequenceStateNames.Length - 1)),
            0,
            SequenceStateNames.Length - 1);

    private void UpdateTimelineState(TimeSpan time) =>
        TimelineStateLabel.Text = GetTimelineStateLabel(time);

    private static string GetTimelineStateLabel(TimeSpan time)
    {
        for (var i = 1; i < TimelineStops.Length; i++)
        {
            var destination = TimelineStops[i];
            if (time == destination.Time)
            {
                return destination.State;
            }

            if (time < destination.Time)
            {
                return $"{TimelineStops[i - 1].State} → {destination.State}";
            }
        }

        return TimelineStops[^1].State;
    }

    private readonly record struct TimelineStop(string State, TimeSpan Time);
}
