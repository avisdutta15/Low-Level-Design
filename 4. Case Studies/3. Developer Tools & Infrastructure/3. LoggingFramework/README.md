# Logging Framework

## Problem Statement

Modern applications need a structured, extensible logging system that can write log messages to multiple destinations (console, file, database) without blocking the main application thread. The logger must be thread-safe, performant under high concurrency, and easy to configure. This project implements such a framework from scratch in C# / .NET 8, progressively evolving a simple synchronous logger into a high-throughput, lock-free, asynchronous system.

---

## Functional Requirements

| # | Requirement |
|---|-------------|
| FR-1 | Support standard log levels: `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL`. |
| FR-2 | Filter log messages based on a configurable minimum log level. |
| FR-3 | Support multiple output destinations (appenders): console, file, and extensible to database or network. |
| FR-4 | Allow a single log message to be sent to multiple appenders simultaneously. |
| FR-5 | Support asynchronous logging to prevent blocking the main application thread. |
| FR-6 | Allow client applications to configure the logger by specifying log level, formatters, and appenders. |

## Non-Functional Requirements

| # | Requirement |
|---|-------------|
| NFR-1 | **Thread Safety** — Logging must be safe in concurrent environments. No interleaved or lost messages. |
| NFR-2 | **Performance** — Logging should have minimal overhead on application performance. The hot path (writing a log message) must be as cheap as possible. |
| NFR-3 | **Extensibility** — The design should support plugging in custom formatters, filters, and appenders with minimal code changes. |
| NFR-4 | **Maintainability** — Clean, object-oriented design with clear separation of concerns. |
| NFR-5 | **Ease of Use** — Simple, intuitive API: `logger.Info("User logged in")`. |

---

## Core Entities

### LogLevel (Enum)
Defines the severity of a log message. Ordered from least to most severe:

```
Debug → Info → Warn → Error → Fatal
```

The logger filters messages below the configured minimum level. If minimum is `Info`, then `Debug` messages are discarded.

### LogMessage
A value object representing a single log entry:
- `TimeStamp` — when the message was created (`DateTime.Now`)
- `Level` — the `LogLevel` severity
- `Message` — the log text
- `Exception?` — optional exception context

### ILogger (Interface)
- The contract every logger must implement. 
- Defines convenience methods: `Debug()`, `Info()`, `Warn()`, `Error()`, `Fatal()`. This interface is the key to the Decorator pattern — both `Logger` and `AsyncLogger` implement it, making them interchangeable.
```csharp
public interface ILogger
{
    void Debug(string message, Exception? ex = null);
    void Info(string message, Exception? ex = null);
    void Warn(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null);
}
```
### Logger
The core synchronous logger. 
 - Holds a list of appenders and has a minimum log level. 
 - When a message is logged, it checks the level filter and dispatches to all registered appenders.

```csharp
public class Logger : ILogger{
    private ImmutableList<IAppender> _appenders = ImmutableList<IAppender>.Empty;
    private LogLevel _minimumLogLevel;

    public Logger(LogLevel minimumLogLevel = LogLevel.Debug)
    {
        _minimumLogLevel = minimumLogLevel;
    }

    public Logger AddAppender(IAppender appender)
    {
        ImmutableInterlocked.Update(ref _appenders, list => list.Add(appender));
        return this;    // for fluent builder pattern
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

        // ImmutableList has snapshot semantic in-built
        foreach (var appender in _appenders)
        {
            appender.Append(logMessage);
        }
    }

    void Debug(string message, Exception? ex = null)
    {
        Log(new LogMessage(LogLevel.Debug, message, ex));
    }

    // Similar implementation for the below ones
    void Info(string message, Exception? ex = null);
    void Warn(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null); 
}   

```
### LoggerManager (Singleton)
- A thread-safe singleton that manages named `Logger` instances using a `ConcurrentDictionary`. 
- Uses `Lazy<T>` for safe, efficient initialization.

```csharp
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
```


### IAppender (Interface)
- The contract for output destinations. 
- A single method: `Append(LogMessage message)`.

```csharp
public interface IAppender
{
    public void Append(LogMessage message);
}
```

### ConsoleAppender / FileAppender
- Concrete appenders that write to `Console` and to a timestamped log file respectively. 
- Both extend `IAppender`.

```csharp
public class ConsoleAppender : IAppender
{
    public void Append(LogMessage message)
    {
        Console.WriteLine(message.ToString());
    }
}
```

### IFormatter (Interface)
- The contract for message formatting. 
- A single method: `string Format(LogMessage message)`.

```csharp
public interface IFormatter
{
    string Format(LogMessage message);
}
```

### TextFormatter
Concrete formatter that produces output like:
```
[2025-01-15 14:30:45.123] - [Info] - [User logged in]
```

```csharp
public class TextFormatter : IFormatter
{
    public string Format(LogMessage message)
    {
        if (message.Exception == null)
            return $"[{message.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] - [{message.Level}] - [{message.Message}]";
        return $"[{message.TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] - [{message.Level}] - [{message.Message}] - [Exception: {message.Exception}]";
    }
}
```
----
- **We have a requirement that appenders can have formatters.**
- **In other words, ConsoleAppender can take a TextFormatter and log the message.**
- **If ConsoleAppender wants then it maynot take any formatter and use the default message.ToString() format.**

