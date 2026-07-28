using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Motion.Avalonia.Tests;

public sealed class SequenceMotionControllerTests
{
    [Test]
    public void CreateBlend_DefaultsToLinearAndCarriesTheSuppliedEdge()
    {
        var child = new Border();
        var controller = new TestMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "dim", 0);
        AddOpacityState(panel, "bright", 1);

        var edge = new MotionEdge { From = "dim", To = "bright" };
        var edgeInterpolator = new TargetDoubleInterpolator
        {
            Target = "editor",
            Property = Visual.OpacityProperty,
        };
        edgeInterpolator.Edges.Add(edge);
        panel.Interpolators.Add(edgeInterpolator);

        controller.Publish(MotionPose.CreateBlend(
            MotionPose.State("dim"),
            MotionPose.State("bright"),
            0.25));
        RunLayoutToConvergence(panel);
        var linearOpacity = child.Opacity;

        controller.Publish(MotionPose.CreateBlend(
            MotionPose.State("dim"),
            MotionPose.State("bright"),
            0.25,
            edge: edge));
        RunLayoutToConvergence(panel);

        Assert.Multiple(() =>
        {
            Assert.That(linearOpacity, Is.EqualTo(0.25).Within(0.001));
            Assert.That(child.Opacity, Is.EqualTo(1));
        });
    }

    [Test]
    public void Progress_AtStopsAndOutsideTheirRange_PresentsTheExpectedState()
    {
        var child = new Border();
        var controller = new SequenceMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "first", 0.1);
        AddOpacityState(panel, "middle", 0.5);
        AddOpacityState(panel, "cut", 0.7);
        AddOpacityState(panel, "last", 0.9);
        controller.Stops.Add(new MotionStop { State = "first", Offset = 0.2 });
        controller.Stops.Add(new MotionStop { State = "middle", Offset = 0.5 });
        controller.Stops.Add(new MotionStop { State = "cut", Offset = 0.5 });
        controller.Stops.Add(new MotionStop { State = "last", Offset = 0.8 });

        controller.Progress = -1;
        RunLayoutToConvergence(panel);
        var beforeFirst = child.Opacity;

        controller.Progress = 0.5;
        RunLayoutToConvergence(panel);
        var exactMiddle = child.Opacity;

        controller.Progress = 2;
        RunLayoutToConvergence(panel);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Progress, Is.EqualTo(1));
            Assert.That(beforeFirst, Is.EqualTo(0.1).Within(0.001));
            Assert.That(exactMiddle, Is.EqualTo(0.7).Within(0.001));
            Assert.That(child.Opacity, Is.EqualTo(0.9).Within(0.001));
        });
    }

    [Test]
    public void Progress_WithinUnevenInterval_UsesIncomingStopEasing()
    {
        var child = new Border();
        var controller = new SequenceMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "source", 0);
        AddOpacityState(panel, "target", 1);
        controller.Stops.Add(new MotionStop { State = "source", Offset = 0.1 });
        controller.Stops.Add(new MotionStop
        {
            State = "target",
            Offset = 0.3,
            Easing = new SquareEasing(),
        });

        controller.Progress = 0.2;
        RunLayoutToConvergence(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.001));
    }

    [Test]
    public void Progress_WithinVeryShortInterval_DoesNotSnapToSource()
    {
        var child = new Border();
        var controller = new SequenceMotionController();
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "source", 0);
        AddOpacityState(panel, "target", 1);
        controller.Stops.Add(new MotionStop { State = "source", Offset = 0.5 });
        controller.Stops.Add(new MotionStop { State = "target", Offset = 0.50001 });

        controller.Progress = 0.500005;
        RunLayoutToConvergence(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void SequenceController_WhenCreated_DoesNotOptIntoPlayerDrivenBase()
    {
        var controller = new SequenceMotionController();

        controller.Progress = 0.25;

        Assert.That(controller, Is.Not.InstanceOf<PlayerDrivenMotionController>());
    }

    [Test]
    public void CreateBlend_WhenEasingOvershoots_PreservesLayoutOvershoot()
    {
        var child = new Border();
        var controller = new TestMotionController();
        var panel = CreatePanel(child, controller);
        AddSizedState(panel, "small", 100);
        AddSizedState(panel, "large", 200);

        controller.Publish(MotionPose.CreateBlend(
            MotionPose.State("small"),
            MotionPose.State("large"),
            0.75,
            new DoubleProgressEasing()));
        RunLayoutToConvergence(panel);

        Assert.That(panel.DesiredSize.Width, Is.EqualTo(250).Within(0.001));
    }

    [Test]
    public void Stops_WhenCollectionOrPropertiesChange_ReevaluateTheCurrentProgress()
    {
        var child = new Border();
        var controller = new SequenceMotionController { Progress = 0.5 };
        var panel = CreatePanel(child, controller);
        AddOpacityState(panel, "start", 0);
        AddOpacityState(panel, "end", 1);
        AddOpacityState(panel, "inserted", 0.6);
        AddOpacityState(panel, "changed", 0.3);
        controller.Stops.Add(new MotionStop { State = "start", Offset = 0 });
        var end = new MotionStop { State = "end", Offset = 1 };
        controller.Stops.Add(end);
        RunLayoutToConvergence(panel);
        var initialBlend = child.Opacity;

        var inserted = new MotionStop { State = "inserted", Offset = 0.5 };
        controller.Stops.Add(inserted);
        RunLayoutToConvergence(panel);
        var afterAdd = child.Opacity;

        inserted.State = "changed";
        RunLayoutToConvergence(panel);
        var afterStateChange = child.Opacity;

        inserted.Offset = 0.75;
        RunLayoutToConvergence(panel);
        var afterOffsetChange = child.Opacity;

        controller.Stops.Remove(inserted);
        end.Easing = new SquareEasing();
        RunLayoutToConvergence(panel);

        Assert.Multiple(() =>
        {
            Assert.That(initialBlend, Is.EqualTo(0.5).Within(0.001));
            Assert.That(afterAdd, Is.EqualTo(0.6).Within(0.001));
            Assert.That(afterStateChange, Is.EqualTo(0.3).Within(0.001));
            Assert.That(afterOffsetChange, Is.EqualTo(0.2).Within(0.001));
            Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.001));
        });
    }

    [Test]
    public void Offset_WhenOutsideNormalizedFiniteRange_IsRejected()
    {
        foreach (var invalidOffset in new[] { -0.01, 1.01, double.NaN })
        {
            Assert.Throws<ArgumentException>(
                () => _ = new MotionStop { Offset = invalidOffset });
        }
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

    private static void AddSizedState(MotionPanel panel, object key, double width)
    {
        panel.States.Add(new MotionState
        {
            Key = key,
            Template = new TestTemplate(() => new MotionSlot
            {
                Target = "editor",
                Width = width,
                Height = 40,
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

    private sealed class TestMotionController : MotionController
    {
        public void Publish(MotionPose pose) => SetCurrentPose(pose);
    }

    private sealed class TestTemplate(Func<Control> build) : IDataTemplate
    {
        public bool Match(object? data) => true;

        public Control Build(object? param) => build();
    }

    private sealed class TargetDoubleInterpolator : MotionInterpolator<double>
    {
        public override double Interpolate(double progress, double oldValue, double newValue) =>
            newValue;
    }

    private sealed class SquareEasing : Easing
    {
        public override double Ease(double progress) => progress * progress;
    }

    private sealed class DoubleProgressEasing : Easing
    {
        public override double Ease(double progress) => progress * 2;
    }
}
