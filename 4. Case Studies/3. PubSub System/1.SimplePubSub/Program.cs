/* DESIGN 1: Simple Pub/Sub (Push Model)
 * 
 * Concept: The simplest possible pub/sub system.
 * - Publisher sends a message
 * - All subscribers receive it immediately (push)
 * - No message persistence
 * - No offset tracking
 * - Synchronous delivery
 * 
 * Limitations:
 * - Messages are lost if no subscribers are listening
 * - Slow subscribers block the publisher
 * - No replay capability
 * - No independent consumption rates
 */


public record Message(string message);

public interface ISubscriber
{
    public string Id { get;set; }
    void Consume(Message message);
}

/* The Subscriber just consumes the message using Consume(message) endpoint */
public class Subscriber : ISubscriber
{
    public string Id { get;set; }
    public Subscriber(string id)
    {
        Id = id;
    }
    public void Consume(Message message)
    {
        Console.WriteLine($"[{Id}]: {message}");
    }
}

/* A topic has a list of subscribers that subscribe using topic.Subscribe() method. 
 * The messages in a topic are publised to all subscribers using Publish() method*/
public class Topic
{
    private List<ISubscriber> _subscribers;
    internal string Name {  get; set; }
    public Topic(string name)
    {
        _subscribers = new List<ISubscriber>();
        Name = name;
    }
    public void Subscribe(ISubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    public void Publish(Message message)
    {
        foreach (var subscriber in _subscribers)
        { 
            subscriber.Consume(message);
        }
    }
}

/* The MessageBroker has a list of Topics. The topics are created using CreateTopic() method.
 * The client sends message to a particuar topic using the MessageBroker's SendMessage(topic).
 * The SendMessage(topic) then send the message to the topic using the topic's Publish() method.
 */

public class MessageBroker
{
    private Dictionary<string ,Topic> _topics;

    public MessageBroker()
    {
        _topics = new Dictionary<string ,Topic>();
    }
    public Topic CreateTopic(string name)
    {
        var topic = new Topic(name);
        _topics[name] = topic;
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
    }
}