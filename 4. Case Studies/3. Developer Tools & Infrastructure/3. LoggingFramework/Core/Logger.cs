using System.Collections.Immutable;
using LoggingFramework.Appenders;

namespace LoggingFramework.Core;

public sealed class Logger : ILogger
{
    private volatile ImmutableList<IAppender> _appenders;
    private LogLevel _minimumLogLevel;

    public Logger(LogLevel minimumLogLevel = LogLevel.Debug)
    {
        _appenders = ImmutableList<IAppender>.Empty;
        _minimumLogLevel = minimumLogLevel;
    }

    public Logger AddAppender(IAppender appender)
    {
        ImmutableList<IAppender> original, updated;
        do
        {
            original = _appenders;
            updated = original.Add(appender);
        } while (Interlocked.CompareExchange(ref _appenders, updated, original) != original);

        return this;
    }

    public Logger AddMinimumLevel(LogLevel minimumLogLevel) 
    {
        _minimumLogLevel = minimumLogLevel;
        return this;
    }

    private void Log(LogMessage logMessage)
    {
        if (logMessage.Level < _minimumLogLevel)
            return;

        // Capture the reference once into a local variable. The ImmutableList itself
        // can't change, but the _appenders field can be swapped to a new list by a
        // concurrent AddAppender call. Without this snapshot, the JIT could re-read
        // the field multiple times within this method, potentially seeing different
        // list instances between the level check and the iteration.
        var appenders = _appenders;
        foreach (var appender in appenders)
        {
            appender.Append(logMessage);
        }
    }

    public void Debug(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Debug, message, ex));
    }

    public void Info(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Info, message, ex));
    }

    public void Warn(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Warn, message, ex));
    }

    public void Error(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Error, message, ex));
    }

    public void Fatal(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Fatal, message, ex));
    }
}
