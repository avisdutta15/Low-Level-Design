/* Issues Found:
 * Tight Coupling: The Topic class is directly responsible for creating and managing SubscriberWorker threads. This violates the Single Responsibility Principle - Topic should manage subscriptions and message distribution, not thread lifecycle.
 * Thread Safety Issue: The ref keyword in SubscriberWorker constructor is unnecessary and potentially problematic. The _messageLog is already a reference type (List).
 * No Graceful Shutdown: There's no mechanism to stop worker threads cleanly. They run indefinitely with no way to signal termination.
 * Lock Ordering Risk: Multiple locks (_messageLog, _subscriberOffset) could lead to deadlocks if not carefully managed.
 * Resource Leak: Worker threads are never stopped or disposed, which could cause issues in a real application.
 * 
 * Recommended Decoupling:
 * Yes, you should decouple the Topic from SubscriberWorker management. Here's why:
 *      Separation of Concerns: Topic should handle message storage and subscription management. A separate component should handle worker thread lifecycle.
 *      Testability: Easier to test Topic logic without dealing with threading complexity.
 *      Flexibility: You could swap out the worker implementation (e.g., use Task-based async instead of threads) without changing Topic.
 *      Resource Management: Better control over thread lifecycle and cleanup.
 */

public record Message(string message);

public interface ISubscriber
{
    public string Id { get; }
    public void Consume(Message message);
}

public class Subscriber : ISubscriber
{
    public string Id { get; }
    public Subscriber(string id)
    {
        Id = id;
    }

    public void Consume(Message message)
    {
        Console.WriteLine($"[{Id}] : {message}");
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

    public void Consume(Message message)
    {
        Console.WriteLine($"[{Id}] Started consuming: {message}");
        if (_sleepTimeMs > 0)
        {
            Thread.Sleep(_sleepTimeMs);
        }
        Console.WriteLine($"[{Id}] Done consuming: {message}");
    }
}

public class SubscriberOffSet
{
    public int OffSet {  get; set; }
    public ISubscriber Subscriber { get; set; }

    public SubscriberOffSet(int offSet, ISubscriber subscriber)
    {
        OffSet = offSet;
        Subscriber = subscriber;
    }
}

//This will have a reference to the message log.
//On Creation it will start as a background thread and start consuming from the messageLog
public class SubscriberWorker
{
    private readonly List<Message> _messageLog;
    private SubscriberOffSet _subscriberOffset;
    private Thread _workerThread;
    public SubscriberWorker(ref List<Message> messageLog, SubscriberOffSet subscriberOffSet)
    {
        _messageLog = messageLog;
        _subscriberOffset = subscriberOffSet;
        _workerThread = new Thread(Run)
        { 
                Name = $"Worker-{subscriberOffSet.Subscriber.Id}", 
                IsBackground = true 
        };
    }

    public void Start()
    {
        _workerThread.Start();
    }

    public void Run()
    {
        lock (_subscriberOffset)
        {
            while (true)
            {
                try
                {
                    var currentOffSet = _subscriberOffset.OffSet;
                    while (currentOffSet >= _messageLog.Count)
                    {
                        Monitor.Wait(_subscriberOffset);    //Sleep until notified
                        currentOffSet = _subscriberOffset.OffSet;
                    }

                    //Read message
                    Message message;
                    lock (_messageLog)
                    {
                        message = _messageLog[currentOffSet];
                    }

                    //Consume message
                    _subscriberOffset.Subscriber.Consume(message);
                    _subscriberOffset.OffSet++;
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker Error: {ex.Message}");
                }
            }
        }
    }

    public void WakeUp()
    {
        lock (_subscriberOffset)
        {
            Monitor.Pulse(_subscriberOffset); //wake up the worker
        }
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

        // Create and start worker thread
        var workerThread = new SubscriberWorker(ref _messageLog, subscriberOffset);
        _subscriberWorkers[subscriber.Id] = workerThread;
        workerThread.Start();

        Console.WriteLine($"[{subscriber.Id}] subscribed with worker thread");
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
    public static void Main(string[] args)
    {
        Console.WriteLine("=== DESIGN 4: Pub/Sub with Worker Threads ===\n");

        var broker = new MessageBroker();
        var topic = broker.CreateTopic("orders");

        var sub1 = new SleepySubscriber("S1", 1000); // 1 sec delay
        var sub2 = new SleepySubscriber("S2", 2000); // 2 sec delay

        topic.Subscribe(sub1);
        topic.Subscribe(sub2);

        // Publish messages - workers consume automatically
        topic.Publish(new Message("order-1"));
        topic.Publish(new Message("order-2"));
        topic.Publish(new Message("order-3"));

        Thread.Sleep(5000); // Let them process

        Console.WriteLine("\n--- Resetting S1 offset to replay ---");
        topic.ResetOffset(sub1, 0);

        Thread.Sleep(4000); // Let S1 replay
    }
}