```csharp
public class ConsoleAppender : IAppender
{
    // Formatter is optional, if not provided,
    // default formatter of the LogMessage will be used
    private IFormatter? _formatter;
    public ConsoleAppender(IFormatter? formatter = null)
    {
        _formatter = formatter;
    }

    public void Append(LogMessage message)
    {
        Console.WriteLine(FormatMessage(message));
    }

    public void SetFormatter(IFormatter formatter)
    {
        _formatter = formatter;
    }

    private string FormatMessage(LogMessage message) 
    {
        string formattedMessage = message.ToString();
        if (_formatter != null)
        {
            formattedMessage = _formatter.Format(message);
        }
        return formattedMessage;
    }
}
```

If we observe the above code, for all the appenders we would have to give it the 
flexibility to either add the formatter or skip it. So the code for 
SetFormatter(IFormatter formatter), FormatMessage(LogMessage message) will be repeated
in every Appender.
--
We can extract the common methods to a base abstract class and let the ConsoleAppender class
inherit from abstract class and the interface.
----
### AppenderBase (Abstract Class)
- Shared base for all appenders. 
- Holds an optional `IFormatter` and provides `FormatMessage()` — if no formatter is set, falls back to `LogMessage.ToString()`.

```csharp
public abstract class AppenderBase : IAppender
{
    private IFormatter? _formatter;

    // Protected Constructor to be used by child classes
    protected AppenderBase(IFormatter? formatter = null)
    {
        _formatter = formatter;
    }

    public void SetFormatter(IFormatter formatter)
    {
        _formatter = formatter;
    }

    protected string FormatMessage(LogMessage message) 
    {
        string formattedMessage = message.ToString();
        if (_formatter != null)
        {
            formattedMessage = _formatter.Format(message);
        }
        return formattedMessage;
    }
}
```

```
public class ConsoleAppender : AppenderBase, IAppender
{
    // _formatter is inherited via inheritance of AppenderBase
    // constructor inject the Formatter to AppenderBase
    public ConsoleAppender(IFormatter? formatter = null) : base(formatter)
    { }

    public void Append(LogMessage message)
    {
        Console.WriteLine(FormatMessage(message));
    }
}
```
---
**1. Should AppenderBase inherit IAppender and concrete classes inherit
only AppenderBase**
```
    IAppender
    └── AppenderBase (abstract)
            └── ConsoleAppender
```
**2. or AppenderBase doesnot inherit from IAppender and ConsoleAppender inherits from both**
```
IAppender       AppenderBase
    └──────────────┘
           |
     ConsoleAppender

```

Answer:
For
```
    IAppender
    └── AppenderBase (abstract)
            └── ConsoleAppender
```
- `AppenderBase` declares `public abstract void Append(LogMessage message)` — enforcing the contract at the base level
- `ConsoleAppender` only needs to implement `Append()`, nothing else
- Any code accepting `IAppender` automatically works with `AppenderBase` and all its children — clean `Liskov substitution`
- The relationship reads naturally: `"AppenderBase is an IAppender"`

For
```
IAppender       AppenderBase
    └──────────────┘
           |
     ConsoleAppender

```
- C# supports multiple inheritance only for interfaces, so this is technically valid — but it's a design smell
- `ConsoleAppender` now has two separate "reasons" to implement `Append()` — from `IAppender` directly AND from `AppenderBase` — creating ambiguity
- If someone adds a new appender and forgets to inherit `AppenderBase`, they still satisfy `IAppender` but miss all the shared formatter logic silently
---

### AsyncLogger (Decorator)
A non-blocking decorator that wraps any `ILogger`. Uses a producer-consumer queue with batching. Detailed in the AsyncLogger section below.

---

```csharp
public abstract class AppenderBase : IAppender
{
    private IFormatter? _formatter;

    // Protected Constructor to be used by child classes
    protected AppenderBase(IFormatter? formatter = null)
    {
        _formatter = formatter;
    }

    public void SetFormatter(IFormatter formatter)
    {
        _formatter = formatter;
    }

    protected string FormatMessage(LogMessage message)
    {
        // if the formatter is set, then use it. Else fallback to logmessage.ToString()
        if (_formatter != null)
            return _formatter.Format(message);

        return message.ToString();
    }

    // Inherited from IAppender. Passed on as abstract to child classes for
    // implementation
    public abstract void Append(LogMessage message);
}
```

```csharp
public class ConsoleAppender : AppenderBase
{
    // _formatter is inherited via inheritance of AppenderBase
    // constructor inject the Formatter to AppenderBase
    public ConsoleAppender(IFormatter? formatter = null) : base(formatter)
    { }

    public override void Append(LogMessage message)
    {
        Console.WriteLine(FormatMessage(message));
    }
}
```
## Class Diagram

