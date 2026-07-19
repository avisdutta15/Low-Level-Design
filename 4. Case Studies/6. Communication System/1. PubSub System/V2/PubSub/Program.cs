using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;

// V1 was push model.
// Topic has the information of registered subscribers
// V2 is pull model
// Topic is just a log
// It has Append and Read capability
// Subscriber decides when and how to read from topic.
// Each subscriber pulls messages from topics at their own pace.
// Each subscriber maintains an offset into each topic it reads from.
// Offset management of each subscriber is done by MessageBroker.
// The key shift from V1 is the push → pull model.
// V1 delivered messages immediately to all subscribers on Publish().
// Now Publish() just appends to the log, and each subscriber decides when to call Poll()
// to consume at their own pace — exactly like Kafka consumer groups.

public class Message
{
    public string TopicName { get; }
    public string Payload { get; }
    public DateTime TimeStamp { get; }

    public Message(string topicName, string payload, DateTime timeStamp)
    {
        TopicName = topicName;
        Payload = payload;
        TimeStamp = timeStamp;
    }

    public override string ToString() => $"[{TimeStamp:HH:mm:ss}] {TopicName}: {Payload}";
}

// Topic acts as an append-only log; subscribers read at their own offset
// Instead of pushing to subscribers immediately on publish, messages are now stored in a List<Message>
public class Topic
{
    private string _name;
    private readonly object _lock = new();
    private readonly List<Message> _log = new();

    public Topic(string name)
    {
        _name = name;
    }

    // Appends a new message to the log under a lock
    public void Append(Message message)
    {
        lock (_lock)
        {
            _log.Add(message);
        }            
    }

    // Returns a readonly list of messages offset onwards
    // Read(offset) slices the log from a given position and returns the
    // new offset(old offset + how many messages were read).
    // The list index is the offset.
    public (IReadOnlyList<Message> messages, int newOffset) Read(int offset)
    {
        lock (_lock)
        {
            var slice = _log.Skip(offset).ToList();
            int newOffset = offset + slice.Count;
            return (slice, newOffset);
        }
    }
}

public abstract class Subscriber
{
    public abstract void OnMessages(IReadOnlyList<Message> messages);
}

public class ConsoleSubscriber : Subscriber
{
    private string _name;
    public ConsoleSubscriber(string name)
    {
        _name = name;
    }

    public override void OnMessages(IReadOnlyList<Message> messages)
    {
        foreach (var msg in messages)
            Console.WriteLine($"[{_name}] received: {msg}");
    }

    public override string ToString()
    {
        return $"{_name}";
    }
}

public sealed class MessageBroker
{
    private ImmutableDictionary<string, Topic> _topics = ImmutableDictionary<string, Topic>.Empty;

    // offsets[subscriber][topicName, current read offset]
    // This is a two-level dictionary: subscriber → topic → current offset.
    // When a subscriber subscribes, their offset for that topic starts at 0:
    private readonly ConcurrentDictionary<Subscriber, ConcurrentDictionary<string, int>> _offsets = new();

    private static MessageBroker? _instance = null;
    private static readonly object _lock = new object();

    private MessageBroker() { }

