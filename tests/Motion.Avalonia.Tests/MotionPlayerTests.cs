using System.Runtime.CompilerServices;

namespace Motion.Avalonia.Tests;

public sealed class MotionPlayerTests
{
    [Test]
    public void PlayerProgressChanged_WhenFirstAndLastHandlersChange_ManagesPlayerLifetime()
    {
        var controller = new TestMotionController();
        EventHandler handler = static (_, _) => { };

        Assert.That(controller.Player, Is.Null);

        controller.Subscribe(handler);
        var player = controller.Player;
        controller.Subscribe(handler);

        controller.Unsubscribe(handler);
        Assert.That(controller.Player, Is.SameAs(player));

        controller.Unsubscribe(handler);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Player, Is.Null);
            Assert.Throws<ObjectDisposedException>(() => player!.Play(TimeSpan.FromSeconds(1)));
        });
    }

    [Test]
    public void Playback_WhenAdvancedToTheEnd_PublishesProgressAndStops()
    {
        var controller = new TestMotionController();
        var notifications = 0;
        controller.Subscribe((_, _) => notifications++);
        var player = controller.Player!;

        player.Play(TimeSpan.FromSeconds(1));
        Advance(player, TimeSpan.Zero);
        Advance(player, TimeSpan.FromMilliseconds(500));

        Assert.Multiple(() =>
        {
            Assert.That(player.Duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(player.Position, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
            Assert.That(player.Progress, Is.EqualTo(0.5));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Playing));
        });

        Advance(player, TimeSpan.FromSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(player.Position, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(player.Progress, Is.EqualTo(1));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(notifications, Is.EqualTo(3));
        });
    }

    [Test]
    public void Playback_WhenPausedSeekedAndResumed_PreservesExplicitPosition()
    {
        var controller = new TestMotionController();
        controller.Subscribe(static (_, _) => { });
        var player = controller.Player!;

        player.Play(TimeSpan.FromSeconds(1));
        Advance(player, TimeSpan.Zero);
        Advance(player, TimeSpan.FromMilliseconds(250));
        player.Pause();
        player.Seek(0.75);

        Assert.Multiple(() =>
        {
            Assert.That(player.Progress, Is.EqualTo(0.75));
            Assert.That(player.Position, Is.EqualTo(TimeSpan.FromMilliseconds(750)));
            Assert.That(player.State, Is.EqualTo(MotionPlayerState.Paused));
        });

        player.Resume();
        Advance(player, TimeSpan.FromSeconds(10));
        Advance(player, TimeSpan.FromMilliseconds(10250));

        Assert.Multiple(() =>
        {
            Assert.That(player.Progress, Is.EqualTo(1));
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

    private sealed class TestMotionController : MotionController
    {
        public void Subscribe(EventHandler handler) => PlayerProgressChanged += handler;

        public void Unsubscribe(EventHandler handler) => PlayerProgressChanged -= handler;
    }
}
