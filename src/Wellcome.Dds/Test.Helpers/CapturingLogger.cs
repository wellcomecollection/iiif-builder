#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Test.Helpers;

/// <summary>
/// An <see cref="ILogger{T}"/> that records the level and formatted message of each entry,
/// for tests that assert how something is logged.
/// </summary>
public class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
