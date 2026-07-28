using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;
using Motion.Avalonia.Extensions;

namespace Motion.Avalonia;

/// <summary>
/// Maps normalized progress across an ordered sequence of state stops.
/// </summary>
/// <remarks>
/// This controller has no playback commands of its own. Bind <see cref="Progress"/> to a slider,
/// scroll position, gesture, or other external progress source. It intentionally does not opt
/// into <see cref="PlayerDrivenMotionController"/>. At an exact stop it publishes a state pose;
/// between stops it publishes a blend using the destination stop's easing. Equal offsets create
/// an instantaneous cut, with the last declared stop winning at and after that offset.
/// </remarks>
/// <example>
/// <code><![CDATA[
/// <motion:SequenceMotionController Progress="{Binding ScrollProgress}">
///   <motion:MotionStop State="Immersive" Offset="0"/>
///   <motion:MotionStop State="Reveal" Offset="0.4" Easing="CubicEaseOut"/>
///   <motion:MotionStop State="Discussion" Offset="1" Easing="CubicEaseInOut"/>
/// </motion:SequenceMotionController>
/// ]]></code>
/// </example>
public sealed class SequenceMotionController : MotionController
{
    /// <summary>
    /// Defines the <see cref="Progress"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SequenceMotionController, double>(
            nameof(Progress),
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, 0, 1));

    static SequenceMotionController()
    {
        ProgressProperty.Changed.AddClassHandler<SequenceMotionController>(static (controller, _) => controller.PublishPose());
    }

    /// <summary>
    /// Gets or sets the normalized position in the sequence.
    /// </summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Gets the state stops that define the sequence.
    /// </summary>
    [Content]
    public AvaloniaList<MotionStop> Stops { get; } = [];

    private readonly HashSet<MotionStop> _attachedStops = [];
    private readonly List<MotionStop> _orderedStops = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceMotionController"/> class.
    /// </summary>
    public SequenceMotionController()
    {
        Stops.CollectionChanged += OnStopsChanged;
        PublishPose();
    }

    private void OnStopsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeStops();
        RebuildOrderedStops();
        PublishPose();
    }

    private void SynchronizeStops()
    {
        foreach (var stop in _attachedStops.Where(stop => !Stops.Contains(stop)).ToArray())
        {
            stop.PropertyChanged -= OnStopPropertyChanged;
            _attachedStops.Remove(stop);
        }

        foreach (var stop in Stops)
        {
            if (!_attachedStops.Add(stop))
            {
                continue;
            }

            stop.PropertyChanged += OnStopPropertyChanged;
        }
    }

    private void OnStopPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MotionStop.OffsetProperty)
        {
            RebuildOrderedStops();
        }

        PublishPose();
    }

    private void RebuildOrderedStops()
    {
        _orderedStops.Clear();

        foreach (var stop in Stops)
        {
            MotionSegmentEvaluator.InsertOrdered(
                _orderedStops,
                stop,
                static item => item.Offset);
        }
    }

    private void PublishPose() =>
        SetCurrentPose(
            MotionSegmentEvaluator.Evaluate(
                _orderedStops,
                Progress,
                static item => item.Offset,
                static item => item.State,
                static item => item.Easing,
                static (left, right) => left.IsCloseTo(right, 1e-10)));
}