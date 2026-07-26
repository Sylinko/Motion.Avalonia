using System.Runtime.CompilerServices;

namespace Motion.Avalonia.Extensions;

internal interface IExceptionHandler
{
    void HandleException(
        Exception exception,
        string? message = null,
        [CallerMemberName] object? source = null,
        [CallerLineNumber] int lineNumber = 0);

    static IExceptionHandler DangerouslyIgnoreAllException { get; } = new AnonymousExceptionHandler(static delegate { });

    private sealed class AnonymousExceptionHandler(Action<Exception, string?, object?, int> handler) : IExceptionHandler
    {
        public void HandleException(
            Exception exception,
            string? message = null,
            [CallerMemberName] object? source = null,
            [CallerLineNumber] int lineNumber = 0)
        {
            handler.Invoke(exception, message, source, lineNumber);
        }
    }
}

internal static class TaskExtensions
{
    public static async void Detach(
        this Task task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] object? source = null)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (exceptionHandler != null)
        {
            exceptionHandler.HandleException(e, message, source);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach<T>(
        this Task<T> task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null) =>
        Detach((Task)task, exceptionHandler, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach(
        this ValueTask task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null) =>
        Detach(task.AsTask(), exceptionHandler, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach<T>(
        this ValueTask<T> task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null) => Detach((Task)task.AsTask(), exceptionHandler, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask ToValueTask(this Task task) => new(task);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> ToValueTask<T>(this Task<T> task) => new(task);
}