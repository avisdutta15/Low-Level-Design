using System.Collections.Concurrent;

namespace _1.BasicLoggigFramework.Core;

public class AsyncLogger : ILogger, IDisposable
{
    private readonly ILogger _logger;
    private readonly BlockingCollection<LogMessage> _queue;
    private readonly Thread _consumerThread;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    public AsyncLogger(ILogger inner, int batchSize = 10, int flushIntervalMs = 1000, int boundedCapacity = 10000)
    {
        _logger = inner;
        _batchSize = batchSize;
        _flushInterval = TimeSpan.FromMilliseconds(flushIntervalMs);
        _queue = new BlockingCollection<LogMessage>(boundedCapacity);

        _consumerThread = new Thread(ProcessQueue)
        {
            IsBackground = true,
            Name = "AsyncLogger-Consumer"
        };
        _consumerThread.Start();
    }

    private void Enqueue(LogMessage message)
    {
        if (!_queue.IsAddingCompleted)
            _queue.Add(message);
    }

    private void ProcessQueue()
    {
        var batch = new List<LogMessage>(_batchSize);

        // Keep processing until the queue is not complete (i.e. AsyncLogger not disposed)
        while (!_queue.IsCompleted)
        {
            batch.Clear();

            try
            {
                // Block until at least one item is available
                if (_queue.TryTake(out var first, _flushInterval))
                {
                    batch.Add(first);

                    // Drain up to batchSize without blocking
                    while (batch.Count < _batchSize && _queue.TryTake(out var next))
                    {
                        batch.Add(next);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Queue was marked complete while waiting
                break;
            }

            FlushBatch(batch);
        }

        // Drain any remaining items after completion (i.e. disposal of AsyncLogger is triggered)
        while (_queue.TryTake(out var remaining))
            batch.Add(remaining);

        FlushBatch(batch);
    }

    private void FlushBatch(List<LogMessage> batch)
    {
        foreach (var message in batch)
        {
            try
            {
                switch (message.Level)
                {
                    case LogLevel.Debug: _logger.Debug(message.Message, message.Exception); break;
                    case LogLevel.Info:  _logger.Info(message.Message, message.Exception);  break;
                    case LogLevel.Warn:  _logger.Warn(message.Message, message.Exception);  break;
                    case LogLevel.Error: _logger.Error(message.Message, message.Exception); break;
                    case LogLevel.Fatal: _logger.Fatal(message.Message, message.Exception); break;
                }
            }
            catch (Exception)
            {
                // Swallow to keep the consumer alive
            }
        }
    }

    public void Debug(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Debug, message, ex));
    public void Info(string message, Exception? ex = null)  => Enqueue(new LogMessage(LogLevel.Info, message, ex));
    public void Warn(string message, Exception? ex = null)  => Enqueue(new LogMessage(LogLevel.Warn, message, ex));
    public void Error(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Error, message, ex));
    public void Fatal(string message, Exception? ex = null) => Enqueue(new LogMessage(LogLevel.Fatal, message, ex));

    public void Dispose()
    {
        _queue.CompleteAdding();
        _consumerThread.Join();
        _queue.Dispose();
    }
}
