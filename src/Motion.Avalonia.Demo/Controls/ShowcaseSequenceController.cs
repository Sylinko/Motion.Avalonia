using Avalonia;
using Avalonia.Animation.Easings;

namespace Motion.Avalonia.Demo.Controls;

/// <summary>
/// Maps normalized showcase progress to three neighboring state poses.
/// </summary>
public sealed class ShowcaseSequenceController : MotionController
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ShowcaseSequenceController, double>(
            nameof(Progress),
            validate: static value => double.IsFinite(value),
            coerce: static (_, value) => Math.Clamp(value, 0, 1));

    private const double RevealOffset = 0.58;

    private static readonly Easing RevealEasing = new CubicEaseOut();
    private static readonly Easing DiscussionEasing = new CubicEaseInOut();

    static ShowcaseSequenceController()
    {
        ProgressProperty.Changed.AddClassHandler<ShowcaseSequenceController>(
            static (controller, _) => controller.PublishPose());
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public ShowcaseSequenceController()
    {
        PublishPose();
    }

    private void PublishPose()
    {
        if (Progress <= 0)
        {
            SetCurrentPose(MotionPose.State("Immersive"));
            return;
        }

        if (Progress < RevealOffset)
        {
            SetCurrentPose(
                MotionPose.Blend(
                    MotionPose.State("Immersive"),
                    MotionPose.State("Reveal"),
                    Progress / RevealOffset,
                    RevealEasing));
            return;
        }

        if (Progress >= 1)
        {
            SetCurrentPose(MotionPose.State("Discussion"));
            return;
        }

        SetCurrentPose(
            MotionPose.Blend(
                MotionPose.State("Reveal"),
                MotionPose.State("Discussion"),
                (Progress - RevealOffset) / (1 - RevealOffset),
                DiscussionEasing));
    }
}
