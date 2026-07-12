namespace Factory.V2;

public class EmailNotification : INotification
{
    public void Send(string message) => Console.WriteLine($"  [Email] Sending: {message}");
}