```
                    ┌──────────────┐
                    │  «interface» │
                    │   ILogger    │
                    │──────────────│
                    │ +Debug()     │
                    │ +Info()      │
                    │ +Warn()      │
                    │ +Error()     │
                    │ +Fatal()     │
                    └──────┬───────┘
                           │ implements
              ┌────────────┴────────────┐
              │                         │
     ┌────────┴────────┐     ┌─────────┴─────────┐
     │     Logger      │     │   AsyncLogger     │
     │─────────────────│     │  «decorator»      │
     │ -_appenders     │     │───────────────────│
     │ -_minimumLevel  │     │ -_inner: ILogger  │
     │─────────────────│     │ -_queue           │
     │ +AddAppender()  │     │ -_batchSize       │
     │ +AddMinimumLevel│     │ -_flushInterval   │
     │ -Log()          │     │───────────────────│
     └────────┬────────┘     │ -Enqueue()        │
              │              │ -ProcessQueue()   │
              │ uses         │ -FlushBatch()     │
              ▼              │ +Dispose()        |
     ┌────────────────┐      └───────────────────┘
     │  «interface»   │
     │   IAppender    │
     │────────────────│
     │   +Append()    │
     └───────┬────────┘
             │ implements
    ┌────────┴──────────---┐
    │                      │
┌───┴──────────---┐  ┌─────┴──────────┐
│ AppenderBase    │  │  «interface»   │
│ «abstract»      │  │  IFormatter    │
│──────────────---│  │────────────────│
│ -_formatter     │  │ +Format()      │
│──────────────---│  └───────┬────────┘
│ #FormatMessage()│          │ implements
│ +SetFormatter() │          │
│ +Append()       │   ┌──────┴────────┐
└──────┬───────---┘   │ TextFormatter │
       │              └───────────────┘
  ┌────┴──────────────┐
  │                   │
┌─┴──────────────┐ ┌──┴─────────────┐
│ConsoleAppender │ │ FileAppender   │
│────────────────│ │────────────────│
│   +Append()    │ │ -_writer       │
└────────────────┘ │ +Append()      │
                   │ +Dispose()     │
                   └────────────────┘

     ┌──────────────────┐
     │  LoggerManager   │
     │  «singleton»     │
     │──────────────────│
     │ -_instance: Lazy │
     │ -_loggers: Dict  │
     │──────────────────│
     │ +GetInstance()   │
     │ +GetOrAddLogger()│
     └──────────────────┘
```

---

### LoggerManager
```csharp
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
```

## Design Patterns Used

### 1. Singleton — `LoggerManager`
Ensures a single, globally accessible registry of loggers. Implemented with `Lazy<T>` for thread-safe, lazy initialization — no manual double-check locking needed.

### 2. Decorator — `AsyncLogger`
Wraps any `ILogger` to add asynchronous, batched behavior without modifying the original class. The caller interacts with the same `ILogger` interface, unaware of the async machinery behind it.

### 3. Strategy — `IFormatter` / `IAppender`
Formatters and appenders are interchangeable strategies. You can swap `TextFormatter` for a `JsonFormatter`, or add a `DatabaseAppender`, without touching the logger core.

### 4. Template Method — `AppenderBase`
The base class defines the skeleton (`FormatMessage` → `Append`), and concrete subclasses fill in the `Append` implementation.

### 5. Builder / Fluent API — `Logger` configuration
`AddAppender()` and `AddMinimumLevel()` return `this`, enabling chained configuration:
```csharp
logger.AddMinimumLevel(LogLevel.Info)
      .AddAppender(new ConsoleAppender(new TextFormatter()))
      .AddAppender(new FileAppender("./logs", new TextFormatter()));
```

---

## Thread Safety: The Copy-on-Write Journey

### The Problem

The `Logger` class holds a list of appenders. The hot path — `Log()` — iterates this list on every log call. Meanwhile, `AddAppender()` can be called from any thread (e.g., dynamically adding a new appender at runtime).

With a plain `List<T>`, this is a race condition:
- **Reader thread** calls `Log()`, starts iterating `_appenders`
- **Writer thread** calls `AddAppender()`, which internally resizes the list's backing array
- Result: `InvalidOperationException` ("Collection was modified during enumeration"), corrupted state, or silently skipped/duplicated entries

### Approaches Considered

#### Approach 1: `lock` on Read and Write

```csharp
private readonly object _lock = new();
private readonly List<IAppender> _appenders = new();

public void AddAppender(IAppender appender)
{
    lock (_lock) { _appenders.Add(appender); }
}

private void Log(LogMessage msg)
{
    lock (_lock)
    {
        foreach (var appender in _appenders)
            appender.Append(msg);
    }
}
```

**Pros:** Simple, correct.
**Cons:** Every single log call contends on the lock. Under high throughput (thousands of log calls/sec from many threads), this becomes a bottleneck. The lock serializes all logging, destroying parallelism.

#### Approach 2: `ConcurrentBag<T>`

```csharp
private readonly ConcurrentBag<IAppender> _appenders = new();
```

**Pros:** Thread-safe out of the box, no explicit locking.
**Cons:** No ordering guarantees. Iteration creates a snapshot internally (hidden allocation). Not designed for "add rarely, read constantly" patterns.

#### Approach 3: `ReaderWriterLockSlim`

```csharp
private readonly ReaderWriterLockSlim _rwLock = new();

private void Log(LogMessage msg)
{
    _rwLock.EnterReadLock();
    try { /* iterate */ }
    finally { _rwLock.ExitReadLock(); }
}

public void AddAppender(IAppender appender)
{
    _rwLock.EnterWriteLock();
    try { _appenders.Add(appender); }
    finally { _rwLock.ExitWriteLock(); }
}
```

**Pros:** Multiple readers can proceed in parallel; only writers block.
**Cons:** Still has overhead on every read (acquiring/releasing the read lock). More complex than needed for a "write once, read forever" pattern.

