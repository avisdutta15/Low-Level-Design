// Message
//      - topicName
//      - payload
//      - TimeStamp
// Topic
//      - topicName
//      - Contains a list of Subscribers
//      - Subscribe(subscriber)
//          add subscriber to list
//      - Unsubscribe(subscriber)
//          remove subscriber from list
//      - Publish(message)
//          foreach subscriber in subscribers
//              subscriber.onMessage(message)
// Subscriber
//      - Subscriber(name)
//      - onMessage(message)
// MessageBroker
//      - Contains a list of Topics
//      - AddTopic()
//      - GetTopic()
//      - Subscribe(topic, subscriber)
//          topics[topic].Subscribe(subscriber)
//      - Publish(topic, message)
//          topics[topic].Publish(message)
// Publisher
//      - MessageBroker _msgBroker
//      - Publisher(MessageBroker msgBroker)
//      - Publish(topic, message)
//          calls _msgBroker.Publish(topic, message);

// Overall Flow
//      Publisher -> MessageBroker (topic)-> Subscriber.



using System.Collections.Concurrent;
using System.Collections.Immutable;

public class Message
{
    public string TopicName { get; } = string.Empty;
    public string Payload { get; } = string.Empty;
    public DateTime TimeStamp { get; } = DateTime.MinValue;

    public Message(string topicName, string payload, DateTime timeStamp)
    {
        TopicName = topicName;
        Payload = payload;
        TimeStamp = timeStamp;
    }
}

public abstract class Subscriber
{
    public abstract void OnMessage(Message message);
}

public class ConsoleSubscriber : Subscriber
{
    private string _name;
    public ConsoleSubscriber(string name)
    {
        _name = name;
    }
    public override void OnMessage(Message message)
    {
        Console.WriteLine($"[{_name}] received : [{message.Payload.ToString()}]");
    }

    public override string ToString()
    {
        return $"{_name}";
    }
}

// Topic has a list of subscribers
public class Topic
{
    private ImmutableHashSet<Subscriber> _subscribers = ImmutableHashSet<Subscriber>.Empty;
    private string _name;
    private readonly object _lock = new();

    public Topic(string name)
    {
        _name = name;
    }

    //      - Subscribe(subscriber)
    //          add subscriber to list
    public void Subscribe(Subscriber subscriber)
    {
        ImmutableInterlocked.Update(ref _subscribers, (set) => set.Add(subscriber));
    }

    //      - Unsubscribe(subscriber)
    //          remove subscriber from list
    public void Unsubscribe(Subscriber subscriber)
    {
        ImmutableInterlocked.Update(ref _subscribers, (set) => set.Remove(subscriber));
    }

    //      - Publish(message)
    //          foreach subscriber in subscribers
    //              subscriber.onMessage(message)
    public void Publish(Message message)
    {
        // ImmutableHashSet has in built snapshot semantic
        foreach (var subscriber in _subscribers)
        {
            subscriber.OnMessage(message);
        }
    }
}

public sealed class MessageBroker
{
    private ImmutableDictionary<string, Topic> _topics = ImmutableDictionary<string, Topic>.Empty;

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

    //- AddTopic()
    public void AddTopic(string topicName)
    {
        // Thread Safe
        ImmutableInterlocked.TryAdd(ref _topics, topicName, new Topic(topicName));
    }

    //- GetTopic()
    public Topic GetTopic(string topicName)
    {
        if (_topics.TryGetValue(topicName, out Topic? topic) == true)
        {
            return topic;
        }
        throw new ArgumentException($"Topic {topicName} not found");
    }

    //- Subscribe(topic, subscriber)
    //    topics[topic].Subscribe(subscriber)
    public void Subscribe(string topicName, Subscriber subscriber)
    {
        if (_topics.TryGetValue(topicName, out Topic? topic) == false)
        {
            throw new ArgumentException($"Topic {topicName} not found");
        }
        topic.Subscribe(subscriber);
    }

    //- Publish(topic, message)
    //      topics[topic].Publish(message)
    public void Publish(string topicName, Message message)
    {
        if (_topics.TryGetValue(topicName, out Topic? topic) == false)
        {
            throw new ArgumentException($"Topic {topicName} not found");
        }
        topic.Publish(message);
    }
}

public class Publisher
{
    private readonly MessageBroker _broker;
    public Publisher(MessageBroker broker)
    {
        _broker = broker;
    }

    public void Publish(string topicName, Message message)
    {
        _broker.Publish(topicName, message);
    }
}


public class Program
{
    public static void Main(string[] args)
    {
        MessageBroker broker = MessageBroker.GetInstance();
        broker.AddTopic("Sports");
        broker.AddTopic("News");

        Subscriber s1 = new ConsoleSubscriber("S1");
        Subscriber s2 = new ConsoleSubscriber("S2");
        Subscriber s3 = new ConsoleSubscriber("S3");

        broker.Subscribe("Sports", s1);
        broker.Subscribe("Sports", s2);
        broker.Subscribe("News", s2);
        broker.Subscribe("News", s3);

        Publisher publisher = new Publisher(broker);
        publisher.Publish("Sports", new Message("Sports", "Hello Sports", DateTime.Now));
        publisher.Publish("News", new Message("News", "Hello News", DateTime.Now));
    }
}