namespace Factory.V2;

/// <summary>
/// The Factory — encapsulates object creation logic.
/// 
/// The client only knows about INotification and NotificationType.
/// It never references EmailNotification, SmsNotification, etc. directly.
/// 
/// To add a new notification type:
///   1. Create the new class implementing INotification
///   2. Add a new enum value
///   3. Add one case to this factory
///   4. Client code is UNCHANGED — Open/Closed Principle satisfied.
/// </summary>
public enum NotificationType
{
    Email,
    Sms,
    Push
}

public class NotificationFactory
{
    public INotification CreateNotification(NotificationType type)
    {
        return type switch
        {
            NotificationType.Email => new EmailNotification(),
            NotificationType.Sms => new SmsNotification(),
            NotificationType.Push => new PushNotification(),
            _ => throw new ArgumentException($"Unknown notification type: {type}")
        };
    }
}
