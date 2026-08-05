using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Collections;
using Avalonia.Metadata;
using Motion.Avalonia.Utilities;

namespace Motion.Avalonia;

/// <summary>
/// Evaluates sparse keyframes along externally supplied normalized progress.
/// </summary>
/// <remarks>
/// This motion deliberately owns no player. Bind <see cref="Progress"/> to scrolling, a slider,
/// a gesture, or any other normalized source. A property omitted from a keyframe receives no key;
/// it interpolates only between the nearest keyframes that explicitly define that channel.
/// </remarks>
public sealed class SequenceMotion : Motion, IKeyFrameMotion
{
    /// <summary>
    /// Defines the <see cref="Progress"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SequenceMotion, double>(
            nameof(Progress),
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, 0, 1));

    static SequenceMotion()
    {
        AffectsMotion<SequenceMotion>(EasingProperty);
    }

    /// <summary>
    /// Gets or sets normalized sequence progress.
    /// </summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Gets the sparse Cue-based keyframes.
    /// </summary>
    [Content]
    public AvaloniaList<SequenceMotionKeyFrame> KeyFrames => _keyFrames;

    /// <inheritdoc/>
    IEnumerable<MotionKeyFrame> IKeyFrameMotion.KeyFrames => _keyFrames.OrderBy(keyFrame => keyFrame.Cue.CueValue);

    private readonly LogicalChildrenList<SequenceMotionKeyFrame> _keyFrames;
    private int _initializationDepth;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceMotion"/> class.
    /// </summary>
    public SequenceMotion()
    {
        _keyFrames = new LogicalChildrenList<SequenceMotionKeyFrame>(this);
        KeyFrames.CollectionChanged += HandleKeyFramesChanged;
    }

    /// <inheritdoc />
    public override void BeginInit()
    {
        base.BeginInit();
        _initializationDepth++;
    }

    /// <inheritdoc />
    public override void EndInit()
    {
        if (_initializationDepth == 0)
        {
            base.EndInit();
            return;
        }

        if (_initializationDepth == 1)
        {
            InvalidateMotion();
        }

        _initializationDepth--;
        base.EndInit();
    }

    /// <inheritdoc />
    protected override MotionRuntime CreateRuntime(MotionCompilationContext context) => new KeyFrameMotionRuntime<SequenceMotion>(context, this);

    Cue IKeyFrameMotion.GetCue(MotionKeyFrame keyFrame)
    {
        if (keyFrame is not SequenceMotionKeyFrame sequenceKeyFrame)
        {
            throw new ArgumentException($"{nameof(SequenceMotion)} can only resolve {nameof(SequenceMotionKeyFrame)} cues.", nameof(keyFrame));
        }

        return sequenceKeyFrame.Cue;
    }

    IDisposable IObservable<double>.Subscribe(IObserver<double> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return this.GetObservable(ProgressProperty).Subscribe(observer);
    }

    private void HandleKeyFramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_initializationDepth > 0)
        {
            return;
        }

        InvalidateMotion();
    }
}