#### Approach 4: Copy-on-Write with `ImmutableList<T>` (Chosen)

```csharp
private volatile ImmutableList<IAppender> _appenders = ImmutableList<IAppender>.Empty;

public Logger AddAppender(IAppender appender)
{
    // CoW as CAS Loop!
    ImmutableList<IAppender> original, updated;
    do
    {
        original = _appenders;
        updated = original.Add(appender);
    } while (Interlocked.CompareExchange(ref _appenders, updated, original) != original);

    or 

    // This is a more concise way of doing the same thing as above
    ImmutableInterlocked.Update(ref _appenders, list => list.Add(appender));

    return this;
}

private void Log(LogMessage logMessage)
{
    if (logMessage.Level < _minimumLogLevel) return;

    // ImmutableList has inbuilt snapshot semantics
    foreach (var appender in _appenders)
        appender.Append(logMessage);
}
```

**How it works:**
1. `_appenders` is an `ImmutableList<T>` — once created, it never changes.
2. `AddAppender()` reads the current list, creates a new list with the appender added, and atomically swaps the reference using `Interlocked.CompareExchange` (CAS). If another thread modified it in between, the CAS fails and we retry.
3. `Log()` captures the reference into a local variable (snapshot) and iterates it. Since the list is immutable, no concurrent modification can occur.

**Why there is no snapshot in `Log()`?**
No, `ImmutableList<T>` does not need to be manually snapshotted. By design, it inherently acts as a `thread-safe snapshot`. When you call methods like `Add()` or `Remove()`, the original list remains untouched, and a completely new `ImmutableList<T>` instance is returned.

**Pros:**
- Zero locks on the hot path (reading). Just a volatile read + local assignment.
- Readers never block each other or writers.
- Perfect for "configure once at startup, read millions of times" — which is exactly the appender pattern.

**Cons:**
- Each `AddAppender()` allocates a new list (GC pressure). Irrelevant here because appenders are configured at startup (2-3 adds total), not on the hot path.

**Trade-off summary:**

| Approach | Read Cost | Write Cost | Complexity | Best For |
|----------|-----------|------------|------------|----------|
| `lock` | High (contention) | Low | Simple | Low throughput |
| `ConcurrentBag` | Medium (snapshot) | Low | Simple | Unordered, frequent writes |
| `ReaderWriterLockSlim` | Medium (lock acquire) | Medium | Moderate | Balanced read/write |
| **Copy-on-Write** | **Near zero** | High (copy) | Moderate | **Read-heavy, write-rare** |

---

## AsyncLogger: Non-Blocking Logging with Producer-Consumer Batching

### The Requirement

From FR-5: *"Support asynchronous logging to prevent blocking the main application thread."*

In a synchronous logger, every `logger.Info(...)` call blocks until the message is formatted and written to all appenders (console I/O, file I/O, network). Under load, this directly impacts application latency.

We need a way to decouple the act of "producing a log message" from the act of "writing it to destinations" — classic producer-consumer.

### Design Pattern: Decorator

We chose the Decorator pattern so that async behavior is layered on top of any existing `ILogger` without modifying it:

```
Application Code
       │
       ▼
  ┌───────────┐    enqueue    ┌──────────────────────┐
  │AsyncLogger│ ──────────►   │ BlockingCollection   │
  │(decorator)│               │ (bounded queue)      │
  └───────────┘               └──────────┬───────────┘
                                         │ consumer thread
                                         ▼
                              ┌──────────────────────┐
                              │   Batch (List)       │
                              │ drain up to N items  │
                              └──────────┬───────────┘
                                         │ flush
                                         ▼
                              ┌──────────────────────┐
                              │   Inner ILogger      │
                              │ (Logger + appenders) │
                              └──────────────────────┘
```

### What Changed in the Original Code

To make the Decorator pattern work, we needed the original `Logger` and the new `AsyncLogger` to share a common contract. We extracted the `ILogger` interface:

```csharp
public interface ILogger
{
    void Debug(string message, Exception? ex = null);
    void Info(string message, Exception? ex = null);
    void Warn(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null);
}
```

`Logger` now implements `ILogger`. `AsyncLogger` also implements `ILogger` and wraps an inner `ILogger`. This means you can stack decorators or swap implementations transparently.

### How the Processing Works — Step by Step

#### Step 1: Construction — Spinning Up the Consumer

When an `AsyncLogger` is created, it immediately starts a dedicated background thread:

```csharp
public AsyncLogger(ILogger inner, int batchSize = 10, int flushIntervalMs = 1000, int boundedCapacity = 10000)
{
    _inner = inner;
    _batchSize = batchSize;
    _flushInterval = TimeSpan.FromMilliseconds(flushIntervalMs);
    _queue = new BlockingCollection<LogMessage>(boundedCapacity);

    _consumerThread = new Thread(ProcessQueue)
    {
        IsBackground = true,       // Won't prevent app exit if orphaned
        Name = "AsyncLogger-Consumer"
    };
    _consumerThread.Start();
}
```

The `BlockingCollection<LogMessage>` is bounded to `boundedCapacity` (default 10,000). This is critical — without a bound, a burst of log messages could consume unbounded memory. When the queue is full, the producer thread blocks on `Add()`, providing natural back-pressure.

