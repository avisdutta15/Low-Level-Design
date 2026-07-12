namespace Factory.V1;

public class SmsNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"  [SMS] Sending: {message}");
}
