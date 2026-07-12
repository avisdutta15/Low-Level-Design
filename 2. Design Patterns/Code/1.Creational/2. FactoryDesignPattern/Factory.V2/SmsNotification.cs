namespace Factory.V2;

public class SmsNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"  [SMS] Sending: {message}");
}
