using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Motion.Avalonia.Tests;

public sealed class MotionPlayerTests
{
    [Test]
    public void Attach_WhenLeaseIsActive_RequiresDisposalBeforeReattachment()
    {
        var player = new MotionPlayer();
        var firstVisual = new Border();
        var secondVisual = new Border();
        var firstAttachment = player.Attach(firstVisual);

        var exception = Assert.Throws<InvalidOperationException>(
            () => player.Attach(secondVisual));

        Assert.That(exception!.Message, Does.Contain("Dispose"));

        firstAttachment.Dispose();
        using var secondAttachment = player.Attach(secondVisual);
    }

    [Test]
    public void PlayerDrivenController_WhenPanelAssociationEnds_ReleasesPlayerAttachment()
    {
        var controller = new TestMotionController();
        var player = controller.Player;
        var panel = new MotionPanel { Controller = controller };

        Assert.Throws<InvalidOperationException>(
            () => player.Attach(new Border()));

        panel.Controller = null;
        using var attachment = player.Attach(new Border());

        Assert.That(controller.Player, Is.SameAs(player));
    }

    [Test]
    public void Playback_WhenFramesAdvance_PublishesClockTicksWithoutCompleting()
    {
        var controller = new TestMotionController();
        var player = controller.Player;

        player.Play();
        Advance(player, TimeSpan.FromSeconds(10));
        Advance(player, TimeSpan.FromMilliseconds(10500));
        Advance(player, TimeSpan.FromSeconds(12));

        Assert.Multiple(() =>
        {
            Assert.That(player.Time, Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Playing));
            Assert.That(controller.Ticks, Has.Count.EqualTo(3));
            Assert.That(
                controller.Ticks[0],
                Is.EqualTo((TimeSpan.Zero, TimeSpan.Zero)));
            Assert.That(
                controller.Ticks[1],
                Is.EqualTo((TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500))));
            Assert.That(
                controller.Ticks[2],
                Is.EqualTo((TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1500))));
        });
    }

    [Test]
    public void Playback_WhenPausedPlayedResetAndStopped_UsesOrthogonalClockControls()
    {
        var controller = new TestMotionController();
        var player = controller.Player;

        player.Play();
        Advance(player, TimeSpan.Zero);
        Advance(player, TimeSpan.FromMilliseconds(250));
        player.Pause();

        Assert.Multiple(() =>
        {
            Assert.That(player.Time, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Paused));
        });

        player.Play();
        Advance(player, TimeSpan.FromSeconds(10));
        Advance(player, TimeSpan.FromMilliseconds(10250));

        Assert.Multiple(() =>
        {
            Assert.That(player.Time, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Playing));
        });

        player.Reset();
        Advance(player, TimeSpan.FromSeconds(20));
        Advance(player, TimeSpan.FromMilliseconds(20100));

        Assert.Multiple(() =>
        {
            Assert.That(player.Time, Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Playing));
        });

        player.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(player.Time, Is.EqualTo(TimeSpan.Zero));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Stopped));
        });
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

    private sealed class TestMotionController : PlayerDrivenMotionController
    {
        public IReadOnlyList<(TimeSpan Time, TimeSpan DeltaTime)> Ticks => _ticks;

        private readonly List<(TimeSpan Time, TimeSpan DeltaTime)> _ticks = [];

        protected override void OnPlayerTick(TimeSpan time, TimeSpan deltaTime) =>
            _ticks.Add((time, deltaTime));
    }
}