    public static MessageBroker GetInstance()
    {
        lock (_lock)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance = new MessageBroker();
                }
            }
            return _instance;
        }
    }

    // Thread-safe topic registration
    public void AddTopic(string topicName)
    {
        ImmutableInterlocked.TryAdd(ref _topics, topicName, new Topic(topicName));
    }

    public Topic GetTopic(string topicName)
    {
        // check if the topic exists
        if (_topics.TryGetValue(topicName, out var topic) == true)
            return topic;
        throw new ArgumentException($"Topic '{topicName}' not found");
    }   


    public void Subscribe(string topicName, Subscriber subscriber)
    {
        // check if the topic exists
        if (_topics.TryGetValue(topicName, out var topic) == false)
            throw new ArgumentException($"Topic '{topicName}' not found");

        // if the subscriber exists then return 
        // else add a new concurrentdictionary with subscriber as key and 0 as offset value
        _offsets.GetOrAdd(subscriber, (x) => {
            // create a new dictionary if subscriber is missing
            var dic = new ConcurrentDictionary<string, int>();
            dic.TryAdd(topicName, 0);
            return dic;
        });
    }

    // Removes the topic offset entry for the subscriber; removes subscriber entirely if no topics remain
    public void Unsubscribe(string topicName, Subscriber subscriber)
    {
        // remove the subscriber from the subscriber offset
        if (_offsets.TryGetValue(subscriber, out var topicOffsets))
        {
            topicOffsets.TryRemove(topicName, out var offset);
        }
        _offsets.TryRemove(subscriber, out _);
    }

    // Publish now just appends the message to the topic's log
    public void Publish(string topicName, Message message)
    {
        // check if the topic exists
        if (_topics.TryGetValue(topicName, out var topic) == false)
            throw new ArgumentException($"Topic '{topicName}' not found");
        topic.Append(message);
    }

    // Poll() ties it together
    public void Poll(string topicName, Subscriber subscriber)
    {
        // check if the topic exists
        if (_topics.TryGetValue(topicName, out var topic) == false)
            throw new ArgumentException($"Topic '{topicName}' not found");

        // check if the subscriber is subscribed to the topic
        if (!_offsets.TryGetValue(subscriber, out var topicOffsets) ||
            !topicOffsets.TryGetValue(topicName, out var offset))
            throw new InvalidOperationException($"{subscriber} is not subscribed to '{topicName}'");

        // Read from the topic starting from the offset.
        var (messages, newOffset) = topic.Read(offset);
        topicOffsets[topicName] = newOffset;    // advance the offset

        if (messages.Count > 0)
            subscriber.OnMessages(messages);
        else
            Console.WriteLine($"[{subscriber}] no new messages on '{topicName}' (at offset {offset})");
    }
}

public class Publisher
{
    private readonly MessageBroker _broker;
    public Publisher(MessageBroker broker) => _broker = broker;

    public void Publish(string topicName, Message message) =>
        _broker.Publish(topicName, message);
}

public class Program
{
    public static void Main(string[] args)
    {
        var broker = MessageBroker.GetInstance();
        broker.AddTopic("Sports");
        broker.AddTopic("News");

        var s1 = new ConsoleSubscriber("S1");
        var s2 = new ConsoleSubscriber("S2");
        var s3 = new ConsoleSubscriber("S3");

        broker.Subscribe("Sports", s1);
        broker.Subscribe("Sports", s2);
        broker.Subscribe("News", s2);
        broker.Subscribe("News", s3);

        var publisher = new Publisher(broker);

        publisher.Publish("Sports", new Message("Sports", "Match started", DateTime.Now));
        publisher.Publish("Sports", new Message("Sports", "Goal scored!", DateTime.Now));
        publisher.Publish("Sports", new Message("Sports", "Half time", DateTime.Now));
        publisher.Publish("News", new Message("News", "Breaking news", DateTime.Now));
        publisher.Publish("News", new Message("News", "Weather update", DateTime.Now));

        Console.WriteLine("--- S1 polls Sports (reads all 3) ---");
        broker.Poll("Sports", s1);

        Console.WriteLine("\n--- S2 polls Sports (reads all 3) ---");
        broker.Poll("Sports", s2);

        publisher.Publish("Sports", new Message("Sports", "Full time", DateTime.Now));

        Console.WriteLine("\n--- S1 polls Sports again (reads only 'Full time') ---");
        broker.Poll("Sports", s1);

        Console.WriteLine("\n--- S2 polls Sports again (reads only 'Full time') ---");
        broker.Poll("Sports", s2);

        Console.WriteLine("\n--- S2 polls News (reads both) ---");
        broker.Poll("News", s2);

        Console.WriteLine("\n--- S3 polls News independently (reads both) ---");
        broker.Poll("News", s3);
    }
}