#### Step 2: Producer Side — Enqueuing Messages

When application code calls `asyncLogger.Info("something")`, it hits these one-liner methods:

```csharp
public void Debug(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Debug, message, ex));
public void Info(string message, Exception? ex = null)  => Enqueue(new LogMessage(LogLevel.Info, message, ex));
public void Warn(string message, Exception? ex = null)  => Enqueue(new LogMessage(LogLevel.Warn, message, ex));
public void Error(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Error, message, ex));
public void Fatal(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Fatal, message, ex));
```

Each creates a `LogMessage` (capturing the timestamp at call time, not flush time) and enqueues it:

```csharp
private void Enqueue(LogMessage message)
{
    if (!_queue.IsAddingCompleted)   // Guard: don't enqueue after Dispose() was called
        _queue.Add(message);         // Blocks if queue is full (back-pressure)
}
```

This is the entire cost on the calling thread — create an object and push it into a concurrent queue. No I/O, no formatting, no file writes. The caller returns immediately.

#### Step 3: Consumer Side — The Processing Loop

The background thread runs `ProcessQueue()`, which loops until the queue is fully drained and marked complete:

```csharp
private void ProcessQueue()
{
    var batch = new List<LogMessage>(_batchSize);

    while (!_queue.IsCompleted)
    {
        batch.Clear();

        try
        {
            // STEP 3a: Block until at least one message arrives, or timeout
            if (_queue.TryTake(out var first, _flushInterval))
            {
                batch.Add(first);

                // STEP 3b: Greedily drain up to batchSize without blocking
                while (batch.Count < _batchSize && _queue.TryTake(out var next))
                {
                    batch.Add(next);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // Queue was marked CompleteAdding while we were waiting — exit loop
            break;
        }

        // STEP 3c: Flush the collected batch to the inner logger
        FlushBatch(batch);
    }

    // STEP 3d: After loop exits, drain any stragglers still in the queue
    while (_queue.TryTake(out var remaining))
        batch.Add(remaining);

    FlushBatch(batch);
}
```

Breaking this down:

- **Step 3a** — `TryTake(out var first, _flushInterval)` blocks the consumer thread for up to `_flushInterval` (default 1 second) waiting for the first message. If no messages arrive in that window, it returns `false` and the loop cycles (checking `IsCompleted` again). This prevents busy-spinning during idle periods.

- **Step 3b** — Once we have at least one message, we greedily drain more with non-blocking `TryTake(out var next)` (no timeout). This fills the batch up to `_batchSize`. If the queue has 100 messages and `batchSize` is 10, we take exactly 10 and flush them, then come back for the next 10 on the next iteration.

- **Step 3c** — The batch is flushed to the inner logger:

```csharp
private void FlushBatch(List<LogMessage> batch)
{
    foreach (var message in batch)
    {
        try
        {
            switch (message.Level)
            {
                case LogLevel.Debug: _inner.Debug(message.Message, message.Exception); break;
                case LogLevel.Info:  _inner.Info(message.Message, message.Exception);  break;
                case LogLevel.Warn:  _inner.Warn(message.Message, message.Exception);  break;
                case LogLevel.Error: _inner.Error(message.Message, message.Exception); break;
                case LogLevel.Fatal: _inner.Fatal(message.Message, message.Exception); break;
            }
        }
        catch (Exception)
        {
            // Swallow exceptions to keep the consumer thread alive.
            // A failing appender (e.g., disk full) must not kill the entire logging pipeline.
        }
    }
}
```

Each message is routed through the correct level method on the inner `ILogger`, which applies the minimum level filter and dispatches to all appenders. The `try/catch` per message ensures that one bad message or a transient appender failure doesn't crash the consumer thread or lose the rest of the batch.

- **Step 3d** — After the `while (!_queue.IsCompleted)` loop exits (because `Dispose()` was called), we do one final drain to catch any messages that were enqueued between the last `TryTake` and the `CompleteAdding` signal.

#### Why Batching Matters

Without batching, the consumer would call `_inner.Info(...)` (which triggers file I/O, console I/O) for every single message individually. With batching:

- File appender: multiple `WriteLine` calls happen back-to-back before the OS flushes the buffer, reducing syscall overhead.
- Console appender: reduces the number of console lock acquisitions.
- Future network appender: could send a batch of messages in a single network round-trip.

The `_flushInterval` timeout ensures low-traffic messages don't sit in the queue indefinitely — even if only 1 message arrives in a quiet period, it gets flushed within the interval window.

### Graceful Shutdown: What Happens on Dispose?

**Yes, the background thread waits to flush all remaining messages before the application exits.** Here's exactly how:

```csharp
public void Dispose()
{
    _queue.CompleteAdding();    // Step 1: Signal "no more messages will be enqueued"
    _consumerThread.Join();     // Step 2: Block until the consumer thread finishes
    _queue.Dispose();           // Step 3: Release the queue resources
}
```

The sequence of events when `Dispose()` is called:

