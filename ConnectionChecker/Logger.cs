using System;
using ConnectionChecker.Enums;
using Microsoft.Extensions.Logging;

namespace ConnectionChecker;

public sealed class AppLogger : ILogger
{
    ILogger _logger;

    private AppLogger()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger(nameof(ConnectionChecker));
    }

    public static AppLogger Instance { get { return Nested.instance; } }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _logger.BeginScope<TState>(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log<TState>(logLevel, eventId, state, exception, formatter);
    }

    public void LogInformation(string? message, params object?[] args)
    {
        _logger.LogInformation(message, args);
    }

    internal void LogCritical(string? message, params object?[] args)
    {
        _logger.LogCritical(message, args);
    }

    internal void LogError(string? message, params object?[] args)
    {
        _logger.LogError(message, args);
    }

    internal void LogWarning(string? message, params object?[] args)
    {
        _logger.LogWarning(message, args);
    }

    private class Nested
    {
        static Nested()
        {
        }

        internal static readonly AppLogger instance = new AppLogger();
    }
}
