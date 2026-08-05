using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Motion.Avalonia.Tests;

public sealed class MotionTrackInterpolationTests
{
    [Test]
    public void CreateTrack_UsesTheMotionInterpolationPolicy()
    {
        var motion = new LinkedOpacityMotion();
        motion.Interpolations.Add(
            new MotionInterpolation
            {
                Target = "Editor",
                Property = Visual.OpacityProperty,
                Interpolator = new SquareInterpolator(),
            });

        var child = new Border
        {
            Name = "Editor",
            Opacity = 0,
        };
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.25).Within(0.0001));
    }

    [Test]
    public void CreateTrack_UsesAvaloniaDefaultInterpolatorWhenNoOverrideExists()
    {
        var child = new Border
        {
            Name = "Editor",
            Opacity = 0,
        };
        var panel = TestMotionFactory.Panel(new LinkedOpacityMotion(), child);

        TestMotionFactory.Layout(panel);

        Assert.That(child.Opacity, Is.EqualTo(0.5).Within(0.0001));
    }

    [Test]
    public void CreateTrack_DispatchesEachChannelToItsDeclaredValueType()
    {
        var child = new Border { Name = "Editor" };
        var panel = TestMotionFactory.Panel(new HeterogeneousMotion(), child);

        TestMotionFactory.Layout(panel);

        var runtime = TestMotionFactory.GetMotionRuntime(panel);
        var valueTypes = runtime.Tracks
            .Select(track => track.GetType().GetGenericArguments().Single())
            .ToArray();

        Assert.That(valueTypes, Is.EquivalentTo([typeof(double), typeof(bool)]));
    }

    private sealed class LinkedOpacityMotion : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new LinkedOpacityRuntime(context);
    }

    private sealed class LinkedOpacityRuntime : MotionRuntime
    {
        public LinkedOpacityRuntime(MotionCompilationContext context) : base(context) { }

        protected override void BuildTracks()
        {
            var target = CompilationContext.ResolveTarget("Editor");
            AddTrack(new LinkedOpacityTrack(
                CompilationContext,
                MotionChannel.ForProperty(target, Visual.OpacityProperty)));
        }
    }

    private sealed class LinkedOpacityTrack : MotionTrack<double>
    {
        public LinkedOpacityTrack(MotionCompilationContext context, MotionChannel channel) : base(context, channel) { }

        public override double Evaluate(double baseValue) => InterpolateValue(baseValue, 1, 0.5);
    }

    private sealed class HeterogeneousMotion : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new HeterogeneousRuntime(context);
    }

    private sealed class HeterogeneousRuntime(MotionCompilationContext context) : MotionRuntime(context)
    {
        protected override void BuildTracks()
        {
            var target = CompilationContext.ResolveTarget("Editor");
            CreateTrack(MotionChannel.ForProperty(target, Visual.OpacityProperty));
            CreateTrack(MotionChannel.ForProperty(target, InputElement.IsHitTestVisibleProperty));
        }

        protected override MotionTrack<T> CreateTrackCore<T>(MotionChannel channel) =>
            new ProbeTrack<T>(CompilationContext, channel);
    }

    private sealed class ProbeTrack<T>(MotionCompilationContext context, MotionChannel channel) : MotionTrack<T>(context, channel)
    {
        public override T Evaluate(T baseValue) => baseValue;
    }

    private sealed class SquareInterpolator : MotionInterpolator<double>
    {
        protected override double Interpolate(double progress, double oldValue, double newValue)
        {
            var square = progress * progress;
            return oldValue + ((newValue - oldValue) * square);
        }
    }
}