```
Main Thread                          Consumer Thread
───────────                          ───────────────
Dispose() called
  │
  ├─ _queue.CompleteAdding()
  │    signals the queue is done
  │                                  TryTake() is currently blocking...
  │                                    │
  │                                    ├─ TryTake sees CompleteAdding
  │                                    │  throws InvalidOperationException
  │                                    │  (caught → breaks out of while loop)
  │                                    │
  │                                    ├─ Enters final drain loop (Step 3d)
  │                                    │  TryTake remaining messages
  │                                    │
  │                                    ├─ FlushBatch(remaining)
  │                                    │  Writes all remaining messages
  │                                    │  to inner logger / appenders
  │                                    │
  │                                    └─ Thread exits ProcessQueue()
  │
  ├─ _consumerThread.Join()
  │    BLOCKS here until consumer
  │    thread has fully exited
  │    (all messages flushed)
  │
  ├─ _queue.Dispose()
  │    cleanup
  │
  └─ Dispose() returns
     Application can now exit safely
```

Key guarantees:

1. **`CompleteAdding()`** — Tells the `BlockingCollection` that no new items will ever be added. Any thread currently blocked on `TryTake` with a timeout will be unblocked. The `IsCompleted` property becomes `true` once the queue is both marked complete AND empty.

2. **Final drain (Step 3d)** — After the main loop exits, the consumer explicitly drains any remaining items with non-blocking `TryTake`. This catches the edge case where messages were enqueued after the last successful `TryTake` but before `CompleteAdding()` was called.

3. **`_consumerThread.Join()`** — This is the critical line. The main thread blocks here until the consumer thread has fully exited `ProcessQueue()`, which only happens after all remaining messages have been flushed. **No messages are lost.**

4. **What if `Dispose()` is never called?** — The consumer thread is marked `IsBackground = true`, which means the .NET runtime will terminate it when the application exits. In that case, messages still in the queue *would* be lost. This is why using `using var asyncLogger = new AsyncLogger(...)` (or explicitly calling `Dispose()`) is essential for graceful shutdown.


---
Refer to [BatchAndFlushProducerConsumer](./BatchAndFlushProducerConsumer.md)
---

### Usage

```csharp
var logger = new Logger(LogLevel.Info);
logger.AddAppender(new ConsoleAppender(new TextFormatter()));

// Wrap with async decorator — batchSize=5, flush every 500ms
using var asyncLogger = new AsyncLogger(logger, batchSize: 5, flushIntervalMs: 500);

asyncLogger.Info("This doesn't block the caller");
asyncLogger.Error("Neither does this", new Exception("oops"));

// When 'using' scope ends, Dispose() is called:
//   1. Signals no more messages
//   2. Consumer thread flushes remaining batch
//   3. Join() waits for consumer to finish
//   4. All messages guaranteed written — nothing lost
```

### Why One Background Thread Is Enough

For most applications, a single consumer thread is sufficient. The bottleneck in logging is almost always I/O (writing to disk, console, network), not CPU. A single thread can saturate a disk's write throughput easily. Adding more consumer threads introduces complexity — ordering guarantees, lock contention on the `StreamWriter`, interleaved console output — without meaningful throughput gain when they're all writing to the same destination.

### When One Thread Isn't Enough

There are scenarios where a single consumer becomes a bottleneck:

1. **Multiple slow appenders** — If the logger has a file appender, a database appender, and a remote HTTP appender, the single consumer processes them sequentially per message. If the database appender takes 50ms per write, it blocks the file and console appenders too. The entire pipeline is gated by the slowest appender.

2. **Extremely high message volume** — If producers generate messages faster than the single consumer can flush them, the bounded queue fills up and back-pressure kicks in, blocking application threads.

#### How Real Frameworks Handle This

- **log4j2** and **Serilog** both use a single background thread by default. It's the proven, battle-tested default.
- **log4j2's `AsyncAppender`** optionally uses the LMAX Disruptor (a lock-free ring buffer) for higher throughput, but still with a single consumer thread — the gain comes from the data structure, not more threads.
- When multiple slow appenders are the problem, the standard solution is **per-appender async** — give each appender its own queue and consumer thread.

#### The Better Solution: Per-Appender Async (AsyncAppender)

Rather than adding N consumer threads pulling from one shared queue (which creates contention and ordering headaches), the cleaner architecture is to push the async boundary down to the appender level:

```
                                    ┌─────────────┐   queue   ┌───────────────┐
                                ┌──►│AsyncAppender│──────────►│ConsoleAppender│
                                │   │  (thread 1) │           └───────────────┘
                                │   └─────────────┘
  ┌──────┐     ┌───────┐        │
  │Caller│────►│ Logger│────────┤
  └──────┘     └───────┘        │
                                │   ┌──────────────┐   queue   ┌───────────────┐
                                └──►│AsyncAppender │──────────►│  DBAppender   │
                                    │  (thread 2)  │           └───────────────┘
                                    └──────────────┘
```

Each `AsyncAppender` wraps a single inner `IAppender` with its own `BlockingCollection` and dedicated consumer thread. This way:

- A slow database appender doesn't block the fast console appender.
- Each appender processes messages at its own pace.
- Message ordering is preserved per-appender (single consumer per queue).
- No shared-state contention between appender threads.

This is the same approach used by log4j2's `AsyncAppender` and Serilog's async sink wrappers.

#### Trade-off Summary

| Approach | Throughput | Ordering | Complexity | Best For |
|----------|-----------|----------|------------|----------|
| Single consumer thread (current) | Good | Global ordering | Simple | Most applications, same-speed appenders |
| Multiple consumers, shared queue | Higher CPU utilization | No ordering guarantee | High | CPU-bound processing (rare for logging) |
| Per-appender async (AsyncAppender) | Highest I/O utilization | Per-appender ordering | Moderate | Multiple slow/heterogeneous appenders |

