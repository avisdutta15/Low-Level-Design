using System.Collections.Concurrent;

// V2 was pull model with offset-based consumption.
// V3 extends V2 with 3 features:
//
// 1. Durable Subscriptions & Message Replay
//      - Offsets persist across polls (subscriber can disconnect and reconnect)
//      - Replay() resets offset to 0 — re-reads entire log
//      - Seek(offset) jumps to any position in the log
//      - Works because Topic is append-only — messages are never deleted
//
// 2. Content Filtering
//      - MessageFilter class with a KeywordFilter string
//      - If KeywordFilter is set, only messages whose Payload contains the keyword are delivered
//      - Filter is applied broker-side inside Poll(), after reading from log, before delivery
//      - Offset advances past ALL messages (filtered or not) to avoid re-evaluating skipped ones
//
// 3. Delivery Acknowledgements & Retry
//      - Subscriber.OnMessages() returns bool: true = ACK, false = NACK
//      - On NACK, broker retries delivery up to MaxRetries (3) times
//      - Offset advances ONLY on ACK
//      - If all retries fail, offset stays — next Poll() re-attempts same batch
//      - This gives at-least-once delivery semantics
//
// Offset storage is same as V2: _offsets[subscriber][topicName] = int
// No partitioning — single log per topic (partitioning is V4)
//
// Message
//      - Id (short GUID for tracing)
//      - TopicName
//      - Payload
//      - TimeStamp
// Topic
//      - Append-only log (same as V2)
//      - Append(message) — adds to end of log
//      - Read(offset) — returns messages from offset onward + newOffset
// MessageFilter
//      - KeywordFilter (string, optional)
//      - Matches(message) — returns true if payload contains keyword
// Subscriber (abstract)
//      - Name
//      - Filter (optional MessageFilter)
//      - OnMessages(messages) → bool (ACK/NACK)
// MessageBroker
//      - _topics: topic registry
//      - _offsets[subscriber][topicName] = int (same V2 structure)
//      - Subscribe(topic, subscriber) — registers subscriber, sets offset to 0
//      - Publish(topic, message) — appends to topic log
//      - Poll(topic, subscriber):
//          1. Read all messages from subscriber's offset
//          2. Apply subscriber's filter
//          3. Deliver filtered batch to subscriber
//          4. If ACK → advance offset
//          5. If NACK → retry up to MaxRetries, then drop
//      - Replay(topic, subscriber) — sets offset to 0
//      - Seek(topic, subscriber, offset) — sets offset to any value
// Publisher
//      - Publish(topic, payload) — delegates to broker
//
// Overall Flow:
//      Publisher → MessageBroker.Publish() → Topic.Append(msg)
//      Subscriber → broker.Poll() → Topic.Read(offset) → Filter → Deliver → ACK? → Advance offset

// ─────────────────────────────────────────────
// Message
// ─────────────────────────────────────────────
public class Message
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string TopicName { get; }
    public string Payload { get; }
    public DateTime TimeStamp { get; }

    public Message(string topicName, string payload, DateTime timeStamp)
    {
        TopicName = topicName;
        Payload = payload;
        TimeStamp = timeStamp;
    }

    public override string ToString() => $"[{TimeStamp:HH:mm:ss}] {TopicName}: {Payload} (id:{Id})";
}

// ─────────────────────────────────────────────
// Topic — append-only log (same as V2)
// ─────────────────────────────────────────────
public class Topic
{
    public string Name { get; }
    private readonly List<Message> _log = new();
    private readonly object _lock = new();

    public Topic(string name) => Name = name;

    public void Append(Message message)
    {
        lock (_lock) { _log.Add(message); }
    }

    public (IReadOnlyList<Message> messages, int newOffset) Read(int offset)
    {
        lock (_lock)
        {
            var slice = _log.Skip(offset).ToList();
            return (slice, offset + slice.Count);
        }
    }
}

// ─────────────────────────────────────────────
// Subscription Filter — simple keyword matching
// ─────────────────────────────────────────────
public class MessageFilter
{
    // If set, message payload must contain this keyword (case-insensitive)
    public string? KeywordFilter { get; init; }

    public bool Matches(Message message)
    {
        if (KeywordFilter != null && message.Payload.Contains(KeywordFilter, StringComparison.OrdinalIgnoreCase)==false)
            return false;
        return true;
    }
}

// ─────────────────────────────────────────────
// Subscriber
// ─────────────────────────────────────────────
public abstract class Subscriber
{
    public string Name { get; }
    public MessageFilter? Filter { get; set; }

    protected Subscriber(string name) => Name = name;

    // Returns true = ACK, false = NACK (triggers retry)
    public abstract bool OnMessages(IReadOnlyList<Message> messages);

    public override string ToString() => Name;
}

public class ConsoleSubscriber : Subscriber
{
    private readonly bool _simulateFailure;
    private int _callCount;

    public ConsoleSubscriber(string name, bool simulateFailure = false) : base(name)
    {
        _simulateFailure = simulateFailure;
    }

    public override bool OnMessages(IReadOnlyList<Message> messages)
    {
        _callCount++;
        if (_simulateFailure && _callCount == 1)
        {
            Console.WriteLine($"  [{Name}] FAILED to process (will retry)");
            return false;
        }

        foreach (var msg in messages)
            Console.WriteLine($"  [{Name}] received: {msg}");
        return true;
    }
}

// ─────────────────────────────────────────────
// MessageBroker
// Uses V2-style offset map: subscriber → topic → int offset
// ─────────────────────────────────────────────
public class MessageBroker
{
    private readonly ConcurrentDictionary<string, Topic> _topics = new();
    private readonly ConcurrentDictionary<Subscriber, ConcurrentDictionary<string, int>> _offsets = new();

