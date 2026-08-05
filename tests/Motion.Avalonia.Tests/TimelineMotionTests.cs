using Avalonia.Controls;

namespace Motion.Avalonia.Tests;

public sealed class TimelineMotionTests
{
    [Test]
    public void ProgressStream_WhenSubscribed_NormalizesTimeAndKeyFrameCues()
    {
        var motion = new TimelineMotion { Time = TimeSpan.FromSeconds(1) };
        var middle = TestMotionFactory.Time(TimeSpan.FromSeconds(1), static () => new Panel());
        motion.KeyFrames.Add(middle);
        motion.KeyFrames.Add(TestMotionFactory.Time(TimeSpan.FromSeconds(2), static () => new Panel()));
        var values = new List<double>();
        using var subscription = TestMotionFactory.Observe((IKeyFrameMotion)motion, values);

        motion.Time = TimeSpan.FromSeconds(1.5);

        Assert.Multiple(() =>
        {
            Assert.That(values, Is.EqualTo(new[] { 0.5, 0.75 }));
            Assert.That(((IKeyFrameMotion)motion).GetCue(middle).CueValue, Is.EqualTo(0.5));
        });
    }

    [Test]
    public void Time_BetweenSparseKeyFrames_InterpolatesPropertyTrack()
    {
        var child = new Border { Name = "Editor" };
        var motion = new TimelineMotion { Time = TimeSpan.FromSeconds(1) };
        motion.KeyFrames.Add(
            TestMotionFactory.Time(
                TimeSpan.Zero,
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0;
                    return slot;
                }));
        motion.KeyFrames.Add(
            TestMotionFactory.Time(
                TimeSpan.FromSeconds(2),
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 200);
                    slot.Opacity = 1;
                    return slot;
                }));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.001));
            Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(150));
        });
    }
}
