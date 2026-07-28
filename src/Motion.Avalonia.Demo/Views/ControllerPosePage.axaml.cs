using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

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

    private readonly MotionPlayer _timelinePlayer;
    private MotionPlayerState _lastTimelinePlayerState;
    private bool _isSynchronizingTimelineSlider;
    private bool _loopRestartQueued;

    public ControllerPosePage()
    {
        InitializeComponent();

        _timelinePlayer = TimelineController.Player;
        _lastTimelinePlayerState = _timelinePlayer.State;

        TimelineController.PropertyChanged += OnTimelineControllerPropertyChanged;
        _timelinePlayer.PropertyChanged += OnTimelinePlayerPropertyChanged;

        UpdateSequenceProgress(PoseSlider.Value);
        SynchronizeTimelineUi();
        UpdateTimelinePlayerControls();
    }

    private void OnPoseSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e) =>
        UpdateSequenceProgress(e.NewValue);

    private void OnTimelineSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSynchronizingTimelineSlider)
        {
            return;
        }

        TimelineController.SetCurrentValue(
            TimelineMotionController.TimeProperty,
            TimeSpan.FromSeconds(e.NewValue));
    }

    private void OnTimelinePlayClick(object? sender, RoutedEventArgs e)
    {
        var duration = TimelineController.Duration;
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        if (TimelineController.Time >= duration)
        {
            TimelineController.SetCurrentValue(
                TimelineMotionController.TimeProperty,
                TimeSpan.Zero);
        }

        _timelinePlayer.Play();
    }

    private void OnTimelinePauseClick(object? sender, RoutedEventArgs e) =>
        _timelinePlayer.Pause();

    private void OnTimelineControllerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TimelineMotionController.TimeProperty ||
            e.Property == TimelineMotionController.DurationProperty)
        {
            SynchronizeTimelineUi();
        }
    }

    private void OnTimelinePlayerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != MotionPlayer.StateProperty)
        {
            return;
        }

        var state = _timelinePlayer.State;
        var completed =
            _lastTimelinePlayerState == MotionPlayerState.Playing &&
            state == MotionPlayerState.Stopped &&
            TimelineController.Duration > TimeSpan.Zero &&
            TimelineController.Time >= TimelineController.Duration;
        _lastTimelinePlayerState = state;
        UpdateTimelinePlayerControls();

        if (completed && TimelineLoopCheckBox.IsChecked == true)
        {
            QueueLoopRestart();
        }
    }

    private void UpdateSequenceProgress(double progress)
    {
        PoseProgressLabel.Text = $"Progress {progress:0.00}";
        PoseStateLabel.Text = SequenceStateNames[GetSequenceStateIndex(progress)];
    }

    private static int GetSequenceStateIndex(double progress) =>
        Math.Clamp(
            (int)Math.Round(progress * (SequenceStateNames.Length - 1)),
            0,
            SequenceStateNames.Length - 1);

    private void SynchronizeTimelineUi()
    {
        var duration = TimelineController.Duration;
        var time = TimelineController.Time > duration ? duration : TimelineController.Time;

        _isSynchronizingTimelineSlider = true;
        try
        {
            TimelineSlider.Maximum = Math.Max(duration.TotalSeconds, 0);
            TimelineSlider.Value = Math.Clamp(
                time.TotalSeconds,
                TimelineSlider.Minimum,
                TimelineSlider.Maximum);
        }
        finally
        {
            _isSynchronizingTimelineSlider = false;
        }

        TimelineTimeLabel.Text = $"{FormatTime(time)} / {FormatTime(duration)}";
        TimelineStateLabel.Text = GetTimelineStateLabel(time);
    }

    private void UpdateTimelinePlayerControls()
    {
        var isPlaying = _timelinePlayer.State == MotionPlayerState.Playing;
        TimelinePlayButton.IsVisible = !isPlaying;
        TimelinePauseButton.IsVisible = isPlaying;
    }

    private void QueueLoopRestart()
    {
        if (_loopRestartQueued)
        {
            return;
        }

        _loopRestartQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _loopRestartQueued = false;
                if (TimelineLoopCheckBox.IsChecked != true ||
                    _timelinePlayer.State != MotionPlayerState.Stopped ||
                    TimelineController.Duration <= TimeSpan.Zero ||
                    TimelineController.Time < TimelineController.Duration)
                {
                    return;
                }

                TimelineController.SetCurrentValue(
                    TimelineMotionController.TimeProperty,
                    TimeSpan.Zero);
                _timelinePlayer.Play();
            },
            DispatcherPriority.Background);
    }

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

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"mm\:ss\.f");

    private readonly record struct TimelineStop(string State, TimeSpan Time);
}
