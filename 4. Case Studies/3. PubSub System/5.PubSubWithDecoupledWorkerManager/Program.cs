/* Key Improvements:
 * SubscriberWorkerManager - New component that:
 *      Manages all worker thread lifecycle (creation, starting, stopping)
 *      Maintains the mapping between subscribers and their workers
 *      Handles offset management and notifications
 *      Provides clean shutdown mechanism
 * Topic - Now simplified to:
 *      Store messages in the log
 *      Coordinate subscriptions (delegates to manager)
 *      Publish messages (delegates notification to manager)
 *      No direct thread management
 *      
 * Benefits of this architecture:
 * Single Responsibility: Topic handles messaging, Manager handles threading
 * Testability: Can test Topic without dealing with threads
 * Flexibility: Easy to swap threading strategy without changing Topic
 * Resource Management: Centralized worker lifecycle with proper shutdown
 * Maintainability: Clear boundaries between components
 * 
 * The SubscriberWorkerManager acts as a facade that encapsulates all the complexity of worker thread management, 
 * making the system more modular and easier to reason about.
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
    public int OffSet { get; set; }
    public ISubscriber Subscriber { get; set; }

    public SubscriberOffSet(int offSet, ISubscriber subscriber)
    {
        OffSet = offSet;
        Subscriber = subscriber;
    }
}

// Worker thread that processes messages for a single subscriber
public class SubscriberWorker
{
    private readonly List<Message> _messageLog;
    private readonly SubscriberOffSet _subscriberOffset;
    private readonly Thread _workerThread;
    private volatile bool _isRunning;

    public SubscriberWorker(List<Message> messageLog, SubscriberOffSet subscriberOffSet)
    {
        _messageLog = messageLog;
        _subscriberOffset = subscriberOffSet;
        _isRunning = false;
        _workerThread = new Thread(Run)
        {
            Name = $"Worker-{subscriberOffSet.Subscriber.Id}",
            IsBackground = true
        };
    }

    public void Start()
    {
        _isRunning = true;
        _workerThread.Start();
    }

    private void Run()
    {
        lock (_subscriberOffset)
        {
            while (_isRunning)
            {
                try
                {
                    var currentOffSet = _subscriberOffset.OffSet;
                    
                    while (currentOffSet >= _messageLog.Count && _isRunning)
                    {
                        Monitor.Wait(_subscriberOffset);    //Put the worker thread to sleep
                        if (!_isRunning) break;             //Check if the worker is still running or being stopped by Stop()
                        currentOffSet = _subscriberOffset.OffSet;   //Read the current offset again as it may have been changed by the ResetOffset()
                    }

                    //Check if the worker is still running or being stopped by Stop()
                    if (!_isRunning) break;

                    //Read the message
                    Message message;
                    lock (_messageLog)
                    {
                        message = _messageLog[currentOffSet];
                    }

                    //Consume the message and increment offset
                    _subscriberOffset.Subscriber.Consume(message);
                    _subscriberOffset.OffSet++;
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker Error [{_subscriberOffset.Subscriber.Id}]: {ex.Message}");
                }
            }
        }
    }

    public void WakeUp()
    {
        lock (_subscriberOffset)
        {
            Monitor.Pulse(_subscriberOffset);
        }
    }

    public void Stop()
    {
        _isRunning = false;
        lock (_subscriberOffset)
        {
            Monitor.Pulse(_subscriberOffset);
        }
    }
}

// Manages the lifecycle of all subscriber workers
public class SubscriberWorkerManager
{
    private readonly Dictionary<string, SubscriberWorker> _workers;
    private readonly Dictionary<string, SubscriberOffSet> _subscriberOffsets;
    private readonly List<Message> _messageLog;

    public SubscriberWorkerManager(List<Message> messageLog)
    {
        _messageLog = messageLog;
        _workers = new Dictionary<string, SubscriberWorker>();
        _subscriberOffsets = new Dictionary<string, SubscriberOffSet>();
    }

    public void RegisterSubscriber(ISubscriber subscriber)
    {
        if (_workers.ContainsKey(subscriber.Id))
        {
            Console.WriteLine($"[{subscriber.Id}] already registered");
            return;
        }

        var subscriberOffset = new SubscriberOffSet(0, subscriber);
        _subscriberOffsets.Add(subscriber.Id, subscriberOffset);

        var worker = new SubscriberWorker(_messageLog, subscriberOffset);
        _workers.Add(subscriber.Id, worker);
        worker.Start();

        Console.WriteLine($"[{subscriber.Id}] registered with worker thread");
    }

    public void NotifyNewMessage()
    {
        foreach (var worker in _workers.Values)
        {
            worker.WakeUp();
        }
    }

    public void ResetOffset(string subscriberId, int newOffset)
    {
        if (_subscriberOffsets.TryGetValue(subscriberId, out var subscriberOffset))
        {
            lock (subscriberOffset)
            {
                subscriberOffset.OffSet = newOffset;
                _workers[subscriberId].WakeUp();
                Console.WriteLine($"[{subscriberId}] Offset reset to: {newOffset}");
            }
        }
    }

    public void ShowOffsets()
    {
        foreach (var kvp in _subscriberOffsets)
        {
            Console.WriteLine($"Subscriber {kvp.Key} : {kvp.Value.OffSet}");
        }
    }

    public void StopAll()
    {
        foreach (var worker in _workers.Values)
        {
            worker.Stop();
        }
        Console.WriteLine("All workers stopped");
    }
}

// Topic now focuses only on message storage and subscription coordination
public class Topic
{
    private readonly List<Message> _messageLog;
    private readonly SubscriberWorkerManager _workerManager;
    public string Name { get; set; }

    public Topic(string name)
    {
        Name = name;
        _messageLog = new List<Message>();
        _workerManager = new SubscriberWorkerManager(_messageLog);
    }

    public void Subscribe(ISubscriber subscriber)
    {
        _workerManager.RegisterSubscriber(subscriber);
    }

    public void Publish(Message message)
    {
        lock (_messageLog)
        {
            _messageLog.Add(message);
        }
        _workerManager.NotifyNewMessage();
    }

    public void ResetOffset(ISubscriber subscriber, int newOffset)
    {
        _workerManager.ResetOffset(subscriber.Id, newOffset);
    }

    public void ShowOffsets()
    {
        _workerManager.ShowOffsets();
    }

    public void Shutdown()
    {
        _workerManager.StopAll();
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
        Console.WriteLine("=== DESIGN 5: Pub/Sub with Decoupled Worker Manager ===\n");

        var broker = new MessageBroker();
        var topic = broker.CreateTopic("orders");

        var sub1 = new SleepySubscriber("S1", 1000);
        var sub2 = new SleepySubscriber("S2", 2000);

        topic.Subscribe(sub1);
        topic.Subscribe(sub2);

        topic.Publish(new Message("order-1"));
        topic.Publish(new Message("order-2"));
        topic.Publish(new Message("order-3"));

        Thread.Sleep(5000);

        Console.WriteLine("\n--- Resetting S1 offset to replay ---");
        topic.ResetOffset(sub1, 0);

        Thread.Sleep(4000);

        Console.WriteLine("\n--- Shutting down ---");
        topic.Shutdown();
        
        Thread.Sleep(500);
        Console.WriteLine("Program complete");
    }
}
