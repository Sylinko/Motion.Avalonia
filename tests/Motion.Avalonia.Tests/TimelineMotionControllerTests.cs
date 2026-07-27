using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace Motion.Avalonia.Tests;

public sealed class TimelineMotionControllerTests
{
    [Test]
    public void Time_AtKeyFramesAndOutsideTheirRange_PresentsTheExpectedState()
    {
        var child = new Border();
        var controller = new TimelineMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "first", 0.1);
        AddOpacityState(panel, "middle", 0.5);
        AddOpacityState(panel, "cut", 0.7);
        AddOpacityState(panel, "last", 0.9);
        controller.KeyFrames.Add(new MotionKeyFrame { State = "first", KeyTime = TimeSpan.FromSeconds(1) });
        controller.KeyFrames.Add(new MotionKeyFrame { State = "middle", KeyTime = TimeSpan.FromSeconds(2) });
        controller.KeyFrames.Add(new MotionKeyFrame { State = "cut", KeyTime = TimeSpan.FromSeconds(2) });
        controller.KeyFrames.Add(new MotionKeyFrame { State = "last", KeyTime = TimeSpan.FromSeconds(4) });

        controller.Time = TimeSpan.FromMilliseconds(500);
        RunLayoutToConvergence(panel);
        var beforeFirst = child.Opacity;

        controller.Time = TimeSpan.FromSeconds(2);
        RunLayoutToConvergence(panel);
        var exactCut = child.Opacity;

        controller.Time = TimeSpan.FromSeconds(8);
        RunLayoutToConvergence(panel);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Duration, Is.EqualTo(TimeSpan.FromSeconds(4)));
            Assert.That(controller.Time, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(beforeFirst, Is.EqualTo(0.1).Within(0.001));
            Assert.That(exactCut, Is.EqualTo(0.7).Within(0.001));
            Assert.That(child.Opacity, Is.EqualTo(0.9).Within(0.001));
        });
    }

    [Test]
    public void Time_WithinUnevenInterval_UsesIncomingKeyFrameEasing()
    {
        var child = new Border();
        var controller = new TimelineMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "source", 0);
        AddOpacityState(panel, "target", 1);
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "source",
            KeyTime = TimeSpan.FromSeconds(1),
        });
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "target",
            KeyTime = TimeSpan.FromSeconds(3),
            Easing = new SquareEasing(),
        });

        controller.Time = TimeSpan.FromSeconds(2);
        RunLayoutToConvergence(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.001));
    }

    [Test]
    public void PlayerAndTime_WhenEitherChanges_StaySynchronized()
    {
        var controller = new TimelineMotionController();
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "start",
            KeyTime = TimeSpan.Zero,
        });
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "end",
            KeyTime = TimeSpan.FromSeconds(4),
        });

        controller.Time = TimeSpan.FromSeconds(1);
        var progressAfterTimeChange = controller.Player!.Progress;

        controller.Player.Seek(0.75);

        Assert.Multiple(() =>
        {
            Assert.That(progressAfterTimeChange, Is.EqualTo(0.25));
            Assert.That(controller.Time, Is.EqualTo(TimeSpan.FromSeconds(3)));
        });
    }

    [Test]
    public void KeyFrames_WhenTimelineChanges_StopPlaybackAndReevaluateDuration()
    {
        var child = new Border();
        var controller = new TimelineMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "start", 0);
        AddOpacityState(panel, "middle", 0.5);
        AddOpacityState(panel, "end", 1);
        controller.KeyFrames.Add(new MotionKeyFrame { State = "start", KeyTime = TimeSpan.Zero });
        var end = new MotionKeyFrame { State = "end", KeyTime = TimeSpan.FromSeconds(4) };
        controller.KeyFrames.Add(end);
        controller.Player!.Play(controller.Duration);
        controller.Time = TimeSpan.FromSeconds(2);

        var middle = new MotionKeyFrame { State = "middle", KeyTime = TimeSpan.FromSeconds(2) };
        controller.KeyFrames.Add(middle);
        RunLayoutToConvergence(panel);
        var afterAdd = child.Opacity;

        end.KeyTime = TimeSpan.FromSeconds(8);
        RunLayoutToConvergence(panel);

        Assert.Multiple(() =>
        {
            Assert.That(afterAdd, Is.EqualTo(0.5).Within(0.001));
            Assert.That(controller.Duration, Is.EqualTo(TimeSpan.FromSeconds(8)));
            Assert.That(controller.Player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(controller.Player.Progress, Is.EqualTo(0.25));
            Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
        });
    }

    [Test]
    public void Player_WhenAdvanced_MapsProgressToAbsoluteTime()
    {
        var controller = new TimelineMotionController();
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "start",
            KeyTime = TimeSpan.Zero,
        });
        controller.KeyFrames.Add(new MotionKeyFrame
        {
            State = "end",
            KeyTime = TimeSpan.FromSeconds(4),
        });
        var player = controller.Player!;

        player.Play(controller.Duration);
        Advance(player, TimeSpan.Zero);
        Advance(player, TimeSpan.FromSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(controller.Time, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(player.Progress, Is.EqualTo(0.25));
        });
    }

    [Test]
    public void KeyTime_WhenNegative_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new MotionKeyFrame { KeyTime = TimeSpan.FromTicks(-1) });
    }

    private static MotionPanel CreatePanel(Control child, MotionController controller)
    {
        child.Name = "editor";
        var panel = new MotionPanel { Controller = controller };
        panel.Children.Add(child);
        return panel;
    }

    private static void AddOpacityState(MotionPanel panel, object key, double opacity)
    {
        panel.States.Add(new MotionState
        {
            Key = key,
            Template = new TestTemplate(() => new MotionSlot
            {
                Target = "editor",
                Width = 100,
                Height = 40,
                Opacity = opacity,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            }),
        });
    }

    private static void RunLayoutToConvergence(MotionPanel panel)
    {
        var size = new Size(400, 200);
        panel.Measure(size);
        panel.Arrange(new Rect(size));
        panel.Measure(size);
        panel.Arrange(new Rect(size));
    }

    private static void Advance(MotionPlayer player, TimeSpan timestamp) =>
        InvokeAnimationFrame(player, timestamp, GetFrameGeneration(player));

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnAnimationFrame")]
    private static extern void InvokeAnimationFrame(
        MotionPlayer player,
        TimeSpan timestamp,
        int generation);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_frameGeneration")]
    private static extern ref int GetFrameGeneration(MotionPlayer player);

    private sealed class TestTemplate(Func<Control> build) : IDataTemplate
    {
        public bool Match(object? data) => true;

        public Control Build(object? param) => build();
    }

    private sealed class SquareEasing : Easing
    {
        public override double Ease(double progress) => progress * progress;
    }
}
