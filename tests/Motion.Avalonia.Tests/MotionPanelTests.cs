using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

public sealed class MotionPanelTests
{
    [Test]
    public void MotionPanel_UsesMotionItemsPanelAndExplicitItemContainerByDefault()
    {
        var itemsControl = new ProbeMotionPanel();
        var panel = itemsControl.ItemsPanel.Build();
        var container = itemsControl.CreateContainer(typeof(object));

        Assert.Multiple(() =>
        {
            Assert.That(panel, Is.TypeOf<MotionItemsPanel>());
            Assert.That(container, Is.TypeOf<MotionItemContainer>());
        });
    }

    [Test]
    public void MotionPanel_DirectControlItemKeepsLogicalOwnershipAndItemsIndex()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Current",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(TestMotionFactory.State("Current", () => TestMotionFactory.Slot("Editor", 100)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(child.GetLogicalParent(), Is.SameAs(panel));
            Assert.That(child.GetVisualParent(), Is.TypeOf<MotionItemContainer>());
            Assert.That(panel.IndexFromContainer(child), Is.Zero);
            Assert.That(panel.ItemFromContainer(child), Is.SameAs(child));
        });
    }

    [Test]
    public void MotionPanel_PreparesItemTemplateInsideTheMotionContainer()
    {
        var itemsControl = new ProbeMotionPanel
        {
            ItemTemplate = new FuncDataTemplate(
                static _ => true,
                static (_, _) => new Border { Name = "Generated" }),
        };
        var item = new object();
        var container = itemsControl.CreateContainer(item);

        itemsControl.PrepareContainer(container, item);

        var motionContainer = (MotionItemContainer)container;
        var child = motionContainer.GetOrCreateMotionChild();
        Assert.Multiple(() =>
        {
            Assert.That(child, Is.TypeOf<Border>());
            Assert.That(motionContainer.Child, Is.SameAs(child));
            Assert.That(motionContainer.MotionChild, Is.SameAs(child));
        });
    }

    [Test]
    public void Axaml_WhenUsingMotionAttachedMembers_BuildsAndAppliesTheLayoutSource()
    {
        var probe = new MotionSyntaxProbe();
        var panel = (MotionPanel)probe.Content!;
        var child = panel.Items.Cast<Control>().Single();

        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetMotionContainer(child).Opacity, Is.Zero);
    }

    [Test]
    public void StateMotion_WhenDurationIsZero_PresentsResponsiveCompleteStates()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Centered",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Centered",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Center)));
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel, new Size(400, 200));
        var centered = TestMotionFactory.GetPresentedBounds(child);

        motion.State = "Leading";
        TestMotionFactory.Layout(panel, new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(centered, Is.EqualTo(new Rect(150, 0, 100, 40)));
            Assert.That(
                TestMotionFactory.GetPresentedBounds(child),
                Is.EqualTo(new Rect(0, 0, 80, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenStateChangesBeforeFirstPresentation_AppliesItImmediately()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Centered",
            Duration = TimeSpan.FromSeconds(1),
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Centered",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Center)));
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        var panel = TestMotionFactory.Panel(motion, child);

        motion.State = "Leading";
        TestMotionFactory.Layout(panel, new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(
                TestMotionFactory.GetPresentedBounds(child),
                Is.EqualTo(new Rect(0, 0, 80, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenStateOmitsTarget_AppliesAbsentPresence()
    {
        var editor = new Border { Name = "Editor" };
        var preview = new Border { Name = "Preview" };
        var motion = new StateMotion
        {
            State = "Full",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Full",
                () =>
                {
                    var root = new Panel();
                    root.Children.Add(TestMotionFactory.Slot("Editor", 100));
                    root.Children.Add(TestMotionFactory.Slot("Preview", 100, HorizontalAlignment.Right));
                    return root;
                }));
        motion.States.Add(
            TestMotionFactory.State(
                "EditorOnly",
                () => TestMotionFactory.Slot("Editor", 100)));
        var panel = TestMotionFactory.Panel(motion, editor, preview);

        TestMotionFactory.Layout(panel);
        motion.State = "EditorOnly";
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(TestMotionFactory.GetMotionContainer(editor).Opacity, Is.EqualTo(1));
            Assert.That(TestMotionFactory.GetMotionContainer(preview).Opacity, Is.Zero);
            Assert.That(TestMotionFactory.GetMotionContainer(preview).IsHitTestVisible, Is.False);
        });
    }

    [Test]
    public void StateMotion_WhenEasingIsNull_UsesLinearTransition()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Centered",
            Duration = TimeSpan.FromSeconds(1),
            Easing = null,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Centered",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Center)));
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel, new Size(400, 200));

        motion.State = "Leading";
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        TestMotionFactory.Layout(panel, new Size(400, 200));

        Assert.That(
            TestMotionFactory.GetPresentedBounds(child),
            Is.EqualTo(new Rect(75, 0, 90, 40)));
    }

    [Test]
    public void StateMotion_WhenTransitionIsInterrupted_FreezesSourcePerTrackAndReadsLiveTarget()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Leading",
            Duration = TimeSpan.FromSeconds(1),
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Left)));
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 200, HorizontalAlignment.Right)));
        motion.States.Add(
            TestMotionFactory.State(
                "Centered",
                () => TestMotionFactory.Slot("Editor", 40, HorizontalAlignment.Center)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel, new Size(400, 200));

        motion.State = "Trailing";
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        TestMotionFactory.Layout(panel, new Size(400, 200));
        Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(100, 0, 150, 40)));

        motion.State = "Centered";
        TestMotionFactory.Layout(panel, new Size(600, 200));
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        TestMotionFactory.Layout(panel, new Size(600, 200));

        Assert.That(
            TestMotionFactory.GetPresentedBounds(child),
            Is.EqualTo(new Rect(190, 0, 95, 40)));
    }

    [Test]
    public void MotionPanel_WhenChildrenHaveDuplicateNames_ThrowsConfigurationError()
    {
        var motion = new StateMotion();
        var panel = TestMotionFactory.Panel(
            motion,
            new Border { Name = "Editor" },
            new Border { Name = "Editor" });

        var exception = Assert.Throws<InvalidOperationException>(
            () => panel.Measure(new Size(400, 200)));

        Assert.That(exception!.Message, Does.Contain("more than one direct child"));
    }

    [Test]
    public void MotionName_WhenSetOnDirectChild_OverridesXNameForLayoutResolution()
    {
        var child = new Border { Name = "LegacyName" };
        Motion.SetName(child, "App42");
        var motion = new StateMotion
        {
            State = "Current",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Current",
                () =>
                {
                    var slot = TestMotionFactory.Slot("LegacySlotName", 120);
                    Motion.SetName(slot, "App42");
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(120));
    }

    [Test]
    public void MotionName_WhenChanged_RebuildsTargetAndSlotMappingsOnNextLayout()
    {
        var identity = "Initial";
        var child = new Border { Name = "LegacyName" };
        Motion.SetName(child, identity);
        var motion = new StateMotion
        {
            State = "Current",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Current",
                () => TestMotionFactory.Slot(identity, 140)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);
        var previousRuntime = TestMotionFactory.GetMotionRuntime(panel);

        identity = "Renamed";
        Motion.SetName(child, identity);
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(140));
            Assert.That(TestMotionFactory.GetMotionRuntime(panel), Is.Not.SameAs(previousRuntime));
        });
    }

    [Test]
    public void MotionName_WhenTwoChildrenResolveToTheSameIdentity_ThrowsConfigurationError()
    {
        var first = new Border { Name = "First" };
        var second = new Border { Name = "Second" };
        Motion.SetName(second, "First");
        var panel = TestMotionFactory.Panel(new StateMotion(), first, second);

        var exception = Assert.Throws<InvalidOperationException>(
            () => panel.Measure(new Size(400, 200)));

        Assert.That(exception!.Message, Does.Contain("Motion identity"));
    }

    [Test]
    public void StateMotion_WhenPanelResizes_ReevaluatesSettledTemplate()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Centered",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Centered",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Center)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel, new Size(400, 200));
        var first = TestMotionFactory.GetPresentedBounds(child);
        TestMotionFactory.Layout(panel, new Size(600, 200));

        Assert.Multiple(() =>
        {
            Assert.That(first.X, Is.EqualTo(150));
            Assert.That(TestMotionFactory.GetPresentedBounds(child).X, Is.EqualTo(250));
        });
    }

    [Test]
    public void StateTemplate_WhenReplaced_RebuildsOnlyTheChangedLayoutSource()
    {
        var child = new Border { Name = "Editor" };
        var state = TestMotionFactory.State(
            "Current",
            () => TestMotionFactory.Slot("Editor", 100));
        var motion = new StateMotion
        {
            State = "Current",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(state);
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        state.Template = TestMotionFactory.State(
            "Replacement",
            () => TestMotionFactory.Slot("Editor", 220)).Template;
        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(220));
    }

    [Test]
    public void Motion_WhenRemoved_RestoresTargetBaseValue()
    {
        var child = new Border
        {
            Name = "Editor",
            Opacity = 0.7,
        };
        var motion = new SequenceMotion();
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0.2;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        panel.Motion = null;
        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.7).Within(0.001));
    }

    [Test]
    public void TargetBaseValue_WhenChangedUnderAnimation_IsPreservedAfterMotionRemoval()
    {
        var child = new Border
        {
            Name = "Editor",
            Opacity = 0.7,
        };
        var motion = new SequenceMotion();
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0.2;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        child.Opacity = 0.9;
        TestMotionFactory.Layout(panel);
        panel.Motion = null;
        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.9).Within(0.001));
    }

    private static void Advance(MotionPlayer player, TimeSpan timestamp) =>
        InvokeAnimationFrame(player, timestamp, GetFrameGeneration(player));

    private sealed class ProbeMotionPanel : MotionPanel
    {
        public Control CreateContainer(object item) =>
            CreateContainerForItemOverride(item, 0, null);

        public void PrepareContainer(Control container, object item) =>
            PrepareContainerForItemOverride(container, item, 0);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnAnimationFrame")]
    private static extern void InvokeAnimationFrame(
        MotionPlayer player,
        TimeSpan timestamp,
        int generation);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_frameGeneration")]
    private static extern ref int GetFrameGeneration(MotionPlayer player);
}
