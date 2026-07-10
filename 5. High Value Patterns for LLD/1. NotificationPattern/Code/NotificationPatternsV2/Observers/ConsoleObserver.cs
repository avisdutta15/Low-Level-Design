namespace NotificationPatternsV2.Observers;

public class ConsoleObserver : IObserver
{
    public void Update(string message)
    {
        Console.WriteLine($"Console Observer: {message}");
    }
}
