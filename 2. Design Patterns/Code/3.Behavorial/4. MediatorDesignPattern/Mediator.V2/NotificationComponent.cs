namespace Mediator.V2;

/// <summary>
/// Component: Notification — sends alerts.
/// Does NOT know about Storage, Quota, or Search.
/// </summary>
public class NotificationComponent : BaseComponent
{
    public NotificationComponent(IStorageMediator mediator) : base(mediator) { }

    public void SendAlert(string message)
        => Console.WriteLine($"  [Notify] {message}");
}
