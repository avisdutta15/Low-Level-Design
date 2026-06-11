/* DESIGN 2: Pub/Sub with Message Log (Kafka-style)
* 
* Evolution: Replace queue with an append-only log.
* - Messages are stored in an ordered list (log)
* - Messages are never deleted
* - Each subscriber tracks their own position
* - Multiple subscribers can read the same message
* + Each subscriber can consume independently
* + Replay capability (can re-read old messages)
* + Multiple subscribers get all messages
* 
* Limitations:
* - Manual offset tracking (no automatic management)
* - Synchronous consumption
* - No concurrent processing per subscriber
* - Manually Poll the Topic
*/

public record Message(string message);

public interface ISubscriber
{
    string Id { get; }
    void Consume(Message message);
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
        Console.WriteLine($"[{Id}]: {message}");
    }
}

public class SubscriberOffset
{
    public int Offset { get; set; }
    public ISubscriber Subscriber;

    public SubscriberOffset(int offset, ISubscriber subscriber)
    {
        Offset = offset;
        Subscriber = subscriber;
    }
}

public class Topic
{
    private Dictionary<string, SubscriberOffset> _subscriberOffsets = new Dictionary<string, SubscriberOffset>();
    private List<Message> _messageLog = new List<Message>();
    public string Name { get; set; }

    public Topic(string name)
    {
        Name = name;
    }

    public void Subscribe(ISubscriber subscriber)
    {
        var subscriberOffset = new SubscriberOffset(0, subscriber);
        _subscriberOffsets.Add(subscriber.Id, subscriberOffset);
    }

    public void Publish(Message msg)
    {
        _messageLog.Add(msg);
        Console.WriteLine($"[Topic: {Name}] Appended: {msg.message} (Log size: {_messageLog.Count})");
    }

    public void Poll()
    {
        foreach (var subscriberOffset in _subscriberOffsets)
        {
            while (subscriberOffset.Value.Offset < _messageLog.Count)
            {
                var offsetMessage = _messageLog[subscriberOffset.Value.Offset];
                subscriberOffset.Value.Subscriber.Consume(offsetMessage);
                subscriberOffset.Value.Offset++;
            }
        }
    }

    public void ResetOffset(ISubscriber subscriber, int newOffset) 
    {
        if(_subscriberOffsets.TryGetValue(subscriber.Id, out SubscriberOffset? subscriberOffset))
        {
            subscriberOffset.Offset = newOffset;
            Console.WriteLine($"[{subscriber.Id}] Offset reset to: {newOffset}");
        }
    }

    public void ShowOffsets()
    {
        foreach (var subscriberOffset in _subscriberOffsets)
        { 
            Console.WriteLine($"Subscriber {subscriberOffset.Value.Subscriber.Id} : {subscriberOffset.Value.Offset}"); 
        }
    }
}

public class MessageBroker
{
    public Dictionary<string, Topic> _topics;
    public MessageBroker()
    {
        _topics = new Dictionary<string, Topic>();
    }

    public Topic CreateTopic(string topicName)
    {
        var topic = new Topic(topicName);
        _topics.Add(topic.Name, topic);
        return topic;
    }

    public void SendMessage(Topic topic, Message message)
    {
        _topics[topic.Name].Publish(message);
    }
}

/* Client */
public class Program
{
    public static void Main(string[] args)
    {
        var broker = new MessageBroker();
        var topic = broker.CreateTopic("Topic1");

        ISubscriber subscriber1 = new Subscriber("S1");
        ISubscriber subscriber2 = new Subscriber("S2");

        topic.Subscribe(subscriber1);
        topic.Subscribe(subscriber2);

        broker.SendMessage(topic, new Message("message-1"));
        broker.SendMessage(topic, new Message("message-2"));
        broker.SendMessage(topic, new Message("message-3"));

        Console.WriteLine("--- First Poll ---");
        topic.Poll();
        topic.ShowOffsets();

        // Publish more messages
        Console.WriteLine("\n--- Publishing more messages ---");
        broker.SendMessage(topic, new Message("message-4"));
        broker.SendMessage(topic, new Message("message-5"));

        Console.WriteLine("\n--- Second Poll ---");
        topic.Poll();
        topic.ShowOffsets();

        // Replay capability - reset S1 to beginning
        Console.WriteLine("\n--- Replaying messages for S1 ---");
        topic.ResetOffset(subscriber1, 0);
        topic.Poll();
        topic.ShowOffsets();
    }
}