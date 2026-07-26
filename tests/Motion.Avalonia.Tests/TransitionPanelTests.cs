using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

public sealed class TransitionPanelTests
{
    [Test]
    public void Arrange_WhenStateChangesWithoutEasing_SnapsToRequestedLiveLayout()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Arrange(panel, "center", new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(150, 0, 100, 40)));

        Arrange(panel, "left", new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(0, 0, 80, 40)));
    }

    [Test]
    public void Arrange_WhenPanelSizeChangesAndLayoutEasingIsNull_ReevaluatesStateImmediately()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));

        Arrange(panel, "center", new Size(400, 200));
        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(150));

        panel.Measure(new Size(600, 200));
        panel.Arrange(new Rect(0, 0, 600, 200));

        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(250));
    }

    [Test]
    public void Arrange_WhenTargetIsAbsentFromState_UpdatesEffectivePresence()
    {
        var editor = new Border();
        var preview = new Border();
        var panel = CreatePanel(editor, preview);
        panel.States.Add(CreateState(
            "quick",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "advanced",
            () =>
            {
                var grid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("100,*"),
                };
                grid.Children.Add(CreateSlot("editor", 100, HorizontalAlignment.Left));
                var previewSlot = CreateSlot("preview", 200, HorizontalAlignment.Stretch);
                Grid.SetColumn(previewSlot, 1);
                grid.Children.Add(previewSlot);
                return grid;
            }));

        Arrange(panel, "quick", new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsVisible, Is.True);
            Assert.That(preview.IsVisible, Is.True);
            Assert.That(GetMotionOpacity(preview), Is.Zero);
        });

        Arrange(panel, "advanced", new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(editor.IsVisible, Is.True);
            Assert.That(preview.IsVisible, Is.True);
            Assert.That(GetMotionOpacity(preview), Is.EqualTo(1));
            Assert.That(GetPresentedBounds(preview), Is.EqualTo(new Rect(150, 0, 200, 40)));
        });
    }

    [Test]
    public void Measure_WhenChildrenHaveDuplicateNames_ThrowsConfigurationError()
    {
        var first = new Border();
        var second = new Border();
        var panel = CreatePanel(first, second);
        second.Name = "editor";

        var exception = Assert.Throws<InvalidOperationException>(
            () => panel.Measure(new Size(400, 200)));

        Assert.That(exception!.Message, Does.Contain("more than one direct child"));
    }

    [Test]
    public void Arrange_WhenStateEasingIsNull_DoesNotFallThroughToLayoutEasing()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.LayoutTransitionEasing = new global::Avalonia.Animation.Easings.LinearEasing();
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Arrange(panel, "center", new Size(400, 200));
        Arrange(panel, "left", new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(0, 0, 80, 40)));
    }

    [Test]
    public void Arrange_WhenStateKeysAreBoolean_MatchesWithoutStringConversion()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            true,
            () => CreateSlot("editor", 120, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            false,
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Arrange(panel, true, new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(140, 0, 120, 40)));
    }

    [Test]
    public void States_WhenAnotherStateIsAdded_PreserveExistingTemplateInstance()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        var firstTemplate = new CountingTemplate(
            () => CreateSlot("editor", 100, HorizontalAlignment.Center));
        panel.States.Add(new TransitionPanelState { Key = "first", Template = firstTemplate });

        panel.States.Add(CreateState(
            "second",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));
        Arrange(panel, "first", new Size(400, 200));
        Arrange(panel, "second", new Size(400, 200));
        Arrange(panel, "first", new Size(400, 200));

        Assert.That(firstTemplate.BuildCount, Is.EqualTo(1));
    }

    [Test]
    public void Arrange_WhenStatesChange_PreservesStableContainerAndLogicalParent()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));

        Arrange(panel, "center", new Size(400, 200));
        var container = child.GetVisualParent();
        Arrange(panel, "left", new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(container, Is.Not.Null);
            Assert.That(child.GetVisualParent(), Is.SameAs(container));
            Assert.That(child.Parent, Is.SameAs(panel));
            Assert.That(panel.IsMeasureValid, Is.True);
            Assert.That(panel.IsArrangeValid, Is.True);
            Assert.That(((Control)container!).IsMeasureValid, Is.True);
            Assert.That(((Control)container).IsArrangeValid, Is.True);
        });
    }

    [Test]
    public void StateTransition_WhenAdvancedHalfway_ArrangesAtInterpolatedRealBounds()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.StateTransitionDuration = TimeSpan.FromSeconds(1);
        panel.StateTransitionEasing = new global::Avalonia.Animation.Easings.LinearEasing();
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));
        Arrange(panel, "center", new Size(400, 200));

        Arrange(panel, "left", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(75, 0, 90, 40)));

        AdvanceAnimation(panel, TimeSpan.FromSeconds(1), new Size(400, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(0, 0, 80, 40)));
    }

    [Test]
    public void StateTransition_WhenInterruptedAndResized_KeepsSourceStatesLive()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.StateTransitionDuration = TimeSpan.FromSeconds(1);
        panel.StateTransitionEasing = new global::Avalonia.Animation.Easings.LinearEasing();
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));
        panel.States.Add(CreateState(
            "right",
            () => CreateSlot("editor", 120, HorizontalAlignment.Right)));
        Arrange(panel, "center", new Size(400, 200));
        Arrange(panel, "left", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));
        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(75, 0, 90, 40)));

        Arrange(panel, "right", new Size(500, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(100, 0, 90, 40)));
    }

    [Test]
    public void LayoutTransition_WhenTargetChangesDuringAnimation_RetargetsFromPresentedBounds()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.LayoutTransitionDuration = TimeSpan.FromSeconds(1);
        panel.LayoutTransitionEasing = new global::Avalonia.Animation.Easings.LinearEasing();
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        Arrange(panel, "center", new Size(400, 200));

        Arrange(panel, "center", new Size(600, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(600, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(600, 200));
        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(200));

        Arrange(panel, "center", new Size(800, 200));
        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(200));
        AdvanceAnimation(panel, TimeSpan.FromSeconds(1), new Size(800, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(1500), new Size(800, 200));

        Assert.That(GetPresentedBounds(child).X, Is.EqualTo(275));
    }

    [Test]
    public void StateTransition_WhenTargetEntersAndLeaves_FadesAndRestoresBaseProperties()
    {
        var editor = new Border();
        var preview = new Border { Opacity = 0.8 };
        var panel = CreatePanel(editor, preview);
        panel.StateTransitionDuration = TimeSpan.FromSeconds(1);
        panel.StateTransitionEasing = new global::Avalonia.Animation.Easings.LinearEasing();
        panel.States.Add(CreateState(
            "quick",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "advanced",
            () =>
            {
                var grid = new Grid();
                grid.Children.Add(CreateSlot("editor", 100, HorizontalAlignment.Left));
                grid.Children.Add(CreateSlot("preview", 200, HorizontalAlignment.Right));
                return grid;
            }));
        Arrange(panel, "quick", new Size(400, 200));

        Arrange(panel, "advanced", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(preview.IsVisible, Is.True);
            Assert.That(GetMotionOpacity(preview), Is.EqualTo(0.5).Within(0.001));
            Assert.That(
                preview.Opacity * GetMotionOpacity(preview),
                Is.EqualTo(0.4).Within(0.001));
        });

        preview.Opacity = 0.6;
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(
            preview.Opacity * GetMotionOpacity(preview),
            Is.EqualTo(0.3).Within(0.001));

        AdvanceAnimation(panel, TimeSpan.FromSeconds(1), new Size(400, 200));
        Arrange(panel, "quick", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromSeconds(1), new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromSeconds(2), new Size(400, 200));

        Assert.That(preview.IsVisible, Is.True);
        Assert.That(GetMotionOpacity(preview), Is.Zero);

        panel.Children.Remove(preview);

        Assert.Multiple(() =>
        {
            Assert.That(preview.IsVisible, Is.True);
            Assert.That(preview.Opacity, Is.EqualTo(0.6).Within(0.001));
            Assert.That(preview.GetVisualParent(), Is.Null);
        });
    }

    private static TransitionPanel CreatePanel(params Control[] children)
    {
        var panel = new TransitionPanel();

        for (var i = 0; i < children.Length; i++)
        {
            children[i].Name = i == 0 ? "editor" : "preview";
            panel.Children.Add(children[i]);
        }

        return panel;
    }

    private static TransitionPanelState CreateState(object key, Func<Control> build) =>
        new()
        {
            Key = key,
            Template = new TestTemplate(build),
        };

    private static TransitionPanelSlot CreateSlot(
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

    private static void Arrange(TransitionPanel panel, object state, Size size)
    {
        panel.State = state;
        RunLayoutToConvergence(panel, size);
    }

    private static void AdvanceAnimation(TransitionPanel panel, TimeSpan timestamp, Size size)
    {
        InvokeAnimationFrame(panel, timestamp, 0);
        RunLayoutToConvergence(panel, size);
    }

    private static void RunLayoutToConvergence(TransitionPanel panel, Size size)
    {
        panel.Measure(size);
        panel.Arrange(new Rect(size));
        panel.Measure(size);
        panel.Arrange(new Rect(size));
    }

    private static Rect GetPresentedBounds(Control child) =>
        ((Control)child.GetVisualParent()!).Bounds;

    private static double GetMotionOpacity(Control child) =>
        ((Control)child.GetVisualParent()!).Opacity;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnAnimationFrame")]
    private static extern void InvokeAnimationFrame(
        TransitionPanel panel,
        TimeSpan timestamp,
        int generation);

    private sealed class TestTemplate : IDataTemplate
    {
        private readonly Func<Control> _build;

        public TestTemplate(Func<Control> build)
        {
            _build = build;
        }

        public bool Match(object? data) => true;

        public Control Build(object? param) => _build();
    }

    private sealed class CountingTemplate : IDataTemplate
    {
        private readonly Func<Control> _build;

        public CountingTemplate(Func<Control> build)
        {
            _build = build;
        }

        public int BuildCount { get; private set; }

        public bool Match(object? data) => true;

        public Control Build(object? param)
        {
            BuildCount++;
            return _build();
        }
    }
}
