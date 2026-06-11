/* Key changes from Thread to Task:
 * ISubscriber.ConsumeAsync() returns Task for async operations
 * SubscriberWorker uses Task.Run() instead of new Thread()
 * SemaphoreSlim replaces Monitor.Wait/Pulse for signaling
 * CancellationTokenSource enables graceful shutdown
 * StopAsync() method for proper cleanup
 * Main is now async to properly await operations
 * 
 * The Task-based approach is more modern, integrates better with async/await patterns, and provides better 
 * resource management with proper disposal. 
 */

public record Message(string message);

public interface ISubscriber
{
    public string Id { get; }
    public Task ConsumeAsync(Message message);
}

public class Subscriber : ISubscriber
{
    public string Id { get; }
    public Subscriber(string id)
    {
        Id = id;
    }

    public Task ConsumeAsync(Message message)
    {
        Console.WriteLine($"[{Id}] : {message}");
        return Task.CompletedTask;
    }
}

public class SleepySubscriber : ISubscriber
{
    public string Id { get; }
    private readonly int _sleepTimeMs;

    public SleepySubscriber(string id, int sleepTimeMs = 0)
    {
        Id = id;
        _sleepTimeMs = sleepTimeMs;
    }

    public async Task ConsumeAsync(Message message)
    {
        Console.WriteLine($"[{Id}] Started consuming: {message}");
        if (_sleepTimeMs > 0)
        {
            await Task.Delay(_sleepTimeMs);
        }
        Console.WriteLine($"[{Id}] Done consuming: {message}");
    }
}

public class SubscriberOffSet
{
    public int OffSet { get; set; }
    public ISubscriber Subscriber { get; set; }

    public SubscriberOffSet(int offSet, ISubscriber subscriber)
    {
        OffSet = offSet;
        Subscriber = subscriber;
    }
}

public class SubscriberWorker
{
    private readonly List<Message> _messageLog;
    private readonly SubscriberOffSet _subscriberOffset;
    private readonly SemaphoreSlim _signal;
    private readonly CancellationTokenSource _cts;
    private Task? _workerTask;

    public SubscriberWorker(List<Message> messageLog, SubscriberOffSet subscriberOffSet)
    {
        _messageLog = messageLog;
        _subscriberOffset = subscriberOffSet;
        _signal = new SemaphoreSlim(0);
        _cts = new CancellationTokenSource();
    }

    public void Start()
    {
        _workerTask = Task.Run(async () => await RunAsync());
    }

    private async Task RunAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                int currentOffSet;
                lock (_subscriberOffset)
                {
                    currentOffSet = _subscriberOffset.OffSet;
                }

                while (currentOffSet >= _messageLog.Count && !_cts.Token.IsCancellationRequested)
                {
                    await _signal.WaitAsync(_cts.Token);
                    lock (_subscriberOffset)
                    {
                        currentOffSet = _subscriberOffset.OffSet;
                    }
                }

                if (_cts.Token.IsCancellationRequested)
                    break;

                Message message;
                lock (_messageLog)
                {
                    message = _messageLog[currentOffSet];
                }

                await _subscriberOffset.Subscriber.ConsumeAsync(message);

                lock (_subscriberOffset)
                {
                    _subscriberOffset.OffSet++;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker Error: {ex.Message}");
            }
        }
    }

    public void WakeUp()
    {
        _signal.Release();
    }

    public async Task StopAsync()
    {
        _cts.Cancel();
        _signal.Release();
        if (_workerTask != null)
        {
            await _workerTask;
        }
        _signal.Dispose();
        _cts.Dispose();
    }
}

public class Topic
{
    private Dictionary<string, SubscriberOffSet> _subscriberOffsets = new();
    private Dictionary<string, SubscriberWorker> _subscriberWorkers = new();
    private List<Message> _messageLog;
    public string Name { get; set; }

    public Topic(string name)
    {
        Name = name;
        _messageLog = new List<Message>();
    }

    public void Subscribe(ISubscriber subscriber)
    {
        var subscriberOffset = new SubscriberOffSet(0, subscriber);
        _subscriberOffsets.Add(subscriber.Id, subscriberOffset);

        var workerTask = new SubscriberWorker(_messageLog, subscriberOffset);
        _subscriberWorkers[subscriber.Id] = workerTask;
        workerTask.Start();

        Console.WriteLine($"[{subscriber.Id}] subscribed with worker task");
    }

    public void Publish(Message message)
    {
        lock (_messageLog)
        {
            _messageLog.Add(message);
        }

        foreach (var worker in _subscriberWorkers.Values)
        {
            worker.WakeUp();
        }
    }

    public void ResetOffset(ISubscriber subscriber, int newOffset)
    {
        if (_subscriberOffsets.TryGetValue(subscriber.Id, out SubscriberOffSet? subscriberOffset))
        {
            lock (subscriberOffset)
            {
                subscriberOffset.OffSet = newOffset;
                _subscriberWorkers[subscriberOffset.Subscriber.Id].WakeUp();
                Console.WriteLine($"[{subscriber.Id}] Offset reset to: {newOffset}");
            }
        }
    }

    public void ShowOffsets()
    {
        foreach (var subscriberOffset in _subscriberOffsets)
        {
            Console.WriteLine($"Subscriber {subscriberOffset.Value.Subscriber.Id} : {subscriberOffset.Value.OffSet}");
        }
    }

    public async Task StopAllWorkersAsync()
    {
        var stopTasks = _subscriberWorkers.Values.Select(w => w.StopAsync());
        await Task.WhenAll(stopTasks);
    }
}

public class MessageBroker
{
    private Dictionary<string, Topic> _topics;

    public MessageBroker()
    {
        _topics = new Dictionary<string, Topic>();
    }

    public Topic CreateTopic(string topicName)
    {
        var topic = new Topic(topicName);
        _topics.Add(topicName, topic);
        return topic;
    }

    public void SendMessage(Topic topic, Message message)
    {
        _topics[topic.Name].Publish(message);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== DESIGN 4: Pub/Sub with Worker Tasks ===\n");

        var broker = new MessageBroker();
        var topic = broker.CreateTopic("orders");

        var sub1 = new SleepySubscriber("S1", 1000);
        var sub2 = new SleepySubscriber("S2", 2000);

        topic.Subscribe(sub1);
        topic.Subscribe(sub2);

        topic.Publish(new Message("order-1"));
        topic.Publish(new Message("order-2"));
        topic.Publish(new Message("order-3"));

        await Task.Delay(5000);

        Console.WriteLine("\n--- Resetting S1 offset to replay ---");
        topic.ResetOffset(sub1, 0);

        await Task.Delay(4000);

        Console.WriteLine("\n--- Stopping all workers ---");
        await topic.StopAllWorkersAsync();
    }
}
