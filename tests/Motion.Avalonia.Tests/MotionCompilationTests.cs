using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

public sealed class MotionCompilationTests
{
    [Test]
    public void InvalidateMotion_WhenCalledRepeatedly_CoalescesIntoOneCompilation()
    {
        var motion = new CountingMotion();
        var panel = TestMotionFactory.Panel(motion);
        TestMotionFactory.Layout(panel);

        motion.InvalidateMotion();
        motion.InvalidateMotion();
        motion.InvalidateMotion();
        TestMotionFactory.Layout(panel);

        Assert.That(motion.CompileCount, Is.EqualTo(2));
    }

    [Test]
    public void RuntimeTracks_UseTheNonGenericMotionTrackContract()
    {
        var child = new Border { Name = "Editor" };
        var motion = new ConstantOpacityMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        var track = TestMotionFactory.GetMotionRuntime(panel).Tracks.Single();

        Assert.Multiple(() =>
        {
            Assert.That(track, Is.TypeOf<ConstantOpacityTrack>());
            Assert.That(track.Target, Is.SameAs(child));
            Assert.That(track.Property, Is.EqualTo(Visual.OpacityProperty));
        });
    }

    [Test]
    public void CreateRuntimeHook_UsesCustomRuntimeAndCommittedHook()
    {
        var child = new Border { Name = "Editor" };
        var motion = new CustomRuntimeMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        var runtime = TestMotionFactory.GetMotionRuntime(panel);

        Assert.Multiple(() =>
        {
            Assert.That(runtime, Is.TypeOf<CustomRuntime>());
            Assert.That(((CustomRuntime)runtime).WasCommitted, Is.True);
            Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
        });
    }

    [Test]
    public void Runtime_DeclaresSourcesBeforeBuildTracks_AndBuildTracksSeesRealizedHosts()
    {
        var child = new Border { Name = "Editor" };
        var source = TestMotionFactory.State("Declared", () => TestMotionFactory.Slot("Editor", 100));
        var motion = new DeclaredSourceMotion(source);
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);

