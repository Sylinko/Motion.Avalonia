using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

public sealed class MotionPanelTests
{
    [Test]
    public void Arrange_WhenControllerDurationIsZero_SnapsToRequestedLiveLayout()
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
    public void Arrange_WhenPanelSizeChanges_ReevaluatesSettledStateImmediately()
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
    public void Arrange_WhenControllerEasingIsNull_SnapsToRequestedState()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = null;
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
        panel.States.Add(new MotionState { Key = "first", Template = firstTemplate });

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
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
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
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
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
    public void StateTransition_WhenPanelResizes_ReevaluatesLiveEndpointsAtCurrentProgress()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "center",
            () => CreateSlot("editor", 100, HorizontalAlignment.Center)));
        panel.States.Add(CreateState(
            "left",
            () => CreateSlot("editor", 80, HorizontalAlignment.Left)));
        Arrange(panel, "center", new Size(400, 200));

        Arrange(panel, "left", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(600, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(600, 200));

        Assert.That(GetPresentedBounds(child), Is.EqualTo(new Rect(125, 0, 90, 40)));
    }

    [Test]
    public void StateTransition_WhenTargetEntersAndLeaves_FadesAndRestoresBaseProperties()
    {
        var editor = new Border();
        var preview = new Border { Opacity = 0.8 };
        var panel = CreatePanel(editor, preview);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
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

    [Test]
    public void Arrange_WhenStateSlotDefinesSetter_AppliesTargetStyledProperty()
    {
        var quickBrush = new SolidColorBrush(Colors.Crimson);
        var advancedBrush = new SolidColorBrush(Colors.CornflowerBlue);
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "quick",
            () => CreateSlotWithSetter("editor", quickBrush)));
        panel.States.Add(CreateState(
            "advanced",
            () => CreateSlotWithSetter("editor", advancedBrush)));

        Arrange(panel, "quick", new Size(400, 200));
        Assert.That(child.Background, Is.SameAs(quickBrush));

        Arrange(panel, "advanced", new Size(400, 200));
        Assert.That(child.Background, Is.SameAs(advancedBrush));
    }

    [Test]
    public void Children_WhenTargetWithSetterIsRemoved_RestoresLatestBoundValue()
    {
        var initialBrush = new SolidColorBrush(Colors.DarkGreen);
        var latestBrush = new SolidColorBrush(Colors.Goldenrod);
        var stateBrush = new SolidColorBrush(Colors.MediumPurple);
        var source = new TestObservable<IBrush?>();
        var child = new Border();
        child.Bind(Border.BackgroundProperty, source);
        source.Publish(initialBrush);
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "active",
            () => CreateSlotWithSetter("editor", stateBrush)));

        Arrange(panel, "active", new Size(400, 200));
        source.Publish(latestBrush);
        Assert.That(child.Background, Is.SameAs(stateBrush));

        panel.Children.Remove(child);

        Assert.That(child.Background, Is.SameAs(latestBrush));
    }

    [Test]
    public void Arrange_WhenSetterValueIsBound_TracksLiveEndpointValue()
    {
        var initialBrush = new SolidColorBrush(Colors.IndianRed);
        var latestBrush = new SolidColorBrush(Colors.SteelBlue);
        var values = new BrushValues(initialBrush);
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "active",
            () =>
            {
                var slot = CreateSlot("editor", 100, HorizontalAlignment.Left);
                slot.Setters.Add(new Setter(
                    Border.BackgroundProperty,
                    new Binding(nameof(BrushValues.Brush)) { Source = values }));
                return slot;
            }));

        Arrange(panel, "active", new Size(400, 200));
        Assert.That(child.Background, Is.SameAs(initialBrush));

        values.Brush = latestBrush;

        Assert.That(child.Background, Is.SameAs(latestBrush));
    }

    [Test]
    public void Setters_WhenPropertyIsDuplicated_ThrowsConfigurationError()
    {
        var slot = CreateSlot("editor", 100, HorizontalAlignment.Left);
        slot.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Red));

        var exception = Assert.Throws<InvalidOperationException>(
            () => slot.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Blue)));

        Assert.That(exception!.Message, Does.Contain("more than one Setter"));
    }

    [Test]
    public void StateTransition_WhenSetterPropertyBlends_UsesPoseProgress()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "square",
            () => CreateSlotWithSetter("editor", Border.CornerRadiusProperty, new CornerRadius(0))));
        panel.States.Add(CreateState(
            "rounded",
            () => CreateSlotWithSetter("editor", Border.CornerRadiusProperty, new CornerRadius(20))));
        Arrange(panel, "square", new Size(400, 200));

        Arrange(panel, "rounded", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(child.CornerRadius, Is.EqualTo(new CornerRadius(10)));
    }

    [Test]
    public void StateTransition_WhenShortcutPropertyBlends_MatchesSetterSemantics()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "dim",
            () => CreateSlotWithOpacity("editor", 0.2)));
        panel.States.Add(CreateState(
            "bright",
            () => CreateSlotWithSetter("editor", Visual.OpacityProperty, 0.8)));
        Arrange(panel, "dim", new Size(400, 200));

        Arrange(panel, "bright", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
            Assert.That(GetMotionOpacity(child), Is.EqualTo(1));
        });
    }

    [Test]
    public void StateTransition_WhenBrushPropertyBlends_UsesAvaloniaBrushAnimator()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "red",
            () => CreateSlotWithSetter(
                "editor",
                Border.BackgroundProperty,
                new SolidColorBrush(Colors.Red))));
        panel.States.Add(CreateState(
            "blue",
            () => CreateSlotWithSetter(
                "editor",
                Border.BackgroundProperty,
                new SolidColorBrush(Colors.Blue))));
        Arrange(panel, "red", new Size(400, 200));

        Arrange(panel, "blue", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(child.Background, Is.InstanceOf<ISolidColorBrush>());
            Assert.That(((ISolidColorBrush)child.Background!).Color, Is.Not.EqualTo(Colors.Red));
            Assert.That(((ISolidColorBrush)child.Background!).Color, Is.Not.EqualTo(Colors.Blue));
        });
    }

    [Test]
    public void StateTransition_WhenCustomInterpolatorMatches_UsesTypedInterpolator()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.Interpolators.Add(new QuadraticDoubleInterpolator
        {
            Target = "editor",
            Property = Visual.OpacityProperty,
        });
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "dim",
            () => CreateSlotWithOpacity("editor", 0.2)));
        panel.States.Add(CreateState(
            "bright",
            () => CreateSlotWithOpacity("editor", 1)));
        Arrange(panel, "dim", new Size(400, 200));

        Arrange(panel, "bright", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void StateTransition_WhenEdgeInterpolatorMatches_OverridesGeneralInterpolator()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        var edge = new MotionEdge { From = "dim", To = "bright" };
        GetBasicController(panel).Edges.Add(edge);
        panel.Interpolators.Add(new QuadraticDoubleInterpolator
        {
            Target = "editor",
            Property = Visual.OpacityProperty,
        });
        var edgeInterpolator = new TargetDoubleInterpolator
        {
            Target = "editor",
            Property = Visual.OpacityProperty,
        };
        edgeInterpolator.Edges.Add(edge);
        panel.Interpolators.Add(edgeInterpolator);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "dim",
            () => CreateSlotWithOpacity("editor", 0)));
        panel.States.Add(CreateState(
            "bright",
            () => CreateSlotWithOpacity("editor", 1)));
        Arrange(panel, "dim", new Size(400, 200));

        Arrange(panel, "bright", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(child.Opacity, Is.EqualTo(1));
    }

    [Test]
    public void Arrange_WhenSlotsHaveNestedZIndices_AppliesRelativeOrderToContainers()
    {
        var editor = new Border();
        var preview = new Border();
        var panel = CreatePanel(editor, preview);
        panel.States.Add(CreateState(
            "stacked",
            () =>
            {
                var root = new Panel();
                var editorSlot = CreateSlot("editor", 100, HorizontalAlignment.Left);
                editorSlot.ZIndex = 20;
                root.Children.Add(editorSlot);

                var nested = new Panel { ZIndex = 10 };
                nested.Children.Add(CreateSlot("preview", 100, HorizontalAlignment.Left));
                root.Children.Add(nested);
                return root;
            }));

        Arrange(panel, "stacked", new Size(400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(((Control)editor.GetVisualParent()!).ZIndex, Is.EqualTo(1));
            Assert.That(((Control)preview.GetVisualParent()!).ZIndex, Is.EqualTo(0));
        });
    }

    [Test]
    public void Arrange_WhenShortcutAndSetterDefineSameProperty_ThrowsConfigurationError()
    {
        var child = new Border();
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "invalid",
            () =>
            {
                var slot = CreateSlotWithOpacity("editor", 0.5);
                slot.Setters.Add(new Setter(Visual.OpacityProperty, 0.8));
                return slot;
            }));

        var exception = Assert.Throws<InvalidOperationException>(
            () => Arrange(panel, "invalid", new Size(400, 200)));

        Assert.That(exception!.Message, Does.Contain("both directly and with a Setter"));
    }

    [Test]
    public void TargetStyles_WhenNestedTargetIsSelected_BlendsTheNestedProperty()
    {
        var details = new Border { Name = "details" };
        var child = new Border { Child = details };
        var panel = CreatePanel(child);
        GetBasicController(panel).Duration = TimeSpan.FromSeconds(1);
        GetBasicController(panel).Easing = new LinearEasing();
        panel.States.Add(CreateState(
            "hidden",
            () => CreateStyledSlot(
                new Style(selector => selector
                    .OfType<Border>()
                    .Name("editor")
                    .Descendant()
                    .OfType<Border>()
                    .Name("details")),
                new Setter(Visual.OpacityProperty, 0.2))));
        panel.States.Add(CreateState(
            "visible",
            () => CreateStyledSlot(
                new Style(selector => selector
                    .OfType<Border>()
                    .Name("editor")
                    .Descendant()
                    .OfType<Border>()
                    .Name("details")),
                new Setter(Visual.OpacityProperty, 1d))));
        Arrange(panel, "hidden", new Size(400, 200));

        Arrange(panel, "visible", new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.Zero, new Size(400, 200));
        AdvanceAnimation(panel, TimeSpan.FromMilliseconds(500), new Size(400, 200));

        Assert.That(details.Opacity, Is.EqualTo(0.6).Within(0.001));
    }

    [Test]
    public void TargetStyles_WhenClassMatchChanges_UpdatesTheSettledEndpoint()
    {
        var details = new Border { Opacity = 0.25 };
        details.Classes.Add("expanded");
        var child = new Border { Child = details };
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "active",
            () => CreateStyledSlot(
                new Style(selector => selector.OfType<Border>().Class("expanded")),
                new Setter(Visual.OpacityProperty, 0.8))));
        Arrange(panel, "active", new Size(400, 200));
        Assert.That(details.Opacity, Is.EqualTo(0.8));

        details.Classes.Remove("expanded");
        Assert.That(details.Opacity, Is.EqualTo(0.25));

        details.Classes.Add("expanded");

        Assert.That(details.Opacity, Is.EqualTo(0.8));
    }

    [Test]
    public void TargetStyles_WhenSetterUsesSelfBinding_ObservesTheActualTarget()
    {
        var details = new Border { Name = "details", Tag = 0.35 };
        var child = new Border { Child = details };
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "active",
            () => CreateStyledSlot(
                new Style(selector => selector.OfType<Border>().Name("details")),
                new Setter(
                    Visual.OpacityProperty,
                    new Binding(nameof(Control.Tag))
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                    }))));
        Arrange(panel, "active", new Size(400, 200));
        Assert.That(details.Opacity, Is.EqualTo(0.35));

        details.Tag = 0.7;

        Assert.That(details.Opacity, Is.EqualTo(0.7));
    }

    [Test]
    public void TargetStyles_WhenSetterUsesTemplateBinding_ObservesTheActualTemplatedParent()
    {
        var templatedParent = new Border { Tag = 0.4 };
        var details = new Border { Name = "details" };
        SetTemplatedParent(details, templatedParent);
        var child = new Border { Child = details };
        var panel = CreatePanel(child);
        panel.States.Add(CreateState(
            "active",
            () => CreateStyledSlot(
                new Style(selector => selector.OfType<Border>().Name("details")),
                new Setter(
                    Visual.OpacityProperty,
                    new TemplateBinding(Control.TagProperty)))));
        Arrange(panel, "active", new Size(400, 200));
        Assert.That(details.Opacity, Is.EqualTo(0.4));

        templatedParent.Tag = 0.75;

        Assert.That(details.Opacity, Is.EqualTo(0.75));
    }

    private static MotionPanel CreatePanel(params Control[] children)
    {
        var panel = new MotionPanel
        {
            Controller = new BasicMotionController
            {
                Duration = TimeSpan.Zero,
            },
        };

        for (var i = 0; i < children.Length; i++)
        {
            children[i].Name = i == 0 ? "editor" : "preview";
            panel.Children.Add(children[i]);
        }

        return panel;
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
            Name = target,
            Width = width,
            Height = 40,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Top,
        };

    private static MotionSlot CreateSlotWithSetter(string target, IBrush brush)
    {
        var slot = CreateSlot(target, 100, HorizontalAlignment.Left);
        slot.Setters.Add(new Setter(Border.BackgroundProperty, brush));
        return slot;
    }

    private static MotionSlot CreateSlotWithSetter(
        string target,
        AvaloniaProperty property,
        object? value)
    {
        var slot = CreateSlot(target, 100, HorizontalAlignment.Left);
        slot.Setters.Add(new Setter(property, value));
        return slot;
    }

    private static MotionSlot CreateSlotWithOpacity(string target, double opacity)
    {
        var slot = CreateSlot(target, 100, HorizontalAlignment.Left);
        slot.Opacity = opacity;
        return slot;
    }

    private static MotionSlot CreateStyledSlot(Style style, Setter setter)
    {
        style.Setters.Add(setter);
        var slot = CreateSlot("editor", 100, HorizontalAlignment.Left);
        slot.TargetStyles.Add(style);
        return slot;
    }

    private static void Arrange(MotionPanel panel, object state, Size size)
    {
        GetBasicController(panel).State = state;
        RunLayoutToConvergence(panel, size);
    }

    private static void AdvanceAnimation(MotionPanel panel, TimeSpan timestamp, Size size)
    {
        var player = GetBasicController(panel).Player;
        InvokeAnimationFrame(player, timestamp, GetFrameGeneration(player));
        RunLayoutToConvergence(panel, size);
    }

    private static BasicMotionController GetBasicController(MotionPanel panel) =>
        (BasicMotionController)panel.Controller!;

    private static void RunLayoutToConvergence(MotionPanel panel, Size size)
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
        MotionPlayer player,
        TimeSpan timestamp,
        int generation);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_frameGeneration")]
    private static extern ref int GetFrameGeneration(MotionPlayer player);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_TemplatedParent")]
    private static extern void SetTemplatedParent(
        StyledElement element,
        AvaloniaObject? templatedParent);

    private sealed class TestTemplate(Func<Control> build) : IDataTemplate
    {
        public bool Match(object? data) => true;

        public Control Build(object? param) => build();
    }

    private sealed class CountingTemplate(Func<Control> build) : IDataTemplate
    {
        public int BuildCount { get; private set; }

        public bool Match(object? data) => true;

        public Control Build(object? param)
        {
            BuildCount++;
            return build();
        }
    }

    private sealed class TestObservable<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Subscription(_observers, observer);
        }

        public void Publish(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        private sealed class Subscription(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
        {
            public void Dispose() => observers.Remove(observer);
        }
    }

    private sealed class BrushValues(IBrush brush) : INotifyPropertyChanged
    {
        public IBrush Brush
        {
            get => field;
            set
            {
                if (ReferenceEquals(field, value))
                {
                    return;
                }

                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Brush)));
            }
        } = brush;

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class QuadraticDoubleInterpolator : MotionInterpolator<double>
    {
        public override double Interpolate(double progress, double oldValue, double newValue) =>
            oldValue + ((newValue - oldValue) * progress * progress);
    }

    private sealed class TargetDoubleInterpolator : MotionInterpolator<double>
    {
        public override double Interpolate(double progress, double oldValue, double newValue) =>
            newValue;
    }
}
