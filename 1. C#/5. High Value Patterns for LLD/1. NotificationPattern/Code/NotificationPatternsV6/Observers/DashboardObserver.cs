namespace NotificationPatternsV6.Observers;

public class DashboardObserver : IObserver
{
    public void Update(string message)
    {
        Console.WriteLine($"Dashboard observer: {message}");
    }
}
