namespace Factory.V1;

public class PushNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"  [Push] Sending: {message}");
}
