using System.Collections.Concurrent;
using _1.BasicLoggigFramework.Core;

namespace _1.BasicLoggigFramework.Appenders;

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
            Name = $"AsyncAppender-{inner.GetType().Name}"
        };
        _consumerThread.Start();
    }

    public void Append(LogMessage message)
    {
        if (!_queue.IsAddingCompleted)
            _queue.Add(message);
    }

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

        // Drain remaining messages after completion signal
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
                _inner.Append(message);
            }
            catch (Exception)
            {
                // Swallow to keep the consumer alive
            }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _consumerThread.Join();
        _queue.Dispose();
    }
}