    private const int MaxRetries = 3;

    private static MessageBroker? _instance;
    private static readonly object _lock = new();

    private MessageBroker() { }

    public static MessageBroker GetInstance()
    {
        if (_instance == null)
            lock (_lock)
                _instance ??= new MessageBroker();
        return _instance;
    }

    public void AddTopic(string topicName)
    {
        _topics.TryAdd(topicName, new Topic(topicName));
    }

    public Topic GetTopic(string topicName)
    {
        if (_topics.TryGetValue(topicName, out var topic)) return topic;
        throw new ArgumentException($"Topic '{topicName}' not found");
    }

    public void Subscribe(string topicName, Subscriber subscriber)
    {
        _ = GetTopic(topicName); // validate topic exists
        var topicOffsets = _offsets.GetOrAdd(subscriber, _ => new ConcurrentDictionary<string, int>());
        topicOffsets.TryAdd(topicName, 0);
    }

    public void Unsubscribe(string topicName, Subscriber subscriber)
    {
        if (_offsets.TryGetValue(subscriber, out var topicOffsets))
            topicOffsets.TryRemove(topicName, out _);
    }

    public void Publish(string topicName, Message message)
    {
        GetTopic(topicName).Append(message);
    }

    // ── Poll with filtering + ACK/Retry ──
    public void Poll(string topicName, Subscriber subscriber)
    {
        var topic = GetTopic(topicName);

        if (!_offsets.TryGetValue(subscriber, out var topicOffsets) ||
            !topicOffsets.TryGetValue(topicName, out var offset))
            throw new InvalidOperationException($"{subscriber} not subscribed to '{topicName}'");

        var (messages, newOffset) = topic.Read(offset);

        // Apply filter
        var filtered = subscriber.Filter != null
            ? messages.Where(m => subscriber.Filter.Matches(m)).ToList()
            : messages.ToList();

        if (filtered.Count == 0)
        {
            // Still advance offset past unfiltered messages
            topicOffsets[topicName] = newOffset;
            Console.WriteLine($"  [{subscriber}] no new messages on '{topicName}'");
            return;
        }

        // Deliver with retry
        bool acked = false;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            acked = subscriber.OnMessages(filtered);
            if (acked) 
                break;
            Console.WriteLine($"  [{subscriber}] retry {attempt}/{MaxRetries} for '{topicName}'");
        }

        if (acked)
        {
            topicOffsets[topicName] = newOffset; // advance only on ACK
        }
        else
        {
            Console.WriteLine($"  [{subscriber}] DROPPED after {MaxRetries} retries on '{topicName}'");
        }
    }

    // ── Replay: reset offset to 0 ──
    public void Replay(string topicName, Subscriber subscriber)
    {
        if (_offsets.TryGetValue(subscriber, out var topicOffsets))
            topicOffsets[topicName] = 0;
    }

    // ── Seek to specific offset ──
    public void Seek(string topicName, Subscriber subscriber, int offset)
    {
        if (_offsets.TryGetValue(subscriber, out var topicOffsets))
            topicOffsets[topicName] = offset;
    }
}

// ─────────────────────────────────────────────
// Publisher
// ─────────────────────────────────────────────
public class Publisher
{
    private readonly MessageBroker _broker;
    public Publisher(MessageBroker broker) => _broker = broker;

    public void Publish(string topicName, string payload) =>
        _broker.Publish(topicName, new Message(topicName, payload, DateTime.Now));
}

// ─────────────────────────────────────────────
// Demo
// ─────────────────────────────────────────────
public class Program
{
    public static void Main(string[] args)
    {
        var broker = MessageBroker.GetInstance();
        broker.AddTopic("Sports");
        broker.AddTopic("News");

        var s1 = new ConsoleSubscriber("S1");
        var s2 = new ConsoleSubscriber("S2");
        var s3 = new ConsoleSubscriber("S3-Flaky", simulateFailure: true);

        // S2 only wants messages containing "goal"
        s2.Filter = new MessageFilter
        {
            KeywordFilter = "goal"
        };

        broker.Subscribe("Sports", s1);
        broker.Subscribe("Sports", s2);
        broker.Subscribe("Sports", s3);
        broker.Subscribe("News", s1);

        var publisher = new Publisher(broker);

        Console.WriteLine("=== Publishing messages ===");
        publisher.Publish("Sports", "Match started");
        publisher.Publish("Sports", "First goal scored!");
        publisher.Publish("Sports", "Yellow card");
        publisher.Publish("Sports", "Second goal!");
        publisher.Publish("News", "Weather update");

        // S1: no filter, gets all
        Console.WriteLine("\n=== S1 polls Sports (no filter) ===");
        broker.Poll("Sports", s1);

        // S2: filter active, only "goal" messages
        Console.WriteLine("\n=== S2 polls Sports (filter: 'goal' only) ===");
        broker.Poll("Sports", s2);

        // S3: fails first attempt, broker retries
        Console.WriteLine("\n=== S3-Flaky polls Sports (ACK/Retry) ===");
        broker.Poll("Sports", s3);

        // Replay: S1 resets offset, re-reads everything
        Console.WriteLine("\n=== S1 replays Sports from beginning ===");
        broker.Replay("Sports", s1);
        broker.Poll("Sports", s1);

        // Seek: S1 jumps to offset 2 (skips first two messages)
        Console.WriteLine("\n=== S1 seeks to offset 2 (skips first two) ===");
        broker.Seek("Sports", s1, offset: 2);
        broker.Poll("Sports", s1);

        // S1 polls News
        Console.WriteLine("\n=== S1 polls News ===");
        broker.Poll("News", s1);
    }
}