### AsyncAppender Implementation

`AsyncAppender` is a Decorator around `IAppender` — the same pattern we used for `AsyncLogger`, but pushed down one level. It implements `IAppender` itself, so it plugs in anywhere a regular appender goes. The logger doesn't know (or care) that the appender is async.

#### Construction

```csharp
public class AsyncAppender : IAppender, IDisposable
{
    private readonly IAppender _inner;
    private readonly BlockingCollection<LogMessage> _queue;
    private readonly Thread _consumerThread;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    public AsyncAppender(IAppender inner, int batchSize = 10, int flushIntervalMs = 1000, int boundedCapacity = 10000)
    {
        _inner = inner;
        _batchSize = batchSize;
        _flushInterval = TimeSpan.FromMilliseconds(flushIntervalMs);
        _queue = new BlockingCollection<LogMessage>(boundedCapacity);

        _consumerThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = $"AsyncAppender-{inner.GetType().Name}"  // e.g. "AsyncAppender-ConsoleAppender"
        };
        _consumerThread.Start();
    }
}
```

Each `AsyncAppender` gets its own bounded queue and its own consumer thread, named after the inner appender for easy debugging. The thread starts immediately on construction.

#### Producer Side — Append()

The `Append` method is what the `Logger` calls during its appender iteration. Instead of doing I/O, it just enqueues:

```csharp
public void Append(LogMessage message)
{
    if (!_queue.IsAddingCompleted)   // Guard against post-Dispose calls
        _queue.Add(message);         // Blocks if queue is full (back-pressure)
}
```

This is the entire cost on the logger's thread — push a reference into a concurrent queue. The actual formatting and I/O happen on the background thread.

#### Consumer Side — Processing Loop

Identical pattern to `AsyncLogger`, but simpler since we just call `_inner.Append()` directly:

```csharp
private void ProcessQueue()
{
    var batch = new List<LogMessage>(_batchSize);

    while (!_queue.IsCompleted)
    {
        batch.Clear();

        try
        {
            if (_queue.TryTake(out var first, _flushInterval))
            {
                batch.Add(first);

                while (batch.Count < _batchSize && _queue.TryTake(out var next))
                {
                    batch.Add(next);
                }
            }
        }
        catch (InvalidOperationException)
        {
            break;
        }

        FlushBatch(batch);
    }

    // Drain remaining after completion signal
    while (_queue.TryTake(out var remaining))
        batch.Add(remaining);

    FlushBatch(batch);
}
```

#### Flushing

```csharp
private void FlushBatch(List<LogMessage> batch)
{
    foreach (var message in batch)
    {
        try
        {
            _inner.Append(message);
        }
        catch (Exception)
        {
            // Swallow to keep the consumer alive
        }
    }
}
```

Each message is forwarded to the inner appender's `Append()` — which does the actual formatting and I/O (console write, file write, etc.). The `try/catch` per message ensures a transient failure (e.g., disk full) doesn't kill the consumer thread.

#### Graceful Shutdown

Same pattern as `AsyncLogger` — signal completion, wait for the consumer to drain and exit:

```csharp
public void Dispose()
{
    _queue.CompleteAdding();
    _consumerThread.Join();
    _queue.Dispose();
}
```

#### How It All Wires Together

The key insight is that `AsyncAppender` implements `IAppender`, so it's transparent to the `Logger`. You wrap each concrete appender individually:

```csharp
LoggerManager loggerManager = LoggerManager.GetInstance();
var mainControllerLogger = loggerManager.GetOrAddLogger("mainControllerLogger");

// Each appender gets its own async queue + background thread
using var asyncConsole = new AsyncAppender(
    new ConsoleAppender(new TextFormatter()), batchSize: 5, flushIntervalMs: 500);
using var asyncFile = new AsyncAppender(
    new FileAppender("./logs", new TextFormatter()), batchSize: 5, flushIntervalMs: 500);

mainControllerLogger.AddMinimumLevel(LogLevel.Info)
                    .AddAppender(asyncConsole)
                    .AddAppender(asyncFile);

mainControllerLogger.Info("This message is dispatched to both appenders");
mainControllerLogger.Error("Each appender flushes independently", new Exception("oops"));
```

The flow for a single `logger.Info(...)` call:

```
Caller thread
  │
  └─ Logger.Log()
       │
       ├─ Level check passes (Info >= Info)
       │
       ├─ Iterates _appenders (copy-on-write snapshot)
       │    │
       │    ├─ asyncConsole.Append(msg)     → enqueues into console queue (non-blocking)
       │    │
       │    └─ asyncFile.Append(msg)        → enqueues into file queue (non-blocking)
       │
       └─ Returns immediately to caller

Console consumer thread              File consumer thread
  │                                     │
  ├─ TryTake from console queue         ├─ TryTake from file queue
  ├─ Batch up to N messages             ├─ Batch up to N messages
  ├─ ConsoleAppender.Append(msg)        ├─ FileAppender.Append(msg)
  │    └─ Console.WriteLine(...)        │    └─ StreamWriter.WriteLine(...)
  └─ Loop                              └─ Loop
```

