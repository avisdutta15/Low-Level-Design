using System.Collections.Concurrent;

namespace LoggingFramework.Core;

public class LoggerManager
{
    private static readonly Lazy<LoggerManager> _instance = new(() => new LoggerManager());
    private readonly ConcurrentDictionary<string, Logger> _loggers;

    private LoggerManager()
    {
        _loggers = new ConcurrentDictionary<string, Logger>();
    }

    public static LoggerManager GetInstance() => _instance.Value;

    public Logger GetOrAddLogger(string name)
    {
        return _loggers.GetOrAdd(name, _ => new Logger());
    }
}
