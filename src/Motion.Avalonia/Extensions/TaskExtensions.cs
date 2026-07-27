using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Logging;

namespace Motion.Avalonia.Extensions;

internal interface IExceptionHandler
{
    void HandleException(
        Exception exception,
        string? message = null,
        [CallerMemberName] string? source = null,
        [CallerLineNumber] int lineNumber = 0);

    static IExceptionHandler DangerouslyIgnoreAllException { get; } = new DangerouslyIgnoreAllExceptionHandler();

    static readonly IExceptionHandler AvaloniaLog = new AvaloniaLogExceptionHandler();

    private sealed class DangerouslyIgnoreAllExceptionHandler : IExceptionHandler
    {
        public void HandleException(
            Exception exception,
            string? message = null,
            [CallerMemberName] string? source = null,
            [CallerLineNumber] int lineNumber = 0)
        {
        }
    }

    private sealed class AvaloniaLogExceptionHandler : IExceptionHandler
    {
        private const string LogArea = "Motion.Avalonia";

        public void HandleException(Exception exception, string? message = null, string? source = null, int lineNumber = 0)
        {
            try
            {
                var logger = Logger.TryGet(LogEventLevel.Error, LogArea);
                if (logger is { } enabledLogger)
                {
                    enabledLogger.Log(
                        source,
                        "{Message} Caller: {Caller}, line {LineNumber}.{NewLine}{Exception}",
                        message ?? "A detached task failed.",
                        source ?? "<unknown>",
                        lineNumber,
                        Environment.NewLine,
                        exception);
                    return;
                }

                if (Logger.Sink is null)
                {
                    Debug.WriteLine(
                        $"{message ?? "A detached task failed."} Caller: {source ?? "<unknown>"}, " +
                        $"line {lineNumber}.{Environment.NewLine}{exception}");
                }
            }
            catch (Exception loggingException)
            {
                Debug.WriteLine(
                    $"Motion.Avalonia could not report a detached task failure.{Environment.NewLine}" +
                    $"{exception}{Environment.NewLine}Logging failure:{Environment.NewLine}{loggingException}");
            }
        }
    }
}

internal static class TaskExtensions
{
    public static async void Detach(
        this Task task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            (exceptionHandler ?? IExceptionHandler.AvaloniaLog).HandleException(e, message, source, lineNumber);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach<T>(
        this Task<T> task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null,
        [CallerLineNumber] int lineNumber = 0) =>
        Detach((Task)task, exceptionHandler, message, source, lineNumber);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach(
        this ValueTask task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null,
        [CallerLineNumber] int lineNumber = 0) =>
        Detach(task.AsTask(), exceptionHandler, message, source, lineNumber);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Detach<T>(
        this ValueTask<T> task,
        IExceptionHandler? exceptionHandler = null,
        string? message = null,
        [CallerMemberName] string? source = null,
        [CallerLineNumber] int lineNumber = 0) =>
        Detach((Task)task.AsTask(), exceptionHandler, message, source, lineNumber);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask ToValueTask(this Task task) => new(task);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> ToValueTask<T>(this Task<T> task) => new(task);
}