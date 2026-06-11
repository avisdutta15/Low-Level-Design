// Component
public interface INotification
{
    void Send(string message);
    string GetChannels();
}

// Concrete Component  
public class BasicNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"Basic: {message}");
    public string GetChannels() => "Basic";
}

// Base Decorator
public abstract class NotificationDecorator : INotification
{
    protected INotification _notification;
    protected NotificationDecorator(INotification notification) => _notification = notification;

    public virtual void Send(string message) => _notification.Send(message);
    public virtual string GetChannels() => _notification.GetChannels();
}

// Concrete Decorators
public class EmailDecorator : NotificationDecorator
{
    public EmailDecorator(INotification notification) : base(notification) { }

    public override void Send(string message)
    {
        _notification.Send(message);
        Console.WriteLine($"Email: {message}");
    }

    public override string GetChannels() => _notification.GetChannels() + ", Email";
}

public class SMSDecorator : NotificationDecorator
{
    public SMSDecorator(INotification notification) : base(notification) { }

    public override void Send(string message)
    {
        _notification.Send(message);
        Console.WriteLine($"SMS: {message}");
    }

    public override string GetChannels() => _notification.GetChannels() + ", SMS";
}

public class PushDecorator : NotificationDecorator
{
    public PushDecorator(INotification notification) : base(notification) { }

    public override void Send(string message)
    {
        _notification.Send(message);
        Console.WriteLine($"Push: {message}");
    }

    public override string GetChannels() => _notification.GetChannels() + ", Push";
}