namespace Factory.V2;

public class PushNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"  [Push] Sending: {message}");
}
