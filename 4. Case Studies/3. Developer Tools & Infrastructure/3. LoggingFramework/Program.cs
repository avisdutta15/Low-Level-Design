/*
 1.1 Functional Requirements
Support standard log levels: DEBUG, INFO, WARN, ERROR, and FATAL.
Filter log messages based on a configurable minimum log level.
Support multiple output destinations (appenders), including console and file.
Allow a single log message to be sent to multiple appenders simultaneously.
Support asynchronous logging to prevent blocking the main application thread.
Allow client applications to configure the logger by specifying log level, formatters, and appenders.

1.2 Non-Functional Requirements
Thread Safety: Logging must be safe in concurrent environments to prevent interleaved or lost messages.
Performance: Logging should have minimal overhead on application performance.
Extensibility: The design should support plugging in custom formatters, filters, and appenders with minimal code changes.
Maintainability: The codebase should follow clean, object-oriented design with clear separation of concerns (e.g., logger, formatter, appender).
Ease of Use: The client-facing API should be simple and intuitive for developers to use (e.g., logger.info("User logged in");).

*/

using LoggingFramework.Appenders;
using LoggingFramework.Core;
using LoggingFramework.Formatters;

LoggerManager loggerManager = LoggerManager.GetInstance();

// ──────────────────────────────────────────────────────────
// Example 1: Normal Synchronous Logger
// Log calls block until all appenders finish writing.
// ──────────────────────────────────────────────────────────
Console.WriteLine("=== Example 1: Synchronous Logger ===\n");

var syncLogger = loggerManager.GetOrAddLogger("syncLogger");
syncLogger.AddMinimumLevel(LogLevel.Info)
          .AddAppender(new ConsoleAppender(new TextFormatter()))
          .AddAppender(new FileAppender("./logs/sync", new TextFormatter()));

syncLogger.Debug("This is filtered out");  // Below minimum level
syncLogger.Info("Sync info message");
syncLogger.Warn("Sync warn message");
syncLogger.Error("Sync error message", new Exception("Sync error!"));

// ──────────────────────────────────────────────────────────
// Example 2: AsyncLogger Decorator
// Wraps the entire logger — a single background thread
// batches and flushes messages to all appenders.
// ──────────────────────────────────────────────────────────
Console.WriteLine("\n=== Example 2: AsyncLogger Decorator ===\n");

var innerLogger = loggerManager.GetOrAddLogger("asyncLoggerInner");
innerLogger.AddMinimumLevel(LogLevel.Info)
           .AddAppender(new ConsoleAppender(new TextFormatter()))
           .AddAppender(new FileAppender("./logs/async-logger", new TextFormatter()));

using (var asyncLogger = new AsyncLogger(innerLogger, batchSize: 5, flushIntervalMs: 500))
{
    asyncLogger.Debug("This is filtered out");
    asyncLogger.Info("AsyncLogger info message");
    asyncLogger.Warn("AsyncLogger warn message");
    asyncLogger.Error("AsyncLogger error message", new Exception("Async error!"));
    asyncLogger.Fatal("AsyncLogger fatal message");
}   // Dispose: drains queue, waits for consumer thread

// ──────────────────────────────────────────────────────────
// Example 3: Per-Appender Async (AsyncAppender)
// Each appender gets its own queue + background thread.
// A slow appender can't block a fast one.
// ──────────────────────────────────────────────────────────
Console.WriteLine("\n=== Example 3: AsyncAppender (Per-Appender) ===\n");

var perAppenderLogger = loggerManager.GetOrAddLogger("perAppenderLogger");

using var asyncConsole = new AsyncAppender(
    new ConsoleAppender(new TextFormatter()), batchSize: 5, flushIntervalMs: 500);
using var asyncFile = new AsyncAppender(
    new FileAppender("./logs/async-appender", new TextFormatter()), batchSize: 5, flushIntervalMs: 500);

perAppenderLogger.AddMinimumLevel(LogLevel.Info)
                 .AddAppender(asyncConsole)
                 .AddAppender(asyncFile);

perAppenderLogger.Debug("This is filtered out");
perAppenderLogger.Info("AsyncAppender info message");
perAppenderLogger.Warn("AsyncAppender warn message");
perAppenderLogger.Error("AsyncAppender error message", new Exception("Per-appender error!"));
perAppenderLogger.Fatal("AsyncAppender fatal message");
// using declarations: each AsyncAppender drains its own queue on exit