On shutdown, the `using` declarations ensure both `AsyncAppender` instances are disposed — each signals its queue, waits for its consumer thread to drain, and exits cleanly. No messages are lost.

### Manual TryTake Loop vs GetConsumingEnumerable

`BlockingCollection<T>` provides a built-in `GetConsumingEnumerable()` method that handles the consume-until-complete pattern out of the box. A simple consumer using it looks like this:

```csharp
private void ProcessQueue()
{
    // Blocks when queue is empty, yields items as they arrive,
    // and exits automatically when CompleteAdding() is called.
    foreach (var message in _queue.GetConsumingEnumerable())
    {
        try
        {
            _inner.Append(message);
        }
        catch (Exception)
        {
            // Swallow to keep consumer alive
        }
    }
}
```

This is clean, correct, and handles graceful shutdown automatically — no manual `IsCompleted` check, no `InvalidOperationException` catch, no final drain loop. When `CompleteAdding()` is called, the enumerable finishes yielding remaining items and the `foreach` exits.

However, our implementation uses a manual `TryTake` loop instead:

```csharp
private void ProcessQueue()
{
    var batch = new List<LogMessage>(_batchSize);

    while (!_queue.IsCompleted)
    {
        batch.Clear();

        try
        {
            // Block for up to _flushInterval waiting for the first item
            if (_queue.TryTake(out var first, _flushInterval))
            {
                batch.Add(first);

                // Greedily drain up to batchSize without blocking
                while (batch.Count < _batchSize && _queue.TryTake(out var next))
                {
                    batch.Add(next);
                }
            }
        }
        catch (InvalidOperationException)
        {
            break;
        }

        FlushBatch(batch);
    }

    // Final drain after CompleteAdding
    while (_queue.TryTake(out var remaining))
        batch.Add(remaining);

    FlushBatch(batch);
}
```

The two key reasons we chose the manual approach:

#### 1. Batching

`GetConsumingEnumerable` yields one item at a time. There's no way to say "give me up to N items at once." Our manual loop collects up to `_batchSize` messages per iteration before flushing, which reduces per-message I/O overhead:

```csharp
// GetConsumingEnumerable: 1 Append call per message
foreach (var message in _queue.GetConsumingEnumerable())
    _inner.Append(message);   // file write, flush, syscall — every single time

// Manual loop: N Append calls per batch
FlushBatch(batch);   // N messages written back-to-back, fewer syscalls
```

For a file appender doing `StreamWriter.WriteLine` + `Flush`, batching means multiple writes happen before the OS buffer flushes — measurably less overhead under load.

#### 2. Flush Interval Timeout

`GetConsumingEnumerable` blocks indefinitely when the queue is empty — it will wait forever until an item arrives or `CompleteAdding()` is called. There's no timeout control.

Our manual loop uses `TryTake(out var first, _flushInterval)` which returns `false` after the timeout. This guarantees that even during low-traffic periods, a message won't sit in the queue longer than `_flushInterval` before being flushed:

```csharp
// GetConsumingEnumerable: blocks forever until next item
foreach (var message in _queue.GetConsumingEnumerable())  // no timeout possible

// Manual loop: wakes up every _flushInterval even if queue is empty
if (_queue.TryTake(out var first, _flushInterval))  // returns false after timeout
```

This matters for latency-sensitive logging — if a single error message is logged during a quiet period, you want it flushed within a bounded time, not sitting in the queue until the next burst of messages arrives.

#### When to Use Which

| | `GetConsumingEnumerable` | Manual `TryTake` loop |
|---|---|---|
| Items per iteration | 1 | Up to `batchSize` |
| Timeout control | No (blocks indefinitely) | Yes (`flushInterval`) |
| Graceful shutdown | Automatic (enumerable ends) | Manual drain (Step 3d) |
| Code complexity | Minimal (~5 lines) | Moderate (~25 lines) |
| Best for | Simple consumers, low volume | Batched, latency-aware consumers |

If you don't need batching or time-based flushing — for example, a simple task queue or a low-volume consumer — `GetConsumingEnumerable` is the right choice. Less code, less room for bugs. But for a logging framework where throughput and flush latency both matter, the manual loop gives us the control we need.

---

## Project Structure

```
├── Core/
│   ├── LogLevel.cs          # Enum: Debug, Info, Warn, Error, Fatal
│   ├── LogMessage.cs        # Value object for a log entry
│   ├── ILogger.cs           # Logger interface (enables Decorator pattern)
│   ├── Logger.cs            # Synchronous logger with copy-on-write appenders
│   ├── AsyncLogger.cs       # Async decorator with producer-consumer batching
│   └── LoggerManager.cs     # Singleton logger registry
├── Appenders/
│   ├── IAppender.cs         # Appender interface
│   ├── AppenderBase.cs      # Abstract base with shared formatter logic
│   ├── AsyncAppender.cs     # Async decorator with per-appender queue + thread
│   ├── ConsoleAppender.cs   # Writes to stdout
│   ├── FileAppender.cs      # Writes to timestamped log files
│   └── DatabaseAppender.cs  # Placeholder for future implementation
├── Formatters/
│   ├── IFormatter.cs        # Formatter interface
│   └── TextFormatter.cs     # Human-readable text format
└── Program.cs               # Demo / entry point
```
