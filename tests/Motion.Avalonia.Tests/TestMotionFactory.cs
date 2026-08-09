using System.Collections;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Motion.Avalonia.Tests;

internal static class TestMotionFactory
{
    private static readonly FuncControlTemplate<MotionPanel> MotionPanelTemplate =
        new((parent, scope) =>
            new ItemsPresenter
            {
                Name = "PART_ItemsPresenter",
                [~ItemsPresenter.ItemsPanelProperty] = parent[~ItemsControl.ItemsPanelProperty],
            }.RegisterInNameScope(scope));

    public static MotionPanel Panel(Motion motion, params Control[] children)
    {
        var panel = new MotionPanel
        {
            Motion = motion,
            Template = MotionPanelTemplate,
        };
        foreach (var child in children)
        {
            panel.Items.Add(child);
        }

        return panel;
    }

    public static MotionPanel Panel(Motion motion, IEnumerable items, IDataTemplate itemTemplate) =>
        new()
        {
            Motion = motion,
            Template = MotionPanelTemplate,
            ItemsSource = items,
            ItemTemplate = itemTemplate,
        };

    public static MotionState State(object? key, Func<Control> build) =>
        new()
        {
            Key = key,
            Template = new TestTemplate(build),
        };

    public static SequenceMotionKeyFrame Cue(double cue, Func<Control> build) =>
        new()
        {
            Cue = new Cue(cue),
            Template = new TestTemplate(build),
        };

    public static TimelineMotionKeyFrame Time(TimeSpan time, Func<Control> build) =>
        new()
        {
            KeyTime = time,
            Template = new TestTemplate(build),
        };

    public static MotionSlot Slot(
        string name,
        double width,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left) =>
        new()
        {
            Name = name,
            Width = width,
            Height = 40,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Top,
        };

    public static void Layout(MotionPanel panel, Size? size = null)
    {
        var finalSize = size ?? new Size(400, 200);
        panel.ApplyTemplate();
        panel.Presenter?.ApplyTemplate();
        panel.Measure(finalSize);
        panel.Arrange(new Rect(finalSize));
        panel.Measure(finalSize);
        panel.Arrange(new Rect(finalSize));
    }

    public static Rect GetPresentedBounds(Control child) =>
        GetPresentedBoundsCore(child);

    public static Control GetMotionContainer(Control child) =>
        child.GetVisualParent() as MotionItemContainer ?? child;

    private static Rect GetPresentedBoundsCore(Control child)
    {
        var position = (Vector)child.Bounds.Position;
        for (var visual = child.GetVisualParent(); visual is not null && visual is not MotionPanel; visual = visual.GetVisualParent())
        {
            position += (Vector)visual.Bounds.Position;
        }

        return new Rect((Point)position, child.Bounds.Size);
    }

    public static MotionRuntime GetMotionRuntime(MotionPanel panel) =>
        GetMotionRuntimeField(panel.ItemsPanelRoot as MotionItemsPanel ??
            throw new InvalidOperationException("The panel has not created its motion items panel.")) ??
        throw new InvalidOperationException("The panel has not prepared its motion runtime.");

    public static MotionCompilationContext GetCompilationContext(MotionRuntime runtime) =>
        GetCompilationContextCore(runtime);

    public static IDisposable Observe<T>(IObservable<T> source, ICollection<T> values) =>
        source.Subscribe(new RecordingObserver<T>(values));

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_motionRuntime")]
    private static extern ref MotionRuntime? GetMotionRuntimeField(MotionItemsPanel panel);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CompilationContext")]
    private static extern MotionCompilationContext GetCompilationContextCore(MotionRuntime runtime);

    private sealed class RecordingObserver<T>(ICollection<T> values) : IObserver<T>
    {
        public void OnNext(T value) => values.Add(value);

        public void OnError(Exception error) =>
            throw new InvalidOperationException("The observed test source failed.", error);

        public void OnCompleted()
        {
        }
    }

    private sealed class TestTemplate(Func<Control> build) : IDataTemplate
    {
        private readonly HashSet<Control> _results = new(ReferenceEqualityComparer.Instance);

        public bool Match(object? data) => true;

        public Control Build(object? param)
        {
            var result = build();
            if (!_results.Add(result))
            {
                throw new InvalidOperationException("A test data template must create a new control for every Build call.");
            }

            return result;
        }
    }
}
