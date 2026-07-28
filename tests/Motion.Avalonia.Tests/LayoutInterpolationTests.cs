using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

public sealed class LayoutInterpolationTests
{
    [Test]
    public void LayoutChannels_WhenCustomInterpolatorsMatch_UseTheTargetValues()
    {
        var child = new Border();
        var (panel, controller) = CreatePanel(child);
        panel.Interpolators.Add(new TargetPointInterpolator
        {
            Target = "editor",
            Property = MotionLayout.PositionProperty,
        });
        panel.Interpolators.Add(new TargetSizeInterpolator
        {
            Target = "editor",
            Property = MotionLayout.SizeProperty,
        });
        panel.Interpolators.Add(new TargetSizeInterpolator
        {
            Property = MotionLayout.DesiredSizeProperty,
        });
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Present(panel, controller, MotionPose.State("center"));
        Present(
            panel,
            controller,
            MotionPose.CreateBlend(
                MotionPose.State("center"),
                MotionPose.State("left"),
                0.5));

        Assert.Multiple(() =>
        {
            Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(0, 0, 80, 40)));
            Assert.That(panel.DesiredSize.Width, Is.EqualTo(80));
        });
    }

    [Test]
    public void PresenceChannel_WhenCustomInterpolatorMatches_ControlsTargetEntry()
    {
        var editor = new Border();
        var preview = new Border();
        var (panel, controller) = CreatePanel(editor, preview);
        panel.Interpolators.Add(new TargetDoubleInterpolator
        {
            Target = "preview",
            Property = MotionLayout.PresenceProperty,
        });
        panel.States.Add(CreateState(
            "hidden",
            () => CreateSlot("editor", 100, HorizontalAlignment.Left)));
        panel.States.Add(CreateState(
            "visible",
            () =>
            {
                var root = new Panel();
                root.Children.Add(CreateSlot("editor", 100, HorizontalAlignment.Left));
                root.Children.Add(CreateSlot("preview", 100, HorizontalAlignment.Right));
                return root;
            }));

        Present(panel, controller, MotionPose.State("hidden"));
        Present(
            panel,
            controller,
            MotionPose.CreateBlend(
                MotionPose.State("hidden"),
                MotionPose.State("visible"),
                0.25));

        Assert.That(((Control)preview.GetVisualParent()!).Opacity, Is.EqualTo(1));
    }

    [Test]
    public void PositionChannel_WhenInterpolatorOverridesEasing_UsesTheOverride()
    {
        var child = new Border();
        var (panel, controller) = CreatePanel(child);
        panel.Interpolators.Add(new MotionAvaloniaInterpolator
        {
            Target = "editor",
            Property = MotionLayout.PositionProperty,
            Easing = new SquareEasing(),
        });
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 80, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Present(panel, controller, MotionPose.State("center"));
        Present(
            panel,
            controller,
            MotionPose.CreateBlend(
                MotionPose.State("center"),
                MotionPose.State("left"),
                0.5));

        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(120).Within(0.001));
    }

    [Test]
    public void PositionChannel_WhenEdgeInterpolatorMatches_OverridesGeneralInterpolator()
    {
        var child = new Border();
        var (panel, controller) = CreatePanel(child);
        var edge = new MotionEdge { From = "center", To = "left" };
        panel.Interpolators.Add(new SourcePointInterpolator
        {
            Target = "editor",
            Property = MotionLayout.PositionProperty,
        });
        var edgeInterpolator = new TargetPointInterpolator
        {
            Target = "editor",
            Property = MotionLayout.PositionProperty,
        };
        edgeInterpolator.Edges.Add(edge);
        panel.Interpolators.Add(edgeInterpolator);
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 100, HorizontalAlignment.Left)));

        Present(panel, controller, MotionPose.State("center"));
        Present(
            panel,
            controller,
            MotionPose.CreateBlend(
                MotionPose.State("center"),
                MotionPose.State("left"),
                0.5,
                edge: edge));

        Assert.That(GetPresentedBounds(child).X, Is.Zero);
    }

    [Test]
    public void PositionChannel_WhenCubicBezierInterpolatorMatches_FollowsTheCurve()
    {
        var child = new Border();
        var (panel, controller) = CreatePanel(child);
        panel.Interpolators.Add(new CubicBezierMotionInterpolator
        {
            Target = "editor",
            Property = MotionLayout.PositionProperty,
            SourceControlOffset = new Vector(0, 100),
            TargetControlOffset = new Vector(0, 100),
        });
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 100, HorizontalAlignment.Left)));

        Present(panel, controller, MotionPose.State("center"));
        Present(
            panel,
            controller,
            MotionPose.CreateBlend(
                MotionPose.State("center"),
                MotionPose.State("left"),
                0.5));

        Assert.That(GetPresentedBounds(child).Position, Is.EqualTo(new Point(75, 75)));
    }

    [Test]
    public void CubicBezierInterpolator_WhenControlOffsetIsNotFinite_RejectsTheValue()
    {
        Assert.Throws<ArgumentException>(
            () => _ = new CubicBezierMotionInterpolator
            {
                SourceControlOffset = new Vector(double.NaN, 0),
            });
    }

    [Test]
    public void MotionSlot_WhenSetterUsesLayoutProperty_RejectsTheChannel()
    {
        var slot = CreateSlot("editor", 100, HorizontalAlignment.Left);

        var exception = Assert.Throws<InvalidOperationException>(
            () => slot.Setters.Add(new Setter(
                MotionLayout.PositionProperty,
                new Point(10, 20))));

        Assert.That(exception!.Message, Does.Contain("evaluated layout channel"));
    }

    private static (MotionPanel Panel, TestMotionController Controller) CreatePanel(params Control[] children)
    {
        var controller = new TestMotionController();
        var panel = new MotionPanel { Controller = controller };

        for (var i = 0; i < children.Length; i++)
        {
            children[i].Name = i == 0 ? "editor" : "preview";
            panel.Children.Add(children[i]);
        }

        return (panel, controller);
    }

    private static MotionState CreateState(object key, Func<Control> build) =>
        new()
        {
            Key = key,
            Template = new TestTemplate(build),
        };

    private static MotionSlot CreateSlot(
        string target,
        double width,
        HorizontalAlignment horizontalAlignment) =>
        new()
        {
            Target = target,
            Width = width,
            Height = 40,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Top,
        };

    private static void Present(
        MotionPanel panel,
        TestMotionController controller,
        MotionPose pose)
    {
        controller.Publish(pose);
        var size = new Size(400, 200);
        panel.Measure(size);
        panel.Arrange(new Rect(size));
        panel.Measure(size);
        panel.Arrange(new Rect(size));
    }

    private static Rect GetPresentedBounds(Control child) =>
        ((Control)child.GetVisualParent()!).Bounds;

    private sealed class TestMotionController : MotionController
    {
        public void Publish(MotionPose pose) => SetCurrentPose(pose);
    }

    private sealed class TestTemplate(Func<Control> build) : IDataTemplate
    {
        public bool Match(object? data) => true;

        public Control Build(object? param) => build();
    }

    private sealed class TargetPointInterpolator : MotionInterpolator<Point>
    {
        public override Point Interpolate(double progress, Point oldValue, Point newValue) =>
            newValue;
    }

    private sealed class SourcePointInterpolator : MotionInterpolator<Point>
    {
        public override Point Interpolate(double progress, Point oldValue, Point newValue) =>
            oldValue;
    }

    private sealed class TargetSizeInterpolator : MotionInterpolator<Size>
    {
        public override Size Interpolate(double progress, Size oldValue, Size newValue) =>
            newValue;
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
}
