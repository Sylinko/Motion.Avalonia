using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Motion.Avalonia.Tests;

public sealed class LayoutInterpolationTests
{
    [Test]
    public void PositionTrack_WhenCustomInterpolatorIsConfigured_UsesValueSpaceCurve()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Left)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        motion.Interpolations.Add(
            new MotionInterpolation
            {
                Target = "Editor",
                Property = Motion.PositionProperty,
                Interpolator = new CubicBezierMotionInterpolator
                {
                    SourceControlOffset = new Vector(0, 100),
                    TargetControlOffset = new Vector(0, 100),
                },
            });
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel, new Size(400, 200));

        Assert.That(
            TestMotionFactory.GetPresentedBounds(child).Position,
            Is.EqualTo(new Point(150, 75)));
    }

    [Test]
    public void MotionName_WhenPropertyInterpolationUsesTheAttachedIdentity_MatchesTheRule()
    {
        var child = new Border { Name = "LegacyName" };
        Motion.SetName(child, "App42");
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => CreateOpacitySetterSlot("App42", 0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () => CreateOpacitySetterSlot("App42", 1)));
        motion.Interpolations.Add(
            new MotionInterpolation
            {
                Target = "App42",
                Property = Visual.OpacityProperty,
                Easing = new SquareEasing(),
            });
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.001));
    }

    [Test]
    public void MotionInterpolation_WhenChangedAfterCompile_DoesNotChangeCompiledRule()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => CreateOpacitySetterSlot(0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () => CreateOpacitySetterSlot(1)));
        var interpolation = new MotionInterpolation
        {
            Target = "Editor",
            Property = Visual.OpacityProperty,
            Easing = new SquareEasing(),
        };
        motion.Interpolations.Add(interpolation);
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        interpolation.Easing = new LinearEasing();
        motion.Progress = 0.6;

        Assert.That(child.Opacity, Is.EqualTo(0.36).Within(0.001));

        panel.Motion = null;
        panel.Motion = motion;
        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.6).Within(0.001));
    }

    [Test]
    public void SetterMetadata_WhenDestinationDefinesEasing_AppliesItToOnlyThatProperty()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => CreateOpacitySetterSlot(0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    var setter = new Setter(Visual.OpacityProperty, 1d);
                    Motion.SetEasing(setter, new SquareEasing());
                    slot.Setters.Add(setter);
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.001));
    }

    [Test]
    public void SetterMetadata_WhenNotConfigured_IsAbsent()
    {
        var setter = new Setter(Visual.OpacityProperty, 1d);

        var metadata = Motion.GetSetterMetadata(setter);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.Easing.HasValue, Is.False);
            Assert.That(metadata.Interpolator.HasValue, Is.False);
        });
    }

    [Test]
    public void SetterMetadata_WhenExplicitlyCleared_IsPresentWithoutValue()
    {
        var setter = new Setter(Visual.OpacityProperty, 1d);
        Motion.SetEasing(setter, null);
        Motion.SetInterpolator(setter, null);

        var metadata = Motion.GetSetterMetadata(setter);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.Easing.HasValue, Is.True);
            Assert.That(metadata.Easing.Value, Is.Null);
            Assert.That(metadata.Interpolator.HasValue, Is.True);
            Assert.That(metadata.Interpolator.Value, Is.Null);
        });
    }

    [Test]
    public void SetterMetadata_WhenEasingIsExplicitlyCleared_OverridesMotionFallback()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion
        {
            Progress = 0.5,
            Easing = new SquareEasing(),
        };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => CreateOpacitySetterSlot(0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    var setter = new Setter(Visual.OpacityProperty, 1d);
                    Motion.SetEasing(setter, null);
                    slot.Setters.Add(setter);
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void SetterMetadata_WhenInterpolatorIsExplicitlyCleared_OverridesMotionFallback()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.Interpolations.Add(
            new MotionInterpolation
            {
                Target = "Editor",
                Property = Visual.OpacityProperty,
                Interpolator = new SquareInterpolator(),
            });
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => CreateOpacitySetterSlot(0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    var setter = new Setter(Visual.OpacityProperty, 1d);
                    Motion.SetInterpolator(setter, null);
                    slot.Setters.Add(setter);
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void PresenceTrack_WhenExplicitlyAuthored_ControlsVisibilityEnvelope()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => TestMotionFactory.Slot("Editor", 100)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    Motion.SetPresence(slot, 0);
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(
            TestMotionFactory.GetMotionContainer(child).Opacity,
            Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void MotionSlot_WhenSetterUsesLayoutChannel_RejectsTheDefinition()
    {
        var slot = TestMotionFactory.Slot("Editor", 100);

        var exception = Assert.Throws<InvalidOperationException>(
            () => slot.Setters.Add(
                new Setter(
                    Motion.PositionProperty,
                    new Point(10, 20))));

        Assert.That(exception!.Message, Does.Contain("evaluated layout channel"));
    }

    private static MotionSlot CreateOpacitySetterSlot(double opacity)
        => CreateOpacitySetterSlot("Editor", opacity);

    private static MotionSlot CreateOpacitySetterSlot(string name, double opacity)
    {
        var slot = TestMotionFactory.Slot(name, 100);
        slot.Setters.Add(new Setter(Visual.OpacityProperty, opacity));
        return slot;
    }

    private sealed class SquareEasing : Easing
    {
        public override double Ease(double progress) => progress * progress;
    }

    private sealed class SquareInterpolator : MotionInterpolator<double>
    {
        protected override double Interpolate(double progress, double oldValue, double newValue) =>
            oldValue + ((newValue - oldValue) * progress * progress);
    }
}