        var runtime = (DeclaredSourceRuntime)TestMotionFactory.GetMotionRuntime(panel);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.HostCountAtConstruction, Is.Zero);
            Assert.That(runtime.HostCountAtBuildTracks, Is.EqualTo(1));
        });
    }

    [Test]
    public void Runtime_RequiredSourceProviderCannotReturnUndeclaredSource()
    {
        var child = new Border { Name = "Editor" };
        var declared = TestMotionFactory.State("Declared", () => TestMotionFactory.Slot("Editor", 100));
        var undeclared = TestMotionFactory.State("Undeclared", () => TestMotionFactory.Slot("Editor", 100));
        var motion = new UndeclaredRequiredSourceMotion(declared, undeclared);
        var panel = TestMotionFactory.Panel(motion, child);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestMotionFactory.Layout(panel));

        Assert.That(exception!.Message, Does.Contain("undeclared IMotionLayoutSource"));
    }

    [Test]
    public void Runtime_CannotCreateTrackBeforeBuildTracks()
    {
        var child = new Border { Name = "Editor" };
        var panel = TestMotionFactory.Panel(new ConstructorTrackMotion(), child);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestMotionFactory.Layout(panel));

        Assert.That(exception!.Message, Does.Contain("only be created during BuildTracks"));
    }

    [Test]
    public void Runtime_CannotDeclareSourceAfterBuildTracksStarts()
    {
        var child = new Border { Name = "Editor" };
        var source = TestMotionFactory.State("Late", () => TestMotionFactory.Slot("Editor", 100));
        var panel = TestMotionFactory.Panel(new LateDeclaredSourceMotion(source), child);

        var exception = Assert.Throws<InvalidOperationException>(
            () => TestMotionFactory.Layout(panel));

        Assert.That(exception!.Message, Does.Contain("after BuildTracks has started"));
    }

    [Test]
    public void Compile_WhenMotionInvalidatesDuringCompilation_RetriesWithoutLosingInvalidation()
    {
        var motion = new CountingMotion();
        var panel = TestMotionFactory.Panel(motion);
        TestMotionFactory.Layout(panel);

        motion.InvalidateDuringNextCompile = true;
        motion.InvalidateMotion();
        TestMotionFactory.Layout(panel);

        Assert.That(motion.CompileCount, Is.EqualTo(3));
    }

    [Test]
    public void InvalidateMotion_BeforeReplacement_KeepsActiveStateGraphRunning()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Leading",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);
        motion.State = "Trailing";
        TestMotionFactory.Layout(panel);
        var active = TestMotionFactory.GetMotionRuntime(panel);

        motion.States.Add(
            TestMotionFactory.State(
                "Extra",
                () => TestMotionFactory.Slot("Editor", 120, HorizontalAlignment.Center)));
        motion.State = "Leading";

        var container = TestMotionFactory.GetMotionContainer(child);
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(TestMotionFactory.GetMotionRuntime(panel), active), Is.True);
            Assert.That(container.GetValue(Motion.PositionProperty), Is.EqualTo(new Point(0, 0)));
        });
    }

    [Test]
    public void StateMotion_WhenStatesDefineDifferentChannels_UsesCompleteChannelPlan()
    {
        var child = new Border
        {
            Name = "Editor",
            Opacity = 0.2,
        };
        var motion = new StateMotion
        {
            State = "Rich",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Rich",
                () =>
                {
                    var slot = TestMotionFactory.Slot("Editor", 100);
                    slot.Opacity = 0.8;
                    return slot;
                }));
        motion.States.Add(TestMotionFactory.State("Plain", () => TestMotionFactory.Slot("Editor", 100)));
        var panel = TestMotionFactory.Panel(motion, child);

        TestMotionFactory.Layout(panel);
        var runtime = TestMotionFactory.GetMotionRuntime(panel);
        motion.State = "Plain";
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.Tracks.Count(track => track.Property == Visual.OpacityProperty), Is.EqualTo(1));
            Assert.That(ReferenceEquals(TestMotionFactory.GetMotionRuntime(panel), runtime), Is.True);
            Assert.That(child.Opacity, Is.EqualTo(0.2).Within(0.001));
        });
    }

    [Test]
    public void Compile_WhenInactiveCandidateFails_KeepsActiveTrackBinding()
    {
        var child = new Border { Name = "Editor" };
        var motion = new ConstantOpacityMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);
        motion.Value = 0.8;
        motion.ThrowDuringCompile = true;
        motion.InvalidateMotion();

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(400, 200)));
        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void Activate_WhenCandidateFails_KeepsPreviousMotionRuntimeActive()
    {
        var child = new Border { Name = "Editor" };
        var motion = new ConstantOpacityMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.Value = 0.8;
        motion.ThrowDuringActivation = true;
        motion.InvalidateMotion();

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(400, 200)));
        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void Prepare_WhenFirstValueFails_KeepsPreviousMotionRuntimeActive()
    {
        var child = new Border { Name = "Editor" };
        var motion = new FailingOpacityMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.Value = 0.8;
        motion.ThrowDuringPrepare = true;
        motion.InvalidateMotion();

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(400, 200)));
        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void Commit_WhenPublicationFails_KeepsPreviousMotionRuntimeActive()
    {
        var child = new Border { Name = "Editor" };
        var motion = new FailingOpacityMotion { Value = 0.4 };
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.Value = 0.8;
        motion.ThrowDuringCommit = true;
        motion.InvalidateMotion();

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(400, 200)));
        Assert.That(child.Opacity, Is.EqualTo(0.4).Within(0.001));
    }

    [Test]
    public void Compile_WhenReplacementTemplateFails_KeepsActiveLayoutScope()
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
            "Invalid",
            () => TestMotionFactory.Slot("Missing", 200)).Template;

        Assert.Throws<InvalidOperationException>(() => panel.Measure(new Size(400, 200)));
        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(100));
    }

    [Test]
    public void TargetTopology_WhenChildIsRemovedAndReadded_BindsNewChildOnlyAfterReplacement()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Current",
            Duration = TimeSpan.Zero,
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Current",
                () => TestMotionFactory.Slot("Editor", 100)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);
        var active = TestMotionFactory.GetMotionRuntime(panel);

        panel.Items.Remove(child);
        panel.Items.Add(child);

        Assert.That(ReferenceEquals(TestMotionFactory.GetMotionRuntime(panel), active), Is.True);

        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child).Width, Is.EqualTo(100));
        Assert.That(ReferenceEquals(TestMotionFactory.GetMotionRuntime(panel), active), Is.False);
    }

    [Test]
    public void StateMotion_WhenTimingChangesDuringRun_UsesCapturedTransitionPolicy()
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
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.State = "Trailing";
        motion.Duration = TimeSpan.Zero;
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Playing));
            Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(150, 0, 90, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenActiveSourceIsRemoved_ReconcilesToRequestedState()
    {
        var child = new Border { Name = "Editor" };
        var leading = TestMotionFactory.State(
            "Leading",
            () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left));
        var motion = new StateMotion
        {
            State = "Leading",
            Duration = TimeSpan.FromSeconds(1),
        };
        motion.States.Add(leading);
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.State = "Trailing";
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        motion.States.Remove(leading);
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(300, 0, 100, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenUnknownRequestArrives_KeepsActiveTransitionRunning()
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
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.State = "Trailing";
        Advance(motion.Player, TimeSpan.Zero);
        Advance(motion.Player, TimeSpan.FromMilliseconds(500));
        motion.State = "Missing";
        Advance(motion.Player, TimeSpan.FromMilliseconds(750));
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Playing));
            Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(225, 0, 95, 40)));
        });

        Advance(motion.Player, TimeSpan.FromSeconds(1));
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(300, 0, 100, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenFirstRequestIsUnknown_AppliesFirstValidStateImmediately()
    {
        var child = new Border { Name = "Editor" };
        var motion = new StateMotion
        {
            State = "Missing",
            Duration = TimeSpan.FromSeconds(1),
        };
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.State = "Leading";
        TestMotionFactory.Layout(panel);

        Assert.Multiple(() =>
        {
            Assert.That(motion.Player.State, Is.EqualTo(MotionPlayerState.Stopped));
            Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(0, 0, 80, 40)));
        });
    }

    [Test]
    public void StateMotion_WhenDerivedPolicyRedirectsState_UsesProtectedTransitionExtension()
    {
        var child = new Border { Name = "Editor" };
        var motion = new RedirectingStateMotion { State = "Leading" };
        motion.States.Add(
            TestMotionFactory.State(
                "Leading",
                () => TestMotionFactory.Slot("Editor", 80, HorizontalAlignment.Left)));
        motion.States.Add(
            TestMotionFactory.State(
                "Trailing",
                () => TestMotionFactory.Slot("Editor", 100, HorizontalAlignment.Right)));
        var panel = TestMotionFactory.Panel(motion, child);
        TestMotionFactory.Layout(panel);

        motion.State = "Redirect";
        TestMotionFactory.Layout(panel);

        Assert.That(TestMotionFactory.GetPresentedBounds(child), Is.EqualTo(new Rect(300, 0, 100, 40)));
    }

    private static void Advance(MotionPlayer player, TimeSpan timestamp) =>
        InvokeAnimationFrame(player, timestamp, GetFrameGeneration(player));

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "OnAnimationFrame")]
    private static extern void InvokeAnimationFrame(MotionPlayer player, TimeSpan timestamp, int generation);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_frameGeneration")]
    private static extern ref int GetFrameGeneration(MotionPlayer player);

    private sealed class CountingMotion : Motion
    {
        public int CompileCount { get; private set; }

        public bool InvalidateDuringNextCompile { get; set; }

        protected override MotionRuntime CreateRuntime(MotionCompilationContext context)
        {
            CompileCount++;
            if (InvalidateDuringNextCompile)
            {
                InvalidateDuringNextCompile = false;
                InvalidateMotion();
            }

            return new MotionRuntime(context);
        }
    }

    private sealed class ConstantOpacityMotion : Motion
    {
        public double Value { get; set; }

        public bool ThrowDuringCompile { get; set; }

        public bool ThrowDuringActivation { get; set; }

        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new ConstantOpacityRuntime(context, Value, ThrowDuringCompile, ThrowDuringActivation);
    }

    private sealed class CustomRuntimeMotion : Motion
    {
        public double Value { get; set; }

        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new CustomRuntime(context, Value);
    }

    private sealed class DeclaredSourceMotion(MotionState source) : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new DeclaredSourceRuntime(context, source);
    }

    private sealed class DeclaredSourceRuntime : MotionRuntime
    {
        public DeclaredSourceRuntime(MotionCompilationContext context, MotionState source) : base(context)
        {
            HostCountAtConstruction = context.LayoutHosts.Count;
            DeclareLayoutSource(source);
        }

        public int HostCountAtConstruction { get; }

        public int HostCountAtBuildTracks { get; private set; }

        protected override void BuildTracks() =>
            HostCountAtBuildTracks = CompilationContext.LayoutHosts.Count;
    }

    private sealed class UndeclaredRequiredSourceMotion(MotionState declared, MotionState undeclared) : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new UndeclaredRequiredSourceRuntime(context, declared, undeclared);
    }

    private sealed class UndeclaredRequiredSourceRuntime : MotionRuntime
    {
        private readonly MotionState _undeclared;

        public UndeclaredRequiredSourceRuntime(
            MotionCompilationContext context,
            MotionState declared,
            MotionState undeclared) : base(context)
        {
            _undeclared = undeclared;
            DeclareLayoutSource(declared);
        }

        protected override void BuildTracks() =>
            SetRequiredLayoutSourceProvider(add => add(_undeclared));
    }

    private sealed class ConstructorTrackMotion : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new ConstructorTrackRuntime(context);
    }

    private sealed class ConstructorTrackRuntime : MotionRuntime
    {
        public ConstructorTrackRuntime(MotionCompilationContext context) : base(context)
        {
            var target = context.ResolveTarget("Editor");
            AddTrack(new ConstantOpacityTrack(
                context,
                MotionChannel.ForProperty(target, Visual.OpacityProperty),
                0.5));
        }
    }

    private sealed class LateDeclaredSourceMotion(MotionState source) : Motion
    {
        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new LateDeclaredSourceRuntime(context, source);
    }

    private sealed class LateDeclaredSourceRuntime : MotionRuntime
    {
        private readonly MotionState _source;

        public LateDeclaredSourceRuntime(MotionCompilationContext context, MotionState source) : base(context)
        {
            _source = source;
        }

        protected override void BuildTracks() => DeclareLayoutSource(_source);
    }

    private sealed class CustomRuntime : MotionRuntime
    {
        private readonly double _value;

        public CustomRuntime(MotionCompilationContext context, double value) : base(context)
        {
            _value = value;
        }

        protected override void BuildTracks()
        {
            var target = CompilationContext.ResolveTarget("Editor");
            AddTrack(new ConstantOpacityTrack(
                CompilationContext,
                MotionChannel.ForProperty(target, Visual.OpacityProperty),
                _value));
        }

        public bool WasCommitted { get; private set; }

        protected override void OnCommitted() => WasCommitted = true;
    }

    private sealed class FailingOpacityMotion : Motion
    {
        public double Value { get; set; }

        public bool ThrowDuringPrepare { get; set; }

        public bool ThrowDuringCommit { get; set; }

        protected override MotionRuntime CreateRuntime(MotionCompilationContext context) =>
            new FailingOpacityRuntime(context, Value, ThrowDuringPrepare, ThrowDuringCommit);
    }

    private sealed class ConstantOpacityRuntime : MotionRuntime
    {
        private readonly double _value;
        private readonly bool _throwDuringCompile;
        private readonly bool _throwDuringActivation;

        public ConstantOpacityRuntime(
            MotionCompilationContext context,
            double value,
            bool throwDuringCompile,
            bool throwDuringActivation) : base(context)
        {
            _value = value;
            _throwDuringCompile = throwDuringCompile;
            _throwDuringActivation = throwDuringActivation;
        }

        protected override void BuildTracks()
        {
            var target = CompilationContext.ResolveTarget("Editor");
            AddTrack(new ConstantOpacityTrack(
                CompilationContext,
                MotionChannel.ForProperty(target, Visual.OpacityProperty),
                _value));

            if (_throwDuringCompile)
            {
                throw new InvalidOperationException("Candidate compilation failed for the test.");
            }

            if (_throwDuringActivation)
            {
                AddActivation(
                    static () => throw new InvalidOperationException("Candidate activation failed for the test."));
            }
        }
    }

    private sealed class FailingOpacityRuntime : MotionRuntime
    {
        private readonly double _value;
        private readonly bool _throwDuringPrepare;
        private readonly bool _throwDuringCommit;

        public FailingOpacityRuntime(
            MotionCompilationContext context,
            double value,
            bool throwDuringPrepare,
            bool throwDuringCommit) : base(context)
        {
            _value = value;
            _throwDuringPrepare = throwDuringPrepare;
            _throwDuringCommit = throwDuringCommit;
        }

        protected override void BuildTracks()
        {
            var target = CompilationContext.ResolveTarget("Editor");
            var throwOnEvaluation = _throwDuringPrepare ? 1 : _throwDuringCommit ? 3 : int.MaxValue;
            AddTrack(new FailingOpacityTrack(
                CompilationContext,
                MotionChannel.ForProperty(target, Visual.OpacityProperty),
                _value,
                throwOnEvaluation));
        }
    }

    private sealed class RedirectingStateMotion : StateMotion
    {
        protected override StateTransition CreateTransition(StateTransitionContext context) =>
            new("Trailing", TimeSpan.Zero);
    }

    private sealed class ConstantOpacityTrack(
        MotionCompilationContext context,
        MotionChannel channel,
        double value) : MotionTrack<double>(context, channel)
    {
        public override double Evaluate(double baseValue) => value;
    }

    private sealed class FailingOpacityTrack(
        MotionCompilationContext context,
        MotionChannel channel,
        double value,
        int throwOnEvaluation) : MotionTrack<double>(context, channel)
    {
        private int _evaluationCount;

        public override double Evaluate(double baseValue)
        {
            _evaluationCount++;
            if (_evaluationCount == throwOnEvaluation)
            {
                throw new InvalidOperationException("The test track failed during candidate evaluation.");
            }

            return value;
        }
    }
}
