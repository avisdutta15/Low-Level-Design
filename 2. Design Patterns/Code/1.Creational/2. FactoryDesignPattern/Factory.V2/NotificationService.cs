namespace Factory.V2;

/// <summary>
/// Client class — uses the factory to create notifications.
/// 
/// Notice: This class has ZERO knowledge of EmailNotification, SmsNotification, etc.
/// It only depends on INotification (abstraction) and NotificationFactory.
/// 
/// Benefits:
///   - Single Responsibility: Service handles logic, Factory handles creation
///   - Open/Closed: New notification types don't require changes here
///   - Testability: Factory can be mocked to return test doubles
/// </summary>
public class NotificationService
{
    private readonly NotificationFactory _factory;

    public NotificationService(NotificationFactory factory)
    {
        _factory = factory;
    }

    public void Notify(NotificationType type, string message)
    {
        // Client doesn't know or care which concrete class is created
        INotification notification = _factory.CreateNotification(type);
        notification.Send(message);
    }

    public void NotifyAll(string message)
    {
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
        {
            INotification notification = _factory.CreateNotification(type);
            notification.Send(message);
        }
    }
}
