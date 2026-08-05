using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Motion.Avalonia.Tests;

public sealed class SequenceMotionTests
{
    [Test]
    public void ProgressStream_WhenSubscribed_PublishesCurrentAndLaterProgress()
    {
        var motion = new SequenceMotion { Progress = 0.25 };
        var values = new List<double>();
        using var subscription = TestMotionFactory.Observe((IKeyFrameMotion)motion, values);

        motion.Progress = 0.75;

        Assert.That(values, Is.EqualTo(new[] { 0.25, 0.75 }));
    }

    [Test]
    public void Progress_WhenIntermediateKeyFrameOmitsTarget_UsesTargetsOwnAdjacentKeys()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(0.5, static () => new Panel()));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 300);
                    slot.Opacity = 1;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(200));
            Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
            Assert.That(TestMotionFactory.GetMotionContainer(child), Is.TypeOf<MotionItemContainer>());
        });
    }

    [Test]
    public void Progress_WhenPropertyHasOneKey_HoldsThatValueAcrossTheSequence()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.9 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () => TestMotionFactory.Slot("Editor", 100)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0.4,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 180);
                    slot.Opacity = 0.35;
                    return slot;
                }));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () => TestMotionFactory.Slot("Editor", 240)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.35).Within(0.001));
    }

    [Test]
    public void Progress_WhenChangedAfterPresentation_PublishesTracksBeforeTheNextLayoutPass()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion();
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 300);
                    slot.Opacity = 1;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.Progress = 0.5;

        Assert.Multiple(() =>
        {
            Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
            Assert.That(panel.IsMeasureValid, Is.False);
        });

        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(200));
    }

    [Test]
    public void Progress_WhenOnlyAPropertyTrackChanges_DoesNotInvalidatePreparedLayout()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                1,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 1;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.Progress = 0.6;

        Assert.Multiple(() =>
        {
            Assert.That(child.Opacity, Is.EqualTo(0.6).Within(0.001));
            Assert.That(panel.IsMeasureValid, Is.True);
            Assert.That(panel.IsArrangeValid, Is.True);
        });
    }

    [Test]
    public void Progress_WhenDuplicateCueDefinesSameChannel_LaterDeclarationWins()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0.5,
                () => TestMotionFactory.Slot("Editor", 100)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0.5,
                () => TestMotionFactory.Slot("Editor", 220)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(220));
    }

    [Test]
    public void SequenceMotion_WhenCreated_DoesNotOwnPlayer()
    {
        var motion = new SequenceMotion();

        Assert.That(motion, Is.Not.InstanceOf<PlayerDrivenMotion>());
    }

    [Test]
    public void TargetStyles_WhenNestedPropertyIsSparse_CompilesItsOwnTrack()
    {
        var details = new Border { Name = "Details" };
        var child = new Border
        {
            Name = "Editor",
            Child = details,
        };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(0, () => CreateStyledSlot(0)));
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(1, () => CreateStyledSlot(1)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(details.Opacity, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void SlotEndpointTopology_WhenChangedAfterApply_RecompilesAffectedTracks()
    {
        var child = new Border { Name = "Editor" };
        var sourceSlot = TestMotionFactory.Slot("Editor", 100);
        var targetSlot = TestMotionFactory.Slot("Editor", 100);
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(TestMotionFactory.Cue(0, () => sourceSlot));
        motion.KeyFrames.Add(TestMotionFactory.Cue(1, () => targetSlot));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        sourceSlot.Opacity = 0;
        targetSlot.Opacity = 0.8;
        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void KeyFrame_WhenEasingIsExplicitlyNull_ResetsMotionDefaultToLinear()
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
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        var target = TestMotionFactory.Cue(
            1,
            () =>
            {
                var slot = TestMotionFactory.Slot("Editor", 100);
                slot.Opacity = 1;
                return slot;
            });
        target.Easing = null;
        motion.KeyFrames.Add(target);
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
    }

    [Test]
    public void KeyFrameEasing_WhenChangedBeforeRecompile_DoesNotMutateActiveTrackPolicy()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion { Progress = 0.5 };
        motion.KeyFrames.Add(
            TestMotionFactory.Cue(
                0,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        var target = TestMotionFactory.Cue(
            1,
            () =>
            {
                var slot = TestMotionFactory.Slot("Editor", 100);
                slot.Opacity = 1;
                return slot;
            });
        target.Easing = new SquareEasing();
        motion.KeyFrames.Add(target);
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        target.Easing = new LinearEasing();
        motion.Progress = 0.6;

        Assert.That(child.Opacity, Is.EqualTo(0.36).Within(0.001));

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.6).Within(0.001));
    }

    [Test]
    public void Compile_WhenUsingSequenceOrTimeline_UsesSharedKeyFrameTrackType()
    {
        var sequencePanel = CreateOpacitySequencePanel();
        var timelinePanel = CreateOpacityTimelinePanel();
        TestMotionFactory.Layout(sequencePanel);
        TestMotionFactory.Layout(timelinePanel);

        var sequenceTrack = TestMotionFactory.GetMotionRuntime(sequencePanel).Tracks
            .Single(track => track.Property == Visual.OpacityProperty);
        var timelineTrack = TestMotionFactory.GetMotionRuntime(timelinePanel).Tracks
            .Single(track => track.Property == Visual.OpacityProperty);

        Assert.Multiple(() =>
        {
            Assert.That(sequenceTrack, Is.InstanceOf<KeyFrameMotionTrack<double>>());
            Assert.That(timelineTrack, Is.InstanceOf<KeyFrameMotionTrack<double>>());
            Assert.That(((KeyFrameMotionTrack<double>)sequenceTrack).Keys, Has.Count.EqualTo(2));
            Assert.That(((KeyFrameMotionTrack<double>)timelineTrack).Keys, Has.Count.EqualTo(2));
        });
    }

    private static MotionPanel CreateOpacitySequencePanel()
    {
        var child = new Border { Name = "Editor" };
        var motion = new SequenceMotion();
        motion.KeyFrames.Add(TestMotionFactory.Cue(0, () => CreateOpacitySlot(0)));
        motion.KeyFrames.Add(TestMotionFactory.Cue(1, () => CreateOpacitySlot(1)));
        return TestMotionFactory.Panel(motion, child);
    }

    private static MotionPanel CreateOpacityTimelinePanel()
    {
        var child = new Border { Name = "Editor" };
        var motion = new TimelineMotion();
        motion.KeyFrames.Add(TestMotionFactory.Time(TimeSpan.Zero, () => CreateOpacitySlot(0)));
        motion.KeyFrames.Add(TestMotionFactory.Time(TimeSpan.FromSeconds(1), () => CreateOpacitySlot(1)));
        return TestMotionFactory.Panel(motion, child);
    }

    private static MotionSlot CreateOpacitySlot(double opacity)
    {
        var slot = TestMotionFactory.Slot("Editor", 100);
        slot.Opacity = opacity;
        return slot;
    }

    private static MotionSlot CreateStyledSlot(double opacity)
    {
        var slot = TestMotionFactory.Slot("Editor", 100);
        var style = new Style(
            selector => selector
                .OfType<Border>()
                .Name("Editor")
                .Descendant()
                .OfType<Border>()
                .Name("Details"));
        style.Setters.Add(new Setter(Visual.OpacityProperty, opacity));
        slot.TargetStyles.Add(style);
        return slot;
    }

    private sealed class SquareEasing : Easing
    {
        public override double Ease(double progress) => progress * progress;
    }
}
